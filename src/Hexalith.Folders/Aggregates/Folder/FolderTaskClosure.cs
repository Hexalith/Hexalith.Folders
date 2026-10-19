namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Source-folded evidence that a task finished work on a workspace binding.</summary>
/// <param name="WorkspaceId">The workspace binding.</param>
/// <param name="TaskId">The task that completed.</param>
/// <param name="TerminalState">The original terminal result.</param>
/// <param name="OperationId">The completing operation.</param>
/// <param name="CorrelationId">The completing correlation.</param>
/// <param name="ClosedAt">The source event time.</param>
public sealed record FolderTaskClosure(string WorkspaceId, string TaskId, string TerminalState,
    string OperationId, string CorrelationId, DateTimeOffset ClosedAt);
