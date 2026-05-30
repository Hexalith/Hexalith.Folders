using Dapr;
using Dapr.Client;

namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class DaprProviderCredentialSecretStoreClient(DaprClient daprClient) : IProviderCredentialSecretStoreClient
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));

    public async ValueTask<ProviderCredentialSecretLookupResult> GetSecretAsync(
        string secretStoreName,
        string credentialReferenceId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialReferenceId);
        ArgumentNullException.ThrowIfNull(metadata);

        try
        {
            IReadOnlyDictionary<string, string> secret = await _daprClient.GetSecretAsync(
                secretStoreName,
                credentialReferenceId,
                metadata,
                cancellationToken).ConfigureAwait(false);

            return secret.Count == 0
                ? ProviderCredentialSecretLookupResult.Missing()
                : ProviderCredentialSecretLookupResult.Found(secret);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DaprApiException)
        {
            return ProviderCredentialSecretLookupResult.Unavailable(TimeSpan.FromSeconds(30));
        }
    }
}
