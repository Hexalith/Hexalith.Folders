using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingProviderRepositoryTargetResolver(
    ProviderRepositoryTargetResolutionResult result) : IProviderRepositoryTargetResolver
{
    public int CreationCalls { get; private set; }

    public int BindingCalls { get; private set; }

    public ProviderRepositoryCreationTargetResolutionRequest? LastCreationRequest { get; private set; }

    public ProviderRepositoryBindingTargetResolutionRequest? LastBindingRequest { get; private set; }

    public static RecordingProviderRepositoryTargetResolver Success(
        ProviderRepositoryResolvedTarget? target = null)
        => new(ProviderRepositoryTargetResolutionResult.Success(target ?? DefaultTarget()));

    public static RecordingProviderRepositoryTargetResolver Failure(
        ProviderFailureCategory category,
        string reasonCode)
        => new(ProviderRepositoryTargetResolutionResult.Failure(category, reasonCode));

    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
        ProviderRepositoryCreationTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreationCalls++;
        LastCreationRequest = request;
        return ValueTask.FromResult(result);
    }

    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
        ProviderRepositoryBindingTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BindingCalls++;
        LastBindingRequest = request;
        return ValueTask.FromResult(result);
    }

    private static ProviderRepositoryResolvedTarget DefaultTarget()
        => new(
            Owner: "octokit-owner-sentinel",
            RepositoryName: "octokit-repository-sentinel",
            Visibility: ProviderRepositoryVisibility.Private,
            DefaultBranch: "octokit-default-branch-sentinel",
            SelectedRef: "octokit-selected-ref-sentinel",
            RequireProtectedRef: true,
            RequireContentsPermission: true,
            RequireAdministrationPermission: true,
            ExpectedCanonicalRepositoryId: null,
            EquivalentExistingAuthorized: false);
}
