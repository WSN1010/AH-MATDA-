using System.Globalization;
using Ajure.Specification;

namespace Ajure.Validation;

public sealed record NormalizedFinding
{
    public required string Id { get; init; }

    public required string Role { get; init; }

    public required string ModelId { get; init; }

    public required FindingSeverity Severity { get; init; }

    public required string RuleKey { get; init; }

    public required string Statement { get; init; }

    public IReadOnlyList<string> AffectedIds { get; init; } = [];

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public bool RequiresUserDecision { get; init; }

    public required string Fingerprint { get; init; }

    /// <summary>False for <c>other</c> findings without affected ids, which EVALUATION 6 excludes from aggregation.</summary>
    public required bool IsAggregatable { get; init; }
}

public sealed record NormalizedReview
{
    public required string Role { get; init; }

    public required string ModelId { get; init; }

    public required IReadOnlyList<NormalizedFinding> Findings { get; init; }
}

/// <summary>Finding normalization from EVALUATION 6.</summary>
public static class FindingNormalizer
{
    public static NormalizedReview Normalize(ReviewResult review, ProjectSpec spec)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(spec);
        return Normalize(review, spec.AllIds().ToHashSet(StringComparer.Ordinal));
    }

    public static NormalizedReview Normalize(ReviewResult review, IReadOnlySet<string> knownIds)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(knownIds);

        var normalized = new List<NormalizedFinding>();
        foreach (var finding in review.Envelope.Findings)
        {
            var ruleKey = RuleKeys.Normalize(finding.RuleKey);
            var affected = finding.AffectedIds
                .Where(knownIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();
            var unknown = finding.AffectedIds
                .Where(id => !knownIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();

            normalized.Add(new NormalizedFinding
            {
                Id = finding.Id,
                Role = review.Role,
                ModelId = review.ModelId,
                Severity = finding.Severity,
                RuleKey = ruleKey,
                Statement = finding.Statement,
                AffectedIds = affected,
                Evidence = [.. finding.Evidence],
                RequiresUserDecision = finding.RequiresUserDecision,
                Fingerprint = FindingFingerprint.Compute(ruleKey, affected),
                IsAggregatable = !(string.Equals(ruleKey, RuleKeys.Other, StringComparison.Ordinal) && affected.Length == 0)
            });

            if (unknown.Length == 0)
            {
                continue;
            }

            // Unknown ids are preserved as a separate Minor finding instead of being silently dropped.
            normalized.Add(new NormalizedFinding
            {
                Id = $"{finding.Id}-unknown-ids",
                Role = review.Role,
                ModelId = review.ModelId,
                Severity = FindingSeverity.Minor,
                RuleKey = RuleKeys.Other,
                Statement = $"Finding '{finding.Id}' referenced ids that do not exist in the ProjectSpec: {string.Join(", ", unknown)}.",
                AffectedIds = [],
                Evidence = unknown,
                RequiresUserDecision = false,
                Fingerprint = FindingFingerprint.Compute(RuleKeys.Other, []),
                IsAggregatable = false
            });
        }

        return new NormalizedReview
        {
            Role = review.Role,
            ModelId = review.ModelId,
            Findings = normalized
        };
    }
}

public enum ClusterConsensus
{
    /// <summary>Supported by a single model. Counts for the score, does not gate Ready on its own.</summary>
    Unconfirmed,
    Confirmed,
    Disputed
}

public sealed record FindingCluster
{
    public required string Fingerprint { get; init; }

    public required string RuleKey { get; init; }

    public required FindingSeverity Severity { get; init; }

    public required string Statement { get; init; }

    public IReadOnlyList<string> AffectedIds { get; init; } = [];

    public IReadOnlyList<string> ModelIds { get; init; } = [];

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public bool RequiresUserDecision { get; init; }

    public required ClusterConsensus Consensus { get; init; }

    public IReadOnlyList<NormalizedFinding> Members { get; init; } = [];

    /// <summary>Support is the number of distinct model ids, not the number of findings.</summary>
    public int Support => ModelIds.Count;
}

/// <summary>Clustering and consensus rules from EVALUATION 6.</summary>
public static class FindingAggregator
{
    public const double SimilarityThreshold = 0.5d;

    public static IReadOnlyList<FindingCluster> Cluster(IEnumerable<NormalizedReview> reviews)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        var materialized = reviews.ToArray();
        var successfulModels = materialized
            .Select(static review => review.ModelId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return Cluster(materialized, successfulModels);
    }

    public static IReadOnlyList<FindingCluster> Cluster(
        IEnumerable<NormalizedReview> reviews,
        int successfulModelCount)
    {
        ArgumentNullException.ThrowIfNull(reviews);

        var findings = reviews
            .SelectMany(static review => review.Findings)
            .Where(static finding => finding.IsAggregatable)
            .OrderBy(static finding => finding.Fingerprint, StringComparer.Ordinal)
            .ThenBy(static finding => finding.ModelId, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Id, StringComparer.Ordinal)
            .ToArray();

        var parents = Enumerable.Range(0, findings.Length).ToArray();
        for (var left = 0; left < findings.Length; left++)
        {
            for (var right = left + 1; right < findings.Length; right++)
            {
                if (ShouldMerge(findings[left], findings[right]))
                {
                    Union(parents, left, right);
                }
            }
        }

        return
        [
            .. findings
                .Select((finding, index) => (Root: Find(parents, index), Finding: finding))
                .GroupBy(static pair => pair.Root)
                .Select(group => BuildCluster([.. group.Select(static pair => pair.Finding)], successfulModelCount))
                .OrderByDescending(static cluster => cluster.Severity)
                .ThenBy(static cluster => cluster.Fingerprint, StringComparer.Ordinal)
        ];
    }

    /// <summary>Jaccard similarity of two affected id sets. Two empty sets are treated as identical.</summary>
    public static double Similarity(IEnumerable<string> left, IEnumerable<string> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        if (leftSet.Count == 0 && rightSet.Count == 0)
        {
            return 1d;
        }

        var intersection = leftSet.Intersect(rightSet, StringComparer.Ordinal).Count();
        var union = leftSet.Count + rightSet.Count - intersection;
        return union == 0 ? 0d : (double)intersection / union;
    }

    /// <summary>
    /// A single tie-break per validation run is allowed when the area scores disagree by three points or more,
    /// or when a Critical cluster is disputed (EVALUATION 6).
    /// </summary>
    public static bool RequiresTieBreak(
        IReadOnlyList<AreaScores> scores,
        IEnumerable<FindingCluster> clusters)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(clusters);

        if (clusters.Any(static cluster =>
                cluster.Severity == FindingSeverity.Critical && cluster.Consensus == ClusterConsensus.Disputed))
        {
            return true;
        }

        if (scores.Count < 2)
        {
            return false;
        }

        return AreaSelectors.Any(selector =>
        {
            var values = scores.Select(selector).ToArray();
            return values.Max() - values.Min() >= 3m;
        });
    }

    private static IReadOnlyList<Func<AreaScores, decimal>> AreaSelectors { get; } =
    [
        static score => score.IntentCoverage,
        static score => score.Traceability,
        static score => score.Testability,
        static score => score.TechnicalExecutability,
        static score => score.TargetAgentFitness,
        static score => score.UxOperationsCompleteness
    ];

    private static bool ShouldMerge(NormalizedFinding left, NormalizedFinding right)
    {
        if (string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(left.RuleKey, right.RuleKey, StringComparison.Ordinal)
            && Similarity(left.AffectedIds, right.AffectedIds) >= SimilarityThreshold;
    }

    private static FindingCluster BuildCluster(IReadOnlyList<NormalizedFinding> members, int successfulModelCount)
    {
        var representative = members
            .OrderByDescending(static member => member.Severity)
            .ThenBy(static member => member.Fingerprint, StringComparer.Ordinal)
            .ThenBy(static member => member.Id, StringComparer.Ordinal)
            .First();
        var severity = members.Max(static member => member.Severity);
        var modelIds = members
            .Select(static member => member.ModelId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        return new FindingCluster
        {
            Fingerprint = representative.Fingerprint,
            RuleKey = representative.RuleKey,
            Severity = severity,
            Statement = representative.Statement,
            AffectedIds =
            [
                .. members
                    .SelectMany(static member => member.AffectedIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static id => id, StringComparer.Ordinal)
            ],
            ModelIds = modelIds,
            Evidence =
            [
                .. members
                    .SelectMany(static member => member.Evidence)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static item => item, StringComparer.Ordinal)
            ],
            RequiresUserDecision = members.Any(static member => member.RequiresUserDecision),
            Consensus = Consensus(severity, modelIds.Length, successfulModelCount),
            Members = [.. members.OrderBy(static member => member.ModelId, StringComparer.Ordinal).ThenBy(static member => member.Id, StringComparer.Ordinal)]
        };
    }

    private static ClusterConsensus Consensus(FindingSeverity severity, int support, int successfulModelCount)
    {
        if (severity != FindingSeverity.Critical)
        {
            return support >= 2 ? ClusterConsensus.Confirmed : ClusterConsensus.Unconfirmed;
        }

        // Critical needs at least two models and a strict majority of the successful models.
        return support >= 2 && support * 2 > successfulModelCount
            ? ClusterConsensus.Confirmed
            : ClusterConsensus.Disputed;
    }

    private static int Find(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }

        return index;
    }

    private static void Union(int[] parents, int left, int right)
    {
        var leftRoot = Find(parents, left);
        var rightRoot = Find(parents, right);
        if (leftRoot == rightRoot)
        {
            return;
        }

        if (leftRoot < rightRoot)
        {
            parents[rightRoot] = leftRoot;
        }
        else
        {
            parents[leftRoot] = rightRoot;
        }
    }
}

/// <summary>Median based score aggregation with one decimal place (EVALUATION 6).</summary>
public static class ScoreAggregator
{
    public static AreaScores Aggregate(IReadOnlyList<AreaScores> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (scores.Count < 2)
        {
            throw new ArgumentException("Semantic scores need at least two independent evaluations.", nameof(scores));
        }

        return new AreaScores
        {
            IntentCoverage = Median(scores.Select(static score => score.IntentCoverage)),
            Traceability = Median(scores.Select(static score => score.Traceability)),
            Testability = Median(scores.Select(static score => score.Testability)),
            TechnicalExecutability = Median(scores.Select(static score => score.TechnicalExecutability)),
            TargetAgentFitness = Median(scores.Select(static score => score.TargetAgentFitness)),
            UxOperationsCompleteness = Median(scores.Select(static score => score.UxOperationsCompleteness))
        };
    }

    /// <summary>Median rounded to one decimal place. An even count averages the two middle values.</summary>
    public static decimal Median(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("Median needs at least one value.", nameof(values));
        }

        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;
        return Round(median);
    }

    public static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public static string Format(decimal value) => Round(value).ToString("0.0", CultureInfo.InvariantCulture);
}
