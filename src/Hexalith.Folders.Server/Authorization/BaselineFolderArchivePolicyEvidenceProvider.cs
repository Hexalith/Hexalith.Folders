using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server.Authorization;

public sealed class BaselineFolderArchivePolicyEvidenceProvider : IFolderArchivePolicyEvidenceProvider
{
    public const string PolicyVersion = "v1-baseline";

    public Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        FolderArchivePolicyEvidence evidence = FolderArchivePolicyEvidence.Allowed(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            PolicyVersion);

        return Task.FromResult(evidence);
    }
}
