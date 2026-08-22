namespace Ajure.Agent;

public sealed record ModelDescriptor(string Id, string Name);

public sealed record ModelRequest(
    AgentRole Role,
    string ModelId,
    string Instructions,
    string Prompt,
    TimeSpan Timeout);

public sealed record ModelResponse(
    AgentRole Role,
    string ModelId,
    string SessionId,
    string Content);

public interface IModelGateway
{
    Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken);

    Task<ModelResponse> RunAsync(ModelRequest request, CancellationToken cancellationToken);
}

public sealed class ModelDiversityException()
    : InvalidOperationException("model_diversity_unavailable");
