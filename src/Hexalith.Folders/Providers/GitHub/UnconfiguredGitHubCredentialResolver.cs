using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class UnconfiguredGitHubCredentialResolver : IGitHubCredentialResolver
{
    public ValueTask<GitHubCredentialResolutionResult> ResolveAsync(
        GitHubCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GitHubCredentialResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "github_credential_resolver_unconfigured"));
    }
}

