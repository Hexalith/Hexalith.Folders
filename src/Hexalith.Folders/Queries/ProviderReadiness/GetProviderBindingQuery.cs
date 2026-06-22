using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>
/// Query for a single redacted provider-binding metadata record by reference.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id from authenticated context.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the read action token.</param>
/// <param name="ProviderBindingRef">Opaque provider-binding reference being read.</param>
/// <param name="CorrelationId">Caller-supplied or generated correlation id.</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
public sealed record GetProviderBindingQuery(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string ProviderBindingRef,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues);
