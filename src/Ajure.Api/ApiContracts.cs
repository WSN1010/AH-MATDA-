using System.Text.Json;

namespace Ajure.Api;

public sealed record CreateProjectRequest(
    string Name,
    JsonElement Idea,
    string[]? TargetIds,
    string? Locale);

public sealed record IdeaInputResponse(
    string Summary,
    string Constraints,
    string Exclusions,
    string ExistingDocs);

public sealed record UpdateDecisionRequest(
    string? OptionId,
    string? Text,
    string? Answer);

public sealed record CreateVersionRequest(
    Guid? BaseVersionId,
    string? GenerationProfile,
    string[]? TargetIds);

public sealed record UpdateArtifactRequest(string Content);

public sealed record SaveModelProviderRequest(string? ApiKey, string? Model);

public sealed record ModelProviderResponse(
    string Id,
    string DisplayName,
    bool Configured,
    string? Source,
    string Model,
    bool Editable,
    string? ErrorCode);

public sealed record ModelProviderListResponse(
    int RequiredCount,
    int ConfiguredCount,
    ModelProviderResponse[] Providers);

public sealed record JobAcceptedResponse(Guid JobId);

public sealed record ProjectSummaryResponse(
    Guid Id,
    string Name,
    string Status,
    decimal? ReadinessScore,
    Guid SpecVersionId,
    int SpecVersionNumber,
    string[] TargetIds,
    int ArtifactCount,
    int OpenCriticalDecisions,
    DateTimeOffset UpdatedAt);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string Status,
    decimal? ReadinessScore,
    Guid SpecVersionId,
    int SpecVersionNumber,
    string[] TargetIds,
    int ArtifactCount,
    int OpenCriticalDecisions,
    DateTimeOffset UpdatedAt,
    IdeaInputResponse Idea,
    DateTimeOffset CreatedAt,
    Guid? LatestRunId,
    Guid? LatestJobId,
    int? BaseVersionNumber);

public sealed record DecisionOptionResponse(string Id, string Label, string Detail);

public sealed record DecisionConflictResponse(
    string OptionId,
    string DecisionId,
    string WithOptionId,
    string Message);

public sealed record DecisionResponse(
    string Id,
    string Kind,
    string Question,
    string Why,
    string Impact,
    DecisionOptionResponse[] Options,
    string RecommendedOptionId,
    string RecommendationRationale,
    string? AnswerOptionId,
    string? AnswerText,
    DateTimeOffset? AnsweredAt,
    DecisionConflictResponse[] Conflicts);

public sealed record JobStageResponse(
    string Id,
    string Label,
    string Detail,
    string Status,
    DateTimeOffset? StartedAt,
    long? DurationMs,
    int? FindingCount);

public sealed record JobFailureResponse(
    string StageId,
    string Code,
    string Message,
    bool Retryable);

public sealed record JobStatusResponse(
    Guid JobId,
    Guid ProjectId,
    Guid? SpecVersionId,
    string Status,
    JobStageResponse[] Stages,
    long LastSequence,
    string CorrelationId,
    JobFailureResponse? Failure,
    Guid? ValidationRunId,
    Guid? OutputArtifactId);

public sealed record JobEventResponse(
    long Sequence,
    string EventType,
    string StageId,
    string Status,
    string Summary,
    DateTimeOffset OccurredAt,
    long? DurationMs,
    int? FindingCount,
    bool Retryable,
    string CorrelationId);

public sealed record ArtifactResponse(
    Guid Id,
    string Kind,
    string? TargetId,
    string Path,
    string Status,
    int SpecVersionNumber,
    string ContentHash,
    DateTimeOffset UpdatedAt,
    string? StaleReason);

public sealed record ArtifactContentResponse(
    Guid Id,
    string Kind,
    string? TargetId,
    string Path,
    string Status,
    int SpecVersionNumber,
    string ContentHash,
    DateTimeOffset UpdatedAt,
    string? StaleReason,
    string Content);

public sealed record ArtifactSaveResponse(
    ArtifactResponse Artifact,
    string[] AffectedPaths,
    string ProjectStatus);

public sealed record ScoreAreaResponse(
    string Id,
    string Label,
    decimal Score,
    decimal Max,
    string Evidence);

public sealed record HardGateResponse(
    string Id,
    string Label,
    bool Passed,
    string? Action);

public sealed record FindingResponse(
    string Id,
    string Severity,
    string Title,
    string Evidence,
    string[] RelatedIds,
    string ArtifactPath,
    int? Line,
    bool AutoFixable,
    string? Suggestion,
    string? DecisionId,
    bool Resolved);

public sealed record RegressionItemResponse(
    string Id,
    string Kind,
    string Severity,
    string RequirementId,
    string Before,
    string After,
    string Summary,
    bool Approved);

public sealed record ValidationRunResponse(
    Guid Id,
    Guid SpecVersionId,
    int SpecVersionNumber,
    int? BaseVersionNumber,
    decimal Score,
    decimal? PreviousScore,
    ScoreAreaResponse[] Areas,
    HardGateResponse[] HardGates,
    FindingResponse[] Findings,
    RegressionItemResponse[] Regression,
    bool Ready,
    string? BlockedReason,
    DateTimeOffset CompletedAt);
