using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderState(
    bool IsCreated,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState? LifecycleState,
    FolderRepositoryBindingState? RepositoryBindingState,
    IReadOnlyDictionary<string, string> IdempotencyFingerprints)
{
    public static FolderState Empty { get; } = new(
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        null,
        null,
        FrozenDictionary<string, string>.Empty);

    public FolderState Apply(IEnumerable<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        FolderState state = this;
        foreach (IFolderEvent folderEvent in events)
        {
            state = FolderStateApply.Apply(state, folderEvent);
        }

        return state;
    }
}
