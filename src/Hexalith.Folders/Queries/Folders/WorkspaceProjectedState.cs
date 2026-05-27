namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceProjectedState(
    string State,
    string StateSource,
    DateTimeOffset ObservedAt);
