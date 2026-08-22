using System.Text.Json;
using Ajure.Infrastructure;

namespace Ajure.Worker;

internal sealed record StoredProjectIdea(
    string Summary,
    string Constraints,
    string Exclusions,
    string ExistingDocs)
{
    public static StoredProjectIdea Parse(string value)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<StoredProjectIdea>(value, JsonDefaults.Options);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Summary))
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
            // Legacy projects store the idea as plain text.
        }

        return new StoredProjectIdea(value, string.Empty, string.Empty, string.Empty);
    }
}
