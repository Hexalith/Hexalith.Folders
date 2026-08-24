namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries current, metadata-safe evidence required before a provider operation may resolve protected input.
/// </summary>
/// <param name="AuthorizedManagedTenantId">The exact managed tenant authorized by this evidence.</param>
/// <param name="AuthorizedFolderId">The exact folder authorized by this evidence.</param>
/// <param name="AuthorizedOrganizationId">The exact organization authorized by this evidence.</param>
/// <param name="AuthorizedCredentialReferenceId">The exact credential reference authorized by this evidence.</param>
/// <param name="AuthorizedRepositoryBindingId">The exact canonical binding authorized by this evidence.</param>
/// <param name="DelegatedTaskFingerprint">The delegated-task authorization fingerprint.</param>
/// <param name="RepositoryBindingFingerprint">The canonical repository-binding fingerprint.</param>
/// <param name="RefPolicyFingerprint">The exact ref-policy fingerprint.</param>
/// <param name="CanonicalLockFingerprint">The active canonical-lock fingerprint.</param>
/// <param name="ExpectedHeadFingerprint">The safe fingerprint of the expected provider head.</param>
/// <param name="CapturedAt">When the evidence was captured.</param>
/// <param name="FreshnessClass">The canonical freshness class.</param>
public sealed record ProviderOperationEvidenceSnapshot(
    string AuthorizedManagedTenantId,
    string AuthorizedFolderId,
    string AuthorizedOrganizationId,
    string AuthorizedCredentialReferenceId,
    string AuthorizedRepositoryBindingId,
    string DelegatedTaskFingerprint,
    string RepositoryBindingFingerprint,
    string RefPolicyFingerprint,
    string CanonicalLockFingerprint,
    string ExpectedHeadFingerprint,
    DateTimeOffset CapturedAt,
    string FreshnessClass);
