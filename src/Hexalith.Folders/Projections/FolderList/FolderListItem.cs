using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.FolderList;

public sealed record FolderListItem(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState LifecycleState,
    FolderRepositoryBindingState RepositoryBindingState,
    long Sequence);
