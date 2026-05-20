using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server.Authorization;

public interface IFolderArchiveAclEvidenceProvider
{
    Task<FolderArchiveAclEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default);
}
