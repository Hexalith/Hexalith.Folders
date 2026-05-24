using System.Net.Http.Headers;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoHttpApiClientFactory : IForgejoApiClientFactory
{
    public ValueTask<IForgejoApiClient> CreateAsync(
        ForgejoApiClientRequest request,
        ForgejoCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,
        };

        HttpClient client = new(handler, disposeHandler: true)
        {
            BaseAddress = request.BaseUri,
        };

        ForgejoAuthorizationHeader authorization = ForgejoAuthorizationHeader.FromBearerToken(credential);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authorization.Scheme, authorization.Parameter);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(request.ProductHeader);

        return ValueTask.FromResult<IForgejoApiClient>(new ForgejoHttpApiClient(client, request.BaseUri));
    }
}
