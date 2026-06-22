using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>
/// Request to configure an organization provider binding. Tenant and organization are resolved from
/// authorization evidence; the caller supplies the binding reference and non-secret configuration.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the configure action token.</param>
/// <param name="ProviderBindingRef">Opaque provider-binding reference.</param>
/// <param name="ProviderKind">Provider kind (provider family) being bound.</param>
/// <param name="CredentialReferenceId">Non-secret credential reference id.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="IdempotencyKey">Idempotency key.</param>
/// <param name="PayloadTenantId">Optional client-asserted payload tenant id (comparison input only).</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
/// <param name="ClientControlledPrincipalValues">Client-asserted principal signals (comparison inputs only).</param>
public sealed record ConfigureProviderBindingServiceRequest(
    string? AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string ProviderBindingRef,
    string ProviderKind,
    string CredentialReferenceId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
