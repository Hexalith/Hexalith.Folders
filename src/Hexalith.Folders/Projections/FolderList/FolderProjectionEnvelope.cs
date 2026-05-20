using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.FolderList;

public sealed record FolderProjectionEnvelope(
    string ManagedTenantId,
    long Sequence,
    IFolderEvent Event);
