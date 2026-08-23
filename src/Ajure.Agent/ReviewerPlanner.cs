namespace Ajure.Agent;

public sealed record ReviewerAssignment(AgentRole Role, string ModelId);

public static class ReviewerPlanner
{
    public static IReadOnlyList<ReviewerAssignment> Assign(IReadOnlyList<string> modelPool)
    {
        ArgumentNullException.ThrowIfNull(modelPool);

        var distinctModels = modelPool
            .Where(static model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctModels.Length < 2)
        {
            throw new ModelDiversityException();
        }

        return AgentRoles.Reviewers
            .Select((role, index) => new ReviewerAssignment(role, distinctModels[index % distinctModels.Length]))
            .ToArray();
    }

    public static IReadOnlyList<string> ResolvePool(
        IReadOnlyList<ModelDescriptor> availableModels,
        IReadOnlyList<string> configuredPool)
    {
        ArgumentNullException.ThrowIfNull(availableModels);
        ArgumentNullException.ThrowIfNull(configuredPool);

        var available = availableModels
            .Select(static model => model.Id)
            .ToHashSet(StringComparer.Ordinal);
        var requested = configuredPool.Count == 0
            ? availableModels.Select(static model => model.Id)
            : configuredPool;
        var pool = requested
            .Where(available.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (pool.Length < 2)
        {
            throw new ModelDiversityException();
        }

        return pool;
    }
}
