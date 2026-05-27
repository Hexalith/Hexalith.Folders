namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceReadModelRequest(
    string ManagedTenantId,
    string PrincipalId,
    string ActionToken,
    string? Cursor,
    int Limit,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency,
    DateTimeOffset RequestedAt)
{
    public ProviderReadinessFreshness EmptyFreshness()
        => new(ReadConsistency, RequestedAt, AuthorizationWatermark, Stale: false);
}
