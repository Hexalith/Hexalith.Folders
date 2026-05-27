namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextLimits(
    string QueryFamily,
    int ConfiguredLimit,
    int ActualCount,
    long ActualBytes,
    long ElapsedMilliseconds,
    bool IsTruncated,
    string TruncatedReason);
