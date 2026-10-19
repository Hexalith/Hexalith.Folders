using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Deterministic candidates produced without storage effects.</summary>
/// <param name="Checkpoint">The new source anchor.</param>
/// <param name="Lifecycle">The folder lifecycle.</param>
/// <param name="Workspaces">Every workspace touched by this slice.</param>
/// <param name="Tasks">Every task touched by this slice.</param>
/// <param name="RequiredViews">The original view scope at every source position in this slice.</param>
public sealed record OperationalStatusFoldResult(OperationalStatusCheckpoint Checkpoint,
    FolderLifecycleStatusReadModelSnapshot Lifecycle,
    IReadOnlyDictionary<string, OperationalWorkspaceStatus> Workspaces,
    IReadOnlyDictionary<string, TaskStatusReadModelSnapshot> Tasks,
    IReadOnlyDictionary<long, OperationalStatusViewBinding> RequiredViews);
