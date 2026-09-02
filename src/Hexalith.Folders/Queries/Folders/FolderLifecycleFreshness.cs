namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale,
    string? ReasonCode)
{
    public static FolderLifecycleFreshness SafeUnavailable(DateTimeOffset observedAt, string reasonCode)
        => new("eventually_consistent", observedAt, null, Stale: true, reasonCode);

    /// <summary>
    /// Marks the freshness unavailable and suppresses its projection watermark.
    /// </summary>
    /// <returns>Unavailable freshness preserving its read context and reason.</returns>
    internal FolderLifecycleFreshness ToUnavailable()
        => this with { ProjectionWatermark = null, Stale = true };

    /// <summary>
    /// Marks the freshness unavailable while retaining a specific inherited reason when present.
    /// </summary>
    /// <param name="fallbackReasonCode">The reason to use when no inherited reason is available.</param>
    /// <returns>Unavailable freshness with its projection watermark suppressed.</returns>
    internal FolderLifecycleFreshness ToUnavailableWithFallback(string fallbackReasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReasonCode);

        return ToUnavailable() with
        {
            ReasonCode = string.IsNullOrWhiteSpace(ReasonCode) ? fallbackReasonCode : ReasonCode,
        };
    }

    /// <summary>
    /// Marks the freshness unavailable using a reason determined by handler validation.
    /// </summary>
    /// <param name="handlerReasonCode">The handler-determined reason.</param>
    /// <returns>Unavailable freshness with its projection watermark suppressed.</returns>
    internal FolderLifecycleFreshness ToUnavailableForHandler(string handlerReasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerReasonCode);

        return ToUnavailable() with
        {
            ReasonCode = handlerReasonCode,
        };
    }
}
