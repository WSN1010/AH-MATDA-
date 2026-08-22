using System.Globalization;
using Ajure.Specification;

namespace Ajure.Validation;

public enum HardGate
{
    UnresolvedCriticalDecision = 1,
    RequirementWithoutAcceptance = 2,
    CriticalContradiction = 3,
    MustRequirementMissingInTargets = 4,
    VersionOrHashMismatch = 5,
    UnapprovedMustRemoval = 6,
    UnverifiableAcceptance = 7,
    MissingSecurityDecision = 8,
    TargetPathOrSyntax = 9,
    SecretOrProductCode = 10,
    RepeatedCriticalAfterRepair = 11,
    UnresolvedEvaluatorConflict = 12,
    CopilotStageIncomplete = 13,
    InsufficientModelDiversity = 14
}

public sealed record HardGateResult
{
    public required HardGate Gate { get; init; }

    public required bool Passed { get; init; }

    public required string Reason { get; init; }

    public string Code => "HG-" + ((int)Gate).ToString("D2", CultureInfo.InvariantCulture);
}

public sealed record HardGateContext
{
    public required DeterministicResult Deterministic { get; init; }

    public IReadOnlyList<FindingCluster> Clusters { get; init; } = [];

    public IReadOnlyList<RegressionFinding> Regressions { get; init; } = [];

    /// <summary>Model ids of reviewer sessions that returned a valid envelope.</summary>
    public IReadOnlyList<string> SuccessfulModelIds { get; init; } = [];

    /// <summary>Failure codes from reviewer sessions whose envelope was rejected.</summary>
    public IReadOnlyList<string> InvalidEnvelopeCodes { get; init; } = [];

    /// <summary>Critical fingerprints confirmed in three consecutive repair iterations.</summary>
    public IReadOnlyList<string> RepeatedCriticalFingerprints { get; init; } = [];

    public bool TieBreakUsed { get; init; }

    public bool TieBreakResolved { get; init; } = true;

    /// <summary>False when a required Copilot SDK authoring or evaluation step did not complete (FR-016).</summary>
    public bool CopilotStagesCompleted { get; init; } = true;
}

/// <summary>Hard gate evaluation (EVALUATION 4). Every gate is decided from data, never from a score.</summary>
public static class HardGateEvaluator
{
    public const int RequiredModelDiversity = 2;

    public static IReadOnlyList<HardGateResult> Evaluate(HardGateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deterministic = context.Deterministic;
        var confirmedCritical = context.Clusters
            .Where(static cluster =>
                cluster.Severity == FindingSeverity.Critical && cluster.Consensus == ClusterConsensus.Confirmed)
            .ToArray();
        var distinctModels = context.SuccessfulModelIds.Distinct(StringComparer.Ordinal).Count();

        return
        [
            Result(
                HardGate.UnresolvedCriticalDecision,
                !deterministic.HasUnresolvedCriticalDecisions,
                "An unresolved critical decision remains."),
            Result(
                HardGate.RequirementWithoutAcceptance,
                deterministic.AcceptanceCoverageComplete,
                "At least one requirement has no acceptance criterion."),
            Result(
                HardGate.CriticalContradiction,
                !confirmedCritical.Any(static cluster =>
                    string.Equals(cluster.RuleKey, RuleKeys.ContradictionPrdTrd, StringComparison.Ordinal)),
                "A confirmed critical contradiction exists between the PRD and the TRD."),
            Result(
                HardGate.MustRequirementMissingInTargets,
                deterministic.MustRequirementsMissingFromTargets.Count == 0,
                "Must requirements missing from a target instruction file: "
                + string.Join(", ", deterministic.MustRequirementsMissingFromTargets)),
            Result(
                HardGate.VersionOrHashMismatch,
                deterministic.ArtifactVersionsConsistent
                    && !context.Regressions.Any(static regression => regression.Type == RegressionType.StaleArtifact),
                "An artifact does not match the current spec version or hash."),
            Result(
                HardGate.UnapprovedMustRemoval,
                !context.Regressions.Any(static regression =>
                    regression.Type == RegressionType.Removed
                    && regression.Severity == FindingSeverity.Critical
                    && regression.RequiresApproval),
                "A baseline Must requirement was removed without approval."),
            Result(
                HardGate.UnverifiableAcceptance,
                deterministic.AcceptanceCriteriaVerifiable
                    && !confirmedCritical.Any(static cluster =>
                        string.Equals(cluster.RuleKey, RuleKeys.UnverifiableAcceptanceCriterion, StringComparison.Ordinal)),
                "An acceptance criterion cannot be verified against an implementation result."),
            Result(
                HardGate.MissingSecurityDecision,
                deterministic.SecurityDecisionsPresent
                    && !confirmedCritical.Any(static cluster =>
                        string.Equals(cluster.RuleKey, RuleKeys.MissingAuthorization, StringComparison.Ordinal)
                        || string.Equals(cluster.RuleKey, RuleKeys.SecurityGap, StringComparison.Ordinal)),
                "A required authentication, authorization or data protection decision is missing."),
            Result(
                HardGate.TargetPathOrSyntax,
                deterministic.TargetFilesValid
                    && !context.Regressions.Any(static regression => regression.Type == RegressionType.FormatRegression),
                "A target instruction file has an invalid path or syntax."),
            Result(
                HardGate.SecretOrProductCode,
                !deterministic.SecretsOrCodeDetected,
                "An artifact contains product implementation code or a real secret."),
            Result(
                HardGate.RepeatedCriticalAfterRepair,
                context.RepeatedCriticalFingerprints.Count == 0,
                "The same critical finding survived three repair iterations."),
            Result(
                HardGate.UnresolvedEvaluatorConflict,
                !context.TieBreakUsed || context.TieBreakResolved,
                "Evaluator results still conflict after the single allowed tie-break."),
            Result(
                HardGate.CopilotStageIncomplete,
                context.CopilotStagesCompleted,
                "A required Copilot SDK authoring or evaluation step did not complete."),
            Result(
                HardGate.InsufficientModelDiversity,
                distinctModels >= RequiredModelDiversity && context.InvalidEnvelopeCodes.Count == 0,
                distinctModels < RequiredModelDiversity
                    ? $"Successful evaluations covered {distinctModels} distinct model ids, {RequiredModelDiversity} are required."
                    : "At least one required evaluation envelope was invalid: "
                        + string.Join(", ", context.InvalidEnvelopeCodes))
        ];
    }

    private static HardGateResult Result(HardGate gate, bool passed, string failureReason) => new()
    {
        Gate = gate,
        Passed = passed,
        Reason = passed ? "Passed." : failureReason
    };
}

/// <summary>Detects a critical fingerprint that survives repeated repair iterations (HG-11).</summary>
public static class CriticalRepeatDetector
{
    public const int RepeatLimit = 3;

    /// <summary>
    /// Returns fingerprints that are confirmed Critical in <see cref="RepeatLimit"/> consecutive iterations,
    /// oldest iteration first.
    /// </summary>
    public static IReadOnlyList<string> Detect(IReadOnlyList<IReadOnlyList<FindingCluster>> iterations)
    {
        ArgumentNullException.ThrowIfNull(iterations);
        if (iterations.Count < RepeatLimit)
        {
            return [];
        }

        var perIteration = iterations
            .Select(static clusters => clusters
                .Where(static cluster =>
                    cluster.Severity == FindingSeverity.Critical && cluster.Consensus == ClusterConsensus.Confirmed)
                .Select(static cluster => cluster.Fingerprint)
                .ToHashSet(StringComparer.Ordinal))
            .ToArray();

        var repeated = new HashSet<string>(StringComparer.Ordinal);
        for (var start = 0; start + RepeatLimit <= perIteration.Length; start++)
        {
            var window = perIteration.Skip(start).Take(RepeatLimit).ToArray();
            foreach (var fingerprint in window[0].Where(fingerprint => window.All(set => set.Contains(fingerprint))))
            {
                repeated.Add(fingerprint);
            }
        }

        return [.. repeated.OrderBy(static fingerprint => fingerprint, StringComparer.Ordinal)];
    }
}

public sealed record RepairInput
{
    public required IReadOnlyList<FindingCluster> Clusters { get; init; }

    /// <summary>Editing is limited to the union of the affected ids.</summary>
    public required IReadOnlyList<string> AllowedIds { get; init; }
}

/// <summary>Selects and orders the repair candidates (EVALUATION 6, "보정 입력").</summary>
public static class RepairInputSelector
{
    public static RepairInput Select(IEnumerable<FindingCluster> clusters)
    {
        ArgumentNullException.ThrowIfNull(clusters);

        var selected = clusters
            .Where(static cluster =>
                cluster.Consensus == ClusterConsensus.Confirmed
                && !cluster.RequiresUserDecision
                && cluster.Evidence.Count > 0)
            .OrderByDescending(static cluster => cluster.Severity)
            .ThenBy(static cluster => string.Join(",", cluster.AffectedIds), StringComparer.Ordinal)
            .ThenBy(static cluster => cluster.Fingerprint, StringComparer.Ordinal)
            .ToArray();

        return new RepairInput
        {
            Clusters = selected,
            AllowedIds =
            [
                .. selected
                    .SelectMany(static cluster => cluster.AffectedIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static id => id, StringComparer.Ordinal)
            ]
        };
    }
}

public enum ReadyStatus
{
    Ready,
    NeedsDecision,
    Failed
}

public sealed record ReadyDecision
{
    public required ReadyStatus Status { get; init; }

    public required decimal Score { get; init; }

    public required IReadOnlyList<HardGateResult> Gates { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public bool IsReady => Status == ReadyStatus.Ready;
}

/// <summary>Ready judgement from EVALUATION 2.</summary>
public static class ReadyEvaluator
{
    public const decimal ReadyScore = 90m;

    public static ReadyDecision Evaluate(
        AreaScores scores,
        HardGateContext context,
        IReadOnlyList<RegressionFinding> regressions)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(regressions);

        var gates = HardGateEvaluator.Evaluate(context);
        var total = ScoreAggregator.Round(scores.Total);
        var reasons = new List<string>();

        reasons.AddRange(gates
            .Where(static gate => !gate.Passed)
            .Select(static gate => $"{gate.Code}: {gate.Reason}"));

        // Confirmed Critical findings block Ready on their own. Major and Minor findings only move the score.
        reasons.AddRange(context.Clusters
            .Where(static cluster =>
                cluster.Severity == FindingSeverity.Critical && cluster.Consensus == ClusterConsensus.Confirmed)
            .Select(static cluster => $"Confirmed critical finding: {cluster.Statement}"));

        if (total < ReadyScore)
        {
            reasons.Add($"The total score {ScoreAggregator.Format(total)} is below {ScoreAggregator.Format(ReadyScore)}.");
        }

        reasons.AddRange(regressions
            .Where(static regression => regression.RequiresApproval && regression.Severity == FindingSeverity.Critical)
            .Select(static regression => $"{regression.Type}: {regression.Detail}"));

        if (reasons.Count == 0)
        {
            return new ReadyDecision { Status = ReadyStatus.Ready, Score = total, Gates = gates };
        }

        var needsDecision = context.Clusters.Any(static cluster => cluster.RequiresUserDecision)
            || regressions.Any(static regression => regression.RequiresApproval)
            || gates.Any(static gate =>
                !gate.Passed
                && gate.Gate is HardGate.UnresolvedCriticalDecision
                    or HardGate.RepeatedCriticalAfterRepair
                    or HardGate.UnresolvedEvaluatorConflict);

        return new ReadyDecision
        {
            Status = needsDecision ? ReadyStatus.NeedsDecision : ReadyStatus.Failed,
            Score = total,
            Gates = gates,
            Reasons = reasons
        };
    }
}
