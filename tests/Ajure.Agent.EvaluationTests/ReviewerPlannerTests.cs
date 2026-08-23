using Ajure.Agent;

namespace Ajure.Agent.EvaluationTests;

public sealed class ReviewerPlannerTests
{
    private static readonly string[] TwoModels = ["claude-opus-5", "gpt-5.6-sol"];

    private static readonly ModelDescriptor[] AvailableModels =
    [
        new("gpt-5.6-sol", "GPT-5.6 Sol"),
        new("claude-opus-5", "Claude Opus 5"),
        new("unused-model", "Unused")
    ];

    private static readonly string[] ConfiguredModels =
    [
        "claude-opus-5",
        "missing-model",
        "gpt-5.6-sol",
        "claude-opus-5"
    ];

    [Fact]
    public void AssignUsesDeterministicRoundRobinAcrossDistinctModels()
    {
        var assignments = ReviewerPlanner.Assign(TwoModels);

        Assert.Collection(
            assignments,
            assignment =>
            {
                Assert.Equal(AgentRole.ProductReviewer, assignment.Role);
                Assert.Equal("claude-opus-5", assignment.ModelId);
            },
            assignment =>
            {
                Assert.Equal(AgentRole.TechnicalReviewer, assignment.Role);
                Assert.Equal("gpt-5.6-sol", assignment.ModelId);
            },
            assignment =>
            {
                Assert.Equal(AgentRole.UxReviewer, assignment.Role);
                Assert.Equal("claude-opus-5", assignment.ModelId);
            });
    }

    [Fact]
    public void ResolvePoolPreservesConfiguredOrderAndRemovesUnavailableModels()
    {
        var pool = ReviewerPlanner.ResolvePool(AvailableModels, ConfiguredModels);

        Assert.Equal(TwoModels, pool);
    }

    [Fact]
    public void ResolvePoolUsesAllAvailableModelsWhenNoPoolIsConfigured()
    {
        var pool = ReviewerPlanner.ResolvePool(AvailableModels, []);

        Assert.Equal(
            ["gpt-5.6-sol", "claude-opus-5", "unused-model"],
            pool);
    }

    [Fact]
    public void ResolvePoolRejectsSingleAvailableModel()
    {
        var configured = new[] { TwoModels[0], "missing-model" };

        var exception = Assert.Throws<ModelDiversityException>(
            () => ReviewerPlanner.ResolvePool(AvailableModels, configured));

        Assert.Equal("model_diversity_unavailable", exception.Message);
    }
}
