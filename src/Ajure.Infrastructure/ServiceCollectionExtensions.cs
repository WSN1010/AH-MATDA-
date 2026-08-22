using Ajure.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ajure.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAjureStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("storage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:storage is required.");
        }

        services.AddSingleton(new AjureStore(connectionString));
        services.AddHostedService<AjureStoreInitializer>();
        return services;
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
