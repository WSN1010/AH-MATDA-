namespace Ajure.Specification.Tests;

public class TargetFileRendererTests
{
    private static AgentInstructionSpec Instruction() =>
        AgentInstructionSpec.FromSpec(SampleSpec.Create(), SampleSpec.Context);

    [Theory]
    [InlineData(TargetCatalog.ClaudeCode, "CLAUDE.md")]
    [InlineData(TargetCatalog.GitHubCopilot, "AGENTS.md")]
    [InlineData(TargetCatalog.OpenAiCodex, "AGENTS.md")]
    [InlineData(TargetCatalog.GeminiCli, "GEMINI.md")]
    [InlineData(TargetCatalog.Cursor, ".cursor/rules/ajure.mdc")]
    [InlineData(TargetCatalog.DevinWindsurf, ".devin/rules/ajure.md")]
    [InlineData(TargetCatalog.Cline, ".clinerules/ajure.md")]
    [InlineData(TargetCatalog.AmazonQ, ".amazonq/rules/ajure.md")]
    [InlineData(TargetCatalog.Generic, "AGENTS.md")]
    public void EachTargetUsesItsNativePath(string targetId, string expectedPath)
    {
        Assert.Equal(expectedPath, TargetCatalog.PathFor(targetId));
        Assert.Equal(expectedPath, TargetFileRenderer.Render(Instruction(), targetId).Path);
    }

    [Fact]
    public void UnknownTargetIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetCatalog.Get("copilot-workspace"));
        Assert.False(TargetCatalog.TryGet("copilot-workspace", out var fallback));
        Assert.Equal(TargetCatalog.Generic, fallback.TargetId);
    }

    [Fact]
    public void EveryTargetFileCarriesAllRequiredSections()
    {
        var instruction = Instruction();

        foreach (var profile in TargetCatalog.All)
        {
            var file = TargetFileRenderer.Render(instruction, profile.TargetId);
            foreach (var section in TargetFileRenderer.RequiredSections)
            {
                Assert.Contains($"## {section}", file.Content, StringComparison.Ordinal);
            }

            Assert.Contains("Spec Version: v1", file.Content, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', file.Content);
        }
    }

    [Fact]
    public void CursorFileStartsWithAlwaysApplyFrontmatter()
    {
        var content = TargetFileRenderer.Render(Instruction(), TargetCatalog.Cursor).Content;

        Assert.StartsWith("---\ndescription: Ajure generated project rule\nalwaysApply: true\n---\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DevinFileStartsWithAlwaysOnTrigger()
    {
        var content = TargetFileRenderer.Render(Instruction(), TargetCatalog.DevinWindsurf).Content;

        Assert.StartsWith("---\ntrigger: always_on\n---\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FilesWithoutFrontmatterStartWithTheTitle()
    {
        foreach (var profile in TargetCatalog.All.Where(static profile => profile.Frontmatter == FrontmatterKind.None))
        {
            var content = TargetFileRenderer.Render(Instruction(), profile.TargetId).Content;
            Assert.StartsWith("# Trip Planner implementation guide", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ImportSyntaxIsUsedOnlyWhereItIsSupported()
    {
        var instruction = Instruction();

        Assert.Contains("@PRD.md", TargetFileRenderer.Render(instruction, TargetCatalog.ClaudeCode).Content, StringComparison.Ordinal);
        Assert.Contains("@./PRD.md", TargetFileRenderer.Render(instruction, TargetCatalog.GeminiCli).Content, StringComparison.Ordinal);
        Assert.DoesNotContain("@PRD.md", TargetFileRenderer.Render(instruction, TargetCatalog.Cursor).Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericProfileWarnsThatDiscoveryIsNotGuaranteed()
    {
        var content = TargetFileRenderer.Render(Instruction(), TargetCatalog.Generic).Content;

        Assert.Contains("## Tool-specific Notes", content, StringComparison.Ordinal);
        Assert.Contains("Generic fallback profile", content, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetsSharingAPathAreMergedIntoOneFile()
    {
        var bundle = TargetFileRenderer.RenderBundle(Instruction());

        Assert.Equal([".cursor/rules/ajure.mdc", "AGENTS.md", "CLAUDE.md"], bundle.Select(static file => file.Path));

        var agents = bundle.Single(static file => file.Path == "AGENTS.md");
        Assert.Equal([TargetCatalog.GitHubCopilot, TargetCatalog.OpenAiCodex], agents.TargetIds);
        Assert.Contains("## Tool-specific Notes", agents.Content, StringComparison.Ordinal);
        Assert.Contains("GitHub Copilot, OpenAI Codex", agents.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void MeaningIsIdenticalAcrossTargetsEvenThoughSyntaxDiffers()
    {
        var instruction = Instruction();
        var fingerprint = instruction.SemanticFingerprint();

        Assert.NotEqual(
            TargetFileRenderer.Render(instruction, TargetCatalog.Cursor).ContentHash,
            TargetFileRenderer.Render(instruction, TargetCatalog.ClaudeCode).ContentHash);
        var withDifferentTargets = instruction with { TargetIds = [TargetCatalog.Cline] };
        Assert.Equal(fingerprint, withDifferentTargets.SemanticFingerprint());
        Assert.NotEqual(fingerprint, (instruction with { Mission = "Something else" }).SemanticFingerprint());
    }

    [Fact]
    public void ScopeCarriesMustAndShouldRequirementIdsButNotNonGoals()
    {
        var instruction = Instruction();

        Assert.Contains(instruction.Scope, static line => line.StartsWith("FR-001 [Must]", StringComparison.Ordinal));
        Assert.Contains(instruction.Scope, static line => line.StartsWith("FR-004 [Should]", StringComparison.Ordinal));
        Assert.Contains(instruction.Scope, static line => line.StartsWith("NFR-002 [Must]", StringComparison.Ordinal));
        Assert.NotEmpty(instruction.NonGoals);
        Assert.Contains(instruction.LockedDecisions, static line => line.Contains("TD-001", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderingIsDeterministic()
    {
        Assert.Equal(
            TargetFileRenderer.RenderBundle(Instruction()).Select(static file => file.ContentHash),
            TargetFileRenderer.RenderBundle(Instruction()).Select(static file => file.ContentHash));
    }
}
