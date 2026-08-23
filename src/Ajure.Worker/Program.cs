using Ajure.Agent;
using Ajure.Worker;
using Ajure.Infrastructure;
using System.Text.Json;

Environment.SetEnvironmentVariable(
    "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT",
    "false");

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAjureStorage();
builder.Services.AddAjureModels(builder.Configuration);
builder.Services.AddSingleton<SpecificationPipeline>();
builder.Services.AddSingleton<JobProcessor>();

var runProbe = args.Contains("--model-probe", StringComparer.Ordinal);
var fakeModel = string.Equals(
    Environment.GetEnvironmentVariable("AJURE_FAKE_MODEL"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (fakeModel && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("AJURE_FAKE_MODEL is allowed only in Development.");
}

if (!runProbe)
{
    builder.Services.AddHostedService<Worker>();
}

using var host = builder.Build();

if (runProbe)
{
    await host.StartAsync();
    var gateway = host.Services.GetRequiredService<IModelGateway>();
    var models = await gateway.ListModelsAsync(CancellationToken.None);
    if (models.Count < 2)
    {
        throw new ModelDiversityException();
    }

    var responses = await Task.WhenAll(
        models.Select(model => gateway.RunAsync(
            new ModelRequest(
                AgentRole.SpecArchitect,
                model.Id,
                "You are a connectivity probe. Do not use tools.",
                "Reply exactly: AJURE_PROBE_OK",
                TimeSpan.FromSeconds(30)),
            CancellationToken.None)));
    var result = new
    {
        completedAt = DateTimeOffset.UtcNow,
        models,
        responses = responses.Select(static response => new
        {
            response.ModelId,
            response.SessionId,
            response.Content
        })
    };
    Console.WriteLine(JsonSerializer.Serialize(result));
    await host.StopAsync();
    return;
}

await host.RunAsync();
