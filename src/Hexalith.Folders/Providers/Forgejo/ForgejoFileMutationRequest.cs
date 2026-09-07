using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoFileMutationRequest(
    ProviderGitOperationResolvedTarget Target,
    IReadOnlyList<ProviderResolvedFileChange> Changes,
    string SupportedSnapshotVersion,
    Func<CancellationToken, ValueTask<bool>> ValidateReservationAsync)
{
    public override string ToString() => nameof(ForgejoFileMutationRequest);
}
