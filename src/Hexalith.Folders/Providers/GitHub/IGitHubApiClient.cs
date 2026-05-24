namespace Hexalith.Folders.Providers.GitHub;

internal interface IGitHubApiClient
{
    Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default);
}

