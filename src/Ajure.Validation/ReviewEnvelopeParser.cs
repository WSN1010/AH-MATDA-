using System.Globalization;
using System.Text.Json;

namespace Ajure.Validation;

public sealed record EnvelopeParseResult
{
    public required bool IsValid { get; init; }

    public ReviewEnvelope? Envelope { get; init; }

    /// <summary>Stable failure code, empty when the envelope is valid.</summary>
    public string ErrorCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public static EnvelopeParseResult Success(ReviewEnvelope envelope) =>
        new() { IsValid = true, Envelope = envelope };

    public static EnvelopeParseResult Failure(string errorCode, string message) =>
        new() { IsValid = false, ErrorCode = errorCode, Message = message };
}

/// <summary>
/// Strict reviewer envelope validation (EVALUATION 5, Stage 3). A missing envelope or a schema violation
/// is an evaluation failure, never an empty finding list.
/// </summary>
public static class ReviewEnvelopeParser
{
    public const string ErrorEnvelopeMissing = "envelope_missing";
    public const string ErrorInvalidJson = "envelope_invalid_json";
    public const string ErrorSchemaViolation = "envelope_schema_violation";
    public const string ErrorReviewIncomplete = "review_incomplete";
    public const string ErrorScoreOutOfRange = "score_out_of_range";
    public const string ErrorFindingInvalid = "finding_invalid";

    private static readonly HashSet<string> EnvelopeProperties =
        new(["reviewComplete", "scores", "findings"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ScoreProperties =
        new(
            [
                "intentCoverage",
                "traceability",
                "testability",
                "technicalExecutability",
                "targetAgentFitness",
                "uxOperationsCompleteness"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> FindingProperties =
        new(
            [
                "id",
                "severity",
                "category",
                "ruleKey",
                "statement",
                "evidence",
                "affectedIds",
                "suggestedResolution",
                "requiresUserDecision"
            ],
            StringComparer.OrdinalIgnoreCase);

    public static EnvelopeParseResult Parse(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return EnvelopeParseResult.Failure(ErrorEnvelopeMissing, "The reviewer returned no envelope.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(StripCodeFence(payload));
        }
        catch (JsonException exception)
        {
            return EnvelopeParseResult.Failure(ErrorInvalidJson, exception.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "The envelope root must be an object.");
            }

            if (!HasOnlyProperties(root, EnvelopeProperties))
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "The envelope contains an unknown or duplicate property.");
            }

            if (!TryGet(root, "reviewComplete", out var complete)
                || complete.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "'reviewComplete' is missing or not a boolean.");
            }

            if (!complete.GetBoolean())
            {
                return EnvelopeParseResult.Failure(ErrorReviewIncomplete, "The reviewer reported an incomplete review.");
            }

            if (!TryGet(root, "scores", out var scores) || scores.ValueKind != JsonValueKind.Object)
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "'scores' is missing or not an object.");
            }

            if (!HasOnlyProperties(scores, ScoreProperties))
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "The scores object contains an unknown or duplicate property.");
            }

            var areaScores = ReadScores(scores, out var scoreError, out var scoreCode);
            if (areaScores is null)
            {
                return EnvelopeParseResult.Failure(scoreCode, scoreError);
            }

            if (!TryGet(root, "findings", out var findings) || findings.ValueKind != JsonValueKind.Array)
            {
                return EnvelopeParseResult.Failure(ErrorSchemaViolation, "'findings' is missing or not an array.");
            }

            var parsed = new List<Finding>();
            foreach (var element in findings.EnumerateArray())
            {
                var finding = ReadFinding(element, out var findingError);
                if (finding is null)
                {
                    return EnvelopeParseResult.Failure(ErrorFindingInvalid, findingError);
                }

                parsed.Add(finding);
            }

            return EnvelopeParseResult.Success(new ReviewEnvelope
            {
                ReviewComplete = true,
                Scores = areaScores,
                Findings = parsed
            });
        }
    }

    private static AreaScores? ReadScores(JsonElement scores, out string error, out string errorCode)
    {
        errorCode = ErrorSchemaViolation;
        error = string.Empty;

        if (!TryReadScore(scores, "intentCoverage", AreaScores.IntentCoverageMax, out var intent, out error, out errorCode)
            || !TryReadScore(scores, "traceability", AreaScores.TraceabilityMax, out var traceability, out error, out errorCode)
            || !TryReadScore(scores, "testability", AreaScores.TestabilityMax, out var testability, out error, out errorCode)
            || !TryReadScore(scores, "technicalExecutability", AreaScores.TechnicalExecutabilityMax, out var executability, out error, out errorCode)
            || !TryReadScore(scores, "targetAgentFitness", AreaScores.TargetAgentFitnessMax, out var fitness, out error, out errorCode)
            || !TryReadScore(scores, "uxOperationsCompleteness", AreaScores.UxOperationsCompletenessMax, out var ux, out error, out errorCode))
        {
            return null;
        }

        return new AreaScores
        {
            IntentCoverage = intent,
            Traceability = traceability,
            Testability = testability,
            TechnicalExecutability = executability,
            TargetAgentFitness = fitness,
            UxOperationsCompleteness = ux
        };
    }

    private static bool TryReadScore(
        JsonElement scores,
        string name,
        decimal maximum,
        out decimal value,
        out string error,
        out string errorCode)
    {
        value = 0m;
        error = string.Empty;
        errorCode = ErrorSchemaViolation;

        if (!TryGet(scores, name, out var element) || element.ValueKind != JsonValueKind.Number)
        {
            error = $"Score '{name}' is missing or not a number.";
            return false;
        }

        if (!element.TryGetDecimal(out value))
        {
            error = $"Score '{name}' is not a decimal number.";
            return false;
        }

        if (value < 0m || value > maximum)
        {
            errorCode = ErrorScoreOutOfRange;
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"Score '{name}' must be between 0 and {maximum}, was {value}.");
            return false;
        }

        return true;
    }

    private static Finding? ReadFinding(JsonElement element, out string error)
    {
        error = string.Empty;
        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "A finding must be an object.";
            return null;
        }

        if (!HasOnlyProperties(element, FindingProperties))
        {
            error = "A finding contains an unknown or duplicate property.";
            return null;
        }

        var id = ReadString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "A finding needs a non-empty 'id'.";
            return null;
        }

        var severityText = ReadString(element, "severity");
        if (!Enum.TryParse<FindingSeverity>(severityText, ignoreCase: true, out var severity))
        {
            error = $"Finding '{id}' has an unsupported severity '{severityText}'.";
            return null;
        }

        var category = ReadString(element, "category");
        if (string.IsNullOrWhiteSpace(category))
        {
            error = $"Finding '{id}' needs a non-empty 'category'.";
            return null;
        }

        if (!TryGet(element, "ruleKey", out var ruleKeyElement) || ruleKeyElement.ValueKind != JsonValueKind.String)
        {
            error = $"Finding '{id}' is missing 'ruleKey'.";
            return null;
        }

        var statement = ReadString(element, "statement");
        if (string.IsNullOrWhiteSpace(statement))
        {
            error = $"Finding '{id}' needs a non-empty 'statement'.";
            return null;
        }

        if (!TryGet(element, "evidence", out _))
        {
            error = $"Finding '{id}' is missing 'evidence'.";
            return null;
        }

        var evidence = ReadStringArray(element, "evidence", out var evidenceError);
        if (evidence is null)
        {
            error = $"Finding '{id}': {evidenceError}";
            return null;
        }

        if (!TryGet(element, "affectedIds", out _))
        {
            error = $"Finding '{id}' is missing 'affectedIds'.";
            return null;
        }

        var affectedIds = ReadStringArray(element, "affectedIds", out var affectedError);
        if (affectedIds is null)
        {
            error = $"Finding '{id}': {affectedError}";
            return null;
        }

        if (!TryGet(element, "suggestedResolution", out var resolutionElement)
            || resolutionElement.ValueKind != JsonValueKind.String)
        {
            error = $"Finding '{id}' is missing string 'suggestedResolution'.";
            return null;
        }

        if (!TryGet(element, "requiresUserDecision", out var decisionElement)
            || decisionElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = $"Finding '{id}' is missing boolean 'requiresUserDecision'.";
            return null;
        }

        return new Finding
        {
            Id = id,
            Severity = severity,
            Category = category,
            RuleKey = ruleKeyElement.GetString() ?? RuleKeys.Other,
            Statement = statement,
            Evidence = evidence,
            AffectedIds = affectedIds,
            SuggestedResolution = resolutionElement.GetString() ?? string.Empty,
            RequiresUserDecision = decisionElement.GetBoolean()
        };
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string>? ReadStringArray(JsonElement element, string name, out string error)
    {
        error = string.Empty;
        if (!TryGet(element, name, out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            error = $"'{name}' must be an array.";
            return null;
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                error = $"'{name}' must contain strings only.";
                return null;
            }

            items.Add(item.GetString() ?? string.Empty);
        }

        return items;
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool HasOnlyProperties(JsonElement element, HashSet<string> allowed)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Removes a surrounding Markdown code fence. Formatting only, no schema relaxation.</summary>
    private static string StripCodeFence(string payload)
    {
        var trimmed = payload.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstBreak = trimmed.IndexOf('\n', StringComparison.Ordinal);
        if (firstBreak < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstBreak + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence < 0 ? body.Trim() : body[..lastFence].Trim();
    }
}
