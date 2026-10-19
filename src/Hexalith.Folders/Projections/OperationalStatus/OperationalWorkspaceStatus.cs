using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>The atomically published workspace status concerns.</summary>
/// <param name="SchemaVersion">The stored schema.</param>
/// <param name="Sequence">The source boundary.</param>
/// <param name="SourceDigest">The ordered source digest.</param>
/// <param name="Lock">Lock evidence.</param>
/// <param name="Workspace">Lifecycle and commit evidence.</param>
/// <param name="Cleanup">Cleanup visibility without invented cleanup execution.</param>
public sealed record OperationalWorkspaceStatus(int SchemaVersion, long Sequence, string SourceDigest,
    WorkspaceLockStatusReadModelSnapshot Lock, WorkspaceStatusReadModelSnapshot Workspace,
    WorkspaceCleanupStatusReadModelSnapshot Cleanup);
