using System.Net;
using System.Text;
using System.Text.Json;
using Ajure.Agent;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure.Tests;

public sealed class DirectModelGatewayTests
{
    [Fact]
    public async Task ListsOnlyProvidersWithApiKeys()
    {
        using var client = Client(static (_, _) =>
            throw new InvalidOperationException("No HTTP request was expected."));
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint("openai-key", "gpt-test", "https://openai.example/v1/"),
            Anthropic = Endpoint(string.Empty, "claude-test", "https://anthropic.example/v1/"),
            Gemini = Endpoint("gemini-key", "gemini-test", "https://gemini.example/v1beta/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var models = await gateway.ListModelsAsync(CancellationToken.None);

        Assert.Equal(
            ["openai:gpt-test", "gemini:gemini-test"],
            models.Select(static model => model.Id));
    }

    [Fact]
    public async Task RejectsNonHttpsProviderEndpoints()
    {
        using var client = Client(static (_, _) =>
            throw new InvalidOperationException("No HTTP request was expected."));
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint("openai-key", "gpt-test", "http://openai.example/v1/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => gateway.ListModelsAsync(CancellationToken.None));

        Assert.Equal("The OpenAI GPT base URL is invalid.", exception.Message);
    }

    [Fact]
    public async Task SendsOpenAiChatCompletionRequest()
    {
        using var client = Client(async (request, cancellationToken) =>
        {
            Assert.Equal(
                new Uri("https://gateway.example/openai/v1/chat/completions"),
                request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("openai-secret", request.Headers.Authorization?.Parameter);

            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("gpt-test", body.RootElement.GetProperty("model").GetString());
            Assert.Equal(
                "developer",
                body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
            Assert.Equal(
                "system instructions",
                body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal(
                "user prompt",
                body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            Assert.Equal(
                16_384,
                body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            return Json(HttpStatusCode.OK, """
                {"id":"chat-1","choices":[{"message":{"content":"openai text"}}]}
                """);
        });
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint(
                "openai-secret",
                "gpt-test",
                "https://gateway.example/openai/v1")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var response = await gateway.RunAsync(Request("openai:gpt-test"), CancellationToken.None);

        Assert.Equal("chat-1", response.SessionId);
        Assert.Equal("openai text", response.Content);
    }

    [Fact]
    public async Task SendsAnthropicMessagesRequest()
    {
        using var client = Client(async (request, cancellationToken) =>
        {
            Assert.Equal(
                new Uri("https://gateway.example/anthropic/v1/messages"),
                request.RequestUri);
            Assert.Equal("anthropic-secret", request.Headers.GetValues("x-api-key").Single());
            Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("claude-test", body.RootElement.GetProperty("model").GetString());
            Assert.Equal("system instructions", body.RootElement.GetProperty("system").GetString());
            Assert.Equal(
                "user prompt",
                body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal(16_384, body.RootElement.GetProperty("max_tokens").GetInt32());
            return Json(HttpStatusCode.OK, """
                {
                  "id":"message-1",
                  "content":[
                    {"type":"text","text":"claude "},
                    {"type":"tool_use","id":"ignored"},
                    {"type":"text","text":"text"}
                  ]
                }
                """);
        });
        var options = new ModelProviderOptions
        {
            Anthropic = Endpoint(
                "anthropic-secret",
                "claude-test",
                "https://gateway.example/anthropic/v1/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var response = await gateway.RunAsync(
            Request("anthropic:claude-test"),
            CancellationToken.None);

        Assert.Equal("message-1", response.SessionId);
        Assert.Equal("claude text", response.Content);
    }

    [Fact]
    public async Task SendsGeminiGenerateContentRequest()
    {
        using var client = Client(async (request, cancellationToken) =>
        {
            Assert.Equal(
                new Uri("https://gateway.example/gemini/v1beta/models/gemini-test:generateContent"),
                request.RequestUri);
            Assert.Equal("gemini-secret", request.Headers.GetValues("x-goog-api-key").Single());

            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(
                "system instructions",
                body.RootElement
                    .GetProperty("system_instruction")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString());
            Assert.Equal(
                "user prompt",
                body.RootElement
                    .GetProperty("contents")[0]
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString());
            Assert.Equal(
                16_384,
                body.RootElement
                    .GetProperty("generation_config")
                    .GetProperty("max_output_tokens")
                    .GetInt32());
            return Json(HttpStatusCode.OK, """
                {
                  "responseId":"gemini-1",
                  "candidates":[{"content":{"parts":[{"text":"gemini text"}]}}]
                }
                """);
        });
        var options = new ModelProviderOptions
        {
            Gemini = Endpoint(
                "gemini-secret",
                "gemini-test",
                "https://gateway.example/gemini/v1beta/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var response = await gateway.RunAsync(
            Request("gemini:gemini-test"),
            CancellationToken.None);

        Assert.Equal("gemini-1", response.SessionId);
        Assert.Equal("gemini text", response.Content);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public async Task ClassifiesProviderErrorsWithoutLeakingSecrets(
        HttpStatusCode statusCode,
        bool retryable)
    {
        using var client = Client((_, _) =>
            Task.FromResult(Json(
                statusCode,
                """{"error":"upstream-sensitive-body"}""")));
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint(
                "openai-sensitive-key",
                "gpt-test",
                "https://openai.example/v1/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => gateway.RunAsync(Request("openai:gpt-test"), CancellationToken.None));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(retryable, exception.Retryable);
        Assert.DoesNotContain("openai-sensitive-key", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("upstream-sensitive-body", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsInvalidJsonWithoutLeakingTheResponse()
    {
        using var client = Client(static (_, _) =>
            Task.FromResult(Json(
                HttpStatusCode.OK,
                """{"sensitive-response":"not-closed""")));
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint("key", "gpt-test", "https://openai.example/v1/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => gateway.RunAsync(Request("openai:gpt-test"), CancellationToken.None));

        Assert.DoesNotContain("sensitive-response", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertsOnlyItsOwnTimeoutToTimeoutException()
    {
        using var client = Client(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json(HttpStatusCode.OK, "{}");
        });
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint("key", "gpt-test", "https://openai.example/v1/")
        };
        var gateway = new DirectModelGateway(client, Options.Create(options));

        await Assert.ThrowsAsync<TimeoutException>(
            () => gateway.RunAsync(
                Request("openai:gpt-test") with { Timeout = TimeSpan.FromMilliseconds(20) },
                CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.RunAsync(Request("openai:gpt-test"), cancellation.Token));
    }

    private static ModelRequest Request(string modelId) =>
        new(
            AgentRole.SpecArchitect,
            modelId,
            "system instructions",
            "user prompt",
            TimeSpan.FromSeconds(1));

    private static ModelEndpointOptions Endpoint(string apiKey, string model, string baseUrl) =>
        new()
        {
            ApiKey = apiKey,
            Model = model,
            BaseUrl = baseUrl
        };

    private static HttpClient Client(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send));

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
