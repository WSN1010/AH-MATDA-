using Ajure.Agent;

namespace Ajure.Agent.EvaluationTests;

public sealed class WorkflowTopologyTests
{
    private static readonly ModelRequest[] Requests =
    [
        CreateRequest(AgentRole.ProductReviewer, "claude-opus-5"),
        CreateRequest(AgentRole.TechnicalReviewer, "gpt-5.6-sol"),
        CreateRequest(AgentRole.UxReviewer, "claude-sonnet-5")
    ];

    [Fact]
    public async Task IndependentReviewRunsConcurrentlyAndReturnsRoleOrder()
    {
        var gateway = new SynchronizingGateway(Requests.Length);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var responses = await WorkflowTopology
            .RunIndependentReviewAsync(gateway, Requests, timeout.Token)
            .ConfigureAwait(true);

        Assert.Equal(Requests.Select(static request => request.Role), responses.Select(static response => response.Role));
        Assert.Equal(Requests.Length, gateway.CallCount);
    }

    [Fact]
    public async Task IndependentReviewPreservesExecutorFailure()
    {
        var gateway = new ThrowingGateway();

        var exception = await Assert.ThrowsAsync<TimeoutException>(
                () => WorkflowTopology.RunIndependentReviewAsync(
                    gateway,
                    Requests,
                    CancellationToken.None))
            .ConfigureAwait(true);

        Assert.Equal("model timeout", exception.Message);
    }

    private static ModelRequest CreateRequest(AgentRole role, string modelId) =>
        new(role, modelId, "Review independently.", "ProjectSpec", TimeSpan.FromSeconds(1));

    private sealed class SynchronizingGateway(int expectedCalls) : IModelGateway
    {
        private readonly TaskCompletionSource _allStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => _callCount;

        public Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelDescriptor>>([]);

        public async Task<ModelResponse> RunAsync(
            ModelRequest request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _callCount) == expectedCalls)
            {
                _allStarted.SetResult();
            }

            await _allStarted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ModelResponse(
                request.Role,
                request.ModelId,
                $"session-{request.Role}",
                "{}");
        }
    }

    private sealed class ThrowingGateway : IModelGateway
    {
        public Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelDescriptor>>([]);

        public Task<ModelResponse> RunAsync(
            ModelRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<ModelResponse>(new TimeoutException("model timeout"));
    }
}
