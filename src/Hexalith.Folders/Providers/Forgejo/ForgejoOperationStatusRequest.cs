using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoOperationStatusRequest(
    ProviderGitOperationResolvedTarget Target,
    string? IntendedCommitSha,
    IReadOnlyList<ProviderResolvedFileChange> Changes,
    string CommitMessage,
    string SupportedSnapshotVersion)
{
    public override string ToString() => nameof(ForgejoOperationStatusRequest);
}
