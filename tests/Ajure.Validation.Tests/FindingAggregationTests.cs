namespace Ajure.Validation.Tests;

public class FindingNormalizerTests
{
    [Fact]
    public void UnknownRuleKeysCollapseToOther()
    {
        var review = ValidationFixture.Review(
            "model-a",
            ValidationFixture.Finding("f1", "hallucinated_rule", FindingSeverity.Major, "FR-001"));

        Assert.Equal(RuleKeys.Other, review.Findings[0].RuleKey);
        Assert.True(review.Findings[0].IsAggregatable);
    }

    [Fact]
    public void AffectedIdsAreFilteredSortedAndDeduplicated()
    {
        var review = ValidationFixture.Review(
            "model-a",
            ValidationFixture.Finding("f1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-002", "FR-001", "FR-001", "FR-999"));

        var finding = review.Findings[0];
        Assert.Equal(["FR-001", "FR-002"], finding.AffectedIds);
        Assert.Equal("model-a", finding.ModelId);
    }

    [Fact]
    public void UnknownIdsArePreservedAsASeparateMinorFinding()
    {
        var review = ValidationFixture.Review(
            "model-a",
            ValidationFixture.Finding("f1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-001", "FR-999"));

        Assert.Equal(2, review.Findings.Count);
        var extra = review.Findings[1];
        Assert.Equal("f1-unknown-ids", extra.Id);
        Assert.Equal(FindingSeverity.Minor, extra.Severity);
        Assert.Equal(RuleKeys.Other, extra.RuleKey);
        Assert.Equal(["FR-999"], extra.Evidence);
        Assert.False(extra.IsAggregatable);
    }

    [Fact]
    public void OtherWithoutAffectedIdsIsNotAggregatable()
    {
        var review = ValidationFixture.Review("model-a", ValidationFixture.Finding("f1", RuleKeys.Other, FindingSeverity.Minor));

        Assert.False(review.Findings[0].IsAggregatable);
    }

    [Fact]
    public void FingerprintIsSha256OfRuleKeyAndSortedIds()
    {
        var first = FindingFingerprint.Compute(RuleKeys.MissingAcceptanceCriterion, ["FR-002", "FR-001"]);
        var second = FindingFingerprint.Compute(RuleKeys.MissingAcceptanceCriterion, ["FR-001", "FR-002", "FR-001"]);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, first.ToLowerInvariant());
        Assert.NotEqual(first, FindingFingerprint.Compute(RuleKeys.MissingAcceptanceCriterion, ["FR-001"]));
        Assert.NotEqual(first, FindingFingerprint.Compute(RuleKeys.TraceabilityBreak, ["FR-001", "FR-002"]));
    }

    [Fact]
    public void UnknownRuleKeyProducesTheSameFingerprintAsOther()
    {
        Assert.Equal(
            FindingFingerprint.Compute(RuleKeys.Other, ["FR-001"]),
            FindingFingerprint.Compute("not_a_rule", ["FR-001"]));
    }
}

public class FindingAggregatorTests
{
    private static NormalizedReview Review(string modelId, params Finding[] findings) =>
        ValidationFixture.Review(modelId, findings);

    [Fact]
    public void IdenticalFingerprintsMergeAndCountDistinctModels()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.MissingAcceptanceCriterion, FindingSeverity.Major, "FR-001")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.MissingAcceptanceCriterion, FindingSeverity.Critical, "FR-001"))
        ]);

        var cluster = Assert.Single(clusters);
        Assert.Equal(2, cluster.Support);
        Assert.Equal(["model-a", "model-b"], cluster.ModelIds);
        Assert.Equal(FindingSeverity.Critical, cluster.Severity);
        Assert.Equal(["FR-001"], cluster.AffectedIds);
        Assert.Equal(2, cluster.Members.Count);
    }

    [Fact]
    public void SameModelReportingTwiceIsStillSupportOfOne()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review(
                "model-a",
                ValidationFixture.Finding("a1", RuleKeys.MissingAcceptanceCriterion, FindingSeverity.Critical, "FR-001"),
                ValidationFixture.Finding("a2", RuleKeys.MissingAcceptanceCriterion, FindingSeverity.Critical, "FR-001"))
        ]);

        Assert.Equal(1, Assert.Single(clusters).Support);
    }

    [Fact]
    public void SameRuleKeyMergesWhenSimilarityReachesTheThreshold()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-001", "FR-002")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-001", "FR-002", "FR-003"))
        ]);

        Assert.Equal(2d / 3d, FindingAggregator.Similarity(["FR-001", "FR-002"], ["FR-001", "FR-002", "FR-003"]), 6);
        var cluster = Assert.Single(clusters);
        Assert.Equal(["FR-001", "FR-002", "FR-003"], cluster.AffectedIds);
    }

    [Fact]
    public void SameRuleKeyStaysSeparateBelowTheThreshold()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-001")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.TraceabilityBreak, FindingSeverity.Major, "FR-002", "FR-003", "NFR-001"))
        ]);

        Assert.Equal(0d, FindingAggregator.Similarity(["FR-001"], ["FR-002", "FR-003", "NFR-001"]));
        Assert.Equal(2, clusters.Count);
        Assert.All(clusters, static cluster => Assert.Equal(1, cluster.Support));
    }

    [Fact]
    public void DifferentRuleKeysNeverMerge()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.MissingAcceptanceCriterion, FindingSeverity.Major, "FR-001")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.MissingState, FindingSeverity.Major, "FR-001"))
        ]);

        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void NonAggregatableFindingsAreExcluded()
    {
        var clusters = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.Other, FindingSeverity.Minor)),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.Other, FindingSeverity.Minor))
        ]);

        Assert.Empty(clusters);
    }

    [Fact]
    public void CriticalNeedsTwoModelsAndAStrictMajority()
    {
        var twoOfThree = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003")),
            Review("model-c", ValidationFixture.Finding("c1", RuleKeys.MissingState, FindingSeverity.Minor, "FR-003"))
        ]);

        Assert.Equal(
            ClusterConsensus.Confirmed,
            twoOfThree.Single(static cluster => cluster.Severity == FindingSeverity.Critical).Consensus);

        var oneOfThree = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.MissingState, FindingSeverity.Minor, "FR-003")),
            Review("model-c", ValidationFixture.Finding("c1", RuleKeys.MissingState, FindingSeverity.Minor, "FR-002"))
        ]);

        Assert.Equal(
            ClusterConsensus.Disputed,
            oneOfThree.Single(static cluster => cluster.Severity == FindingSeverity.Critical).Consensus);
    }

    [Fact]
    public void CriticalSupportedByHalfOfTheModelsIsDisputed()
    {
        var clusters = FindingAggregator.Cluster(
            [
                Review("model-a", ValidationFixture.Finding("a1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003")),
                Review("model-b", ValidationFixture.Finding("b1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003"))
            ],
            successfulModelCount: 4);

        Assert.Equal(ClusterConsensus.Disputed, Assert.Single(clusters).Consensus);
    }

    [Fact]
    public void NonCriticalIsConfirmedByTwoModelsAndUnconfirmedByOne()
    {
        var confirmed = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.AmbiguousMetric, FindingSeverity.Major, "NFR-001")),
            Review("model-b", ValidationFixture.Finding("b1", RuleKeys.AmbiguousMetric, FindingSeverity.Major, "NFR-001"))
        ]);
        var single = FindingAggregator.Cluster(
        [
            Review("model-a", ValidationFixture.Finding("a1", RuleKeys.AmbiguousMetric, FindingSeverity.Major, "NFR-001"))
        ]);

        Assert.Equal(ClusterConsensus.Confirmed, Assert.Single(confirmed).Consensus);
        Assert.Equal(ClusterConsensus.Unconfirmed, Assert.Single(single).Consensus);
    }

    [Fact]
    public void ClusteringIsDeterministicRegardlessOfReviewOrder()
    {
        var a = Review("model-a", ValidationFixture.Finding("a1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003"));
        var b = Review("model-b", ValidationFixture.Finding("b1", RuleKeys.AmbiguousMetric, FindingSeverity.Major, "NFR-001"));
        var c = Review("model-c", ValidationFixture.Finding("c1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003"));

        var forward = FindingAggregator.Cluster([a, b, c]).Select(static cluster => cluster.Fingerprint);
        var backward = FindingAggregator.Cluster([c, b, a]).Select(static cluster => cluster.Fingerprint);

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void TieBreakIsRequiredForDisputedCriticalOrWideScoreSpread()
    {
        var disputed = FindingAggregator.Cluster(
            [Review("model-a", ValidationFixture.Finding("a1", RuleKeys.SecurityGap, FindingSeverity.Critical, "FR-003"))],
            successfulModelCount: 2);

        Assert.True(FindingAggregator.RequiresTieBreak([ValidationFixture.Scores()], disputed));
        Assert.False(FindingAggregator.RequiresTieBreak([ValidationFixture.Scores(), ValidationFixture.Scores()], []));
        Assert.True(FindingAggregator.RequiresTieBreak(
            [ValidationFixture.Scores(intent: 25m), ValidationFixture.Scores(intent: 22m)],
            []));
        Assert.False(FindingAggregator.RequiresTieBreak(
            [ValidationFixture.Scores(intent: 25m), ValidationFixture.Scores(intent: 23m)],
            []));
    }
}

public class ScoreAggregatorTests
{
    [Fact]
    public void OddCountUsesTheMiddleValue()
    {
        Assert.Equal(18m, ScoreAggregator.Median([20m, 18m, 14m]));
    }

    [Fact]
    public void EvenCountAveragesTheTwoMiddleValues()
    {
        Assert.Equal(19m, ScoreAggregator.Median([20m, 18m]));
        Assert.Equal(18.5m, ScoreAggregator.Median([20m, 18m, 17m, 19m]));
    }

    [Fact]
    public void MedianIsRoundedToOneDecimalPlace()
    {
        Assert.Equal(18.3m, ScoreAggregator.Median([18.25m, 18.35m]));
        Assert.Equal(87.7m, ScoreAggregator.Round(87.65m));
        Assert.Equal("90.0", ScoreAggregator.Format(90m));
        Assert.Equal("89.9", ScoreAggregator.Format(89.94m));
    }

    [Fact]
    public void AggregateTakesTheMedianPerArea()
    {
        var scores = new[]
        {
            ValidationFixture.Scores(intent: 25m, traceability: 20m, testability: 16m),
            ValidationFixture.Scores(intent: 20m, traceability: 18m, testability: 20m),
            ValidationFixture.Scores(intent: 22m, traceability: 19m, testability: 18m)
        };

        var aggregated = ScoreAggregator.Aggregate(scores);

        Assert.Equal(22m, aggregated.IntentCoverage);
        Assert.Equal(19m, aggregated.Traceability);
        Assert.Equal(18m, aggregated.Testability);
        Assert.Equal(14m, aggregated.TechnicalExecutability);
        Assert.Equal(91m, aggregated.Total);
    }

    [Fact]
    public void ASingleEvaluationCannotProduceASemanticScore()
    {
        Assert.Throws<ArgumentException>(() => ScoreAggregator.Aggregate([ValidationFixture.Scores()]));
        Assert.Throws<ArgumentException>(static () => ScoreAggregator.Median([]));
    }
}
