namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingProjectionFreshness(
    long Watermark,
    string LastEventType,
    string LastEventFingerprint,
    DateTimeOffset ObservedAt)
{
    public SemanticIndexingProjectionFreshness()
        : this(0, "unknown", "unknown", DateTimeOffset.UnixEpoch)
    {
    }
}
