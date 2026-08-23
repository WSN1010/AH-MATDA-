using Ajure.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ajure.Infrastructure;

public static class ModelServiceCollectionExtensions
{
    public static IServiceCollection AddAjureModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ModelProviderOptions>()
            .Bind(configuration.GetSection(ModelProviderOptions.SectionName))
            .PostConfigure(options =>
            {
                ApplyEnvironment(
                    configuration,
                    options.OpenAI,
                    "OPENAI_API_KEY",
                    "AJURE_OPENAI_MODEL");
                ApplyEnvironment(
                    configuration,
                    options.Anthropic,
                    "ANTHROPIC_API_KEY",
                    "AJURE_ANTHROPIC_MODEL");
                ApplyEnvironment(
                    configuration,
                    options.Gemini,
                    "GEMINI_API_KEY",
                    "AJURE_GEMINI_MODEL");
            })
            .Validate(
                static options => options.SessionTimeoutSeconds is >= 10 and <= 600,
                "Ajure:Models:SessionTimeoutSeconds must be between 10 and 600.")
            .Validate(
                static options => options.MaxOutputTokens is >= 256 and <= 131_072,
                "Ajure:Models:MaxOutputTokens must be between 256 and 131072.")
            .ValidateOnStart();
        services.AddSingleton(static _ => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<DirectModelGateway>();
        services.AddSingleton<IModelGateway>(
            static provider => provider.GetRequiredService<DirectModelGateway>());
        return services;
    }

    private static void ApplyEnvironment(
        IConfiguration configuration,
        ModelEndpointOptions options,
        string apiKeyName,
        string modelName)
    {
        options.ApiKey = configuration[apiKeyName] ?? options.ApiKey;
        options.Model = configuration[modelName] ?? options.Model;
    }
}
