using System.Net.Http.Headers;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoHttpApiClientFactory : IForgejoApiClientFactory
{
    internal const string HttpClientName = "Hexalith.Folders.Forgejo";
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };
    private readonly Func<HttpClient> _httpClientFactory;

    public ForgejoHttpApiClientFactory()
        : this(static () => new HttpClient(SharedHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(30),
        })
    {
    }

    internal ForgejoHttpApiClientFactory(IHttpClientFactory httpClientFactory)
        : this(() => httpClientFactory.CreateClient(HttpClientName))
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
    }

    internal ForgejoHttpApiClientFactory(Func<HttpClient> httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public ValueTask<IForgejoApiClient> CreateAsync(
        ForgejoApiClientRequest request,
        ForgejoCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        HttpClient client = _httpClientFactory();
        client.BaseAddress = request.BaseUri;
        ForgejoAuthorizationHeader authorization = ForgejoAuthorizationHeader.FromBearerToken(credential);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authorization.Scheme, authorization.Parameter);
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(request.ProductHeader);

        return ValueTask.FromResult<IForgejoApiClient>(new ForgejoHttpApiClient(client, request.BaseUri));
    }
}
