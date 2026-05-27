namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceAcceptedCommandState(
    string TaskId,
    string OperationId,
    string State,
    DateTimeOffset AcceptedAt);
