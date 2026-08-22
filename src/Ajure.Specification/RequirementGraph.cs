namespace Ajure.Specification;

public sealed record RequirementNode
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Statement { get; init; }

    public required Priority Priority { get; init; }

    public required bool IsFunctional { get; init; }

    public IReadOnlyList<string> AcceptanceCriteriaIds { get; init; } = [];

    public IReadOnlyList<string> TechnicalDecisionIds { get; init; } = [];

    public IReadOnlyList<string> JourneyIds { get; init; } = [];

    public bool NoTechnicalImpact { get; init; }
}

/// <summary>
/// Requirement graph used by traceability checks, impact analysis and regression comparison.
/// Nodes are ordered by identifier so every consumer sees the same sequence.
/// </summary>
public sealed class RequirementGraph
{
    private readonly Dictionary<string, RequirementNode> _byId;

    private RequirementGraph(
        IReadOnlyList<RequirementNode> nodes,
        IReadOnlyList<string> acceptanceCriterionIds,
        IReadOnlyList<string> technicalDecisionIds,
        IReadOnlyList<string> journeyIds)
    {
        Nodes = nodes;
        AcceptanceCriterionIds = acceptanceCriterionIds;
        TechnicalDecisionIds = technicalDecisionIds;
        JourneyIds = journeyIds;
        _byId = nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<RequirementNode> Nodes { get; }

    public IReadOnlyList<string> AcceptanceCriterionIds { get; }

    public IReadOnlyList<string> TechnicalDecisionIds { get; }

    public IReadOnlyList<string> JourneyIds { get; }

    public static RequirementGraph Build(ProjectSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var nodes = spec.Requirements
            .Select(requirement => ToNode(requirement, isFunctional: true))
            .Concat(spec.NonFunctionalRequirements.Select(requirement => ToNode(requirement, isFunctional: false)))
            .OrderBy(static node => node.Id, StringComparer.Ordinal)
            .ToArray();

        return new RequirementGraph(
            nodes,
            [.. spec.AcceptanceCriteria.Select(static criterion => criterion.Id).OrderBy(static id => id, StringComparer.Ordinal)],
            [.. spec.TechnicalDecisions.Select(static decision => decision.Id).OrderBy(static id => id, StringComparer.Ordinal)],
            [.. spec.Journeys.Select(static journey => journey.Id).OrderBy(static id => id, StringComparer.Ordinal)]);
    }

    public RequirementNode? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out var node) ? node : null;
    }

    /// <summary>Requirements without any acceptance criterion link. Drives HG-02.</summary>
    public IReadOnlyList<string> RequirementsWithoutAcceptance() =>
    [
        .. Nodes
            .Where(node => node.AcceptanceCriteriaIds.Count == 0
                || node.AcceptanceCriteriaIds.All(id => !AcceptanceCriterionIds.Contains(id, StringComparer.Ordinal)))
            .Select(static node => node.Id)
    ];

    /// <summary>Requirements without a technical decision and without an explicit "no technical impact" marker.</summary>
    public IReadOnlyList<string> RequirementsWithoutTechnicalDecision() =>
    [
        .. Nodes
            .Where(node => !node.NoTechnicalImpact
                && (node.TechnicalDecisionIds.Count == 0
                    || node.TechnicalDecisionIds.All(id => !TechnicalDecisionIds.Contains(id, StringComparer.Ordinal))))
            .Select(static node => node.Id)
    ];

    /// <summary>Acceptance link ratio in the range 0..1.</summary>
    public double AcceptanceCoverage() =>
        Nodes.Count == 0 ? 1d : 1d - ((double)RequirementsWithoutAcceptance().Count / Nodes.Count);

    public IReadOnlyList<string> MustRequirementIds() =>
    [
        .. Nodes.Where(static node => node.Priority == Priority.Must).Select(static node => node.Id)
    ];

    private static RequirementNode ToNode(Requirement requirement, bool isFunctional) => new()
    {
        Id = requirement.Id,
        Title = requirement.Title,
        Statement = requirement.Statement,
        Priority = requirement.Priority,
        IsFunctional = isFunctional,
        AcceptanceCriteriaIds = [.. requirement.AcceptanceCriteriaIds.OrderBy(static id => id, StringComparer.Ordinal)],
        TechnicalDecisionIds = [.. requirement.TechnicalDecisionIds.OrderBy(static id => id, StringComparer.Ordinal)],
        JourneyIds = [.. requirement.JourneyIds.OrderBy(static id => id, StringComparer.Ordinal)],
        NoTechnicalImpact = requirement.NoTechnicalImpact
    };
}
