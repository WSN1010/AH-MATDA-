using Ajure.Agent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure;

public static class ModelServiceCollectionExtensions
{
    public static IServiceCollection AddAjureModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAjureProviderCredentials(configuration);
        services.AddSingleton(static _ => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<DirectModelGateway>();
        services.AddSingleton<IModelGateway>(
            static provider => provider.GetRequiredService<DirectModelGateway>());
        return services;
    }

    public static IServiceCollection AddAjureProviderCredentials(
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
        services.TryAddSingleton<IDataProtectionProvider>(CreateDataProtectionProvider);
        services.TryAddSingleton<ModelProviderRegistry>();
        services.TryAddSingleton<IModelProviderResolver>(
            static provider => provider.GetRequiredService<ModelProviderRegistry>());
        return services;
    }

    private static IDataProtectionProvider CreateDataProtectionProvider(
        IServiceProvider services)
    {
        var storage = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(storage.DataPath))
            ?? throw new InvalidOperationException(
                "Ajure:Storage:DataPath must include a parent directory.");
        var keyDirectory = new DirectoryInfo(Path.Combine(dataDirectory, ".ajure-keys"));
        keyDirectory.Create();
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                keyDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return DataProtectionProvider.Create(
            keyDirectory,
            builder =>
            {
                builder.SetApplicationName("Ajure.LocalModelCredentials");
                if (OperatingSystem.IsWindows())
                {
                    builder.ProtectKeysWithDpapi();
                }
            });
    }

    private static void ApplyEnvironment(
        IConfiguration configuration,
        ModelEndpointOptions options,
        string apiKeyName,
        string modelName)
    {
        if (configuration[apiKeyName] is { } apiKey
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            options.ApiKey = apiKey;
        }

        if (configuration[modelName] is { } model
            && !string.IsNullOrWhiteSpace(model))
        {
            options.Model = model;
        }
    }
}
