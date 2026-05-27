using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextQuery(
    WorkspaceFileContextQueryKind Kind,
    string? FolderId,
    string? WorkspaceId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues,
    IReadOnlyList<PathMetadata>? Paths = null,
    string? QueryText = null,
    string? GlobPattern = null,
    int? Limit = null,
    string? Cursor = null,
    long? StartOffset = null,
    long? EndOffset = null);
