using System.Collections.Frozen;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.FolderAccess;

public sealed record FolderAccessProjection(
    string ManagedTenantId,
    string FolderId,
    long Watermark,
    IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> Overrides)
{
    public static FolderAccessProjection Empty(string managedTenantId, string folderId)
        => new(
            managedTenantId,
            folderId,
            0,
            FrozenDictionary<FolderAccessEntryKey, FolderAccessOverride>.Empty);

    public static FolderAccessProjection FromEvents(
        string managedTenantId,
        string folderId,
        IEnumerable<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        FolderStreamName streamName = FolderStreamName.Create(managedTenantId, folderId);
        FolderState state = FolderState.Empty.Apply(events, streamName);
        return new FolderAccessProjection(
            managedTenantId,
            folderId,
            state.AccessSequence,
            state.AccessOverrides);
    }
}
