using System.Text.Json;
using Ajure.Specification;
using Ajure.Validation;

namespace Ajure.Agent;

public sealed record ReviewerExecution
{
    public required AgentRole Role { get; init; }

    public required string ModelId { get; init; }

    public IReadOnlyList<string> SessionIds { get; init; } = [];

    public required int Attempts { get; init; }

    public ReviewEnvelope? Envelope { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public bool Succeeded => Envelope is not null && ErrorCode.Length == 0;
}

public sealed record SimulationExecution
{
    public required string ModelId { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public required bool Succeeded { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public string ResultJson { get; init; } = string.Empty;
}

public sealed record MultiModelValidationResult
{
    public IReadOnlyList<ReviewerExecution> Reviewers { get; init; } = [];

    public IReadOnlyList<NormalizedReview> NormalizedReviews { get; init; } = [];

    public IReadOnlyList<FindingCluster> Clusters { get; init; } = [];

    public required AreaScores Scores { get; init; }

    public IReadOnlyList<string> InvalidEnvelopeCodes { get; init; } = [];

    public IReadOnlyList<string> SuccessfulModelIds { get; init; } = [];

    public bool TieBreakUsed { get; init; }

    public bool TieBreakRequired { get; init; }

    public bool TieBreakResolved { get; init; } = true;

    public SimulationExecution? Simulation { get; init; }

    public bool CopilotStagesCompleted =>
        InvalidEnvelopeCodes.Count == 0
        && (!TieBreakUsed || TieBreakResolved)
        && Simulation?.Succeeded == true;

    public IReadOnlyList<string> SessionIds =>
    [
        .. Reviewers.SelectMany(static execution => execution.SessionIds),
        .. Simulation is null || Simulation.SessionId.Length == 0 ? [] : new[] { Simulation.SessionId }
    ];
}

public static class MultiModelValidationWorkflow
{
    public const string ErrorSimulationInvalid = "simulation_envelope_invalid";

    private static readonly HashSet<string> SimulationProperties =
        new(
            ["components", "tasks", "files", "dependencies", "verification", "gaps"],
            StringComparer.Ordinal);

    public static async Task<MultiModelValidationResult> RunAsync(
        IModelGateway gateway,
        ProjectSpec spec,
        IReadOnlyList<string> modelPool,
        string reviewPrompt,
        string simulationPrompt,
        TimeSpan timeout,
        bool allowTieBreak,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(modelPool);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(simulationPrompt);

        var requests = ReviewerPlanner
            .Assign(modelPool)
            .Select(assignment => new ModelRequest(
                assignment.Role,
                assignment.ModelId,
                AgentPrompts.Instructions(assignment.Role),
                reviewPrompt,
                timeout))
            .ToArray();
        var responses = await WorkflowTopology
            .RunIndependentReviewAsync(gateway, requests, cancellationToken)
            .ConfigureAwait(false);

        var executions = new List<ReviewerExecution>(requests.Length + 1);
        for (var index = 0; index < requests.Length; index++)
        {
            executions.Add(await ParseWithSingleRetryAsync(
                    gateway,
                    requests[index],
                    responses[index],
                    cancellationToken)
                .ConfigureAwait(false));
        }

        var invalidCodes = InvalidCodes(executions);
        var normalized = Normalize(executions, spec);
        var clusters = FindingAggregator.Cluster(normalized);
        var scores = AggregateScores(executions);
        var tieBreakUsed = false;
        var tieBreakRequired = false;
        var tieBreakResolved = true;

        if (invalidCodes.Count == 0)
        {
            tieBreakRequired = FindingAggregator.RequiresTieBreak(
                executions.Where(static execution => execution.Succeeded).Select(static execution => execution.Envelope!.Scores).ToArray(),
                clusters);
        }

        if (tieBreakRequired)
        {
            if (!allowTieBreak)
            {
                tieBreakResolved = false;
            }
            else
            {
                tieBreakUsed = true;
                if (modelPool.Count < 3)
                {
                    tieBreakResolved = false;
                }
                else
                {
                    var tieRequest = new ModelRequest(
                        AgentRole.TieBreaker,
                        modelPool[2],
                        AgentPrompts.Instructions(AgentRole.TieBreaker),
                        BuildTieBreakPrompt(reviewPrompt, clusters),
                        timeout);
                    var response = await WorkflowTopology
                        .RunAgentAsync(gateway, tieRequest, cancellationToken)
                        .ConfigureAwait(false);
                    executions.Add(await ParseWithSingleRetryAsync(
                            gateway,
                            tieRequest,
                            response,
                            cancellationToken)
                        .ConfigureAwait(false));

                    invalidCodes = InvalidCodes(executions);
                    normalized = Normalize(executions, spec);
                    clusters = FindingAggregator.Cluster(normalized);
                    scores = AggregateScores(executions);
                    tieBreakResolved = invalidCodes.Count == 0
                        && !clusters.Any(static cluster =>
                            cluster.Severity == FindingSeverity.Critical
                            && cluster.Consensus == ClusterConsensus.Disputed);
                }
            }
        }

        SimulationExecution? simulation = null;
        if (invalidCodes.Count == 0 && (!tieBreakUsed || tieBreakResolved))
        {
            var simulationRequest = new ModelRequest(
                AgentRole.ImplementationSimulator,
                modelPool[0],
                AgentPrompts.Instructions(AgentRole.ImplementationSimulator),
                simulationPrompt,
                timeout);
            var response = await WorkflowTopology
                .RunAgentAsync(gateway, simulationRequest, cancellationToken)
                .ConfigureAwait(false);
            simulation = ParseSimulation(response);
        }

        return new MultiModelValidationResult
        {
            Reviewers = executions,
            NormalizedReviews = normalized,
            Clusters = clusters,
            Scores = scores,
            InvalidEnvelopeCodes = invalidCodes,
            SuccessfulModelIds =
            [
                .. executions
                    .Where(static execution => execution.Succeeded)
                    .Select(static execution => execution.ModelId)
                    .Distinct(StringComparer.Ordinal)
            ],
            TieBreakUsed = tieBreakUsed,
            TieBreakRequired = tieBreakRequired,
            TieBreakResolved = tieBreakResolved,
            Simulation = simulation
        };
    }

    private static async Task<ReviewerExecution> ParseWithSingleRetryAsync(
        IModelGateway gateway,
        ModelRequest request,
        ModelResponse initialResponse,
        CancellationToken cancellationToken)
    {
        var sessions = new List<string> { initialResponse.SessionId };
        var parsed = ReviewEnvelopeParser.Parse(initialResponse.Content);
        if (parsed.IsValid)
        {
            return Success(request, sessions, parsed.Envelope!, attempts: 1);
        }

        var retry = await WorkflowTopology
            .RunAgentAsync(gateway, request, cancellationToken)
            .ConfigureAwait(false);
        sessions.Add(retry.SessionId);
        parsed = ReviewEnvelopeParser.Parse(retry.Content);
        return parsed.IsValid
            ? Success(request, sessions, parsed.Envelope!, attempts: 2)
            : new ReviewerExecution
            {
                Role = request.Role,
                ModelId = request.ModelId,
                SessionIds = sessions,
                Attempts = 2,
                ErrorCode = parsed.ErrorCode
            };
    }

    private static ReviewerExecution Success(
        ModelRequest request,
        IReadOnlyList<string> sessions,
        ReviewEnvelope envelope,
        int attempts) => new()
        {
            Role = request.Role,
            ModelId = request.ModelId,
            SessionIds = sessions,
            Attempts = attempts,
            Envelope = envelope
        };

    private static List<string> InvalidCodes(IEnumerable<ReviewerExecution> executions) =>
    [
        .. executions
            .Where(static execution => !execution.Succeeded)
            .Select(static execution => $"{execution.Role}:{execution.ErrorCode}")
    ];

    private static List<NormalizedReview> Normalize(
        IEnumerable<ReviewerExecution> executions,
        ProjectSpec spec) =>
    [
        .. executions
            .Where(static execution => execution.Succeeded)
            .Select(execution => FindingNormalizer.Normalize(
                new ReviewResult
                {
                    Role = execution.Role.DisplayName(),
                    ModelId = execution.ModelId,
                    SessionId = execution.SessionIds[^1],
                    Envelope = execution.Envelope!
                },
                spec))
    ];

    private static AreaScores AggregateScores(IEnumerable<ReviewerExecution> executions)
    {
        var scores = executions
            .Where(static execution => execution.Succeeded)
            .Select(static execution => execution.Envelope!.Scores)
            .ToArray();
        return scores.Length < 2
            ? new AreaScores
            {
                IntentCoverage = 0m,
                Traceability = 0m,
                Testability = 0m,
                TechnicalExecutability = 0m,
                TargetAgentFitness = 0m,
                UxOperationsCompleteness = 0m
            }
            : ScoreAggregator.Aggregate(scores);
    }

    private static string BuildTieBreakPrompt(
        string reviewPrompt,
        IReadOnlyList<FindingCluster> clusters)
    {
        var disputed = clusters
            .Where(static cluster => cluster.Consensus == ClusterConsensus.Disputed)
            .Select(static cluster => new
            {
                cluster.Fingerprint,
                cluster.RuleKey,
                cluster.Severity,
                cluster.Statement,
                cluster.AffectedIds,
                cluster.Evidence
            });
        return reviewPrompt
            + "\nAct as the one permitted tie breaker. Resolve these disputed clusters and independently score all areas: "
            + JsonSerializer.Serialize(disputed);
    }

    private static SimulationExecution ParseSimulation(ModelResponse response)
    {
        try
        {
            using var document = JsonDocument.Parse(StripFence(response.Content));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return SimulationFailure(response);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!SimulationProperties.Contains(property.Name)
                    || !seen.Add(property.Name)
                    || property.Value.ValueKind != JsonValueKind.Array)
                {
                    return SimulationFailure(response);
                }
            }

            if (!SimulationProperties.SetEquals(seen))
            {
                return SimulationFailure(response);
            }

            return new SimulationExecution
            {
                ModelId = response.ModelId,
                SessionId = response.SessionId,
                Succeeded = true,
                ResultJson = root.GetRawText()
            };
        }
        catch (JsonException)
        {
            return SimulationFailure(response);
        }
    }

    private static SimulationExecution SimulationFailure(ModelResponse response) => new()
    {
        ModelId = response.ModelId,
        SessionId = response.SessionId,
        Succeeded = false,
        ErrorCode = ErrorSimulationInvalid
    };

    private static string StripFence(string payload)
    {
        var trimmed = payload.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstBreak = trimmed.IndexOf('\n', StringComparison.Ordinal);
        if (firstBreak < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstBreak + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence < 0 ? body.Trim() : body[..lastFence].Trim();
    }
}
