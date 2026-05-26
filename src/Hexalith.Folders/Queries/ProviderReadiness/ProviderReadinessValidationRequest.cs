using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessValidationRequest(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    string? ProviderBindingRef,
    ProviderReadinessRequestedCapability RequestedCapability,
    string? CorrelationId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues);
