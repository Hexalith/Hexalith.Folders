using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClientFactory : IGitHubApiClientFactory
{
    public ValueTask<IGitHubApiClient> CreateAsync(
        GitHubApiClientRequest request,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        GitHubClient client = new(new ProductHeaderValue(request.ProductHeader))
        {
            Credentials = new Credentials(credential.AccessToken),
        };

        return ValueTask.FromResult<IGitHubApiClient>(new OctokitGitHubApiClient(client));
    }
}

