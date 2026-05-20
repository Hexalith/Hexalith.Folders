using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server.Authorization;

public interface IFolderArchivePolicyEvidenceProvider
{
    Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default);
}
