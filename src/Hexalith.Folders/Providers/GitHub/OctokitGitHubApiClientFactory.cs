using Microsoft.Extensions.Options;

using Octokit;
using Octokit.Internal;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClientFactory : IGitHubApiClientFactory
{
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    };
    private readonly Func<HttpMessageHandler> _octokitHandlerFactory;
    private readonly Func<HttpMessageHandler> _operationHandlerFactory;
    private readonly TimeSpan _requestTimeout;

    internal OctokitGitHubApiClientFactory()
        : this(Options.Create(new GitHubTransportOptions()))
    {
    }

    public OctokitGitHubApiClientFactory(IOptions<GitHubTransportOptions> options)
        : this(
            static () => new PooledGitHubHttpMessageHandler(SharedHandler),
            static () => new PooledGitHubHttpMessageHandler(SharedHandler),
            (options ?? throw new ArgumentNullException(nameof(options))).Value.RequestTimeout)
    {
    }

    internal OctokitGitHubApiClientFactory(
        Func<HttpMessageHandler> octokitHandlerFactory,
        Func<HttpMessageHandler> operationHandlerFactory,
        TimeSpan? requestTimeout = null)
    {
        _octokitHandlerFactory = octokitHandlerFactory ?? throw new ArgumentNullException(nameof(octokitHandlerFactory));
        _operationHandlerFactory = operationHandlerFactory ?? throw new ArgumentNullException(nameof(operationHandlerFactory));
        _requestTimeout = requestTimeout ?? GitHubTransportOptions.DefaultRequestTimeout;
    }

    public ValueTask<IGitHubApiClient> CreateAsync(
        GitHubApiClientRequest request,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        IHttpClient transport = new HttpClientAdapter(() => new GitHubResponseBufferingHandler(_octokitHandlerFactory()));
        IHttpClient versionedHttpClient = new GitHubApiVersionHttpClient(transport, request.ApiVersion);
        versionedHttpClient.SetRequestTimeout(_requestTimeout);
        Connection connection = new(new ProductHeaderValue(request.ProductHeader), versionedHttpClient);
        GitHubClient client = new(connection)
        {
            Credentials = new Octokit.Credentials(credential.AccessToken),
        };

        return ValueTask.FromResult<IGitHubApiClient>(new OctokitGitHubApiClient(
            client,
            _operationHandlerFactory,
            credential.AccessToken,
            request.ProductHeader,
            request.ApiVersion));
    }
}
