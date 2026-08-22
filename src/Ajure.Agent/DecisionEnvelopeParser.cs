using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajure.Agent;

public sealed record DecisionProposal(
    string Id,
    string Question,
    string[] Options,
    string Recommended,
    DecisionSeverity Severity,
    string Reason,
    IReadOnlyDictionary<string, string> Impacts)
{
    public bool Critical => Severity == DecisionSeverity.Critical;
}

public enum DecisionSeverity
{
    Critical,
    Important,
    Defaultable
}

public static class DecisionEnvelopeParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    static DecisionEnvelopeParser()
    {
        Options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }

    public static IReadOnlyList<DecisionProposal> Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var envelope = JsonSerializer.Deserialize<DecisionEnvelope>(json, Options)
            ?? throw new JsonException("The decision envelope was empty.");
        if (envelope.Decisions.Length > 20)
        {
            throw new JsonException("The decision envelope exceeded 20 decisions.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        return envelope.Decisions.Select(item =>
        {
            var options = item.Options
                .Where(static option => !string.IsNullOrWhiteSpace(option))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (!IsDecisionId(item.Id)
                || !ids.Add(item.Id)
                || string.IsNullOrWhiteSpace(item.Question)
                || item.Question.Length > 2_000
                || options.Length < 2
                || options.Length != item.Options.Length
                || !options.Contains(item.Recommended, StringComparer.Ordinal)
                || string.IsNullOrWhiteSpace(item.Reason)
                || item.Reason.Length > 2_000
                || item.Impacts.Count != options.Length
                || options.Any(option =>
                    !item.Impacts.TryGetValue(option, out var impact)
                    || string.IsNullOrWhiteSpace(impact)
                    || impact.Length > 2_000))
            {
                throw new JsonException($"Decision '{item.Id}' was invalid.");
            }

            return new DecisionProposal(
                item.Id,
                item.Question.Trim(),
                options,
                item.Recommended,
                item.Severity,
                item.Reason.Trim(),
                new Dictionary<string, string>(item.Impacts, StringComparer.Ordinal));
        }).ToArray();
    }

    private static bool IsDecisionId(string id) =>
        id.StartsWith("DEC-", StringComparison.Ordinal)
        && id.Length > 4
        && id.AsSpan(4).IndexOfAnyExceptInRange('0', '9') < 0;

    private sealed record DecisionEnvelope
    {
        public required DecisionItem[] Decisions { get; init; }
    }

    private sealed record DecisionItem
    {
        public required string Id { get; init; }

        public required string Question { get; init; }

        public required string[] Options { get; init; }

        public required string Recommended { get; init; }

        public required DecisionSeverity Severity { get; init; }

        public required string Reason { get; init; }

        public required Dictionary<string, string> Impacts { get; init; }
    }
}
