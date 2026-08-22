using System.Security.Cryptography;
using System.Text;

namespace Ajure.Validation;

public enum FindingSeverity
{
    Minor,
    Major,
    Critical
}

/// <summary>The closed rule key set from EVALUATION 6.</summary>
public static class RuleKeys
{
    public const string MissingAcceptanceCriterion = "missing_ac";
    public const string UnverifiableAcceptanceCriterion = "unverifiable_ac";
    public const string ContradictionPrdTrd = "contradiction_prd_trd";
    public const string UndefinedTerm = "undefined_term";
    public const string MissingState = "missing_state";
    public const string MissingAuthorization = "missing_authz";
    public const string MissingFailureHandling = "missing_failure_handling";
    public const string UnjustifiedComponent = "unjustified_component";
    public const string ScopeLeak = "scope_leak";
    public const string NonGoalViolation = "nongoal_violation";
    public const string AmbiguousMetric = "ambiguous_metric";
    public const string TraceabilityBreak = "traceability_break";
    public const string TargetFileMismatch = "target_file_mismatch";
    public const string SecurityGap = "security_gap";
    public const string OperationsGap = "ops_gap";
    public const string Other = "other";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        MissingAcceptanceCriterion,
        UnverifiableAcceptanceCriterion,
        ContradictionPrdTrd,
        UndefinedTerm,
        MissingState,
        MissingAuthorization,
        MissingFailureHandling,
        UnjustifiedComponent,
        ScopeLeak,
        NonGoalViolation,
        AmbiguousMetric,
        TraceabilityBreak,
        TargetFileMismatch,
        SecurityGap,
        OperationsGap,
        Other
    };

    /// <summary>Unknown or empty keys collapse to <c>other</c> instead of failing the run.</summary>
    public static string Normalize(string? ruleKey) =>
        ruleKey is not null && All.Contains(ruleKey) ? ruleKey : Other;
}

public sealed record Finding
{
    public required string Id { get; init; }

    public required FindingSeverity Severity { get; init; }

    public string Category { get; init; } = string.Empty;

    public required string RuleKey { get; init; }

    public required string Statement { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public IReadOnlyList<string> AffectedIds { get; init; } = [];

    public string SuggestedResolution { get; init; } = string.Empty;

    public bool RequiresUserDecision { get; init; }
}

/// <summary>Six evaluation areas from EVALUATION 3, with their maximum points.</summary>
public sealed record AreaScores
{
    public required decimal IntentCoverage { get; init; }

    public required decimal Traceability { get; init; }

    public required decimal Testability { get; init; }

    public required decimal TechnicalExecutability { get; init; }

    public required decimal TargetAgentFitness { get; init; }

    public required decimal UxOperationsCompleteness { get; init; }

    public decimal Total =>
        IntentCoverage
        + Traceability
        + Testability
        + TechnicalExecutability
        + TargetAgentFitness
        + UxOperationsCompleteness;

    public const decimal IntentCoverageMax = 25m;
    public const decimal TraceabilityMax = 20m;
    public const decimal TestabilityMax = 20m;
    public const decimal TechnicalExecutabilityMax = 15m;
    public const decimal TargetAgentFitnessMax = 10m;
    public const decimal UxOperationsCompletenessMax = 10m;
}

public sealed record ReviewEnvelope
{
    public required bool ReviewComplete { get; init; }

    public required AreaScores Scores { get; init; }

    public IReadOnlyList<Finding> Findings { get; init; } = [];
}

/// <summary>One reviewer session result. Role and model id come from the deterministic assignment.</summary>
public sealed record ReviewResult
{
    public required string Role { get; init; }

    public required string ModelId { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public required ReviewEnvelope Envelope { get; init; }
}

public static class FindingFingerprint
{
    /// <summary>SHA-256 over <c>ruleKey|sortedAffectedIds</c> (EVALUATION 6).</summary>
    public static string Compute(string ruleKey, IEnumerable<string> affectedIds)
    {
        ArgumentNullException.ThrowIfNull(affectedIds);
        var sorted = affectedIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);
        var payload = $"{RuleKeys.Normalize(ruleKey)}|{string.Join(",", sorted)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
