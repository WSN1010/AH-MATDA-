var builder = DistributedApplication.CreateBuilder(args);

const string developmentStorage = "UseDevelopmentStorage=true";
var fakeModel = builder.Configuration["AJURE_FAKE_MODEL"] ?? "true";

var api = builder
    .AddProject<Projects.Ajure_Api>("api")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");

var worker = builder
    .AddProject<Projects.Ajure_Worker>("worker")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("aca-env");

    var githubToken = builder.AddParameter("copilot-github-token", secret: true);
    var storage = builder.AddAzureStorage("storage");
    var blobs = storage.AddBlobs("blobs");
    var queues = storage.AddQueues("queues");
    var tables = storage.AddTables("tables");

    api
        .WithReference(blobs)
        .WithReference(queues)
        .WithReference(tables);

    worker
        .WithReference(blobs)
        .WithReference(queues)
        .WithReference(tables)
        .WithEnvironment("AJURE_FAKE_MODEL", "false")
        .WithEnvironment("Ajure__Copilot__UseLoggedInUser", "false")
        .WithEnvironment("COPILOT_GITHUB_TOKEN", githubToken);
}
else
{
    var azuriteScript = Path.Combine(
        builder.AppHostDirectory,
        "node_modules",
        "azurite",
        "dist",
        "src",
        "azurite.js");
    var azuriteData = Path.Combine(builder.AppHostDirectory, ".azurite");

    var storage = builder
        .AddExecutable(
            "storage",
            "node",
            builder.AppHostDirectory,
            azuriteScript,
            "--silent",
            "--skipApiVersionCheck",
            "--location",
            azuriteData,
            "--blobHost",
            "127.0.0.1",
            "--blobPort",
            "10000",
            "--queueHost",
            "127.0.0.1",
            "--queuePort",
            "10001",
            "--tableHost",
            "127.0.0.1",
            "--tablePort",
            "10002")
        .WithHttpEndpoint(targetPort: 10000, port: 10000, name: "blob", isProxied: false)
        .WithHttpEndpoint(targetPort: 10001, port: 10001, name: "queue", isProxied: false)
        .WithHttpEndpoint(targetPort: 10002, port: 10002, name: "table", isProxied: false);

    api
        .WithEnvironment("ConnectionStrings__blobs", developmentStorage)
        .WithEnvironment("ConnectionStrings__queues", developmentStorage)
        .WithEnvironment("ConnectionStrings__tables", developmentStorage)
        .WaitFor(storage);

    worker
        .WithEnvironment("ConnectionStrings__blobs", developmentStorage)
        .WithEnvironment("ConnectionStrings__queues", developmentStorage)
        .WithEnvironment("ConnectionStrings__tables", developmentStorage)
        .WithEnvironment("AJURE_FAKE_MODEL", fakeModel)
        .WaitFor(storage);
}

var web = builder
    .AddViteApp("web", "../Ajure.Web")
    .WithReference(api)
    .WithEnvironment("VITE_AJURE_API_MODE", "live")
    .WithExternalHttpEndpoints()
    .WaitFor(api);

#pragma warning disable ASPIREJAVASCRIPT001
web.PublishAsStaticWebsite("/api", api);
#pragma warning restore ASPIREJAVASCRIPT001

builder.Build().Run();
