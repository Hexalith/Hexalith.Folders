namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidencePage(
    string? Cursor,
    int Limit,
    bool IsTruncated,
    string? TruncatedReason);
