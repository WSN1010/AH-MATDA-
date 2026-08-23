using Ajure.Agent;

namespace Ajure.Infrastructure;

public enum SpecVersionStatus
{
    Draft,
    Analyzing,
    Validating,
    Ready,
    NeedsDecision,
    Failed,
    Superseded
}

public enum JobKind
{
    Analyze,
    Generate,
    Validate,
    Export
}

public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

public enum ArtifactKind
{
    Ideation,
    ProductRequirements,
    TechnicalRequirements,
    TargetInstruction,
    ValidationReport,
    ExportZip
}

public enum ArtifactStatus
{
    Current,
    Stale,
    Proposed
}

public sealed record ProjectRecord(
    Guid Id,
    string Name,
    string OwnerId,
    string Locale,
    string Idea,
    DateTimeOffset CreatedAt);

public sealed record SpecVersionRecord(
    Guid Id,
    Guid ProjectId,
    int Number,
    SpecVersionStatus Status,
    Guid? BaseVersionId,
    string InputHash,
    string GenerationProfile,
    string[] TargetIds,
    bool IsSimulated,
    string? SpecBlobName,
    string? SpecHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);

public sealed record DecisionRecord(
    Guid ProjectId,
    string Id,
    string Question,
    string[] Options,
    string Recommended,
    string? Answer,
    bool Critical,
    DateTimeOffset UpdatedAt,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? Impacts = null,
    DecisionSeverity Severity = DecisionSeverity.Defaultable,
    string? AnswerOptionId = null,
    string? AnswerText = null);

public sealed record JobRecord(
    Guid Id,
    JobKind Kind,
    Guid ProjectId,
    Guid? SpecVersionId,
    Guid? BaseVersionId,
    JobStatus Status,
    long LastSequence,
    bool IsSimulated,
    Guid? ValidationRunId,
    Guid? OutputArtifactId,
    string? ErrorCode,
    string? ErrorMessage,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record JobMessage(
    Guid JobId,
    JobKind Kind,
    Guid ProjectId,
    Guid? SpecVersionId,
    Guid? BaseVersionId);

public sealed record DequeuedJob(
    JobMessage Message,
    string MessageId,
    string PopReceipt,
    long DequeueCount);

public sealed record PoisonJobMessage(
    JobMessage Message,
    string ErrorType,
    DateTimeOffset FailedAt);

public sealed record ModelProviderCredentialRecord(
    string ProviderId,
    string ProtectedApiKey,
    string Model,
    DateTimeOffset UpdatedAt);

public sealed record JobEventRecord(
    Guid JobId,
    long Sequence,
    string EventType,
    string Stage,
    string Status,
    string Summary,
    DateTimeOffset OccurredAt,
    bool Retryable,
    string CorrelationId);

public sealed record ArtifactRecord(
    Guid Id,
    Guid SpecVersionId,
    ArtifactKind Kind,
    string? TargetId,
    string Path,
    string ContentHash,
    string TemplateVersion,
    ArtifactStatus Status,
    string BlobName,
    string ContentType,
    DateTimeOffset CreatedAt);

public sealed record ValidationRunRecord(
    Guid Id,
    Guid SpecVersionId,
    Guid? BaseVersionId,
    int Iteration,
    string Status,
    decimal Score,
    string[] HardGates,
    string FindingsJson,
    string[] ModelIds,
    bool IsSimulated,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    public string[] SessionIds { get; init; } = [];

    public string ExecutionTraceJson { get; init; } = "[]";

    public string AreaScoresJson { get; init; } = "{}";

    public string RegressionsJson { get; init; } = "[]";

    public bool TieBreakUsed { get; init; }
}

public sealed record SemanticDiff(
    Guid VersionId,
    Guid BaseVersionId,
    string[] Added,
    string[] Removed,
    string[] Changed);
