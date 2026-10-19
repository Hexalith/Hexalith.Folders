using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessValidationRequest(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    string? ProviderBindingRef,
    ProviderReadinessRequestedCapability RequestedCapability,
    string? CorrelationId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues)
{
    /// <summary>The folder whose source fold established the repository binding.</summary>
    internal string? BoundFolderId { get; init; }
    /// <summary>The repository binding from that authorized source fold.</summary>
    internal string? BoundRepositoryBindingId { get; init; }
    /// <summary>The source-owned external target fingerprint.</summary>
    internal string? BoundExternalRepositoryRefFingerprint { get; init; }
    /// <summary>The source-owned branch policy reference.</summary>
    internal string? BoundBranchRefPolicyRef { get; init; }
}
