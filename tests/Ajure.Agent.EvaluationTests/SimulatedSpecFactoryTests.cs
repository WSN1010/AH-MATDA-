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
}
