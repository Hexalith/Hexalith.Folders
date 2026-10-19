using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusQuery(
    string TaskId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    string? FolderId = null,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues = null);
