using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ajure.Agent;

internal sealed class ModelGatewayChatClient(
    IModelGateway gateway,
    ModelRequest request) : IChatClient
{
    private readonly ChatClientMetadata _metadata =
        new("github.copilot", defaultModelId: request.ModelId);
    private ModelResponse? _lastResponse;

    public ModelResponse? LastResponse => Volatile.Read(ref _lastResponse);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await InvokeAsync(messages, cancellationToken).ConfigureAwait(false);
        return ToChatResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await InvokeAsync(messages, cancellationToken).ConfigureAwait(false);
        foreach (var update in ToChatResponse(response).ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(ChatClientMetadata)
            ? _metadata
            : serviceType.IsInstanceOfType(this)
                ? this
                : null;
    }

    public void Dispose()
    {
    }

    private async Task<ModelResponse> InvokeAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToArray();
        var prompt = string.Empty;
        for (var index = materialized.Count - 1; index >= 0; index--)
        {
            if (materialized[index].Role == ChatRole.User)
            {
                prompt = materialized[index].Text;
                break;
            }
        }

        if (prompt.Length == 0 && materialized.Count > 0)
        {
            prompt = materialized[materialized.Count - 1].Text;
        }

        var response = await gateway
            .RunAsync(request with { Prompt = prompt }, cancellationToken)
            .ConfigureAwait(false);
        Volatile.Write(ref _lastResponse, response);
        return response;
    }

    private static ChatResponse ToChatResponse(ModelResponse response) =>
        new(new ChatMessage(ChatRole.Assistant, response.Content)
        {
            AuthorName = response.Role.ToString()
        })
        {
            ConversationId = response.SessionId,
            CreatedAt = DateTimeOffset.UtcNow,
            ModelId = response.ModelId,
            ResponseId = response.SessionId
        };
}
