namespace Ajure.Agent;

public enum AgentRole
{
    IdeaAnalyst,
    DecisionFacilitator,
    SpecArchitect,
    ProductReviewer,
    TechnicalReviewer,
    UxReviewer,
    TieBreaker,
    ImplementationSimulator,
    RepairAgent,
    TargetAdapter
}

public static class AgentRoles
{
    public static IReadOnlyList<AgentRole> Reviewers { get; } =
    [
        AgentRole.ProductReviewer,
        AgentRole.TechnicalReviewer,
        AgentRole.UxReviewer
    ];

    public static string DisplayName(this AgentRole role) => role switch
    {
        AgentRole.IdeaAnalyst => "Idea Analyst",
        AgentRole.DecisionFacilitator => "Decision Facilitator",
        AgentRole.SpecArchitect => "Spec Architect",
        AgentRole.ProductReviewer => "Product Reviewer",
        AgentRole.TechnicalReviewer => "Technical Reviewer",
        AgentRole.UxReviewer => "UX Reviewer",
        AgentRole.TieBreaker => "Tie Breaker",
        AgentRole.ImplementationSimulator => "Implementation Simulator",
        AgentRole.RepairAgent => "Repair Agent",
        AgentRole.TargetAdapter => "Target Adapter",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
