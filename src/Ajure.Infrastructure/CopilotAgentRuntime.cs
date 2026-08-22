using Ajure.Agent;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure;

public sealed record CopilotProbeSession(
    string ModelId,
    string SessionId,
    string Response,
    int ToolExecutions);

public sealed record CopilotProbeResult(
    DateTimeOffset CompletedAt,
    IReadOnlyList<string> AvailableModelIds,
    IReadOnlyList<CopilotProbeSession> Sessions,
    bool DistinctSessions,
    bool CancellationObserved,
    bool TimeoutObserved,
    bool SessionsDeleted);

public sealed class CopilotAgentRuntime :
    IModelGateway,
    IHostedService,
    IAsyncDisposable
{
    private readonly CopilotRuntimeOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private CopilotClient? _client;
    private bool _started;

    public CopilotAgentRuntime(
        IOptions<CopilotRuntimeOptions> options,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            Directory.CreateDirectory(_options.HomeDirectory);
            _client = new CopilotClient(new CopilotClientOptions
            {
                Mode = CopilotClientMode.Empty,
                BaseDirectory = _options.HomeDirectory,
                WorkingDirectory = _options.HomeDirectory,
                UseLoggedInUser = _options.UseLoggedInUser,
                EnableRemoteSessions = false,
                SessionIdleTimeoutSeconds = Math.Max(_options.SessionTimeoutSeconds * 2, 60),
                Telemetry = null,
                Logger = _loggerFactory.CreateLogger<CopilotClient>()
            });
            await _client.StartAsync(cancellationToken).ConfigureAwait(false);
            _started = true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        await _client.StopAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        _started = false;
    }

    public async Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);
        return models
            .Select(static model => new ModelDescriptor(model.Id, model.Name ?? model.Id))
            .OrderBy(static model => model.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ModelResponse> RunAsync(
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var sessionConfig = CreateSessionConfig(request.ModelId, request.Instructions);
        await using var agent = (GitHubCopilotAgent)client.AsAIAgent(
            sessionConfig,
            ownsClient: false,
            id: $"{request.Role}-{Guid.NewGuid():N}",
            name: request.Role.DisplayName(),
            description: $"Ajure {request.Role.DisplayName()}");
        var session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        var sessionId = ((GitHubCopilotAgentSession)session).SessionId
            ?? throw new InvalidOperationException("Copilot did not assign a session ID.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        try
        {
            try
            {
                var response = await agent
                    .RunAsync(request.Prompt, session, options: null, timeout.Token)
                    .ConfigureAwait(false);
                return new ModelResponse(request.Role, request.ModelId, sessionId, response.Text);
            }
            catch (OperationCanceledException exception)
                when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"The {request.Role.DisplayName()} model session timed out.",
                    exception);
            }
        }
        finally
        {
            await client.DeleteSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<CopilotProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var available = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
        var pool = _options.ModelPool.Length == 0
            ? available.Select(static model => model.Id).Take(2).ToArray()
            : ReviewerPlanner.ResolvePool(available, _options.ModelPool).Take(2).ToArray();

        if (pool.Length < 2)
        {
            throw new ModelDiversityException();
        }

        var sessions = await Task.WhenAll(
            pool.Select(modelId => ProbeSessionAsync(client, modelId, cancellationToken)))
            .ConfigureAwait(false);
        var cancellationObserved = await ProbeCancellationAsync(client, pool[0]).ConfigureAwait(false);
        var timeoutObserved = await ProbeTimeoutAsync(client, pool[0], cancellationToken).ConfigureAwait(false);

        return new CopilotProbeResult(
            DateTimeOffset.UtcNow,
            available.Select(static model => model.Id).ToArray(),
            sessions,
            sessions.Select(static session => session.SessionId).Distinct(StringComparer.Ordinal).Count() == sessions.Length,
            cancellationObserved,
            timeoutObserved,
            SessionsDeleted: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        _startLock.Dispose();
    }

    private async Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        return _client ?? throw new InvalidOperationException("Copilot runtime did not start.");
    }

    private SessionConfig CreateSessionConfig(string modelId, string instructions) => new()
    {
        ClientName = "Ajure",
        Model = modelId,
        SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Replace,
            Content = instructions
        },
        AvailableTools = [],
        Tools = [],
#pragma warning disable GHCP001
        OnPermissionRequest = static (_, _) =>
            Task.FromResult(PermissionDecision.Reject("Ajure model sessions do not permit tools.")),
#pragma warning restore GHCP001
        EnableConfigDiscovery = false,
        EnableOnDemandInstructionDiscovery = false,
        EnableFileHooks = false,
        EnableHostGitOperations = false,
        EnableSessionStore = false,
        EnableSessionTelemetry = false,
        EnableSkills = false,
        SkipCustomInstructions = true,
        SkipEmbeddingRetrieval = true,
        EmbeddingCacheStorage = EmbeddingCacheStorageMode.InMemory,
        Memory = new MemoryConfiguration { Enabled = false },
        Streaming = false,
        WorkingDirectory = _options.HomeDirectory
    };

    private async Task<CopilotProbeSession> ProbeSessionAsync(
        CopilotClient client,
        string modelId,
        CancellationToken cancellationToken)
    {
        var toolExecutions = 0;
        await using var session = await client
            .CreateSessionAsync(
                CreateSessionConfig(
                    modelId,
                    "You are a capability probe. Never use tools. Reply with one short line."),
                cancellationToken)
            .ConfigureAwait(false);
        using var subscription = session.On<ToolExecutionStartEvent>(_ => Interlocked.Increment(ref toolExecutions));

        try
        {
            var response = await session
                .SendAndWaitAsync(
                    "Attempt no file operation. Reply exactly: AJURE_PROBE_OK",
                    TimeSpan.FromSeconds(_options.SessionTimeoutSeconds),
                    cancellationToken)
                .ConfigureAwait(false);
            return new CopilotProbeSession(
                modelId,
                session.SessionId,
                response?.Data?.Content ?? string.Empty,
                toolExecutions);
        }
        finally
        {
            await client.DeleteSessionAsync(session.SessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> ProbeCancellationAsync(CopilotClient client, string modelId)
    {
        await using var session = await client
            .CreateSessionAsync(CreateSessionConfig(modelId, "Reply briefly."), CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                await session
                    .SendAndWaitAsync("Cancellation probe.", TimeSpan.FromSeconds(30), cancellation.Token)
                    .ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
        finally
        {
            await session.AbortAsync(CancellationToken.None).ConfigureAwait(false);
            await client.DeleteSessionAsync(session.SessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> ProbeTimeoutAsync(
        CopilotClient client,
        string modelId,
        CancellationToken cancellationToken)
    {
        await using var session = await client
            .CreateSessionAsync(CreateSessionConfig(modelId, "Reply briefly."), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            try
            {
                await session
                    .SendAndWaitAsync("Timeout probe.", TimeSpan.FromMilliseconds(1), cancellationToken)
                    .ConfigureAwait(false);
                return false;
            }
            catch (TimeoutException)
            {
                return true;
            }
        }
        finally
        {
            await session.AbortAsync(CancellationToken.None).ConfigureAwait(false);
            await client.DeleteSessionAsync(session.SessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
