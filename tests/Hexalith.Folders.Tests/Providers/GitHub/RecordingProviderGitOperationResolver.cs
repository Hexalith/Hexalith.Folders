using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingProviderGitOperationResolver(
    ProviderGitChangeSetResolutionResult changeSetResult,
    ProviderGitCommitResolutionResult commitResult,
    ProviderGitStatusResolutionResult statusResult) : IProviderGitOperationResolver
{
    private ProviderGitCommitResolutionResult CommitResult => commitResult;

    private ProviderGitStatusResolutionResult StatusResult => statusResult;

    public int ChangeSetCalls { get; private set; }

    public int CommitCalls { get; private set; }

    public int StatusCalls { get; private set; }

    public static RecordingProviderGitOperationResolver Success(
        string expectedHeadSha = "1111111111111111111111111111111111111111",
        string stagedTreeSha = "2222222222222222222222222222222222222222",
        string expectedCommitSha = "3333333333333333333333333333333333333333",
        DateTimeOffset? authoritativeUnknownOutcomeObservedAt = null,
        DateTimeOffset? authoritativeRequestedAt = null,
        int authoritativeCheckNumber = 1)
    {
        ProviderRepositoryResolvedTarget target = new(
            Owner: "provider-owner-sentinel",
            RepositoryName: "provider-repository-sentinel",
            Visibility: ProviderRepositoryVisibility.Private,
            DefaultBranch: "main",
            SelectedRef: "main",
            RequireProtectedRef: false,
            RequireContentsPermission: true,
            RequireAdministrationPermission: false,
            ExpectedCanonicalRepositoryId: "101",
            EquivalentExistingAuthorized: false);
        return new(
            ProviderGitChangeSetResolutionResult.Success(new ProviderGitChangeSetResolvedInput(
                target,
                expectedHeadSha,
                [
                    new ProviderGitResolvedFileChange("change-a", "src/a.txt", ProviderFileChangeKind.Add, "alpha"u8.ToArray()),
                    new ProviderGitResolvedFileChange("change-b", "src/b.txt", ProviderFileChangeKind.Remove, null),
                ])),
            ProviderGitCommitResolutionResult.Success(new ProviderGitCommitResolvedInput(
                target,
                expectedHeadSha,
                stagedTreeSha,
                "provider commit message sentinel")),
            ProviderGitStatusResolutionResult.Success(new ProviderGitStatusResolvedInput(
                target,
                expectedHeadSha,
                expectedCommitSha,
                authoritativeUnknownOutcomeObservedAt ?? DateTimeOffset.Parse("2026-08-24T10:00:00+00:00"),
                authoritativeRequestedAt ?? DateTimeOffset.Parse("2026-08-24T10:05:00+00:00"),
                authoritativeCheckNumber)));
    }

    public static RecordingProviderGitOperationResolver ChangeSetFailure(
        ProviderFailureCategory category,
        string reasonCode)
    {
        RecordingProviderGitOperationResolver successful = Success();
        return new(
            ProviderGitChangeSetResolutionResult.Failure(category, reasonCode),
            successful.CommitResult,
            successful.StatusResult);
    }

    public ValueTask<ProviderGitChangeSetResolutionResult> ResolveChangeSetAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChangeSetCalls++;
        return ValueTask.FromResult(changeSetResult);
    }

    public ValueTask<ProviderGitCommitResolutionResult> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommitCalls++;
        return ValueTask.FromResult(commitResult);
    }

    public ValueTask<ProviderGitStatusResolutionResult> ResolveStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StatusCalls++;
        return ValueTask.FromResult(statusResult);
    }
}
