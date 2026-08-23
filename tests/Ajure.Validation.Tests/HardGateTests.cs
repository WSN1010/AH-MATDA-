using Ajure.Specification;

namespace Ajure.Validation.Tests;

public class DeterministicValidatorTests
{
    [Fact]
    public void CleanSpecificationProducesNoCriticalFinding()
    {
        var result = DeterministicValidator.Validate(ValidationFixture.Input());

        Assert.DoesNotContain(result.Findings, static finding => finding.Severity == FindingSeverity.Critical);
        Assert.True(result.Passed);
        Assert.True(result.AcceptanceCoverageComplete);
        Assert.True(result.AcceptanceCriteriaVerifiable);
        Assert.True(result.SecurityDecisionsPresent);
        Assert.True(result.TargetFilesValid);
        Assert.True(result.ArtifactVersionsConsistent);
        Assert.False(result.SecretsOrCodeDetected);
        Assert.False(result.HasUnresolvedCriticalDecisions);
        Assert.Empty(result.MustRequirementsMissingFromTargets);
    }

    [Fact]
    public void MissingAcceptanceCriterionIsCritical()
    {
        var spec = SampleSpec.Create();
        var broken = spec with
        {
            Requirements = [spec.Requirements[0] with { AcceptanceCriteriaIds = [] }, .. spec.Requirements.Skip(1)]
        };

        var result = DeterministicValidator.Validate(ValidationFixture.Input(broken));

        Assert.False(result.AcceptanceCoverageComplete);
        Assert.Contains(result.Findings, static finding =>
            finding.RuleKey == RuleKeys.MissingAcceptanceCriterion
            && finding.Severity == FindingSeverity.Critical
            && finding.AffectedIds.Contains("FR-001"));
    }

    [Fact]
    public void DanglingIdentifierLinksAreReported()
    {
        var spec = SampleSpec.Create();
        var broken = spec with
        {
            Requirements = [spec.Requirements[0] with { AcceptanceCriteriaIds = ["AC-404"] }, .. spec.Requirements.Skip(1)]
        };

        var result = DeterministicValidator.Validate(ValidationFixture.Input(broken));

        Assert.Contains(result.Findings, static finding => finding.Statement.Contains("AC-404", StringComparison.Ordinal));
    }

    [Fact]
    public void TargetFileWrittenToTheWrongPathIsCritical()
    {
        var input = ValidationFixture.Input();
        var moved = input with
        {
            TargetFiles =
            [
                .. input.TargetFiles.Select(static file => file.Path == "CLAUDE.md" ? file with { Path = "docs/CLAUDE.md" } : file)
            ]
        };

        var result = DeterministicValidator.Validate(moved);

        Assert.False(result.TargetFilesValid);
        Assert.Contains(result.Findings, static finding =>
            finding.RuleKey == RuleKeys.TargetFileMismatch && finding.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public void MustRequirementMissingFromATargetFileIsReported()
    {
        var input = ValidationFixture.Input();
        var stripped = input with
        {
            TargetFiles =
            [
                .. input.TargetFiles.Select(static file => file with
                {
                    Content = file.Content.Replace("FR-003", "the sharing requirement", StringComparison.Ordinal)
                })
            ]
        };

        var result = DeterministicValidator.Validate(stripped);

        Assert.Equal(["FR-003"], result.MustRequirementsMissingFromTargets);
        Assert.False(result.TargetFilesValid);
    }

    [Fact]
    public void SecretsInAnArtifactAreDetected()
    {
        var input = ValidationFixture.Input();
        var leaking = input with
        {
            Documents =
            [
                .. input.Documents.Select(static document => document with
                {
                    Content = document.Content + "\nAWS_SECRET_ACCESS_KEY=AKIAIOSFODNN7EXAMPLEKEY1234567890abcd\n"
                })
            ]
        };

        var result = DeterministicValidator.Validate(leaking);

        Assert.True(result.SecretsOrCodeDetected);
    }

    [Fact]
    public void ArtifactsFromAnotherSpecVersionAreInconsistent()
    {
        var input = ValidationFixture.Input();
        var stale = input with { Context = input.Context with { SpecVersion = "v2" } };

        var result = DeterministicValidator.Validate(stale);

        Assert.False(result.ArtifactVersionsConsistent);
    }

    [Fact]
    public void ValidationIsDeterministic()
    {
        var first = DeterministicValidator.Validate(ValidationFixture.Input());
        var second = DeterministicValidator.Validate(ValidationFixture.Input());

        Assert.Equal(
            first.Findings.Select(static finding => finding.Id),
            second.Findings.Select(static finding => finding.Id));
    }
}

public class HardGateTests
{
    private static FindingCluster Cluster(
        string ruleKey,
        FindingSeverity severity,
        ClusterConsensus consensus,
        params string[] affectedIds) => new()
        {
            Fingerprint = FindingFingerprint.Compute(ruleKey, affectedIds),
            RuleKey = ruleKey,
            Severity = severity,
            Statement = $"Cluster for {ruleKey}.",
            AffectedIds = affectedIds,
            ModelIds = ["model-a", "model-b"],
            Evidence = ["evidence"],
            Consensus = consensus
        };

    private static HardGateResult Gate(IReadOnlyList<HardGateResult> gates, HardGate gate) =>
        gates.Single(result => result.Gate == gate);

    [Fact]
    public void GateCodesRunFromHg01ToHg14()
    {
        var gates = HardGateEvaluator.Evaluate(ValidationFixture.Context());

        Assert.Equal(14, gates.Count);
        Assert.Equal(
            Enumerable.Range(1, 14).Select(static number => $"HG-{number:D2}"),
            gates.Select(static gate => gate.Code));
    }

    [Fact]
    public void CleanRunPassesEveryGate()
    {
        var gates = HardGateEvaluator.Evaluate(ValidationFixture.Context());

        Assert.All(gates, static gate => Assert.True(gate.Passed, gate.Code + ": " + gate.Reason));
    }

    [Fact]
    public void Hg02FailsWhenARequirementHasNoAcceptanceCriterion()
    {
        var deterministic = DeterministicValidator.Validate(ValidationFixture.Input()) with { AcceptanceCoverageComplete = false };

        var gates = HardGateEvaluator.Evaluate(ValidationFixture.Context(deterministic));

        Assert.False(Gate(gates, HardGate.RequirementWithoutAcceptance).Passed);
    }

    [Fact]
    public void Hg03FailsOnlyForAConfirmedCriticalContradiction()
    {
        var confirmed = ValidationFixture.Context(
            clusters: [Cluster(RuleKeys.ContradictionPrdTrd, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-001")]);
        var disputed = ValidationFixture.Context(
            clusters: [Cluster(RuleKeys.ContradictionPrdTrd, FindingSeverity.Critical, ClusterConsensus.Disputed, "FR-001")]);

        Assert.False(Gate(HardGateEvaluator.Evaluate(confirmed), HardGate.CriticalContradiction).Passed);
        Assert.True(Gate(HardGateEvaluator.Evaluate(disputed), HardGate.CriticalContradiction).Passed);
    }

    [Fact]
    public void Hg04FailsWhenAMustRequirementIsMissingFromTheTargetFiles()
    {
        var deterministic = DeterministicValidator.Validate(ValidationFixture.Input()) with
        {
            MustRequirementsMissingFromTargets = ["FR-003"]
        };

        var gate = Gate(HardGateEvaluator.Evaluate(ValidationFixture.Context(deterministic)), HardGate.MustRequirementMissingInTargets);

        Assert.False(gate.Passed);
        Assert.Contains("FR-003", gate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Hg05FailsForAStaleArtifact()
    {
        var regressions = new[]
        {
            new RegressionFinding
            {
                Type = RegressionType.StaleArtifact,
                Severity = FindingSeverity.Critical,
                Subject = "CLAUDE.md",
                Detail = "Stale version.",
                RequiresApproval = false
            }
        };

        var gates = HardGateEvaluator.Evaluate(ValidationFixture.Context(regressions: regressions));

        Assert.False(Gate(gates, HardGate.VersionOrHashMismatch).Passed);
    }

    [Fact]
    public void Hg06FailsWhenAMustRequirementWasRemovedWithoutApproval()
    {
        var removal = new RegressionFinding
        {
            Type = RegressionType.Removed,
            Severity = FindingSeverity.Critical,
            Subject = "FR-001",
            Detail = "Removed.",
            RequiresApproval = true
        };

        Assert.False(Gate(
            HardGateEvaluator.Evaluate(ValidationFixture.Context(regressions: [removal])),
            HardGate.UnapprovedMustRemoval).Passed);
        Assert.True(Gate(
            HardGateEvaluator.Evaluate(ValidationFixture.Context(regressions: [removal with { RequiresApproval = false }])),
            HardGate.UnapprovedMustRemoval).Passed);
    }

    [Fact]
    public void Hg10FailsWhenASecretOrProductCodeIsPresent()
    {
        var deterministic = DeterministicValidator.Validate(ValidationFixture.Input()) with { SecretsOrCodeDetected = true };

        Assert.False(Gate(
            HardGateEvaluator.Evaluate(ValidationFixture.Context(deterministic)),
            HardGate.SecretOrProductCode).Passed);
    }

    [Fact]
    public void Hg11FailsWhenACriticalFindingSurvivesThreeRepairs()
    {
        var repeated = Cluster(RuleKeys.SecurityGap, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-003");
        var other = Cluster(RuleKeys.MissingState, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-002");

        var fingerprints = CriticalRepeatDetector.Detect([[repeated, other], [repeated], [repeated]]);

        Assert.Equal([repeated.Fingerprint], fingerprints);
        Assert.False(Gate(
            HardGateEvaluator.Evaluate(ValidationFixture.Context(repeatedCritical: fingerprints)),
            HardGate.RepeatedCriticalAfterRepair).Passed);
    }

    [Fact]
    public void CriticalRepeatDetectorNeedsThreeConsecutiveIterations()
    {
        var repeated = Cluster(RuleKeys.SecurityGap, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-003");
        var disputed = repeated with { Consensus = ClusterConsensus.Disputed };

        Assert.Empty(CriticalRepeatDetector.Detect([[repeated], [repeated]]));
        Assert.Empty(CriticalRepeatDetector.Detect([[repeated], [], [repeated]]));
        Assert.Empty(CriticalRepeatDetector.Detect([[repeated], [disputed], [repeated]]));
    }

    [Fact]
    public void Hg12FailsWhenTheTieBreakDidNotResolveTheConflict()
    {
        var context = ValidationFixture.Context() with { TieBreakUsed = true, TieBreakResolved = false };

        Assert.False(Gate(HardGateEvaluator.Evaluate(context), HardGate.UnresolvedEvaluatorConflict).Passed);
    }

    [Fact]
    public void Hg13FailsWhenAProviderStageDidNotComplete()
    {
        var context = ValidationFixture.Context() with { ProviderStagesCompleted = false };

        Assert.False(Gate(HardGateEvaluator.Evaluate(context), HardGate.ProviderStageIncomplete).Passed);
    }

    [Fact]
    public void Hg14FailsWithoutTwoDistinctModels()
    {
        var single = ValidationFixture.Context(models: ["openai/gpt-5"]);
        var duplicate = ValidationFixture.Context(models: ["openai/gpt-5", "openai/gpt-5"]);

        Assert.False(Gate(HardGateEvaluator.Evaluate(single), HardGate.InsufficientModelDiversity).Passed);
        Assert.Contains("1 distinct model", Gate(HardGateEvaluator.Evaluate(single), HardGate.InsufficientModelDiversity).Reason, StringComparison.Ordinal);
        Assert.False(Gate(HardGateEvaluator.Evaluate(duplicate), HardGate.InsufficientModelDiversity).Passed);
    }

    [Fact]
    public void Hg14FailsWhenAnEvaluationEnvelopeWasInvalid()
    {
        var context = ValidationFixture.Context(invalidEnvelopes: [ReviewEnvelopeParser.ErrorEnvelopeMissing]);

        var gate = Gate(HardGateEvaluator.Evaluate(context), HardGate.InsufficientModelDiversity);

        Assert.False(gate.Passed);
        Assert.Contains(ReviewEnvelopeParser.ErrorEnvelopeMissing, gate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadyNeedsNinetyPointsAndEveryGate()
    {
        var passing = ValidationFixture.Scores(intent: 24m, traceability: 19m, testability: 19m, executability: 14m, fitness: 9m, ux: 9m);

        var decision = ReadyEvaluator.Evaluate(passing, ValidationFixture.Context(), []);

        Assert.Equal(94m, decision.Score);
        Assert.True(decision.IsReady);
        Assert.Empty(decision.Reasons);
    }

    [Fact]
    public void ScoreBelowNinetyFailsEvenWhenEveryGatePasses()
    {
        var decision = ReadyEvaluator.Evaluate(ValidationFixture.Scores(intent: 15m), ValidationFixture.Context(), []);

        Assert.Equal(ReadyStatus.Failed, decision.Status);
        Assert.Contains(decision.Reasons, static reason => reason.Contains("below 90.0", StringComparison.Ordinal));
    }

    [Fact]
    public void AConfirmedCriticalFindingBlocksReadyButASingleMajorDoesNot()
    {
        var passing = ValidationFixture.Scores(intent: 24m);
        var critical = ValidationFixture.Context(
            clusters: [Cluster(RuleKeys.MissingFailureHandling, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-002")]);
        var major = ValidationFixture.Context(
            clusters: [Cluster(RuleKeys.AmbiguousMetric, FindingSeverity.Major, ClusterConsensus.Unconfirmed, "NFR-001")]);

        Assert.Equal(ReadyStatus.Failed, ReadyEvaluator.Evaluate(passing, critical, []).Status);
        Assert.True(ReadyEvaluator.Evaluate(passing, major, []).IsReady);
    }

    [Fact]
    public void AFindingThatNeedsAUserDecisionProducesNeedsDecision()
    {
        var cluster = Cluster(RuleKeys.NonGoalViolation, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-004") with
        {
            RequiresUserDecision = true
        };
        var context = ValidationFixture.Context(clusters: [cluster]);

        var decision = ReadyEvaluator.Evaluate(ValidationFixture.Scores(intent: 24m), context, []);

        Assert.Equal(ReadyStatus.NeedsDecision, decision.Status);
    }

    [Theory]
    [InlineData(HardGate.RepeatedCriticalAfterRepair)]
    [InlineData(HardGate.UnresolvedEvaluatorConflict)]
    public void UnresolvedRepairOrEvaluatorConflictProducesNeedsDecision(HardGate failedGate)
    {
        var context = failedGate switch
        {
            HardGate.RepeatedCriticalAfterRepair =>
                ValidationFixture.Context(repeatedCritical: ["critical-fingerprint"]),
            HardGate.UnresolvedEvaluatorConflict =>
                ValidationFixture.Context() with { TieBreakUsed = true, TieBreakResolved = false },
            _ => throw new ArgumentOutOfRangeException(nameof(failedGate))
        };

        var decision = ReadyEvaluator.Evaluate(ValidationFixture.Scores(intent: 24m), context, []);

        Assert.Equal(ReadyStatus.NeedsDecision, decision.Status);
    }

    [Fact]
    public void RepairInputTakesConfirmedEvidenceBackedClustersInAStableOrder()
    {
        var critical = Cluster(RuleKeys.SecurityGap, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-003");
        var major = Cluster(RuleKeys.TraceabilityBreak, FindingSeverity.Major, ClusterConsensus.Confirmed, "FR-001");
        var majorLater = Cluster(RuleKeys.AmbiguousMetric, FindingSeverity.Major, ClusterConsensus.Confirmed, "NFR-001");
        var unconfirmed = Cluster(RuleKeys.MissingState, FindingSeverity.Major, ClusterConsensus.Unconfirmed, "FR-002");
        var needsDecision = Cluster(RuleKeys.NonGoalViolation, FindingSeverity.Critical, ClusterConsensus.Confirmed, "FR-004") with
        {
            RequiresUserDecision = true
        };
        var withoutEvidence = Cluster(RuleKeys.UndefinedTerm, FindingSeverity.Major, ClusterConsensus.Confirmed, "FR-002") with
        {
            Evidence = []
        };

        var repair = RepairInputSelector.Select([majorLater, unconfirmed, needsDecision, withoutEvidence, major, critical]);

        Assert.Equal([critical, major, majorLater], repair.Clusters);
        Assert.Equal(["FR-001", "FR-003", "NFR-001"], repair.AllowedIds);
    }
}
