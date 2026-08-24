namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Requests ordered Git-object staging without moving the selected provider ref.
/// </summary>
public sealed record ProviderFileChangeSetRequest(
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
    string ChangeSetReference,
    ProviderIdempotencyAdmission IdempotencyAdmission,
    IReadOnlyList<ProviderFileChange> Changes);
