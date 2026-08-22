using Ajure.Agent;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ajure.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddAjureStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureBlobServiceClient(
            "blobs",
            configureClientBuilder: client => client.ConfigureOptions(ConfigureRetry));
        builder.AddAzureQueueServiceClient(
            "queues",
            configureClientBuilder: client => client.ConfigureOptions(options =>
            {
                ConfigureRetry(options);
                options.MessageEncoding = QueueMessageEncoding.Base64;
            }));
        builder.AddAzureTableServiceClient(
            "tables",
            configureClientBuilder: client => client.ConfigureOptions(ConfigureRetry));
        builder.Services.AddSingleton<AjureStore>();
        builder.Services.AddHostedService<AjureStoreInitializer>();
        return builder;
    }

    private static void ConfigureRetry(Azure.Core.ClientOptions options)
    {
        options.Retry.MaxRetries = 5;
        options.Retry.Delay = TimeSpan.FromMilliseconds(250);
    }

    public static IServiceCollection AddAjureCopilot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CopilotRuntimeOptions>()
            .Bind(configuration.GetSection(CopilotRuntimeOptions.SectionName))
            .PostConfigure(options =>
                options.ModelPool = configuration
                    .GetSection(CopilotRuntimeOptions.ModelPoolSectionName)
                    .Get<string[]>() ?? [])
            .Validate(
                static options => options.SessionTimeoutSeconds is >= 10 and <= 600,
                "Ajure:Copilot:SessionTimeoutSeconds must be between 10 and 600.")
            .ValidateOnStart();
        services.AddSingleton<CopilotAgentRuntime>();
        services.AddSingleton<IModelGateway>(
            static provider => provider.GetRequiredService<CopilotAgentRuntime>());
        services.AddHostedService(
            static provider => provider.GetRequiredService<CopilotAgentRuntime>());
        return services;
    }
}
