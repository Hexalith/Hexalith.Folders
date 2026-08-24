namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Requests one bounded read-only check of an exact provider mutation outcome.
/// </summary>
public sealed record ProviderMutationStatusRequest(
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
    string OperationReference,
    string ReconciliationReference,
    string SafeExpectedCommitFingerprint,
    DateTimeOffset UnknownOutcomeObservedAt,
    DateTimeOffset RequestedAt,
    int CheckNumber,
    string? IdempotencyKey = null);
