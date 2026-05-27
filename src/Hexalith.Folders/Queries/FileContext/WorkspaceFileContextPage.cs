namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextPage(
    string? Cursor,
    int Limit,
    bool IsTruncated,
    string? TruncatedReason);
