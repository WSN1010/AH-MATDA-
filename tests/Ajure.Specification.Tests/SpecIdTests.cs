namespace Ajure.Specification.Tests;

public class SpecIdTests
{
    [Theory]
    [InlineData(SpecIdKind.Goal, 1, "GOAL-001")]
    [InlineData(SpecIdKind.Persona, 12, "P-012")]
    [InlineData(SpecIdKind.Journey, 7, "J-007")]
    [InlineData(SpecIdKind.FunctionalRequirement, 1, "FR-001")]
    [InlineData(SpecIdKind.NonFunctionalRequirement, 23, "NFR-023")]
    [InlineData(SpecIdKind.AcceptanceCriterion, 999, "AC-999")]
    [InlineData(SpecIdKind.TechnicalDecision, 1000, "TD-1000")]
    [InlineData(SpecIdKind.UxDecision, 4, "UX-004")]
    [InlineData(SpecIdKind.Risk, 2, "RISK-002")]
    public void FormatsCanonicalIds(SpecIdKind kind, int number, string expected)
    {
        Assert.Equal(expected, SpecIds.Format(kind, number));
        Assert.True(SpecIds.TryParse(expected, out var parsedKind, out var parsedNumber));
        Assert.Equal(kind, parsedKind);
        Assert.Equal(number, parsedNumber);
    }

    [Theory]
    [InlineData("FR-1")]
    [InlineData("FR-01")]
    [InlineData("FR-0001")]
    [InlineData("FR-000")]
    [InlineData("FRX-001")]
    [InlineData("fr-001")]
    [InlineData("FR001")]
    [InlineData("FR-")]
    [InlineData("-001")]
    [InlineData("FR-00a")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsNonCanonicalIds(string? value) => Assert.False(SpecIds.IsValid(value));

    [Fact]
    public void NextUsesHighestNumberSoIdsAreNeverReused()
    {
        string[] existing = ["FR-001", "FR-003", "AC-009", "NFR-002"];

        Assert.Equal("FR-004", SpecIds.Next(SpecIdKind.FunctionalRequirement, existing));
        Assert.Equal("AC-010", SpecIds.Next(SpecIdKind.AcceptanceCriterion, existing));
        Assert.Equal("NFR-003", SpecIds.Next(SpecIdKind.NonFunctionalRequirement, existing));
        Assert.Equal("TD-001", SpecIds.Next(SpecIdKind.TechnicalDecision, existing));
    }

    [Fact]
    public void NextIgnoresGapsAndDeletedIds()
    {
        // FR-002 was deleted; its number must not be handed out again.
        string[] afterDeletion = ["FR-001", "FR-003"];
        Assert.Equal("FR-004", SpecIds.Next(SpecIdKind.FunctionalRequirement, afterDeletion));
    }

    [Fact]
    public void AllocatesConsecutiveIds()
    {
        var allocated = SpecIds.Allocate(SpecIdKind.AcceptanceCriterion, ["AC-002"], 3);
        Assert.Equal(["AC-003", "AC-004", "AC-005"], allocated);
    }

    [Fact]
    public void RenamingContentDoesNotChangeTheIdentifier()
    {
        var spec = SampleSpec.Create();
        var renamed = spec with
        {
            Requirements =
            [
                .. spec.Requirements.Select(requirement => requirement with { Title = requirement.Title + " (revised)" })
            ]
        };

        Assert.Equal(
            spec.Requirements.Select(requirement => requirement.Id),
            renamed.Requirements.Select(requirement => requirement.Id));
    }

    [Fact]
    public void AllIdsAreSortedAndValid()
    {
        var ids = SampleSpec.Create().AllIds();

        Assert.All(ids, id => Assert.True(SpecIds.IsValid(id), id));
        Assert.Equal([.. ids.OrderBy(id => id, StringComparer.Ordinal)], ids);
        Assert.Contains("FR-001", ids);
        Assert.Contains("RISK-002", ids);
    }

    [Fact]
    public void IsRequirementIdOnlyAcceptsRequirementFamilies()
    {
        Assert.True(SpecIds.IsRequirementId("FR-001"));
        Assert.True(SpecIds.IsRequirementId("NFR-001"));
        Assert.False(SpecIds.IsRequirementId("AC-001"));
        Assert.False(SpecIds.IsRequirementId("GOAL-001"));
    }
}
