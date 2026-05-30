using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class DaprProviderCredentialReferenceResolver(
    IProviderCredentialSecretStoreClient secretStoreClient,
    IOptions<FoldersProviderCredentialOptions> options) : IProviderCredentialReferenceResolver
{
    private readonly IProviderCredentialSecretStoreClient _secretStoreClient =
        secretStoreClient ?? throw new ArgumentNullException(nameof(secretStoreClient));
    private readonly FoldersProviderCredentialOptions _options = options.Value;

    public async ValueTask<ProviderCredentialReferenceResolutionResult> ResolveAsync(
        ProviderCredentialReferenceResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.CredentialReferenceId))
        {
            return ProviderCredentialReferenceResolutionResult.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_credential_reference_missing");
        }

        ProviderCredentialSecretLookupResult lookup = await _secretStoreClient.GetSecretAsync(
            _options.SecretStoreName,
            request.CredentialReferenceId,
            BuildMetadata(request),
            cancellationToken).ConfigureAwait(false);

        return lookup.Status switch
        {
            ProviderCredentialSecretLookupStatus.Found => MapFound(lookup.Values),
            ProviderCredentialSecretLookupStatus.Missing => ProviderCredentialReferenceResolutionResult.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_credential_reference_missing"),
            ProviderCredentialSecretLookupStatus.Denied => ProviderCredentialReferenceResolutionResult.Failure(
                ProviderFailureCategory.ProviderPermissionInsufficient,
                "provider_credential_reference_denied"),
            _ => ProviderCredentialReferenceResolutionResult.Failure(
                ProviderFailureCategory.ProviderUnavailable,
                "provider_credential_store_unavailable",
                lookup.RetryAfter),
        };
    }

    private ProviderCredentialReferenceResolutionResult MapFound(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count != 1
            || !values.TryGetValue(_options.AccessTokenKey, out string? accessToken)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return ProviderCredentialReferenceResolutionResult.Failure(
                ProviderFailureCategory.ProviderAuthenticationRequired,
                "provider_credential_secret_malformed");
        }

        return ProviderCredentialReferenceResolutionResult.Success(accessToken);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(ProviderCredentialReferenceResolutionRequest request)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider_family"] = request.ProviderFamily,
            ["provider_key"] = request.ProviderKey,
            ["credential_mode"] = request.CredentialMode.ToString(),
            ["correlation_id"] = string.IsNullOrWhiteSpace(request.CorrelationId) ? "correlation_unavailable" : request.CorrelationId,
        };
}
