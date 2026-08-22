using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ajure.Specification.Tests;

public class SpecSerializationTests
{
    [Fact]
    public void RoundTripsThroughJsonWithoutLosingContent()
    {
        var spec = SampleSpec.Create();

        var restored = SpecJson.Deserialize<ProjectSpec>(SpecJson.Serialize(spec));

        Assert.Equal(CanonicalJson.Serialize(spec), CanonicalJson.Serialize(restored));
        Assert.Equal(spec.Requirements.Count, restored.Requirements.Count);
        Assert.Equal("FR-001", restored.Requirements[0].Id);
        Assert.Equal(Priority.Should, restored.Requirements[3].Priority);
        Assert.Equal(VerificationType.Ui, restored.AcceptanceCriteria[2].VerificationType);
        Assert.True(restored.NonFunctionalRequirements[1].NoTechnicalImpact);
        Assert.Equal("Trip", restored.Technical.DataEntities[0].Name);
    }

    [Fact]
    public void RoundTripsTheVersionRecord()
    {
        var version = new SpecVersion
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Number = 3,
            Status = SpecStatus.Ready,
            BaseVersionId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            InputHash = "abc",
            TargetIds = [TargetCatalog.ClaudeCode],
            SpecHash = "def",
            CreatedAt = SampleSpec.GeneratedAt,
            ApprovedAt = SampleSpec.GeneratedAt
        };

        var restored = SpecJson.Deserialize<SpecVersion>(SpecJson.Serialize(version));

        Assert.Equal(version.Id, restored.Id);
        Assert.Equal(SpecStatus.Ready, restored.Status);
        Assert.Equal("v3", restored.Label);
        Assert.Equal(version.BaseVersionId, restored.BaseVersionId);
    }

    [Fact]
    public void SerializesEnumsAsCamelCaseStrings()
    {
        var json = SpecJson.Serialize(SampleSpec.Create());

        Assert.Contains("\"priority\":\"must\"", json, StringComparison.Ordinal);
        Assert.Contains("\"verificationType\":\"automated\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPayloadsThatMissRequiredMembers()
    {
        Assert.Throws<JsonException>(() => SpecJson.Deserialize<Requirement>("""{"id":"FR-001"}"""));
    }
}

public class CanonicalHashTests
{
    [Fact]
    public void PropertyOrderDoesNotChangeTheHash()
    {
        const string First = """{"b":1,"a":{"d":[1,2],"c":"x"}}""";
        const string Second = """{"a":{"c":"x","d":[1,2]},"b":1}""";

        var firstNode = JsonNode.Parse(First);
        var secondNode = JsonNode.Parse(Second);

        Assert.Equal(CanonicalJson.Serialize(firstNode), CanonicalJson.Serialize(secondNode));
        Assert.Equal(CanonicalJson.ComputeHash(firstNode), CanonicalJson.ComputeHash(secondNode));
        Assert.Equal("""{"a":{"c":"x","d":[1,2]},"b":1}""", CanonicalJson.Serialize(firstNode));
    }

    [Fact]
    public void ArrayOrderChangesTheHash()
    {
        var first = JsonNode.Parse("""{"a":[1,2]}""");
        var second = JsonNode.Parse("""{"a":[2,1]}""");

        Assert.NotEqual(CanonicalJson.ComputeHash(first), CanonicalJson.ComputeHash(second));
    }

    [Fact]
    public void HashIsStableAcrossRepeatedComputation()
    {
        var spec = SampleSpec.Create();

        var first = CanonicalJson.ComputeHash(spec);
        var second = CanonicalJson.ComputeHash(SpecJson.Deserialize<ProjectSpec>(SpecJson.Serialize(spec)));

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, first.ToLowerInvariant());
    }

    [Fact]
    public void AnyMeaningChangeChangesTheHash()
    {
        var spec = SampleSpec.Create();
        var changed = spec with { Vision = spec.Vision + "." };

        Assert.NotEqual(CanonicalJson.ComputeHash(spec), CanonicalJson.ComputeHash(changed));
    }

    [Fact]
    public void ContentHashMatchesKnownVector()
    {
        Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            ContentHash.OfText("hello"));
    }
}
