namespace Hexalith.Folders.Providers.GitHub;

internal interface IGitHubApiClientFactory
{
    ValueTask<IGitHubApiClient> CreateAsync(
        GitHubApiClientRequest request,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken = default);
}

