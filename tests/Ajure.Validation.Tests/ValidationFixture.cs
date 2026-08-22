using Ajure.Specification;

namespace Ajure.Validation.Tests;

/// <summary>Shared builders so every validation test starts from the same clean specification.</summary>
internal static class ValidationFixture
{
    public static DeterministicInput Input(ProjectSpec? spec = null)
    {
        spec ??= SampleSpec.Create();
        var instruction = AgentInstructionSpec.FromSpec(spec, SampleSpec.Context);
        return new DeterministicInput
        {
            Spec = spec,
            Context = SampleSpec.Context,
            Documents = DocumentRenderer.RenderAll(spec, SampleSpec.Context),
            TargetFiles = TargetFileRenderer.RenderBundle(instruction)
        };
    }

    public static AreaScores Scores(
        decimal intent = 23m,
        decimal traceability = 19m,
        decimal testability = 19m,
        decimal executability = 14m,
        decimal fitness = 9m,
        decimal ux = 9m) => new()
        {
            IntentCoverage = intent,
            Traceability = traceability,
            Testability = testability,
            TechnicalExecutability = executability,
            TargetAgentFitness = fitness,
            UxOperationsCompleteness = ux
        };

    public static Finding Finding(
        string id,
        string ruleKey,
        FindingSeverity severity,
        params string[] affectedIds) => new()
        {
            Id = id,
            Severity = severity,
            RuleKey = ruleKey,
            Statement = $"Finding {id} about {ruleKey}.",
            Evidence = [$"{ruleKey} evidence for {id}"],
            AffectedIds = affectedIds
        };

    public static NormalizedReview Review(string modelId, params Finding[] findings) =>
        FindingNormalizer.Normalize(
            new ReviewResult
            {
                Role = "product",
                ModelId = modelId,
                Envelope = new ReviewEnvelope { ReviewComplete = true, Scores = Scores(), Findings = findings }
            },
            SampleSpec.Create());

    public static HardGateContext Context(
        DeterministicResult? deterministic = null,
        IReadOnlyList<FindingCluster>? clusters = null,
        IReadOnlyList<RegressionFinding>? regressions = null,
        IReadOnlyList<string>? models = null,
        IReadOnlyList<string>? invalidEnvelopes = null,
        IReadOnlyList<string>? repeatedCritical = null) => new()
        {
            Deterministic = deterministic ?? DeterministicValidator.Validate(Input()),
            Clusters = clusters ?? [],
            Regressions = regressions ?? [],
            SuccessfulModelIds = models ?? ["anthropic/claude-sonnet-4.5", "openai/gpt-5"],
            InvalidEnvelopeCodes = invalidEnvelopes ?? [],
            RepeatedCriticalFingerprints = repeatedCritical ?? []
        };
}
