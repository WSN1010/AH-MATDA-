using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Ajure.Infrastructure;

public static class ModelProviderIds
{
    public const string OpenAI = "openai";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
}

public sealed record ModelProviderStatus(
    string Id,
    string DisplayName,
    bool Configured,
    string? Source,
    string Model,
    bool Editable,
    string? ErrorCode);

public sealed record ResolvedModelProvider(
    string Id,
    string DisplayName,
    string Model,
    string ModelId,
    string ApiKey,
    Uri BaseUri);

public interface IModelProviderResolver
{
    Task<IReadOnlyList<ResolvedModelProvider>> ListConfiguredAsync(
        CancellationToken cancellationToken);
}

public sealed class ModelProviderRegistry : IModelProviderResolver
{
    private static readonly ProviderDefinition[] Definitions =
    [
        new(ModelProviderIds.OpenAI, "OpenAI GPT", static options => options.OpenAI),
        new(ModelProviderIds.Anthropic, "Anthropic Claude", static options => options.Anthropic),
        new(ModelProviderIds.Gemini, "Google Gemini", static options => options.Gemini)
    ];

    private readonly AjureStore _store;
    private readonly ModelProviderOptions _options;
    private readonly IDataProtector _protector;
    private readonly ILogger<ModelProviderRegistry> _logger;

    public ModelProviderRegistry(
        AjureStore store,
        IOptions<ModelProviderOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ModelProviderRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _options = options.Value;
        _protector = dataProtectionProvider.CreateProtector(
            "Ajure.ModelProviderCredentials.v1");
        _logger = logger;
    }

    public static bool IsSupported(string providerId) =>
        Definitions.Any(definition =>
            string.Equals(definition.Id, providerId, StringComparison.Ordinal));

    public async Task<IReadOnlyList<ModelProviderStatus>> ListStatusesAsync(
        CancellationToken cancellationToken)
    {
        var localCredentials = await LocalCredentialsAsync(cancellationToken).ConfigureAwait(false);
        return Definitions
            .Select(definition =>
            {
                var options = definition.Options(_options);
                var environmentManaged = !string.IsNullOrWhiteSpace(options.ApiKey);
                var environmentConfigured =
                    environmentManaged && !string.IsNullOrWhiteSpace(options.Model);
                localCredentials.TryGetValue(definition.Id, out var local);
                var localConfigured = !environmentManaged
                    && local is not null
                    && TryUnprotect(local, out _);
                var errorCode = environmentManaged && !environmentConfigured
                    ? "model_required"
                    : local is not null && !environmentManaged && !localConfigured
                        ? "credential_unreadable"
                        : null;
                return new ModelProviderStatus(
                    definition.Id,
                    definition.DisplayName,
                    environmentConfigured || localConfigured,
                    environmentManaged ? "environment" : local is not null ? "local" : null,
                    environmentManaged ? options.Model : local?.Model ?? options.Model,
                    !environmentManaged,
                    errorCode);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<ResolvedModelProvider>> ListConfiguredAsync(
        CancellationToken cancellationToken)
    {
        var localCredentials = await LocalCredentialsAsync(cancellationToken).ConfigureAwait(false);
        var providers = new List<ResolvedModelProvider>(Definitions.Length);
        foreach (var definition in Definitions)
        {
            var options = definition.Options(_options);
            var environmentManaged = !string.IsNullOrWhiteSpace(options.ApiKey);
            localCredentials.TryGetValue(definition.Id, out var local);
            if (!environmentManaged && local is null)
            {
                continue;
            }

            string apiKey;
            if (environmentManaged)
            {
                apiKey = options.ApiKey;
            }
            else if (!TryUnprotect(local!, out apiKey))
            {
                continue;
            }

            var model = environmentManaged ? options.Model : local!.Model;
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException(
                    $"A model ID is required when the {definition.DisplayName} API key is configured.");
            }

            var baseUrl = options.BaseUrl.EndsWith('/')
                ? options.BaseUrl
                : options.BaseUrl + "/";
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                || baseUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"The {definition.DisplayName} base URL is invalid.");
            }

            providers.Add(new ResolvedModelProvider(
                definition.Id,
                definition.DisplayName,
                model,
                $"{definition.Id}:{model}",
                apiKey,
                baseUri));
        }

        return providers;
    }

    public async Task<ModelProviderStatus> SaveLocalAsync(
        string providerId,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (apiKey.Length > 4_096)
        {
            throw new ArgumentException(
                "The API key must contain at most 4,096 characters.",
                nameof(apiKey));
        }

        if (model.Length > 200)
        {
            throw new ArgumentException(
                "The model ID must contain at most 200 characters.",
                nameof(model));
        }

        var definition = GetDefinition(providerId);
        if (!string.IsNullOrWhiteSpace(definition.Options(_options).ApiKey))
        {
            throw new ModelProviderManagedException(providerId);
        }

        var credential = new ModelProviderCredentialRecord(
            providerId,
            _protector.Protect(apiKey),
            model,
            DateTimeOffset.UtcNow);
        await _store.SaveModelProviderCredentialAsync(credential, cancellationToken)
            .ConfigureAwait(false);
        return (await ListStatusesAsync(cancellationToken).ConfigureAwait(false))
            .Single(status => status.Id == providerId);
    }

    public async Task DeleteLocalAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        var definition = GetDefinition(providerId);
        if (!string.IsNullOrWhiteSpace(definition.Options(_options).ApiKey))
        {
            throw new ModelProviderManagedException(providerId);
        }

        await _store.DeleteModelProviderCredentialAsync(providerId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, ModelProviderCredentialRecord>>
        LocalCredentialsAsync(CancellationToken cancellationToken) =>
        (await _store.ListModelProviderCredentialsAsync(cancellationToken).ConfigureAwait(false))
        .ToDictionary(static credential => credential.ProviderId, StringComparer.Ordinal);

    private static ProviderDefinition GetDefinition(string providerId) =>
        Definitions.SingleOrDefault(definition =>
            string.Equals(definition.Id, providerId, StringComparison.Ordinal))
        ?? throw new ArgumentOutOfRangeException(
            nameof(providerId),
            providerId,
            "The model provider is not supported.");

    private bool TryUnprotect(
        ModelProviderCredentialRecord credential,
        out string apiKey)
    {
        try
        {
            apiKey = _protector.Unprotect(credential.ProtectedApiKey);
            return true;
        }
        catch (CryptographicException)
        {
            ModelProviderLog.CredentialUnreadable(_logger, credential.ProviderId);
            apiKey = string.Empty;
            return false;
        }
    }

    private sealed record ProviderDefinition(
        string Id,
        string DisplayName,
        Func<ModelProviderOptions, ModelEndpointOptions> Options);
}

public sealed class ModelProviderManagedException(string providerId)
    : InvalidOperationException(
        $"The '{providerId}' model provider is managed by environment configuration.");

internal static partial class ModelProviderLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "The protected credential for provider {ProviderId} could not be read.")]
    internal static partial void CredentialUnreadable(
        ILogger logger,
        string providerId);
}
