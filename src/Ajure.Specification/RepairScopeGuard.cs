using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ajure.Specification;

/// <summary>Rejects a model repair when it changes anything outside the explicitly affected stable IDs.</summary>
public static class RepairScopeGuard
{
    private static readonly string[] ScopedCollections =
    [
        "goals",
        "personas",
        "journeys",
        "requirements",
        "nonFunctionalRequirements",
        "acceptanceCriteria",
        "technicalDecisions",
        "uxDecisions",
        "risks",
        "openDecisions"
    ];

    public static bool OnlyTouches(
        ProjectSpec before,
        ProjectSpec after,
        IEnumerable<string> allowedIds)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(allowedIds);

        var allowed = allowedIds.ToHashSet(StringComparer.Ordinal);
        var beforeNode = JsonSerializer.SerializeToNode(before, SpecJson.Options)
            ?? throw new JsonException("The original ProjectSpec could not be represented as JSON.");
        var afterNode = JsonSerializer.SerializeToNode(after, SpecJson.Options)
            ?? throw new JsonException("The repaired ProjectSpec could not be represented as JSON.");

        RedactAllowedEntities(beforeNode, allowed);
        RedactAllowedEntities(afterNode, allowed);
        return JsonNode.DeepEquals(beforeNode, afterNode);
    }

    private static void RedactAllowedEntities(JsonNode node, HashSet<string> allowed)
    {
        if (node is not JsonObject root)
        {
            throw new JsonException("ProjectSpec must serialize as a JSON object.");
        }

        foreach (var property in ScopedCollections)
        {
            if (root[property] is not JsonArray collection)
            {
                continue;
            }

            for (var index = 0; index < collection.Count; index++)
            {
                if (collection[index] is JsonObject entity
                    && entity["id"] is JsonValue idNode
                    && idNode.TryGetValue<string>(out var id)
                    && allowed.Contains(id))
                {
                    collection[index] = JsonValue.Create($"$allowed:{id}");
                }
            }
        }
    }
}
