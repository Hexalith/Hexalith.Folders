using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoCommitRequest(
    ProviderGitOperationResolvedTarget Target,
    IReadOnlyList<ProviderResolvedFileChange> Changes,
    string StagedTreeSha,
    string CommitMessage,
    string SupportedSnapshotVersion,
    Func<CancellationToken, ValueTask<ForgejoReservationValidationStatus>> ValidateReservationAsync,
    Func<string, ValueTask<bool>> RecordCreatedCommitAsync)
{
    public override string ToString() => nameof(ForgejoCommitRequest);
}
