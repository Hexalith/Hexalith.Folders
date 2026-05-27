namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceReadModelResult(
    ProviderSupportEvidenceReadModelStatus Status,
    IReadOnlyList<ProviderSupportEvidenceItem> Items,
    ProviderReadinessFreshness Freshness,
    string? NextCursor)
{
    public static ProviderSupportEvidenceReadModelResult Available(
        IReadOnlyList<ProviderSupportEvidenceItem> items,
        ProviderReadinessFreshness freshness,
        string? nextCursor)
        => new(ProviderSupportEvidenceReadModelStatus.Available, items, freshness, nextCursor);

    public static ProviderSupportEvidenceReadModelResult Malformed(ProviderReadinessFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(ProviderSupportEvidenceReadModelStatus.Malformed, [], freshness with { Stale = true }, null);
    }

    public static ProviderSupportEvidenceReadModelResult Stale(ProviderReadinessFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(ProviderSupportEvidenceReadModelStatus.Stale, [], freshness with { Stale = true }, null);
    }

    public static ProviderSupportEvidenceReadModelResult Unavailable(ProviderReadinessFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(ProviderSupportEvidenceReadModelStatus.Unavailable, [], freshness with { Stale = true }, null);
    }
}
