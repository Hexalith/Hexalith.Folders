namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Requests one explicit commit and at most one non-force update of the selected ref.
/// </summary>
public sealed record ProviderCommitRequest(
    string ManagedTenantId,
    string FolderId,
    string OrganizationId,
    string ProviderBindingRef,
    string CredentialReferenceId,
    string RepositoryBindingId,
    string ProviderFamily,
    string ProviderKey,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence,
    ProviderOperationEvidenceSnapshot OperationEvidence,
    string CorrelationId,
    string IdempotencyKey,
    string StagedChangeSetReference,
    string SafeStagedChangeSetFingerprint,
    string CommitMessageReference,
    ProviderIdempotencyAdmission IdempotencyAdmission);
