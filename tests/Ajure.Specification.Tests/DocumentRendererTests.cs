namespace Ajure.Specification.Tests;

public class DocumentRendererTests
{
    [Fact]
    public void RendersTheThreeCommonDocumentsWithFixedPaths()
    {
        var documents = DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context);

        Assert.Equal(["IDEATION.md", "PRD.md", "TRD.md"], documents.Select(document => document.Path));
    }

    [Fact]
    public void EveryDocumentCarriesTheControlTable()
    {
        foreach (var document in DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context))
        {
            Assert.Contains("| Project | Trip Planner |", document.Content, StringComparison.Ordinal);
            Assert.Contains("| Spec Version | v1 |", document.Content, StringComparison.Ordinal);
            Assert.Contains("| Status | Validating |", document.Content, StringComparison.Ordinal);
            Assert.Contains("| Generated At | 2026-08-22T05:00:00Z |", document.Content, StringComparison.Ordinal);
            Assert.Contains("| Source | Ajure ProjectSpec |", document.Content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void IdeationContainsEveryRequiredSection()
    {
        var content = DocumentRenderer.RenderIdeation(SampleSpec.Create(), SampleSpec.Context).Content;

        for (var index = 0; index < DocumentSections.Ideation.Count; index++)
        {
            Assert.Contains($"## {index + 1}. {DocumentSections.Ideation[index]}", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PrdContainsEveryRequiredSectionAndRequirementFields()
    {
        var content = DocumentRenderer.RenderPrd(SampleSpec.Create(), SampleSpec.Context).Content;

        for (var index = 0; index < DocumentSections.Prd.Count; index++)
        {
            Assert.Contains($"## {index + 1}. {DocumentSections.Prd[index]}", content, StringComparison.Ordinal);
        }

        Assert.Contains("### FR-001 [Must] Create a trip", content, StringComparison.Ordinal);
        Assert.Contains("- Statement: ", content, StringComparison.Ordinal);
        Assert.Contains("- Acceptance: AC-001", content, StringComparison.Ordinal);
        Assert.Contains("- Journeys: J-001", content, StringComparison.Ordinal);
        Assert.Contains("### NFR-002 [Must] Keyboard accessibility", content, StringComparison.Ordinal);
        Assert.Contains("Technical decisions: no technical impact", content, StringComparison.Ordinal);
    }

    [Fact]
    public void TrdContainsEveryRequiredSectionAndTraceability()
    {
        var content = DocumentRenderer.RenderTrd(SampleSpec.Create(), SampleSpec.Context).Content;

        for (var index = 0; index < DocumentSections.Trd.Count; index++)
        {
            Assert.Contains($"## {index + 1}. {DocumentSections.Trd[index]}", content, StringComparison.Ordinal);
        }

        Assert.Contains("### TD-001 Relational storage for trips", content, StringComparison.Ordinal);
        Assert.Contains("| FR-001 | Planner API | TD-001 | AC-001 |", content, StringComparison.Ordinal);
        Assert.Contains("| NFR-002 | Accessibility test suite | No technical impact | AC-006 |", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingIsDeterministic()
    {
        var first = DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context);
        var second = DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context);

        Assert.Equal(
            first.Select(document => document.ContentHash),
            second.Select(document => document.ContentHash));
    }

    [Fact]
    public void RenderedDocumentsUseLineFeedOnly()
    {
        foreach (var document in DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context))
        {
            Assert.DoesNotContain('\r', document.Content);
        }
    }

    [Fact]
    public void SpecChangeChangesTheRenderedHash()
    {
        var spec = SampleSpec.Create();
        var changed = spec with { Vision = "A different product idea entirely." };

        Assert.NotEqual(
            DocumentRenderer.RenderIdeation(spec, SampleSpec.Context).ContentHash,
            DocumentRenderer.RenderIdeation(changed, SampleSpec.Context).ContentHash);
    }

    [Fact]
    public void RenderedDocumentsHaveNoEmptyRequiredSection()
    {
        foreach (var document in DocumentRenderer.RenderAll(SampleSpec.Create(), SampleSpec.Context))
        {
            Assert.DoesNotContain("_Not provided in the current specification._", document.Content, StringComparison.Ordinal);
        }
    }
}
