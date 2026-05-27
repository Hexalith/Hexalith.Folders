using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record BranchRefPolicyConfigurationRequest(
    string? AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string RequestSchemaVersion,
    string RepositoryBindingId,
    string PolicyRef,
    string DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string>? ProtectedRefPatterns,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
