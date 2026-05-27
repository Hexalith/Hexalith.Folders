namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextRange(
    long StartOffset,
    long EndOffset,
    long ActualBytes,
    bool Partial);
