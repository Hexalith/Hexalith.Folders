namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceProjectionLag(
    long? AgeMilliseconds,
    string StateSource);
