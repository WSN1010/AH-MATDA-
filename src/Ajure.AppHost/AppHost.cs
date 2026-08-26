var builder = DistributedApplication.CreateBuilder(args);

var fakeModel = builder.Configuration["AJURE_FAKE_MODEL"] ?? "false";
var dataPath = Path.GetFullPath(
    builder.Configuration["AJURE_DATA_PATH"]
    ?? Path.Combine(builder.AppHostDirectory, ".data", "ajure.db"));

var api = builder
    .AddProject<Projects.Ajure_Api>("api")
    .WithEnvironment("AJURE_DATA_PATH", dataPath)
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");

var worker = builder
    .AddProject<Projects.Ajure_Worker>("worker")
    .WithEnvironment("AJURE_DATA_PATH", dataPath)
    .WithEnvironment("AJURE_FAKE_MODEL", fakeModel)
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");

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
