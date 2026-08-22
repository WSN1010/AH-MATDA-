using System.Text.Json;
using Ajure.Agent;

namespace Ajure.Agent.EvaluationTests;

public sealed class DecisionEnvelopeParserTests
{
    private const string ValidEnvelope =
        """
        {
          "decisions": [
            {
              "id": "DEC-001",
              "question": "Which sign-in method should be used?",
              "options": ["GitHub OAuth", "Email link"],
              "recommended": "GitHub OAuth",
              "severity": "Critical",
              "reason": "The choice changes identity and account linking.",
              "impacts": {
                "GitHub OAuth": "Requires a GitHub OAuth application.",
                "Email link": "Requires transactional email delivery."
              }
            }
          ]
        }
        """;

    [Fact]
    public void ParseAcceptsStrictDecisionEnvelope()
    {
        var decisions = DecisionEnvelopeParser.Parse(ValidEnvelope);

        var decision = Assert.Single(decisions);
        Assert.Equal("DEC-001", decision.Id);
        Assert.True(decision.Critical);
        Assert.Equal("GitHub OAuth", decision.Recommended);
    }

    [Theory]
    [InlineData("""{"decisions":[{"id":"DEC-001","question":"Q?","options":["A","B"],"recommended":"C","severity":"Critical","reason":"R","impacts":{"A":"a","B":"b"}}]}""")]
    [InlineData("""{"decisions":[],"unexpected":true}""")]
    public void ParseRejectsInvalidDecisionEnvelope(string json)
    {
        Assert.Throws<JsonException>(() => DecisionEnvelopeParser.Parse(json));
    }
}
