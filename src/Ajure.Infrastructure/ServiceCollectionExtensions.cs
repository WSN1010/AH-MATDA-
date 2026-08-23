using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ajure.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddAjureStorage(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<StorageOptions>()
            .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
            .PostConfigure(options =>
                options.DataPath = builder.Configuration["AJURE_DATA_PATH"] ?? options.DataPath)
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.DataPath),
                "Ajure:Storage:DataPath is required.")
            .Validate(
                static options => Path.IsPathFullyQualified(options.DataPath),
                "Ajure:Storage:DataPath must be an absolute path.")
            .Validate(
                static options => options.BusyTimeoutMilliseconds > 0,
                "Ajure:Storage:BusyTimeoutMilliseconds must be positive.")
            .Validate(
                static options => options.LeaseSeconds > 0,
                "Ajure:Storage:LeaseSeconds must be positive.")
            .ValidateOnStart();
        builder.Services.AddSingleton(static provider =>
            new AjureStore(provider.GetRequiredService<IOptions<StorageOptions>>().Value));
        builder.Services.AddHostedService<AjureStoreInitializer>();
        return builder;
    }
}
