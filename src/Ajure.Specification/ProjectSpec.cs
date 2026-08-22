using System.Globalization;
using System.Text.Json.Serialization;

namespace Ajure.Specification;

public enum SpecStatus
{
    Draft,
    Validating,
    Ready,
    Superseded
}

public enum Priority
{
    Must,
    Should,
    Could
}

public enum VerificationType
{
    Automated,
    Manual,
    Api,
    Ui
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

public enum MetricKind
{
    UserOutcome,
    Product
}

public sealed record Project
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string OwnerId { get; init; }

    public string Locale { get; init; } = "ko-KR";

    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record SpecVersion
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required int Number { get; init; }

    public required SpecStatus Status { get; init; }

    public Guid? BaseVersionId { get; init; }

    public string InputHash { get; init; } = string.Empty;

    public string GenerationProfile { get; init; } = "balanced";

    public IReadOnlyList<string> TargetIds { get; init; } = [];

    public string SpecHash { get; init; } = string.Empty;

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>Human readable version shown in every generated document.</summary>
    [JsonIgnore]
    public string Label => "v" + Number.ToString(CultureInfo.InvariantCulture);
}

public sealed record Persona
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Situation { get; init; } = string.Empty;

    public string Motivation { get; init; } = string.Empty;

    public string ExpectedOutcome { get; init; } = string.Empty;

    public string Constraints { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}

public sealed record Goal
{
    public required string Id { get; init; }

    public required string Statement { get; init; }

    public string SuccessMetric { get; init; } = string.Empty;
}

public sealed record Journey
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Entry { get; init; } = string.Empty;

    public IReadOnlyList<string> Steps { get; init; } = [];

    public string SuccessExit { get; init; } = string.Empty;

    public IReadOnlyList<string> FailurePaths { get; init; } = [];

    public IReadOnlyList<string> RequirementIds { get; init; } = [];
}

/// <summary>Functional and non-functional requirements share one shape; the identifier prefix separates them.</summary>
public sealed record Requirement
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Statement { get; init; }

    public required Priority Priority { get; init; }

    public string Rationale { get; init; } = string.Empty;

    /// <summary>Measured value or verification method. Required for non-functional requirements.</summary>
    public string? Measurement { get; init; }

    public IReadOnlyList<string> AcceptanceCriteriaIds { get; init; } = [];

    public IReadOnlyList<string> TechnicalDecisionIds { get; init; } = [];

    public IReadOnlyList<string> JourneyIds { get; init; } = [];

    public IReadOnlyList<string> SourceDecisionIds { get; init; } = [];

    /// <summary>Explicit "no technical impact" marker accepted by the traceability check.</summary>
    public bool NoTechnicalImpact { get; init; }
}

public sealed record AcceptanceCriterion
{
    public required string Id { get; init; }

    public required string Given { get; init; }

    public required string When { get; init; }

    public required string Then { get; init; }

    public required VerificationType VerificationType { get; init; }

    public IReadOnlyList<string> RequirementIds { get; init; } = [];
}

public sealed record TechnicalDecision
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Decision { get; init; }

    public string Rationale { get; init; } = string.Empty;

    public IReadOnlyList<string> Alternatives { get; init; } = [];

    public IReadOnlyList<string> RequirementIds { get; init; } = [];

    public bool IsLocked { get; init; }
}

public sealed record UxDecision
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Decision { get; init; }

    public string Rationale { get; init; } = string.Empty;

    public IReadOnlyList<string> RequirementIds { get; init; } = [];
}

public sealed record Risk
{
    public required string Id { get; init; }

    public required string Statement { get; init; }

    public RiskLevel Likelihood { get; init; } = RiskLevel.Medium;

    public RiskLevel Impact { get; init; } = RiskLevel.Medium;

    public string Mitigation { get; init; } = string.Empty;
}

public sealed record GlossaryEntry
{
    public required string Term { get; init; }

    public required string Definition { get; init; }
}

public sealed record OpenDecision
{
    public required string Id { get; init; }

    public required string Question { get; init; }

    public IReadOnlyList<string> Options { get; init; } = [];

    public string Recommendation { get; init; } = string.Empty;

    public bool IsCritical { get; init; }

    /// <summary>Null while the decision is unresolved.</summary>
    public string? Resolution { get; init; }
}

public sealed record EvidenceItem
{
    public required string Statement { get; init; }

    public bool IsVerified { get; init; }

    public string VerificationMethod { get; init; } = string.Empty;
}

public sealed record ConsideredOption
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public bool IsChosen { get; init; }

    public string RejectionReason { get; init; } = string.Empty;
}

public sealed record SuccessMetric
{
    public required string Name { get; init; }

    public required string Target { get; init; }

    public MetricKind Kind { get; init; } = MetricKind.UserOutcome;
}

public sealed record StateMatrixEntry
{
    public required string Screen { get; init; }

    public string Loading { get; init; } = string.Empty;

    public string Empty { get; init; } = string.Empty;

    public string Failure { get; init; } = string.Empty;

    public string Success { get; init; } = string.Empty;

    public string Disabled { get; init; } = string.Empty;

    public string Permission { get; init; } = string.Empty;

    /// <summary>Reason recorded when a state does not apply to the screen.</summary>
    public string? NotApplicableReason { get; init; }
}

public sealed record BusinessRule
{
    public required string Statement { get; init; }

    /// <summary>Lower values win when rules overlap.</summary>
    public int Precedence { get; init; }
}

public sealed record AnalyticsEvent
{
    public required string Name { get; init; }

    public IReadOnlyList<string> Properties { get; init; } = [];

    public string Purpose { get; init; } = string.Empty;
}

public sealed record ReleaseScope
{
    public IReadOnlyList<string> Mvp { get; init; } = [];

    public IReadOnlyList<string> Later { get; init; } = [];

    public IReadOnlyList<string> BlockingConditions { get; init; } = [];
}

public sealed record ComponentSpec
{
    public required string Name { get; init; }

    public required string Responsibility { get; init; }

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> RequirementIds { get; init; } = [];
}

public sealed record RepositoryArea
{
    public required string Path { get; init; }

    public required string Ownership { get; init; }
}

public sealed record DataEntity
{
    public required string Name { get; init; }

    public IReadOnlyList<string> Fields { get; init; } = [];

    public IReadOnlyList<string> Relationships { get; init; } = [];

    public string Retention { get; init; } = string.Empty;
}

public sealed record ApiContract
{
    public required string Operation { get; init; }

    public required string Purpose { get; init; }

    public string Auth { get; init; } = string.Empty;

    public string Request { get; init; } = string.Empty;

    public string SuccessResponse { get; init; } = string.Empty;

    public IReadOnlyList<string> ErrorResponses { get; init; } = [];

    public string Idempotency { get; init; } = string.Empty;

    public string TimeoutAndRetry { get; init; } = string.Empty;

    public IReadOnlyList<string> RequirementIds { get; init; } = [];
}

public sealed record WorkflowState
{
    public required string Name { get; init; }

    public IReadOnlyList<string> AllowedTransitions { get; init; } = [];

    public string FailureHandling { get; init; } = string.Empty;
}

/// <summary>Technical content required by DOCUMENT-SPEC 5.</summary>
public sealed record TechnicalProfile
{
    public IReadOnlyList<string> Constraints { get; init; } = [];

    public IReadOnlyList<string> MustTechnologies { get; init; } = [];

    public IReadOnlyList<string> ForbiddenChoices { get; init; } = [];

    public string Architecture { get; init; } = string.Empty;

    public IReadOnlyList<string> TrustBoundaries { get; init; } = [];

    public IReadOnlyList<ComponentSpec> Components { get; init; } = [];

    public IReadOnlyList<RepositoryArea> RepositoryStructure { get; init; } = [];

    public IReadOnlyList<DataEntity> DataEntities { get; init; } = [];

    public IReadOnlyList<ApiContract> ApiContracts { get; init; } = [];

    public IReadOnlyList<WorkflowState> States { get; init; } = [];

    public IReadOnlyList<string> Security { get; init; } = [];

    public IReadOnlyList<string> Reliability { get; init; } = [];

    public IReadOnlyList<string> Observability { get; init; } = [];

    public IReadOnlyList<string> Deployment { get; init; } = [];

    public IReadOnlyList<string> TestingStrategy { get; init; } = [];

    public IReadOnlyList<string> ImplementationOrder { get; init; } = [];
}

/// <summary>The single semantic source of truth (TRD 6.3). Documents are rendered from this record.</summary>
public sealed record ProjectSpec
{
    public required string ProjectName { get; init; }

    public required string Vision { get; init; }

    public required string Problem { get; init; }

    public IReadOnlyList<Persona> Personas { get; init; } = [];

    public IReadOnlyList<Goal> Goals { get; init; } = [];

    public IReadOnlyList<string> NonGoals { get; init; } = [];

    public IReadOnlyList<Journey> Journeys { get; init; } = [];

    public IReadOnlyList<Requirement> Requirements { get; init; } = [];

    public IReadOnlyList<Requirement> NonFunctionalRequirements { get; init; } = [];

    public IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; } = [];

    public IReadOnlyList<TechnicalDecision> TechnicalDecisions { get; init; } = [];

    public IReadOnlyList<UxDecision> UxDecisions { get; init; } = [];

    public IReadOnlyList<Risk> Risks { get; init; } = [];

    public IReadOnlyList<GlossaryEntry> Glossary { get; init; } = [];

    public IReadOnlyList<OpenDecision> OpenDecisions { get; init; } = [];

    public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];

    public IReadOnlyList<ConsideredOption> OptionsConsidered { get; init; } = [];

    public IReadOnlyList<string> ValuePropositions { get; init; } = [];

    public IReadOnlyList<SuccessMetric> SuccessMetrics { get; init; } = [];

    public IReadOnlyList<string> LockedDecisions { get; init; } = [];

    public IReadOnlyList<StateMatrixEntry> StateMatrix { get; init; } = [];

    public IReadOnlyList<BusinessRule> BusinessRules { get; init; } = [];

    public IReadOnlyList<AnalyticsEvent> AnalyticsEvents { get; init; } = [];

    public ReleaseScope Release { get; init; } = new();

    public TechnicalProfile Technical { get; init; } = new();

    /// <summary>Every identifier the specification defines, sorted, used for affected-id filtering.</summary>
    public IReadOnlyList<string> AllIds()
    {
        var ids = new List<string>();
        ids.AddRange(Goals.Select(static goal => goal.Id));
        ids.AddRange(Personas.Select(static persona => persona.Id));
        ids.AddRange(Journeys.Select(static journey => journey.Id));
        ids.AddRange(Requirements.Select(static requirement => requirement.Id));
        ids.AddRange(NonFunctionalRequirements.Select(static requirement => requirement.Id));
        ids.AddRange(AcceptanceCriteria.Select(static criterion => criterion.Id));
        ids.AddRange(TechnicalDecisions.Select(static decision => decision.Id));
        ids.AddRange(UxDecisions.Select(static decision => decision.Id));
        ids.AddRange(Risks.Select(static risk => risk.Id));
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }
}
