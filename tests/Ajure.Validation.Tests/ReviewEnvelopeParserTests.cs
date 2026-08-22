namespace Ajure.Validation.Tests;

public class ReviewEnvelopeParserTests
{
    private const string ValidPayload = """
    {
      "reviewComplete": true,
      "scores": {
        "intentCoverage": 22.5,
        "traceability": 18,
        "testability": 17,
        "technicalExecutability": 13,
        "targetAgentFitness": 9,
        "uxOperationsCompleteness": 8
      },
      "findings": [
        {
          "id": "f1",
          "severity": "critical",
          "category": "product",
          "ruleKey": "missing_ac",
          "statement": "FR-002 has no measurable acceptance criterion.",
          "evidence": ["PRD section 5"],
          "affectedIds": ["FR-002"],
          "suggestedResolution": "Add AC-010.",
          "requiresUserDecision": false
        }
      ]
    }
    """;

    [Fact]
    public void ValidEnvelopeIsParsed()
    {
        var result = ReviewEnvelopeParser.Parse(ValidPayload);

        Assert.True(result.IsValid);
        Assert.Empty(result.ErrorCode);
        var envelope = Assert.IsType<ReviewEnvelope>(result.Envelope);
        Assert.True(envelope.ReviewComplete);
        Assert.Equal(22.5m, envelope.Scores.IntentCoverage);
        Assert.Equal(87.5m, envelope.Scores.Total);
        var finding = Assert.Single(envelope.Findings);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Equal(RuleKeys.MissingAcceptanceCriterion, finding.RuleKey);
        Assert.Equal(["FR-002"], finding.AffectedIds);
        Assert.False(finding.RequiresUserDecision);
    }

    [Fact]
    public void FencedJsonIsAccepted()
    {
        var result = ReviewEnvelopeParser.Parse("```json\n" + ValidPayload + "\n```");

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingEnvelopeIsAFailure(string? payload)
    {
        var result = ReviewEnvelopeParser.Parse(payload);

        Assert.False(result.IsValid);
        Assert.Equal(ReviewEnvelopeParser.ErrorEnvelopeMissing, result.ErrorCode);
        Assert.Null(result.Envelope);
    }

    [Fact]
    public void MalformedJsonIsAFailureNotAnEmptyReview()
    {
        var result = ReviewEnvelopeParser.Parse("{\"reviewComplete\": true, ");

        Assert.False(result.IsValid);
        Assert.Equal(ReviewEnvelopeParser.ErrorInvalidJson, result.ErrorCode);
    }

    [Fact]
    public void NonObjectRootIsRejected()
    {
        var result = ReviewEnvelopeParser.Parse("[]");

        Assert.False(result.IsValid);
        Assert.Equal(ReviewEnvelopeParser.ErrorSchemaViolation, result.ErrorCode);
    }

    [Theory]
    [InlineData("""{"scores":{},"findings":[]}""")]
    [InlineData("""{"reviewComplete":"yes","scores":{},"findings":[]}""")]
    public void MissingOrNonBooleanReviewCompleteIsRejected(string payload)
    {
        Assert.Equal(ReviewEnvelopeParser.ErrorSchemaViolation, ReviewEnvelopeParser.Parse(payload).ErrorCode);
    }

    [Fact]
    public void IncompleteReviewIsRejected()
    {
        var payload = ValidPayload.Replace("\"reviewComplete\": true", "\"reviewComplete\": false", StringComparison.Ordinal);

        Assert.Equal(ReviewEnvelopeParser.ErrorReviewIncomplete, ReviewEnvelopeParser.Parse(payload).ErrorCode);
    }

    [Fact]
    public void MissingScoreAreaIsRejected()
    {
        var payload = ValidPayload.Replace("\"testability\": 17,", string.Empty, StringComparison.Ordinal);

        var result = ReviewEnvelopeParser.Parse(payload);

        Assert.Equal(ReviewEnvelopeParser.ErrorSchemaViolation, result.ErrorCode);
        Assert.Contains("testability", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"intentCoverage\": 22.5", "\"intentCoverage\": 26")]
    [InlineData("\"traceability\": 18", "\"traceability\": -1")]
    [InlineData("\"targetAgentFitness\": 9", "\"targetAgentFitness\": 10.5")]
    public void ScoresOutsideTheAreaMaximumAreRejected(string original, string replacement)
    {
        var payload = ValidPayload.Replace(original, replacement, StringComparison.Ordinal);

        Assert.Equal(ReviewEnvelopeParser.ErrorScoreOutOfRange, ReviewEnvelopeParser.Parse(payload).ErrorCode);
    }

    [Fact]
    public void MissingFindingsArrayIsRejected()
    {
        var payload = """{"reviewComplete":true,"scores":{"intentCoverage":1,"traceability":1,"testability":1,"technicalExecutability":1,"targetAgentFitness":1,"uxOperationsCompleteness":1}}""";

        Assert.Equal(ReviewEnvelopeParser.ErrorSchemaViolation, ReviewEnvelopeParser.Parse(payload).ErrorCode);
    }

    [Theory]
    [InlineData("\"severity\": \"critical\"", "\"severity\": \"blocker\"")]
    [InlineData("\"statement\": \"FR-002 has no measurable acceptance criterion.\"", "\"statement\": \"\"")]
    [InlineData("\"ruleKey\": \"missing_ac\"", "\"ruleKey\": 7")]
    [InlineData("\"affectedIds\": [\"FR-002\"]", "\"affectedIds\": \"FR-002\"")]
    [InlineData("\"requiresUserDecision\": false", "\"requiresUserDecision\": \"no\"")]
    [InlineData("\"id\": \"f1\"", "\"id\": \"\"")]
    public void MalformedFindingsAreRejected(string original, string replacement)
    {
        var payload = ValidPayload.Replace(original, replacement, StringComparison.Ordinal);

        Assert.Equal(ReviewEnvelopeParser.ErrorFindingInvalid, ReviewEnvelopeParser.Parse(payload).ErrorCode);
    }

    [Fact]
    public void UnknownRuleKeysSurviveParsingAndAreNormalizedLater()
    {
        var payload = ValidPayload.Replace("\"ruleKey\": \"missing_ac\"", "\"ruleKey\": \"made_up_rule\"", StringComparison.Ordinal);

        var result = ReviewEnvelopeParser.Parse(payload);

        Assert.True(result.IsValid);
        Assert.Equal("made_up_rule", result.Envelope!.Findings[0].RuleKey);
        Assert.Equal(RuleKeys.Other, RuleKeys.Normalize(result.Envelope.Findings[0].RuleKey));
    }

    [Fact]
    public void MissingRequiredFindingFieldsAreRejected()
    {
        var payload = """
        {"reviewComplete":true,
         "scores":{"intentCoverage":1,"traceability":1,"testability":1,"technicalExecutability":1,"targetAgentFitness":1,"uxOperationsCompleteness":1},
         "findings":[{"id":"f1","severity":"Minor","ruleKey":"other","statement":"Something small."}]}
        """;

        var result = ReviewEnvelopeParser.Parse(payload);

        Assert.False(result.IsValid);
        Assert.Equal(ReviewEnvelopeParser.ErrorFindingInvalid, result.ErrorCode);
    }

    [Theory]
    [InlineData("\"findings\"", "\"unexpected\":true,\"findings\"")]
    [InlineData("\"traceability\"", "\"unexpected\":1,\"traceability\"")]
    [InlineData("\"statement\"", "\"unexpected\":1,\"statement\"")]
    public void UnknownPropertiesAreRejected(string original, string replacement)
    {
        var payload = ValidPayload.Replace(original, replacement, StringComparison.Ordinal);

        Assert.False(ReviewEnvelopeParser.Parse(payload).IsValid);
    }
}
