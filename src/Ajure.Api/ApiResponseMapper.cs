using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ajure.Infrastructure;
using Ajure.Specification;
using Ajure.Validation;

namespace Ajure.Api;

public static class ApiResponseMapper
{
    private static readonly string[] GateIds =
    [
        "HG-01", "HG-02", "HG-03", "HG-04", "HG-05", "HG-06", "HG-07",
        "HG-08", "HG-09", "HG-10", "HG-11", "HG-12", "HG-13", "HG-14"
    ];

    public static IdeaInputResponse ParseIdea(JsonElement idea)
    {
        if (idea.ValueKind == JsonValueKind.String)
        {
            return new IdeaInputResponse(idea.GetString() ?? string.Empty, string.Empty, string.Empty, string.Empty);
        }

        if (idea.ValueKind != JsonValueKind.Object)
        {
            return new IdeaInputResponse(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        return JsonSerializer.Deserialize<IdeaInputResponse>(idea.GetRawText(), JsonDefaults.Options)
            ?? new IdeaInputResponse(string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public static IdeaInputResponse ParseStoredIdea(string idea)
    {
        try
        {
            return JsonSerializer.Deserialize<IdeaInputResponse>(idea, JsonDefaults.Options)
                ?? new IdeaInputResponse(idea, string.Empty, string.Empty, string.Empty);
        }
        catch (JsonException)
        {
            return new IdeaInputResponse(idea, string.Empty, string.Empty, string.Empty);
        }
    }

    public static string SerializeIdea(IdeaInputResponse idea) =>
        JsonSerializer.Serialize(idea, JsonDefaults.Options);

    public static async Task<ProjectResponse> ProjectAsync(
        ProjectRecord project,
        AjureStore store,
        CancellationToken cancellationToken)
    {
        var versions = await store.ListVersionsAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var version = versions.Count == 0 ? null : versions[0];
        var jobs = await store.ListJobsAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var latestJob = jobs.Count == 0 ? null : jobs[0];
        var decisions = await store.ListDecisionsAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var artifacts = version is null
            ? []
            : await store.ListArtifactsAsync(version.Id, cancellationToken).ConfigureAwait(false);
        var run = version is null
            ? null
            : await store.GetLatestValidationRunAsync(version.Id, cancellationToken).ConfigureAwait(false);
        var baseVersion = version?.BaseVersionId is { } baseId
            ? await store.GetVersionAsync(baseId, cancellationToken).ConfigureAwait(false)
            : null;
        var updatedAt = new[]
        {
            project.CreatedAt,
            version?.ApprovedAt ?? version?.CreatedAt ?? project.CreatedAt,
            latestJob?.CompletedAt ?? latestJob?.StartedAt ?? latestJob?.CreatedAt ?? project.CreatedAt
        }.Max();

        return new ProjectResponse(
            project.Id,
            project.Name,
            ProjectStatus(version, latestJob),
            run?.Score,
            version?.Id ?? Guid.Empty,
            version?.Number ?? 0,
            version?.TargetIds ?? [],
            artifacts
                .Where(static artifact =>
                    artifact.Status == ArtifactStatus.Current
                    && artifact.Kind is not ArtifactKind.ExportZip
                    && artifact.Kind is not ArtifactKind.ValidationReport)
                .Select(static artifact => artifact.Path)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            decisions.Count(static decision => decision.Critical && string.IsNullOrWhiteSpace(decision.Answer)),
            updatedAt,
            ParseStoredIdea(project.Idea),
            project.CreatedAt,
            run?.Id,
            latestJob?.Id,
            baseVersion?.Number);
    }

    public static ProjectSummaryResponse Summary(ProjectResponse project) => new(
        project.Id,
        project.Name,
        project.Status,
        project.ReadinessScore,
        project.SpecVersionId,
        project.SpecVersionNumber,
        project.TargetIds,
        project.ArtifactCount,
        project.OpenCriticalDecisions,
        project.UpdatedAt);

    public static DecisionResponse Decision(DecisionRecord decision)
    {
        var impacts = decision.Impacts ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return new DecisionResponse(
            decision.Id,
            decision.Severity.ToString(),
            decision.Question,
            decision.Reason ?? string.Empty,
            string.Join(" ", impacts.Values),
            decision.Options
                .Select(option => new DecisionOptionResponse(
                    option,
                    option,
                    impacts.GetValueOrDefault(option, string.Empty)))
                .ToArray(),
            decision.Recommended,
            decision.Reason ?? string.Empty,
            decision.AnswerOptionId
                ?? (decision.Answer is not null && decision.Options.Contains(decision.Answer, StringComparer.Ordinal)
                    ? decision.Answer
                    : null),
            decision.AnswerText
                ?? (decision.Answer is not null && !decision.Options.Contains(decision.Answer, StringComparer.Ordinal)
                    ? decision.Answer
                    : null),
            string.IsNullOrWhiteSpace(decision.Answer) ? null : decision.UpdatedAt,
            []);
    }

    public static JobStatusResponse Job(JobRecord job, IReadOnlyList<JobEventRecord> events)
    {
        var definitions = StageDefinitions(job.Kind);
        var stages = definitions.Select(definition => Stage(definition, job, events)).ToArray();
        return new JobStatusResponse(
            job.Id,
            job.ProjectId,
            job.SpecVersionId,
            job.Status.ToString(),
            stages,
            job.LastSequence,
            job.CorrelationId,
            job.ErrorCode is null
                ? null
                : new JobFailureResponse(
                    stages.FirstOrDefault(static stage => stage.Status == "Failed")?.Id ?? "worker",
                    job.ErrorCode,
                    job.ErrorMessage ?? "The job failed.",
                    events.Count > 0 && events[^1].Retryable),
            job.ValidationRunId,
            job.OutputArtifactId);
    }

    public static JobEventResponse Event(JobEventRecord jobEvent)
    {
        var terminal = jobEvent.EventType is "job.succeeded" or "job.failed";
        var status = terminal
            ? jobEvent.EventType == "job.succeeded" ? "Succeeded" : "Failed"
            : jobEvent.EventType.EndsWith(".started", StringComparison.Ordinal)
                ? "Running"
                : jobEvent.Status.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    ? "Failed"
                    : jobEvent.EventType.EndsWith(".completed", StringComparison.Ordinal)
                        ? "Done"
                        : "Running";
        return new JobEventResponse(
            jobEvent.Sequence,
            terminal ? "terminal" : "stage",
            jobEvent.Stage,
            status,
            jobEvent.Summary,
            jobEvent.OccurredAt,
            DurationMs: null,
            FindingCount: null,
            jobEvent.Retryable,
            jobEvent.CorrelationId);
    }

    public static ArtifactResponse Artifact(ArtifactRecord artifact, int versionNumber) => new(
        artifact.Id,
        artifact.Kind switch
        {
            ArtifactKind.Ideation => "Ideation",
            ArtifactKind.ProductRequirements => "Prd",
            ArtifactKind.TechnicalRequirements => "Trd",
            ArtifactKind.TargetInstruction => "AgentInstruction",
            _ => artifact.Kind.ToString()
        },
        artifact.TargetId?.Contains(',', StringComparison.Ordinal) == true ? null : artifact.TargetId,
        artifact.Path,
        artifact.Status switch
        {
            ArtifactStatus.Current => "Valid",
            ArtifactStatus.Stale => "Stale",
            _ => "Error"
        },
        versionNumber,
        artifact.ContentHash,
        artifact.CreatedAt,
        artifact.Status == ArtifactStatus.Stale
            ? "This artifact no longer matches the current specification."
            : null);

    public static async Task<ValidationRunResponse> ValidationRunAsync(
        ValidationRunRecord run,
        AjureStore store,
        CancellationToken cancellationToken)
    {
        var version = await store.GetVersionAsync(run.SpecVersionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The validation run version was not found.");
        var baseVersion = run.BaseVersionId is { } baseId
            ? await store.GetVersionAsync(baseId, cancellationToken).ConfigureAwait(false)
            : null;
        var previous = baseVersion is null
            ? null
            : await store.GetLatestValidationRunAsync(baseVersion.Id, cancellationToken).ConfigureAwait(false);
        var scores = ParseScores(run.AreaScoresJson);
        var failedGates = run.HardGates.ToHashSet(StringComparer.Ordinal);
        var ready = string.Equals(run.Status, "Ready", StringComparison.Ordinal)
            || string.Equals(run.Status, "SimulatedReady", StringComparison.Ordinal);

        return new ValidationRunResponse(
            run.Id,
            run.SpecVersionId,
            version.Number,
            baseVersion?.Number,
            run.Score,
            previous?.Score,
            Areas(scores),
            GateIds.Select(id => new HardGateResponse(
                id,
                id,
                !failedGates.Contains(id),
                failedGates.Contains(id) ? "Resolve this hard gate and run validation again." : null)).ToArray(),
            ParseFindings(run.FindingsJson),
            ParseRegressions(run.RegressionsJson),
            ready,
            ready ? null : $"Validation status is {run.Status}.",
            run.CompletedAt ?? run.StartedAt);
    }

    private static string ProjectStatus(SpecVersionRecord? version, JobRecord? job)
    {
        if (job?.Status is JobStatus.Queued or JobStatus.Running)
        {
            return job.Kind switch
            {
                JobKind.Analyze => "Analyzing",
                JobKind.Generate => "Generating",
                JobKind.Validate => "Validating",
                _ => version?.Status.ToString() ?? "Draft"
            };
        }

        return version?.Status switch
        {
            SpecVersionStatus.Failed => "NeedsDecision",
            null => "Draft",
            _ => version.Status.ToString()
        };
    }

    private static JobStageResponse Stage(
        StageDefinition definition,
        JobRecord job,
        IReadOnlyList<JobEventRecord> events)
    {
        var matches = definition.Id == "intake"
            ? events.Where(static item => item.EventType == "job.started").ToArray()
            : events.Where(item => string.Equals(item.Stage, definition.Id, StringComparison.Ordinal)).ToArray();
        var started = matches.FirstOrDefault();
        var completed = matches.LastOrDefault(static item =>
            item.EventType.EndsWith(".completed", StringComparison.Ordinal)
            || item.EventType is "job.succeeded" or "job.failed");
        var status = matches.Any(static item =>
                item.Status.Contains("fail", StringComparison.OrdinalIgnoreCase))
            ? "Failed"
            : completed is not null
                ? "Done"
                : started is not null
                    ? "Running"
                    : "Pending";
        if (job.Status == JobStatus.Succeeded && status == "Pending")
        {
            status = "Done";
        }
        else if (job.Status == JobStatus.Failed
                 && status == "Running"
                 && !events.Any(static item => item.EventType == "job.retrying"))
        {
            status = "Failed";
        }

        var duration = started is null
            ? null
            : (long?)Math.Max(
                0,
                ((completed?.OccurredAt ?? job.CompletedAt ?? DateTimeOffset.UtcNow) - started.OccurredAt)
                .TotalMilliseconds);
        return new JobStageResponse(
            definition.Id,
            definition.Label,
            matches.LastOrDefault()?.Summary ?? definition.Detail,
            status,
            started?.OccurredAt,
            duration,
            FindingCount: null);
    }

    private static StageDefinition[] StageDefinitions(JobKind kind) => kind switch
    {
        JobKind.Analyze =>
        [
            new("idea-analysis", "Idea analysis", "Normalize explicit intent and constraints."),
            new("decision-facilitation", "Decision facilitation", "Identify implementation-changing decisions.")
        ],
        JobKind.Generate =>
        [
            new("intake", "Intake", "Load approved project intent and decisions."),
            new("spec-architect", "Spec architect", "Create the canonical ProjectSpec."),
            new("deterministic-validation", "Deterministic validation", "Run traceability and artifact checks."),
            new("independent-review", "Independent review", "Run isolated multi-model reviewers."),
            new("repair", "Repair", "Repair confirmed findings within affected IDs."),
            new("rendering", "Rendering", "Render native Markdown artifacts."),
            new("validation", "Ready decision", "Apply score and hard gates.")
        ],
        JobKind.Validate =>
        [
            new("deterministic-validation", "Deterministic validation", "Run traceability and artifact checks."),
            new("independent-review", "Independent review", "Run isolated multi-model reviewers."),
            new("repair", "Repair", "Repair confirmed findings within affected IDs."),
            new("rendering", "Rendering", "Render native Markdown artifacts."),
            new("validation", "Ready decision", "Apply score and hard gates.")
        ],
        JobKind.Export => [new("export", "Export", "Create the deterministic Ready ZIP.")],
        _ => []
    };

    private static AreaScores ParseScores(string json)
    {
        try
        {
            return SpecJson.Deserialize<AreaScores>(json);
        }
        catch (JsonException)
        {
            return new AreaScores
            {
                IntentCoverage = 0,
                Traceability = 0,
                Testability = 0,
                TechnicalExecutability = 0,
                TargetAgentFitness = 0,
                UxOperationsCompleteness = 0
            };
        }
    }

    private static ScoreAreaResponse[] Areas(AreaScores scores) =>
    [
        new("intentCoverage", "Intent coverage", scores.IntentCoverage, AreaScores.IntentCoverageMax, "Median reviewer score."),
        new("traceability", "Traceability", scores.Traceability, AreaScores.TraceabilityMax, "Median reviewer score."),
        new("testability", "Testability", scores.Testability, AreaScores.TestabilityMax, "Median reviewer score."),
        new("technicalExecutability", "Technical executability", scores.TechnicalExecutability, AreaScores.TechnicalExecutabilityMax, "Median reviewer score."),
        new("targetAgentFitness", "Target agent fitness", scores.TargetAgentFitness, AreaScores.TargetAgentFitnessMax, "Median reviewer score."),
        new("uxOperationsCompleteness", "UX and operations", scores.UxOperationsCompleteness, AreaScores.UxOperationsCompletenessMax, "Median reviewer score.")
    ];

    private static FindingResponse[] ParseFindings(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var findings = new List<FindingResponse>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                AddFindings(root, findings);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("deterministic", out var deterministic))
                {
                    AddFindings(deterministic, findings);
                }

                if (root.TryGetProperty("clusters", out var clusters))
                {
                    AddFindings(clusters, findings);
                }
            }

            return findings.DistinctBy(static finding => finding.Id).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddFindings(JsonElement values, List<FindingResponse> findings)
    {
        if (values.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var value in values.EnumerateArray())
        {
            var id = String(value, "id")
                ?? String(value, "fingerprint")
                ?? Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(value.GetRawText())))
                    .ToLowerInvariant();
            var severity = NormalizeSeverity(String(value, "severity"));
            var title = String(value, "statement") ?? "Validation finding";
            var relatedIds = Strings(value, "affectedIds");
            var evidence = string.Join(" ", Strings(value, "evidence"));
            var ruleKey = String(value, "ruleKey") ?? RuleKeys.Other;
            var requiresDecision = Boolean(value, "requiresUserDecision");
            var suggestion = String(value, "suggestedResolution");
            findings.Add(new FindingResponse(
                id,
                severity,
                title,
                evidence,
                relatedIds,
                ArtifactPath(id, ruleKey),
                Line: null,
                AutoFixable: !requiresDecision && !string.IsNullOrWhiteSpace(suggestion),
                suggestion,
                DecisionId: null,
                Resolved: false));
        }
    }

    private static RegressionItemResponse[] ParseRegressions(string json)
    {
        try
        {
            var regressions = SpecJson.Deserialize<RegressionFinding[]>(json);
            return regressions.Select((regression, index) => new RegressionItemResponse(
                $"reg-{index + 1}",
                regression.Type.ToString(),
                regression.Severity.ToString(),
                regression.Subject,
                string.Empty,
                regression.Detail,
                regression.Detail,
                !regression.RequiresApproval)).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ArtifactPath(string id, string ruleKey)
    {
        foreach (var path in new[]
                 {
                     DocumentRenderer.IdeationPath,
                     DocumentRenderer.PrdPath,
                     DocumentRenderer.TrdPath
                 })
        {
            if (id.EndsWith(path, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return ruleKey is RuleKeys.MissingAuthorization
            or RuleKeys.MissingFailureHandling
            or RuleKeys.UnjustifiedComponent
            or RuleKeys.SecurityGap
            or RuleKeys.OperationsGap
            ? DocumentRenderer.TrdPath
            : DocumentRenderer.PrdPath;
    }

    private static string NormalizeSeverity(string? severity) =>
        Enum.TryParse<FindingSeverity>(severity, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : FindingSeverity.Minor.ToString();

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string[] Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString() ?? string.Empty)
                .ToArray()
            : [];

    private static bool Boolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();

    private sealed record StageDefinition(string Id, string Label, string Detail);
}
