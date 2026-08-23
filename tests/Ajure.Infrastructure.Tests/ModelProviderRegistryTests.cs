using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure.Tests;

public sealed class ModelProviderRegistryTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        AppContext.BaseDirectory,
        "provider-registry-test-data",
        Guid.NewGuid().ToString("N"));
    private AjureStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = new AjureStore(new StorageOptions
        {
            DataPath = DataPath,
            BusyTimeoutMilliseconds = 5_000,
            LeaseSeconds = 60
        });
        await _store.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProtectsAndReloadsLocalCredentials()
    {
        const string secret = "openai-local-secret-value";
        var options = new ModelProviderOptions();
        var first = Registry(options);

        var saved = await first.SaveLocalAsync(
            ModelProviderIds.OpenAI,
            secret,
            "gpt-local",
            CancellationToken.None);

        Assert.True(saved.Configured);
        Assert.Equal("local", saved.Source);
        Assert.True(saved.Editable);
        Assert.Equal("gpt-local", saved.Model);
        var stored = Assert.Single(
            await _store.ListModelProviderCredentialsAsync(CancellationToken.None));
        Assert.NotEqual(secret, stored.ProtectedApiKey);
        Assert.DoesNotContain(secret, stored.ProtectedApiKey, StringComparison.Ordinal);
        AssertDatabaseDoesNotContain(secret);

        var restarted = Registry(options);
        var resolved = Assert.Single(
            await restarted.ListConfiguredAsync(CancellationToken.None));
        Assert.Equal(ModelProviderIds.OpenAI, resolved.Id);
        Assert.Equal("openai:gpt-local", resolved.ModelId);
        Assert.Equal(secret, resolved.ApiKey);

        await restarted.DeleteLocalAsync(ModelProviderIds.OpenAI, CancellationToken.None);
        Assert.Empty(await restarted.ListConfiguredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EnvironmentConfigurationOverridesAndLocksLocalCredentials()
    {
        await Registry(new ModelProviderOptions()).SaveLocalAsync(
            ModelProviderIds.OpenAI,
            "local-secret",
            "gpt-local",
            CancellationToken.None);
        var options = new ModelProviderOptions
        {
            OpenAI = Endpoint(
                "environment-secret",
                "gpt-environment",
                "https://openai.example/v1/")
        };
        var registry = Registry(options);

        var status = (await registry.ListStatusesAsync(CancellationToken.None))
            .Single(provider => provider.Id == ModelProviderIds.OpenAI);
        Assert.True(status.Configured);
        Assert.Equal("environment", status.Source);
        Assert.False(status.Editable);
        Assert.Equal("gpt-environment", status.Model);
        var resolved = Assert.Single(
            await registry.ListConfiguredAsync(CancellationToken.None));
        Assert.Equal("environment-secret", resolved.ApiKey);

        await Assert.ThrowsAsync<ModelProviderManagedException>(() =>
            registry.SaveLocalAsync(
                ModelProviderIds.OpenAI,
                "replacement",
                "replacement-model",
                CancellationToken.None));
        await Assert.ThrowsAsync<ModelProviderManagedException>(() =>
            registry.DeleteLocalAsync(ModelProviderIds.OpenAI, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsNonHttpsProviderEndpoints()
    {
        var registry = Registry(new ModelProviderOptions
        {
            OpenAI = Endpoint(
                "openai-key",
                "gpt-test",
                "http://openai.example/v1/")
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.ListConfiguredAsync(CancellationToken.None));

        Assert.Equal("The OpenAI GPT base URL is invalid.", exception.Message);
    }

    [Fact]
    public async Task GatewayReadsLocalConfigurationOnEveryCall()
    {
        var options = new ModelProviderOptions();
        var registry = Registry(options);
        using var client = new HttpClient(new NoopHandler());
        var gateway = new DirectModelGateway(client, Options.Create(options), registry);

        Assert.Empty(await gateway.ListModelsAsync(CancellationToken.None));
        await registry.SaveLocalAsync(
            ModelProviderIds.Gemini,
            "gemini-secret",
            "gemini-local",
            CancellationToken.None);
        Assert.Equal(
            ["gemini:gemini-local"],
            (await gateway.ListModelsAsync(CancellationToken.None))
            .Select(static model => model.Id));
    }

    [Fact]
    public async Task IsolatesAnUnreadableLocalCredential()
    {
        await _store.SaveModelProviderCredentialAsync(
            new ModelProviderCredentialRecord(
                ModelProviderIds.OpenAI,
                "not-a-protected-value",
                "gpt-local",
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var options = new ModelProviderOptions
        {
            Gemini = Endpoint(
                "gemini-environment-secret",
                "gemini-environment",
                "https://gemini.example/v1beta/")
        };
        var registry = Registry(options);

        var statuses = await registry.ListStatusesAsync(CancellationToken.None);
        var openAi = statuses.Single(provider => provider.Id == ModelProviderIds.OpenAI);
        Assert.False(openAi.Configured);
        Assert.Equal("credential_unreadable", openAi.ErrorCode);
        var resolved = Assert.Single(
            await registry.ListConfiguredAsync(CancellationToken.None));
        Assert.Equal(ModelProviderIds.Gemini, resolved.Id);
    }

    private string DataPath => Path.Combine(_root, "ajure.db");

    private ModelProviderRegistry Registry(ModelProviderOptions options)
    {
        var keyDirectory = new DirectoryInfo(Path.Combine(_root, "keys"));
        var protection = DataProtectionProvider.Create(
            keyDirectory,
            builder => builder.SetApplicationName("Ajure.Registry.Tests"));
        return new ModelProviderRegistry(
            _store,
            Options.Create(options),
            protection,
            NullLogger<ModelProviderRegistry>.Instance);
    }

    private void AssertDatabaseDoesNotContain(string value)
    {
        SqliteConnection.ClearAllPools();
        var needle = Encoding.UTF8.GetBytes(value);
        foreach (var path in Directory.EnumerateFiles(_root, "ajure.db*"))
        {
            Assert.Equal(-1, File.ReadAllBytes(path).AsSpan().IndexOf(needle));
        }
    }

    private static ModelEndpointOptions Endpoint(
        string apiKey,
        string model,
        string baseUrl) =>
        new()
        {
            ApiKey = apiKey,
            Model = model,
            BaseUrl = baseUrl
        };

    private sealed class NoopHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No HTTP request was expected.");
    }
}
