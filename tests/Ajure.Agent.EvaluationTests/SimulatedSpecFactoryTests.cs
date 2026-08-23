using Ajure.Specification;
using Ajure.Validation;
using Ajure.Worker;

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
}
