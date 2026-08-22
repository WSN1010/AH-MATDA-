using System.Collections.Concurrent;
using Ajure.Agent;
using Ajure.Specification;

namespace Ajure.Agent.EvaluationTests;

public sealed class MultiModelValidationWorkflowTests
{
    private static readonly string[] Models = ["model-a", "model-b", "model-c"];

    [Fact]
    public async Task InvalidReviewerEnvelopeIsRetriedOnceOnTheSameModel()
    {
        var gateway = new ScriptedGateway(
            new Dictionary<AgentRole, IEnumerable<string>>
            {
                [AgentRole.ProductReviewer] = ["not-json", Review()],
                [AgentRole.TechnicalReviewer] = [Review()],
                [AgentRole.UxReviewer] = [Review()],
                [AgentRole.ImplementationSimulator] = [Simulation()]
            });

        var result = await RunAsync(gateway).ConfigureAwait(true);

        var product = Assert.Single(
            result.Reviewers,
            static execution => execution.Role == AgentRole.ProductReviewer);
        Assert.Equal(2, product.Attempts);
        Assert.Equal("model-a", product.ModelId);
        Assert.Equal(2, product.SessionIds.Count);
        Assert.Empty(result.InvalidEnvelopeCodes);
        Assert.True(result.CopilotStagesCompleted);
    }

    [Fact]
    public async Task SecondInvalidEnvelopeFailsClosedAndSkipsSimulation()
    {
        var gateway = new ScriptedGateway(
            new Dictionary<AgentRole, IEnumerable<string>>
            {
                [AgentRole.ProductReviewer] = ["not-json", "still-not-json"],
                [AgentRole.TechnicalReviewer] = [Review()],
                [AgentRole.UxReviewer] = [Review()]
            });

        var result = await RunAsync(gateway).ConfigureAwait(true);

        Assert.Single(result.InvalidEnvelopeCodes);
        Assert.Null(result.Simulation);
        Assert.False(result.CopilotStagesCompleted);
        Assert.Equal(2, gateway.Calls(AgentRole.ProductReviewer));
        Assert.Equal(0, gateway.Calls(AgentRole.ImplementationSimulator));
    }

    [Fact]
    public async Task ScoreConflictUsesExactlyOneTieBreakerAndThenSimulates()
    {
        var gateway = new ScriptedGateway(
            new Dictionary<AgentRole, IEnumerable<string>>
            {
                [AgentRole.ProductReviewer] = [Review(intent: 10)],
                [AgentRole.TechnicalReviewer] = [Review(intent: 16)],
                [AgentRole.UxReviewer] = [Review(intent: 10)],
                [AgentRole.TieBreaker] = [Review(intent: 13)],
                [AgentRole.ImplementationSimulator] = [Simulation()]
            });

        var result = await RunAsync(gateway).ConfigureAwait(true);

        Assert.True(result.TieBreakUsed);
        Assert.True(result.TieBreakResolved);
        Assert.Equal(1, gateway.Calls(AgentRole.TieBreaker));
        Assert.Equal("model-c", Assert.Single(
            result.Reviewers,
            static execution => execution.Role == AgentRole.TieBreaker).ModelId);
        Assert.True(result.Simulation?.Succeeded);
    }

    private static Task<MultiModelValidationResult> RunAsync(IModelGateway gateway) =>
        MultiModelValidationWorkflow.RunAsync(
            gateway,
            Spec(),
            Models,
            "Review this ProjectSpec.",
            "Simulate this ProjectSpec.",
            TimeSpan.FromSeconds(5),
            allowTieBreak: true,
            CancellationToken.None);

    private static ProjectSpec Spec() => new()
    {
        ProjectName = "Test",
        Vision = "Test vision.",
        Problem = "Test problem.",
        Technical = new TechnicalProfile
        {
            Architecture = "Test architecture."
        },
        Release = new ReleaseScope()
    };

    private static string Review(decimal intent = 20) =>
        $$"""
        {
          "reviewComplete": true,
          "scores": {
            "intentCoverage": {{intent}},
            "traceability": 18,
            "testability": 18,
            "technicalExecutability": 13,
            "targetAgentFitness": 9,
            "uxOperationsCompleteness": 9
          },
          "findings": []
        }
        """;

    private static string Simulation() =>
        """{"components":[],"tasks":[],"files":[],"dependencies":[],"verification":[],"gaps":[]}""";

    private sealed class ScriptedGateway : IModelGateway
    {
        private readonly ConcurrentDictionary<AgentRole, ConcurrentQueue<string>> _responses;
        private readonly ConcurrentDictionary<AgentRole, int> _calls = new();
        private int _session;

        public ScriptedGateway(IReadOnlyDictionary<AgentRole, IEnumerable<string>> responses)
        {
            _responses = new ConcurrentDictionary<AgentRole, ConcurrentQueue<string>>(
                responses.Select(static pair =>
                    new KeyValuePair<AgentRole, ConcurrentQueue<string>>(
                        pair.Key,
                        new ConcurrentQueue<string>(pair.Value))));
        }

        public int Calls(AgentRole role) => _calls.GetValueOrDefault(role);

        public Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelDescriptor>>([]);

        public Task<ModelResponse> RunAsync(
            ModelRequest request,
            CancellationToken cancellationToken)
        {
            _calls.AddOrUpdate(request.Role, 1, static (_, count) => count + 1);
            if (!_responses.TryGetValue(request.Role, out var queue) || !queue.TryDequeue(out var content))
            {
                throw new InvalidOperationException($"No response was configured for {request.Role}.");
            }

            return Task.FromResult(
                new ModelResponse(
                    request.Role,
                    request.ModelId,
                    $"session-{Interlocked.Increment(ref _session)}",
                    content));
        }
    }
}
