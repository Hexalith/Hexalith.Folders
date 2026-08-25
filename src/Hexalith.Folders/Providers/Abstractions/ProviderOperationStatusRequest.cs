namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Requests exactly one authorized read-only provider outcome observation.
/// </summary>
/// <param name="ManagedTenantId">The managed tenant identifier.</param>
/// <param name="OrganizationId">The organization identifier.</param>
/// <param name="FolderId">The folder identifier.</param>
/// <param name="DelegatedTaskId">The delegated task identifier that owns the lock.</param>
/// <param name="ProviderBindingRef">The opaque provider binding reference.</param>
/// <param name="CredentialReferenceId">The opaque credential reference.</param>
/// <param name="RepositoryBindingId">The canonical repository binding identifier.</param>
/// <param name="ProviderFamily">The provider family.</param>
/// <param name="ProviderKey">The provider key.</param>
/// <param name="TargetEvidence">The safe target evidence.</param>
/// <param name="CredentialModeRequirements">The authorized credential modes.</param>
/// <param name="AuthorizationEvidence">The current authorization evidence.</param>
/// <param name="LockEvidence">The canonical lock evidence.</param>
/// <param name="RefPolicyEvidence">The ref-policy evidence.</param>
/// <param name="OperationReference">The opaque operation reference.</param>
/// <param name="SafeResolvedTargetFingerprint">The binding for the private repository target.</param>
/// <param name="SafeFullRefFingerprint">The binding for the exact full ref.</param>
/// <param name="SafeExpectedHeadFingerprint">The safe pre-operation head fingerprint.</param>
/// <param name="SafeIntendedCommitFingerprint">The safe intended commit fingerprint.</param>
/// <param name="SafeCheckWindowFingerprint">The operation-bound check number and window binding.</param>
/// <param name="CheckNumber">The authoritative one-based reconciliation check number.</param>
/// <param name="ReconciliationStartedAt">When the durable reconciliation window started.</param>
/// <param name="RequestedAt">The authoritative time for this check.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="IdempotencyKey">Must be null because status is read-only.</param>
public sealed record ProviderOperationStatusRequest(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DelegatedTaskId,
    string ProviderBindingRef,
    string CredentialReferenceId,
    string RepositoryBindingId,
    string ProviderFamily,
    string ProviderKey,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence,
    ProviderOperationLockEvidence LockEvidence,
    ProviderRefPolicyEvidence RefPolicyEvidence,
    string OperationReference,
    string SafeResolvedTargetFingerprint,
    string SafeFullRefFingerprint,
    string SafeExpectedHeadFingerprint,
    string SafeIntendedCommitFingerprint,
    string SafeCheckWindowFingerprint,
    int CheckNumber,
    DateTimeOffset ReconciliationStartedAt,
    DateTimeOffset RequestedAt,
    string CorrelationId,
    string? IdempotencyKey = null)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderOperationStatusRequest);
}
