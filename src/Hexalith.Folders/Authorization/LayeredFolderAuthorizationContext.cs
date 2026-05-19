namespace Hexalith.Folders.Authorization;

public sealed record LayeredFolderAuthorizationContext(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    string? ActorSafeIdentifier,
    string ActionToken,
    LayeredFolderOperationPolicy OperationPolicy,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? OperationScope,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
