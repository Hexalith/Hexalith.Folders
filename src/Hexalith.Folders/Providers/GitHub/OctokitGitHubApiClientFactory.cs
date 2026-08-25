using Octokit;
using Octokit.Internal;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClientFactory : IGitHubApiClientFactory
{
    private readonly Func<IHttpClient> _httpClientFactory;
    private readonly Func<HttpMessageHandler> _operationHandlerFactory;

    public OctokitGitHubApiClientFactory()
        : this(
            static () => new HttpClientAdapter(static () => new HttpClientHandler()),
            static () => new HttpClientHandler())
    {
    }

    internal OctokitGitHubApiClientFactory(Func<IHttpClient> httpClientFactory)
        : this(httpClientFactory, static () => new HttpClientHandler())
    {
    }

    internal OctokitGitHubApiClientFactory(
        Func<IHttpClient> httpClientFactory,
        Func<HttpMessageHandler> operationHandlerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _operationHandlerFactory = operationHandlerFactory ?? throw new ArgumentNullException(nameof(operationHandlerFactory));
    }

    public ValueTask<IGitHubApiClient> CreateAsync(
        GitHubApiClientRequest request,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        IHttpClient versionedHttpClient = new GitHubApiVersionHttpClient(_httpClientFactory(), request.ApiVersion);
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
