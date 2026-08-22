using Ajure.Worker;
using Ajure.Infrastructure;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAjureStorage();
builder.Services.AddSingleton<SpecificationPipeline>();
builder.Services.AddSingleton<JobProcessor>();

var runProbe = args.Contains("--copilot-probe", StringComparer.Ordinal);
var fakeModel = string.Equals(
    Environment.GetEnvironmentVariable("AJURE_FAKE_MODEL"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (fakeModel && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("AJURE_FAKE_MODEL is allowed only in Development.");
}

if (!fakeModel || runProbe)
{
    Environment.SetEnvironmentVariable(
        "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT",
        "false");
    builder.Services.AddAjureCopilot(builder.Configuration);
}

if (!runProbe)
{
    builder.Services.AddHostedService<Worker>();
}

using var host = builder.Build();

if (runProbe)
{
    await host.StartAsync();
    var runtime = host.Services.GetRequiredService<CopilotAgentRuntime>();
    var result = await runtime.ProbeAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result));
    await host.StopAsync();
    return;
}

await host.RunAsync();
