namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusReadModelRequest(
    string ManagedTenantId,
    string TaskId,
    string PrincipalId,
    string ActionToken,
    string? CorrelationId,
    string ReadConsistency)
{
    /// <summary>Gets the folder scope established by the caller's current layered authorization.</summary>
    public string? AuthorizedFolderId { get; init; }

    /// <summary>Gets the fresh authorization watermark established before protected task lookup.</summary>
    public string? AuthorizationWatermark { get; init; }
}
