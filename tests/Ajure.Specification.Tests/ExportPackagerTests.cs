using System.IO.Compression;
using System.Text;

namespace Ajure.Specification.Tests;

public class ExportPackagerTests
{
    private static ExportRequest Request()
    {
        var spec = SampleSpec.Create();
        var instruction = AgentInstructionSpec.FromSpec(spec, SampleSpec.Context);
        var files = DocumentRenderer.RenderAll(spec, SampleSpec.Context)
            .Select(static document => new ExportFile { Path = document.Path, Content = document.Content })
            .Concat(TargetFileRenderer.RenderBundle(instruction)
                .Select(static file => new ExportFile { Path = file.Path, Content = file.Content }))
            .ToArray();

        return new ExportRequest
        {
            SpecVersion = "v1",
            SpecHash = CanonicalJson.ComputeHash(spec),
            Files = files,
            TargetIds = SampleSpec.Context.TargetIds,
            EvaluationModelIds = ["openai/gpt-5", "anthropic/claude-sonnet-4.5", "openai/gpt-5"],
            IsSimulated = true,
            GeneratedAt = SampleSpec.GeneratedAt
        };
    }

    [Fact]
    public void PackageContainsEveryArtifactAndTheManifest()
    {
        var result = ExportPackager.Create(Request());

        using var archive = new ZipArchive(new MemoryStream(result.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(
            [".cursor/rules/ajure.mdc", "AGENTS.md", "CLAUDE.md", "IDEATION.md", "PRD.md", "TRD.md", "ajure-manifest.json"],
            archive.Entries.Select(static entry => entry.FullName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ManifestRecordsHashesModelIdsAndSimulationFlag()
    {
        var request = Request();

        var manifest = ExportPackager.Create(request).Manifest;

        Assert.Equal("v1", manifest.SpecVersion);
        Assert.Equal(request.SpecHash, manifest.SpecHash);
        Assert.True(manifest.IsSimulated);
        Assert.Equal(["anthropic/claude-sonnet-4.5", "openai/gpt-5"], manifest.EvaluationModelIds);
        Assert.Equal(
            [TargetCatalog.ClaudeCode, TargetCatalog.GitHubCopilot, TargetCatalog.OpenAiCodex, TargetCatalog.Cursor],
            manifest.TargetIds);
        Assert.Equal(request.Files.Count, manifest.Files.Count);
        Assert.Equal(manifest.Files.Select(static file => file.Path).Order(StringComparer.Ordinal), manifest.Files.Select(static file => file.Path));

        foreach (var file in manifest.Files)
        {
            var source = request.Files.Single(entry => entry.Path == file.Path);
            Assert.Equal(ContentHash.OfText(source.Content), file.Sha256);
        }
    }

    [Fact]
    public void ManifestHashesMatchTheBytesInsideTheArchive()
    {
        var result = ExportPackager.Create(Request());

        using var archive = new ZipArchive(new MemoryStream(result.Content.ToArray()), ZipArchiveMode.Read);
        foreach (var file in result.Manifest.Files)
        {
            var entry = archive.GetEntry(file.Path);
            Assert.NotNull(entry);
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Assert.Equal(file.Sha256, ContentHash.OfBytes(buffer.ToArray()));
        }
    }

    [Fact]
    public void IdenticalInputProducesIdenticalBytes()
    {
        var first = ExportPackager.Create(Request());
        var second = ExportPackager.Create(Request());

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.Content.ToArray(), second.Content.ToArray());
    }

    [Fact]
    public void FileOrderInTheRequestDoesNotChangeTheBytes()
    {
        var request = Request();
        var reversed = request with { Files = [.. request.Files.Reverse()] };

        Assert.Equal(ExportPackager.Create(request).Sha256, ExportPackager.Create(reversed).Sha256);
    }

    [Fact]
    public void ChangedContentChangesTheArchiveHash()
    {
        var request = Request();
        var changed = request with
        {
            Files = [.. request.Files.Select(static file => file.Path == "PRD.md" ? file with { Content = file.Content + "\n" } : file)]
        };

        Assert.NotEqual(ExportPackager.Create(request).Sha256, ExportPackager.Create(changed).Sha256);
    }

    [Fact]
    public void EntryTimestampsAreFixed()
    {
        using var archive = new ZipArchive(new MemoryStream(ExportPackager.Create(Request()).Content.ToArray()), ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), entry.LastWriteTime.DateTime);
        }
    }

    [Fact]
    public void ManifestIsCanonicalJson()
    {
        var result = ExportPackager.Create(Request());

        using var archive = new ZipArchive(new MemoryStream(result.Content.ToArray()), ZipArchiveMode.Read);
        using var stream = archive.GetEntry(ExportPackager.ManifestPath)!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = reader.ReadToEnd();

        Assert.StartsWith("{\"evaluationModelIds\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', json);
        Assert.Contains("\"isSimulated\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyAndDuplicateInputIsRejected()
    {
        var request = Request();

        Assert.Throws<ArgumentException>(() => ExportPackager.Create(request with { Files = [] }));
        Assert.Throws<ArgumentException>(() => ExportPackager.Create(request with { Files = [request.Files[0], request.Files[0]] }));
    }
}

public class RequirementGraphTests
{
    [Fact]
    public void GraphLinksRequirementsToCriteriaAndDecisions()
    {
        var graph = RequirementGraph.Build(SampleSpec.Create());

        var node = graph.Find("FR-001");
        Assert.NotNull(node);
        Assert.Equal(Priority.Must, node.Priority);
        Assert.True(node.IsFunctional);
        Assert.Equal(["AC-001"], node.AcceptanceCriteriaIds);
        Assert.Equal(["TD-001"], node.TechnicalDecisionIds);
        Assert.Equal(6, graph.Nodes.Count);
    }

    [Fact]
    public void CleanSpecHasFullCoverage()
    {
        var graph = RequirementGraph.Build(SampleSpec.Create());

        Assert.Empty(graph.RequirementsWithoutAcceptance());
        Assert.Empty(graph.RequirementsWithoutTechnicalDecision());
        Assert.Equal(1d, graph.AcceptanceCoverage());
        Assert.Equal(["FR-001", "FR-002", "FR-003", "NFR-001", "NFR-002"], graph.MustRequirementIds());
    }

    [Fact]
    public void MissingLinksAreReported()
    {
        var spec = SampleSpec.Create();
        var broken = spec with
        {
            Requirements =
            [
                spec.Requirements[0] with { AcceptanceCriteriaIds = [], TechnicalDecisionIds = [] },
                .. spec.Requirements.Skip(1)
            ]
        };

        var graph = RequirementGraph.Build(broken);

        Assert.Equal(["FR-001"], graph.RequirementsWithoutAcceptance());
        Assert.Equal(["FR-001"], graph.RequirementsWithoutTechnicalDecision());
        Assert.True(graph.AcceptanceCoverage() < 1d);
    }
}
