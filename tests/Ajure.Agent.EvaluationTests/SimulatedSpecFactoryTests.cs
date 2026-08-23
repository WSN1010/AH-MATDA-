using Ajure.Infrastructure;
using Ajure.Specification;
using Ajure.Validation;
using Ajure.Worker;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ajure.Agent.EvaluationTests;

public sealed class SimulatedSpecFactoryTests
{
    [Fact]
    public void SimulatedSpecPassesDeterministicHardChecksWithNativeTargets()
    {
        var spec = SimulatedSpecFactory.Create("Ajure", "Generate implementation-ready specifications.");
        var context = new DocumentContext
        {
            ProjectName = spec.ProjectName,
            SpecVersion = "v1",
            Status = SpecStatus.Validating,
            TargetIds =
            [
                TargetCatalog.ClaudeCode,
                TargetCatalog.GitHubCopilot,
                TargetCatalog.OpenAiCodex,
                TargetCatalog.Cursor
            ],
            GeneratedAt = new DateTimeOffset(2026, 8, 22, 5, 0, 0, TimeSpan.Zero)
        };
        var instruction = AgentInstructionSpec.FromSpec(spec, context);
        var result = DeterministicValidator.Validate(new DeterministicInput
        {
            Spec = spec,
            Context = context,
            Documents = DocumentRenderer.RenderAll(spec, context),
            TargetFiles = TargetFileRenderer.RenderBundle(instruction)
        });

        Assert.True(result.Passed);
        Assert.True(result.AcceptanceCoverageComplete);
        Assert.True(result.TargetFilesValid);
        Assert.True(result.ArtifactVersionsConsistent);
    }

    [Fact]
    public void SimulatedSpecPreservesTheCompleteSubmittedIdea()
    {
        const string summary = "A meal planner for busy parents who need weekly menus.";
        const string constraints = "Must work offline.\nUse the existing SQLite database.";
        const string exclusions = "No grocery delivery integration.";
        const string existingDocs = "Research note: families currently plan meals in spreadsheets.";
        const string approvedDecision = "DEC-001: Offline-first storage";

        var spec = SimulatedSpecFactory.Create(
            "Meal planner",
            summary,
            constraints,
            exclusions,
            existingDocs,
            [approvedDecision]);
        var context = new DocumentContext
        {
            ProjectName = spec.ProjectName,
            SpecVersion = "v1",
            Status = SpecStatus.Validating,
            TargetIds = [TargetCatalog.ClaudeCode],
            GeneratedAt = new DateTimeOffset(2026, 8, 22, 5, 0, 0, TimeSpan.Zero)
        };
        var documents = DocumentRenderer.RenderAll(spec, context);
        var instructions = TargetFileRenderer.RenderBundle(AgentInstructionSpec.FromSpec(spec, context));

        Assert.Equal(summary, spec.Vision);
        Assert.Contains(constraints.Split('\n')[0], spec.Technical.Constraints);
        Assert.Contains(exclusions, spec.NonGoals.Single(), StringComparison.Ordinal);
        Assert.Contains(existingDocs, documents.Single(document => document.Path == DocumentRenderer.IdeationPath).Content, StringComparison.Ordinal);
        Assert.Contains(approvedDecision, spec.LockedDecisions.Single(decision => decision.Contains(approvedDecision, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains(summary, instructions.Single().Content, StringComparison.Ordinal);
        Assert.All(
            documents,
            document => Assert.DoesNotContain("Product teams need one traceable specification", document.Content, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevalidatingVersionReusesArtifactRecords()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "pipeline-test-data",
            Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(root, "ajure.db");
        var previousFakeModel = Environment.GetEnvironmentVariable("AJURE_FAKE_MODEL");
        Environment.SetEnvironmentVariable("AJURE_FAKE_MODEL", "true");

        try
        {
            var store = new AjureStore(new StorageOptions
            {
                DataPath = dataPath,
                BusyTimeoutMilliseconds = 5_000,
                LeaseSeconds = 60
            });
            await store.InitializeAsync(CancellationToken.None);

            var projectId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            await store.CreateProjectAsync(
                new ProjectRecord(
                    projectId,
                    "Meal planner",
                    "test",
                    "en-US",
                    "A meal planner for busy parents.",
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
            await store.SaveVersionAsync(
                new SpecVersionRecord(
                    versionId,
                    projectId,
                    1,
                    SpecVersionStatus.Draft,
                    null,
                    "input-hash",
                    "balanced",
                    [TargetCatalog.ClaudeCode],
                    false,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    null),
                CancellationToken.None);

            using var services = new ServiceCollection().BuildServiceProvider();
            var pipeline = new SpecificationPipeline(
                store,
                services,
                new ConfigurationBuilder().Build(),
                Options.Create(new ModelProviderOptions()));

            var firstJob = Job(projectId, versionId, JobKind.Generate);
            await store.SaveJobAsync(firstJob, CancellationToken.None);
            await pipeline.GenerateAsync(
                new JobMessage(firstJob.Id, firstJob.Kind, projectId, versionId, null),
                CancellationToken.None);
            var firstArtifacts = await store.ListArtifactsAsync(versionId, CancellationToken.None);

            var secondJob = Job(projectId, versionId, JobKind.Validate);
            await store.SaveJobAsync(secondJob, CancellationToken.None);
            await pipeline.ValidateAsync(
                new JobMessage(secondJob.Id, secondJob.Kind, projectId, versionId, null),
                CancellationToken.None);
            var secondArtifacts = await store.ListArtifactsAsync(versionId, CancellationToken.None);

            Assert.NotEmpty(firstArtifacts);
            Assert.Equal(firstArtifacts.Count, secondArtifacts.Count);
            Assert.Equal(
                firstArtifacts.Select(static artifact => artifact.Id).Order(),
                secondArtifacts.Select(static artifact => artifact.Id).Order());
            Assert.Equal(
                secondArtifacts.Count,
                secondArtifacts.Count(static artifact => artifact.Status == ArtifactStatus.Current));
            Assert.Equal(
                secondArtifacts.Count,
                secondArtifacts.Select(static artifact => artifact.Path).Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AJURE_FAKE_MODEL", previousFakeModel);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static JobRecord Job(Guid projectId, Guid versionId, JobKind kind) =>
        new(
            Guid.NewGuid(),
            kind,
            projectId,
            versionId,
            null,
            JobStatus.Queued,
            0,
            false,
            null,
            null,
            null,
            null,
            "test-correlation",
            DateTimeOffset.UtcNow,
            null,
            null);
}
