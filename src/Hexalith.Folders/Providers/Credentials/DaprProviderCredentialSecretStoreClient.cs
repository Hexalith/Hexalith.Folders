using Dapr;
using Dapr.Client;

using Grpc.Core;

using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Credentials;

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
        catch (DaprApiException ex) when (IsPermissionDenied(ex))
        {
            // A Dapr secret-scope denial (defaultAccess: deny / not in allowedSecrets) surfaces as a
            // gRPC PermissionDenied status. Report it as a distinct denied outcome so the resolver maps
            // it to ProviderPermissionInsufficient instead of a transient/retryable unavailable error.
            return ProviderCredentialSecretLookupResult.Denied();
        }
        catch (DaprApiException)
        {
            return ProviderCredentialSecretLookupResult.Unavailable(TimeSpan.FromSeconds(30));
        }
    }

    private static bool IsPermissionDenied(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is RpcException { StatusCode: StatusCode.PermissionDenied })
            {
                return true;
            }
        }

        return false;
    }
}
