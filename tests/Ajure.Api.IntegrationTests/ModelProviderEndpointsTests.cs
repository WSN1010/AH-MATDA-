using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ajure.Agent;
using Ajure.Api;
using Ajure.Infrastructure;
using Ajure.Specification;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ajure.Api.IntegrationTests;

public sealed class ModelProviderEndpointsTests
{
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SavesReloadsAndDeletesLocalProviderWithoutReturningTheKey()
    {
        var root = TestRoot();
        var dataPath = Path.Combine(root, "ajure.db");
        const string apiKey = "api-key-that-must-not-leak";
        try
        {
            using (var factory = new ProviderApiFactory(dataPath, IPAddress.Loopback))
            using (var client = Client(factory))
            {
                var response = await PutAsync(
                    client,
                    "/api/model-providers/openai",
                    new SaveModelProviderRequest(apiKey, "gpt-local"),
                    "http://localhost:5173");

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain(apiKey, body, StringComparison.Ordinal);
                var saved = await response.Content.ReadFromJsonAsync<ModelProviderResponse>();
                Assert.NotNull(saved);
                Assert.True(saved.Configured);
                Assert.Equal("local", saved.Source);
                Assert.Equal("gpt-local", saved.Model);
            }

            using (var restartedFactory = new ProviderApiFactory(dataPath, IPAddress.Loopback))
            using (var restartedClient = Client(restartedFactory))
            {
                Assert.Equal(
                    ["openai:gpt-local"],
                    await ListModelsFromWorkerServicesAsync(dataPath));

                var list = await restartedClient
                    .GetFromJsonAsync<ModelProviderListResponse>("/api/model-providers");
                Assert.NotNull(list);
                Assert.Equal(2, list.RequiredCount);
                Assert.Equal(1, list.ConfiguredCount);
                var openAi = list.Providers.Single(provider => provider.Id == "openai");
                Assert.True(openAi.Configured);
                Assert.Equal("local", openAi.Source);
                Assert.Equal("gpt-local", openAi.Model);

                using var delete = await restartedClient.DeleteAsync(
                    "/api/model-providers/openai");
                Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
                var afterDelete = await restartedClient
                    .GetFromJsonAsync<ModelProviderListResponse>("/api/model-providers");
                Assert.NotNull(afterDelete);
                Assert.Equal(0, afterDelete.ConfiguredCount);
            }

            AssertDatabaseDoesNotContain(root, apiKey);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task RejectsNonLocalOriginsAndRemoteClients()
    {
        var root = TestRoot();
        try
        {
            using var localFactory = new ProviderApiFactory(
                Path.Combine(root, "local.db"),
                IPAddress.Loopback);
            using var localClient = Client(localFactory);
            using var hostileOrigin = await PutAsync(
                localClient,
                "/api/model-providers/openai",
                new SaveModelProviderRequest("secret", "gpt-local"),
                "https://example.com");
            Assert.Equal(HttpStatusCode.Forbidden, hostileOrigin.StatusCode);
            using var publicHostRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/model-providers");
            publicHostRequest.Headers.Host = "public.example";
            using var publicHost = await localClient.SendAsync(publicHostRequest);
            Assert.Equal(HttpStatusCode.Forbidden, publicHost.StatusCode);

            using var remoteFactory = new ProviderApiFactory(
                Path.Combine(root, "remote.db"),
                IPAddress.Parse("203.0.113.10"));
            using var remoteClient = Client(remoteFactory);
            using var remote = await remoteClient.GetAsync("/api/model-providers");
            Assert.Equal(HttpStatusCode.Forbidden, remote.StatusCode);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task EnvironmentManagedProviderCannotBeChanged()
    {
        var root = TestRoot();
        try
        {
            using var factory = new ProviderApiFactory(
                Path.Combine(root, "ajure.db"),
                IPAddress.Loopback,
                new Dictionary<string, string?>
                {
                    ["OPENAI_API_KEY"] = "environment-secret",
                    ["AJURE_OPENAI_MODEL"] = "gpt-environment"
                });
            using var client = Client(factory);

            var listBody = await client.GetStringAsync("/api/model-providers");
            Assert.DoesNotContain("environment-secret", listBody, StringComparison.Ordinal);
            var list = JsonSerializer.Deserialize<ModelProviderListResponse>(
                listBody,
                WebJsonOptions);
            Assert.NotNull(list);
            var openAi = list.Providers.Single(provider => provider.Id == "openai");
            Assert.Equal("environment", openAi.Source);
            Assert.False(openAi.Editable);

            using var save = await PutAsync(
                client,
                "/api/model-providers/openai",
                new SaveModelProviderRequest("replacement", "gpt-replacement"),
                "http://localhost");
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Contains(
                "provider_managed_by_environment",
                await save.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);

            using var delete = await client.DeleteAsync("/api/model-providers/openai");
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task ArtifactListReturnsOneEntryPerPath()
    {
        var root = TestRoot();
        var dataPath = Path.Combine(root, "ajure.db");
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        try
        {
            var store = new AjureStore(new StorageOptions
            {
                DataPath = dataPath,
                BusyTimeoutMilliseconds = 5_000,
                LeaseSeconds = 60
            });
            await store.InitializeAsync(CancellationToken.None);
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
                    SpecVersionStatus.Ready,
                    null,
                    "input-hash",
                    "balanced",
                    [TargetCatalog.ClaudeCode],
                    true,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    null),
                CancellationToken.None);

            await store.SaveArtifactAsync(
                new ArtifactRecord(
                    Guid.NewGuid(),
                    versionId,
                    ArtifactKind.Ideation,
                    null,
                    "IDEATION.md",
                    "stale-hash",
                    "2.0",
                    ArtifactStatus.Stale,
                    "artifacts/stale",
                    "text/markdown",
                    DateTimeOffset.UtcNow.AddMinutes(-1)),
                CancellationToken.None);
            await store.SaveArtifactAsync(
                new ArtifactRecord(
                    Guid.NewGuid(),
                    versionId,
                    ArtifactKind.Ideation,
                    null,
                    "IDEATION.md",
                    "current-hash",
                    "2.0",
                    ArtifactStatus.Current,
                    "artifacts/current",
                    "text/markdown",
                    DateTimeOffset.UtcNow),
                CancellationToken.None);

            using var factory = new ProviderApiFactory(dataPath, IPAddress.Loopback);
            using var client = Client(factory);
            var artifacts = await client.GetFromJsonAsync<ArtifactResponse[]>(
                $"/api/spec-versions/{versionId}/artifacts");

            Assert.NotNull(artifacts);
            var artifact = Assert.Single(artifacts);
            Assert.Equal("IDEATION.md", artifact.Path);
            Assert.Equal("Valid", artifact.Status);
            Assert.Equal("current-hash", artifact.ContentHash);

            var project = await client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{projectId}");
            Assert.NotNull(project);
            Assert.Equal(1, project.ArtifactCount);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

    private static async Task<HttpResponseMessage> PutAsync<T>(
        HttpClient client,
        string path,
        T value,
        string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(value)
        };
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }

    private static string TestRoot() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "provider-api-test-data",
            Guid.NewGuid().ToString("N"));

    private static async Task<string[]> ListModelsFromWorkerServicesAsync(string dataPath)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AJURE_DATA_PATH"] = dataPath,
                ["OPENAI_API_KEY"] = string.Empty,
                ["ANTHROPIC_API_KEY"] = string.Empty,
                ["GEMINI_API_KEY"] = string.Empty
            });
        builder.AddAjureStorage();
        builder.Services.AddAjureModels(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var gateway = host.Services.GetRequiredService<IModelGateway>();
            return (await gateway.ListModelsAsync(CancellationToken.None))
                .Select(static model => model.Id)
                .ToArray();
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void AssertDatabaseDoesNotContain(string root, string value)
    {
        SqliteConnection.ClearAllPools();
        var needle = Encoding.UTF8.GetBytes(value);
        foreach (var path in Directory.EnumerateFiles(root, "ajure.db*"))
        {
            Assert.Equal(-1, File.ReadAllBytes(path).AsSpan().IndexOf(needle));
        }
    }

    private static void DeleteTestRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ProviderApiFactory(
        string dataPath,
        IPAddress remoteAddress,
        IReadOnlyDictionary<string, string?>? overrides = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["AJURE_DATA_PATH"] = dataPath,
                    ["OPENAI_API_KEY"] = string.Empty,
                    ["ANTHROPIC_API_KEY"] = string.Empty,
                    ["GEMINI_API_KEY"] = string.Empty
                };
                if (overrides is not null)
                {
                    foreach (var (key, value) in overrides)
                    {
                        values[key] = value;
                    }
                }

                configuration.AddInMemoryCollection(values);
            });
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(
                    new RemoteAddressStartupFilter(remoteAddress)));
        }
    }

    private sealed class RemoteAddressStartupFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = address;
                    await nextMiddleware();
                });
                next(app);
            };
    }
}
