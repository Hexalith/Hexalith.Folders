using Octokit;
using Octokit.Internal;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClientFactory : IGitHubApiClientFactory
{
    private readonly Func<IHttpClient> _httpClientFactory;

    public OctokitGitHubApiClientFactory()
        : this(static () => new HttpClientAdapter(static () => new HttpClientHandler()))
    {
    }

    internal OctokitGitHubApiClientFactory(Func<IHttpClient> httpClientFactory)
        => _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

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

        return ValueTask.FromResult<IGitHubApiClient>(new OctokitGitHubApiClient(client));
    }
}
