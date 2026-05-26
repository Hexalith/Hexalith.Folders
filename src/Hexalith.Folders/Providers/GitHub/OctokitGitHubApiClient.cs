using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClient : IGitHubApiClient
{
    public OctokitGitHubApiClient(GitHubClient client)
        => ArgumentNullException.ThrowIfNull(client);

    public Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The live Octokit readiness probe is intentionally deferred to the provider
        // contract / live-nightly drift path (AC 12). Fail loudly here so the
        // unimplemented seam cannot masquerade as a runtime transport failure that
        // would otherwise be mapped to unknown_provider_outcome / reconciliation.
        throw new NotImplementedException(
            "Live GitHub readiness probing is deferred to the provider contract/live-nightly path; "
            + "supply an IGitHubApiClient seam for offline scenarios.");
    }

    public Task<GitHubRepositoryCreationResult> CreateRepositoryAsync(
        GitHubRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The live repository creation call is intentionally deferred to a
        // provider-owned runtime slice. Story 3.6 exercises the provider port
        // through fakeable seams so PR validation remains hermetic.
        throw new NotImplementedException(
            "Live GitHub repository creation is deferred to the provider runtime path; "
            + "supply an IGitHubApiClient seam for offline scenarios.");
    }
}
