var builder = DistributedApplication.CreateBuilder(args);

const string developmentStorage = "UseDevelopmentStorage=true";
var fakeModel = builder.Configuration["AJURE_FAKE_MODEL"] ?? "true";
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

builder
    .AddProject<Projects.Ajure_Api>("api")
    .WithEnvironment("ConnectionStrings__storage", developmentStorage)
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false")
    .WaitFor(storage);

builder
    .AddProject<Projects.Ajure_Worker>("worker")
    .WithEnvironment("ConnectionStrings__storage", developmentStorage)
    .WithEnvironment("AJURE_FAKE_MODEL", fakeModel)
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false")
    .WaitFor(storage);

builder.Build().Run();
