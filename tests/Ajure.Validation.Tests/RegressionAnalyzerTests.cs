using Ajure.Specification;

namespace Ajure.Validation.Tests;

public class RegressionAnalyzerTests
{
    private static RegressionInput Input(ProjectSpec candidate, params string[] approved) => new()
    {
        Baseline = SampleSpec.Create(),
        Candidate = candidate,
        CandidateSpecVersion = "v2",
        ApprovedChangeIds = approved
    };

    [Fact]
    public void AnUnchangedSpecificationHasNoRegression()
    {
        Assert.Empty(RegressionAnalyzer.Compare(Input(SampleSpec.Create())));
    }

    [Fact]
    public void RemovingAMustRequirementIsCriticalAndNeedsApproval()
    {
        var candidate = SampleSpec.Create();
        candidate = candidate with { Requirements = [.. candidate.Requirements.Where(static requirement => requirement.Id != "FR-002")] };

        var findings = RegressionAnalyzer.Compare(Input(candidate));

        var removal = Assert.Single(findings, static finding => finding.Type == RegressionType.Removed);
        Assert.Equal(FindingSeverity.Critical, removal.Severity);
        Assert.Equal("FR-002", removal.Subject);
        Assert.True(removal.RequiresApproval);
    }

    [Fact]
    public void AnApprovedRemovalStillReportsButNoLongerRequiresApproval()
    {
        var candidate = SampleSpec.Create();
        candidate = candidate with { Requirements = [.. candidate.Requirements.Where(static requirement => requirement.Id != "FR-002")] };

        var removal = Assert.Single(
            RegressionAnalyzer.Compare(Input(candidate, "FR-002")),
            static finding => finding.Type == RegressionType.Removed);

        Assert.False(removal.RequiresApproval);
    }

    [Fact]
    public void DowngradingAMustRequirementIsWeakened()
    {
        var candidate = SampleSpec.Create();
        candidate = candidate with
        {
            Requirements =
            [
                candidate.Requirements[0] with { Priority = Priority.Could },
                .. candidate.Requirements.Skip(1)
            ]
        };

        var weakened = Assert.Single(RegressionAnalyzer.Compare(Input(candidate)), static finding => finding.Type == RegressionType.Weakened);

        Assert.Equal(FindingSeverity.Critical, weakened.Severity);
        Assert.Contains("Must to Could", weakened.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void LosingEveryAcceptanceLinkIsUnlinked()
    {
        var candidate = SampleSpec.Create();
        candidate = candidate with
        {
            Requirements =
            [
                candidate.Requirements[0] with { AcceptanceCriteriaIds = [] },
                .. candidate.Requirements.Skip(1)
            ]
        };

        var unlinked = Assert.Single(RegressionAnalyzer.Compare(Input(candidate)), static finding => finding.Type == RegressionType.Unlinked);

        Assert.Equal(FindingSeverity.Critical, unlinked.Severity);
        Assert.Equal("FR-001", unlinked.Subject);
    }

    [Fact]
    public void ReintroducingANonGoalIsAScopeLeak()
    {
        var baseline = SampleSpec.Create();
        var candidate = baseline with
        {
            Requirements =
            [
                .. baseline.Requirements,
                new Requirement
                {
                    Id = "FR-005",
                    Title = "Handle payments",
                    Statement = $"The planner must {baseline.NonGoals[0]}",
                    Priority = Priority.Should,
                    AcceptanceCriteriaIds = ["AC-001"],
                    TechnicalDecisionIds = ["TD-001"]
                }
            ]
        };

        var leak = Assert.Single(RegressionAnalyzer.Compare(Input(candidate)), static finding => finding.Type == RegressionType.ScopeLeak);

        Assert.Equal("FR-005", leak.Subject);
        Assert.Contains(baseline.NonGoals[0], leak.Detail, StringComparison.Ordinal);
        Assert.True(leak.RequiresApproval);
    }

    [Fact]
    public void AnApprovedNewRequirementIsNotAScopeLeak()
    {
        var baseline = SampleSpec.Create();
        var candidate = baseline with
        {
            Requirements =
            [
                .. baseline.Requirements,
                new Requirement
                {
                    Id = "FR-005",
                    Title = "Duplicate a trip",
                    Statement = "The organiser should be able to duplicate an existing trip.",
                    Priority = Priority.Should,
                    AcceptanceCriteriaIds = ["AC-001"],
                    TechnicalDecisionIds = ["TD-001"]
                }
            ]
        };

        Assert.DoesNotContain(
            RegressionAnalyzer.Compare(Input(candidate, "FR-005")),
            static finding => finding.Type == RegressionType.ScopeLeak);
    }

    [Fact]
    public void DroppingALockedDecisionIsACriticalContradiction()
    {
        var baseline = SampleSpec.Create();
        var candidate = baseline with { LockedDecisions = [baseline.LockedDecisions[0]] };

        var contradiction = Assert.Single(
            RegressionAnalyzer.Compare(Input(candidate)),
            static finding => finding.Type == RegressionType.Contradicted);

        Assert.Equal(FindingSeverity.Critical, contradiction.Severity);
    }

    [Fact]
    public void LosingAScreenStateIsReported()
    {
        var baseline = SampleSpec.Create();
        var candidate = baseline with
        {
            StateMatrix = [baseline.StateMatrix[0] with { Failure = string.Empty }, .. baseline.StateMatrix.Skip(1)]
        };

        var loss = Assert.Single(RegressionAnalyzer.Compare(Input(candidate)), static finding => finding.Type == RegressionType.StateLoss);

        Assert.Contains("error", loss.Detail, StringComparison.Ordinal);
        Assert.Equal(baseline.StateMatrix[0].Screen, loss.Subject);
    }

    [Fact]
    public void AScoreDropOfFivePointsOrMoreIsAQualityDrop()
    {
        var drop = RegressionAnalyzer.Compare(Input(SampleSpec.Create()) with { BaselineScore = 92m, CandidateScore = 87m });
        var small = RegressionAnalyzer.Compare(Input(SampleSpec.Create()) with { BaselineScore = 92m, CandidateScore = 88m });

        Assert.Single(drop, static finding => finding.Type == RegressionType.QualityDrop);
        Assert.Empty(small);
    }

    [Fact]
    public void ArtifactsOnAnOldVersionOrTheWrongPathAreReported()
    {
        var input = Input(SampleSpec.Create()) with
        {
            Artifacts =
            [
                new ArtifactStamp { Path = "CLAUDE.md", SpecVersion = "v1", TargetIds = [TargetCatalog.ClaudeCode] },
                new ArtifactStamp { Path = "docs/rules.md", SpecVersion = "v2", TargetIds = [TargetCatalog.Cursor] }
            ]
        };

        var findings = RegressionAnalyzer.Compare(input);

        var stale = Assert.Single(findings, static finding => finding.Type == RegressionType.StaleArtifact);
        Assert.Equal("CLAUDE.md", stale.Subject);
        var format = Assert.Single(findings, static finding => finding.Type == RegressionType.FormatRegression);
        Assert.Equal("docs/rules.md", format.Subject);
        Assert.False(format.RequiresApproval);
    }

    [Fact]
    public void FindingsAreOrderedBySeverityThenTypeThenSubject()
    {
        var baseline = SampleSpec.Create();
        var candidate = baseline with
        {
            Requirements = [.. baseline.Requirements.Where(static requirement => requirement.Id != "FR-002")],
            LockedDecisions = [baseline.LockedDecisions[0]],
            StateMatrix = [baseline.StateMatrix[0] with { Empty = string.Empty }, .. baseline.StateMatrix.Skip(1)]
        };

        var findings = RegressionAnalyzer.Compare(Input(candidate));

        Assert.Equal(
            findings.OrderByDescending(static finding => finding.Severity).ThenBy(static finding => finding.Type).Select(static finding => finding.Subject),
            findings.Select(static finding => finding.Subject));
        Assert.Equal(FindingSeverity.Critical, findings[0].Severity);
    }
}
