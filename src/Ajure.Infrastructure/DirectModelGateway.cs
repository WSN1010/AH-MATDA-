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
    private const string OpenAiProvider = "openai";
    private const string AnthropicProvider = "anthropic";
    private const string GeminiProvider = "gemini";
    private readonly HttpClient _httpClient;
    private readonly ModelProviderOptions _options;

    public DirectModelGateway(
        HttpClient httpClient,
        IOptions<ModelProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ModelDescriptor> models = ConfiguredProviders()
            .Select(static provider => new ModelDescriptor(
                provider.ModelId,
                $"{provider.DisplayName} ({provider.Model})"))
            .ToArray();
        return Task.FromResult(models);
    }

    public async Task<ModelResponse> RunAsync(
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = ConfiguredProviders()
            .SingleOrDefault(candidate => candidate.ModelId == request.ModelId)
            ?? throw new InvalidOperationException(
                $"Model '{request.ModelId}' is not configured.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        try
        {
            var (content, responseId) = provider.Name switch
            {
                OpenAiProvider => await RunOpenAiAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                AnthropicProvider => await RunAnthropicAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                GeminiProvider => await RunGeminiAsync(provider, request, timeout.Token)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException(
                    $"Model provider '{provider.Name}' is not supported.")
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
        ConfiguredProvider provider,
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

        using var document = await SendAsync(message, provider.Name, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Name);
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var responseMessage)
            || responseMessage.ValueKind != JsonValueKind.Object
            || !responseMessage.TryGetProperty("content", out var responseContent)
            || responseContent.ValueKind != JsonValueKind.String)
        {
            throw InvalidResponse(provider.Name);
        }

        var content = responseContent.GetString();
        return (RequiredContent(content, provider.Name), ResponseId(root));
    }

    private async Task<(string Content, string ResponseId)> RunAnthropicAsync(
        ConfiguredProvider provider,
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

        using var document = await SendAsync(message, provider.Name, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Name);
        if (!root.TryGetProperty("content", out var contentBlocks)
            || contentBlocks.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse(provider.Name);
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
        return (RequiredContent(content, provider.Name), ResponseId(root));
    }

    private async Task<(string Content, string ResponseId)> RunGeminiAsync(
        ConfiguredProvider provider,
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

        using var document = await SendAsync(message, provider.Name, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        EnsureObject(root, provider.Name);
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var candidateContent)
            || candidateContent.ValueKind != JsonValueKind.Object
            || !candidateContent.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse(provider.Name);
        }

        var content = string.Concat(
            parts.EnumerateArray()
                .Where(static part => part.ValueKind == JsonValueKind.Object)
                .Select(static part =>
                    part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String
                        ? text.GetString()
                        : null));
        return (RequiredContent(content, provider.Name), ResponseId(root));
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

    private List<ConfiguredProvider> ConfiguredProviders()
    {
        var providers = new List<ConfiguredProvider>(3);
        AddProvider(providers, OpenAiProvider, "OpenAI GPT", _options.OpenAI);
        AddProvider(providers, AnthropicProvider, "Anthropic Claude", _options.Anthropic);
        AddProvider(providers, GeminiProvider, "Google Gemini", _options.Gemini);
        return providers;
    }

    private static void AddProvider(
        ICollection<ConfiguredProvider> providers,
        string name,
        string displayName,
        ModelEndpointOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException(
                $"A model ID is required when the {displayName} API key is configured.");
        }

        var baseUrl = options.BaseUrl.EndsWith('/')
            ? options.BaseUrl
            : options.BaseUrl + "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"The {displayName} base URL is invalid.");
        }

        providers.Add(new ConfiguredProvider(
            name,
            displayName,
            options.Model,
            $"{name}:{options.Model}",
            options.ApiKey,
            baseUri));
    }

    private sealed record ConfiguredProvider(
        string Name,
        string DisplayName,
        string Model,
        string ModelId,
        string ApiKey,
        Uri BaseUri);
}
