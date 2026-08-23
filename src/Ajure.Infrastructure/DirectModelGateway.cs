using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ajure.Agent;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure;

public sealed class ModelProviderException(
    string provider,
    HttpStatusCode statusCode,
    bool retryable)
    : InvalidOperationException($"The {provider} model API rejected the request.")
{
    public string Provider { get; } = provider;

    public HttpStatusCode StatusCode { get; } = statusCode;

    public bool Retryable { get; } = retryable;
}

public sealed class DirectModelGateway : IModelGateway
{
    private readonly HttpClient _httpClient;
    private readonly ModelProviderOptions _options;
    private readonly IModelProviderResolver _providerResolver;

    public DirectModelGateway(
        HttpClient httpClient,
        IOptions<ModelProviderOptions> options,
        IModelProviderResolver providerResolver)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providerResolver);

        _httpClient = httpClient;
        _options = options.Value;
        _providerResolver = providerResolver;
    }

    public async Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        CancellationToken cancellationToken)
    {
        var providers = await _providerResolver.ListConfiguredAsync(cancellationToken)
            .ConfigureAwait(false);
        return providers
            .Select(static provider => new ModelDescriptor(
                provider.ModelId,
                $"{provider.DisplayName} ({provider.Model})"))
            .ToArray();
    }

    public async Task<ModelResponse> RunAsync(
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providers = await _providerResolver.ListConfiguredAsync(cancellationToken)
            .ConfigureAwait(false);
        var provider = providers
            .SingleOrDefault(candidate => candidate.ModelId == request.ModelId)
            ?? throw new InvalidOperationException(
                $"Model '{request.ModelId}' is not configured.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        try
        {
            var (content, responseId) = provider.Id switch
            {
                ModelProviderIds.OpenAI => await RunOpenAiAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                ModelProviderIds.Anthropic => await RunAnthropicAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                ModelProviderIds.Gemini => await RunGeminiAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException(
                    $"Model provider '{provider.Id}' is not supported.")
            };
            return new ModelResponse(
                request.Role,
                request.ModelId,
                responseId,
                content);
        }
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The {request.Role.DisplayName()} model request timed out.",
                exception);
        }
    }

    private async Task<(string Content, string ResponseId)> RunOpenAiAsync(
        ResolvedModelProvider provider,
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        using var message = JsonRequest(
            new Uri(provider.BaseUri, "chat/completions"),
            new
            {
                model = provider.Model,
                messages = new[]
                {
                    new { role = "developer", content = request.Instructions },
                    new { role = "user", content = request.Prompt }
                },
                max_completion_tokens = _options.MaxOutputTokens
            });
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        using var document = await SendAsync(message, provider.Id, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Id);
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var responseMessage)
            || responseMessage.ValueKind != JsonValueKind.Object
            || !responseMessage.TryGetProperty("content", out var responseContent)
            || responseContent.ValueKind != JsonValueKind.String)
        {
            throw InvalidResponse(provider.Id);
        }

        var content = responseContent.GetString();
        return (RequiredContent(content, provider.Id), ResponseId(root));
    }

    private async Task<(string Content, string ResponseId)> RunAnthropicAsync(
        ResolvedModelProvider provider,
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        using var message = JsonRequest(
            new Uri(provider.BaseUri, "messages"),
            new
            {
                model = provider.Model,
                max_tokens = _options.MaxOutputTokens,
                system = request.Instructions,
                messages = new[]
                {
                    new { role = "user", content = request.Prompt }
                }
            });
        message.Headers.Add("x-api-key", provider.ApiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");

        using var document = await SendAsync(message, provider.Id, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Id);
        if (!root.TryGetProperty("content", out var contentBlocks)
            || contentBlocks.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse(provider.Id);
        }

        var content = string.Concat(
            contentBlocks.EnumerateArray()
                .Where(static item =>
                    item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() == "text")
                .Select(static item =>
                    item.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String
                        ? text.GetString()
                        : null));
        return (RequiredContent(content, provider.Id), ResponseId(root));
    }

    private async Task<(string Content, string ResponseId)> RunGeminiAsync(
        ResolvedModelProvider provider,
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        var model = Uri.EscapeDataString(provider.Model);
        using var message = JsonRequest(
            new Uri(provider.BaseUri, $"models/{model}:generateContent"),
            new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = request.Instructions } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = request.Prompt } }
                    }
                },
                generation_config = new
                {
                    max_output_tokens = _options.MaxOutputTokens
                }
            });
        message.Headers.Add("x-goog-api-key", provider.ApiKey);

        using var document = await SendAsync(message, provider.Id, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Id);
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var candidateContent)
            || candidateContent.ValueKind != JsonValueKind.Object
            || !candidateContent.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse(provider.Id);
        }

        var content = string.Concat(
            parts.EnumerateArray()
                .Where(static part => part.ValueKind == JsonValueKind.Object)
                .Select(static part =>
                    part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String
                        ? text.GetString()
                        : null));
        return (RequiredContent(content, provider.Id), ResponseId(root));
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        string provider,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ModelProviderException(
                provider,
                response.StatusCode,
                response.StatusCode is HttpStatusCode.RequestTimeout
                    or HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500);
        }

        await using var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await JsonDocument
                .ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The {provider} model API returned invalid JSON.",
                exception);
        }
    }

    private static HttpRequestMessage JsonRequest(Uri uri, object payload) =>
        new(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonDefaults.Options),
                Encoding.UTF8,
                "application/json")
        };

    private static string RequiredContent(string? content, string provider) =>
        string.IsNullOrWhiteSpace(content)
            ? throw new InvalidDataException(
                $"The {provider} model API returned no text.")
            : content;

    private static void EnsureObject(JsonElement root, string provider)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw InvalidResponse(provider);
        }
    }

    private static InvalidDataException InvalidResponse(string provider) =>
        new($"The {provider} model API returned an invalid response.");

    private static string ResponseId(JsonElement root)
    {
        if (root.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(id.GetString()))
        {
            return id.GetString()!;
        }

        if (root.TryGetProperty("responseId", out var responseId)
            && responseId.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(responseId.GetString()))
        {
            return responseId.GetString()!;
        }

        return Guid.NewGuid().ToString("N");
    }

}
