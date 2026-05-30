namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderCredentialSecretStoreClient
{
    ValueTask<ProviderCredentialSecretLookupResult> GetSecretAsync(
        string secretStoreName,
        string credentialReferenceId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default);
}
