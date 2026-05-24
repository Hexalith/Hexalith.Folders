namespace Hexalith.Folders.Providers.GitHub;

internal interface IGitHubCredentialResolver
{
    ValueTask<GitHubCredentialResolutionResult> ResolveAsync(
        GitHubCredentialResolutionRequest request,
        CancellationToken cancellationToken = default);
}

