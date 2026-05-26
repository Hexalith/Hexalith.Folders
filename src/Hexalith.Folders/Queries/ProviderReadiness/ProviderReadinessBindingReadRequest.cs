namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessBindingReadRequest(
    string ManagedTenantId,
    string ProviderBindingRef,
    string CorrelationId);
