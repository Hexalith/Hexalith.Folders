namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusReadModelRequest(
    string ManagedTenantId,
    string TaskId,
    string PrincipalId,
    string ActionToken,
    string? CorrelationId,
    string ReadConsistency);
