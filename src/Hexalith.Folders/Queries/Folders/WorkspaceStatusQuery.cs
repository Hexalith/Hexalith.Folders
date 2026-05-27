using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusQuery(
    string FolderId,
    string WorkspaceId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
