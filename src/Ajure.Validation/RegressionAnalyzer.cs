using Ajure.Specification;

namespace Ajure.Validation;

public enum RegressionType
{
    Removed,
    Weakened,
    Unlinked,
    Contradicted,
    ScopeLeak,
    StateLoss,
    QualityDrop,
    StaleArtifact,
    FormatRegression
}

public sealed record RegressionFinding
{
    public required RegressionType Type { get; init; }

    public required FindingSeverity Severity { get; init; }

    /// <summary>Requirement id, screen name or artifact path the regression applies to.</summary>
    public required string Subject { get; init; }

    public required string Detail { get; init; }

    public bool RequiresApproval { get; init; } = true;
}

public sealed record ArtifactStamp
{
    public required string Path { get; init; }

    public required string SpecVersion { get; init; }

    public IReadOnlyList<string> TargetIds { get; init; } = [];
}

public sealed record RegressionInput
{
    public required ProjectSpec Baseline { get; init; }

    public required ProjectSpec Candidate { get; init; }

    public required string CandidateSpecVersion { get; init; }

    public decimal? BaselineScore { get; init; }

    public decimal? CandidateScore { get; init; }

    /// <summary>Requirement ids whose removal or weakening the user approved.</summary>
    public IReadOnlyList<string> ApprovedChangeIds { get; init; } = [];

    public IReadOnlyList<ArtifactStamp> Artifacts { get; init; } = [];
}

/// <summary>Regression comparison of two requirement graphs (EVALUATION 7, TRD 8.3).</summary>
public static class RegressionAnalyzer
{
    public const decimal MaximumScoreDrop = 5m;

    public static IReadOnlyList<RegressionFinding> Compare(RegressionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var baseline = RequirementGraph.Build(input.Baseline);
        var candidate = RequirementGraph.Build(input.Candidate);
        var approved = input.ApprovedChangeIds.ToHashSet(StringComparer.Ordinal);
        var findings = new List<RegressionFinding>();

        foreach (var previous in baseline.Nodes)
        {
            var current = candidate.Find(previous.Id);
            if (current is null)
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.Removed,
                    Severity = previous.Priority == Priority.Must ? FindingSeverity.Critical : FindingSeverity.Major,
                    Subject = previous.Id,
                    Detail = $"Requirement {previous.Id} '{previous.Title}' is missing from the candidate version.",
                    RequiresApproval = !approved.Contains(previous.Id)
                });
                continue;
            }

            if (current.Priority > previous.Priority)
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.Weakened,
                    Severity = previous.Priority == Priority.Must ? FindingSeverity.Critical : FindingSeverity.Major,
                    Subject = previous.Id,
                    Detail = $"Requirement {previous.Id} dropped from {previous.Priority} to {current.Priority}.",
                    RequiresApproval = !approved.Contains(previous.Id)
                });
            }

            if (previous.AcceptanceCriteriaIds.Count > 0 && current.AcceptanceCriteriaIds.Count == 0)
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.Unlinked,
                    Severity = FindingSeverity.Critical,
                    Subject = previous.Id,
                    Detail = $"Requirement {previous.Id} lost every acceptance criterion link.",
                    RequiresApproval = !approved.Contains(previous.Id)
                });
            }
            else if (current.AcceptanceCriteriaIds.Count < previous.AcceptanceCriteriaIds.Count)
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.Weakened,
                    Severity = FindingSeverity.Major,
                    Subject = previous.Id,
                    Detail =
                        $"Requirement {previous.Id} lost acceptance criteria: "
                        + string.Join(", ", previous.AcceptanceCriteriaIds.Except(current.AcceptanceCriteriaIds, StringComparer.Ordinal)),
                    RequiresApproval = !approved.Contains(previous.Id)
                });
            }
        }

        var baselineNonGoals = input.Baseline.NonGoals;
        foreach (var added in candidate.Nodes.Where(node => baseline.Find(node.Id) is null))
        {
            var violated = baselineNonGoals.FirstOrDefault(nonGoal =>
                nonGoal.Length >= 4
                && (added.Title.Contains(nonGoal, StringComparison.OrdinalIgnoreCase)
                    || added.Statement.Contains(nonGoal, StringComparison.OrdinalIgnoreCase)));
            if (violated is null && approved.Contains(added.Id))
            {
                continue;
            }

            findings.Add(new RegressionFinding
            {
                Type = RegressionType.ScopeLeak,
                Severity = FindingSeverity.Major,
                Subject = added.Id,
                Detail = violated is null
                    ? $"Requirement {added.Id} was added without an approved scope change."
                    : $"Requirement {added.Id} implements the baseline non-goal '{violated}'.",
                RequiresApproval = true
            });
        }

        foreach (var locked in input.Baseline.LockedDecisions.Where(locked =>
                     !input.Candidate.LockedDecisions.Contains(locked, StringComparer.Ordinal)))
        {
            findings.Add(new RegressionFinding
            {
                Type = RegressionType.Contradicted,
                Severity = FindingSeverity.Critical,
                Subject = "locked-decision",
                Detail = $"Locked decision '{locked}' is no longer present in the candidate version.",
                RequiresApproval = true
            });
        }

        CompareStates(input, findings);

        if (input.BaselineScore is { } baselineScore
            && input.CandidateScore is { } candidateScore
            && baselineScore - candidateScore >= MaximumScoreDrop)
        {
            findings.Add(new RegressionFinding
            {
                Type = RegressionType.QualityDrop,
                Severity = FindingSeverity.Major,
                Subject = "score",
                Detail = $"The total score dropped from {ScoreAggregator.Format(baselineScore)} to {ScoreAggregator.Format(candidateScore)}.",
                RequiresApproval = true
            });
        }

        foreach (var artifact in input.Artifacts)
        {
            if (!string.Equals(artifact.SpecVersion, input.CandidateSpecVersion, StringComparison.Ordinal))
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.StaleArtifact,
                    Severity = FindingSeverity.Critical,
                    Subject = artifact.Path,
                    Detail = $"Artifact '{artifact.Path}' still references spec version '{artifact.SpecVersion}'.",
                    RequiresApproval = false
                });
            }

            foreach (var targetId in artifact.TargetIds)
            {
                if (TargetCatalog.TryGet(targetId, out var profile)
                    && string.Equals(profile.Path, artifact.Path, StringComparison.Ordinal))
                {
                    continue;
                }

                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.FormatRegression,
                    Severity = FindingSeverity.Critical,
                    Subject = artifact.Path,
                    Detail = $"Target '{targetId}' no longer uses its native path.",
                    RequiresApproval = false
                });
            }
        }

        return
        [
            .. findings
                .OrderByDescending(static finding => finding.Severity)
                .ThenBy(static finding => finding.Type)
                .ThenBy(static finding => finding.Subject, StringComparer.Ordinal)
        ];
    }

    private static void CompareStates(RegressionInput input, List<RegressionFinding> findings)
    {
        foreach (var previous in input.Baseline.StateMatrix)
        {
            var current = input.Candidate.StateMatrix.FirstOrDefault(entry =>
                string.Equals(entry.Screen, previous.Screen, StringComparison.Ordinal));
            if (current is null)
            {
                findings.Add(new RegressionFinding
                {
                    Type = RegressionType.StateLoss,
                    Severity = FindingSeverity.Major,
                    Subject = previous.Screen,
                    Detail = $"Screen '{previous.Screen}' lost its state definition.",
                    RequiresApproval = true
                });
                continue;
            }

            var lost = new List<string>();
            AddIfLost(previous.Loading, current.Loading, "loading", lost);
            AddIfLost(previous.Empty, current.Empty, "empty", lost);
            AddIfLost(previous.Failure, current.Failure, "error", lost);
            AddIfLost(previous.Permission, current.Permission, "permission", lost);
            if (lost.Count == 0)
            {
                continue;
            }

            findings.Add(new RegressionFinding
            {
                Type = RegressionType.StateLoss,
                Severity = FindingSeverity.Major,
                Subject = previous.Screen,
                Detail = $"Screen '{previous.Screen}' lost the {string.Join(", ", lost)} state.",
                RequiresApproval = true
            });
        }
    }

    private static void AddIfLost(string previous, string current, string name, List<string> lost)
    {
        if (!string.IsNullOrWhiteSpace(previous) && string.IsNullOrWhiteSpace(current))
        {
            lost.Add(name);
        }
    }
}
