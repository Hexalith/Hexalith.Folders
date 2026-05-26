namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale);
