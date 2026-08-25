namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Requests ordered Git file staging after all authoritative decisions have been made durably.
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
/// <param name="FilePolicyEvidence">The file-policy evidence.</param>
/// <param name="SafeResolvedTargetFingerprint">The binding for the private repository, full ref, and expected head.</param>
/// <param name="ChangeSetReference">The opaque ordered change-set reference.</param>
/// <param name="SafeChangeSetFingerprint">The safe ordered change-set fingerprint.</param>
/// <param name="Changes">The ordered opaque changes.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="IdempotencyKey">The durable idempotency key.</param>
/// <param name="IdempotencyAdmission">The durable admission decision.</param>
public sealed record ProviderFileMutationRequest(
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
    ProviderFilePolicyEvidence FilePolicyEvidence,
    string SafeResolvedTargetFingerprint,
    string ChangeSetReference,
    string SafeChangeSetFingerprint,
    IReadOnlyList<ProviderOrderedFileChange> Changes,
    string CorrelationId,
    string IdempotencyKey,
    ProviderIdempotencyAdmission IdempotencyAdmission)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderFileMutationRequest);
}
