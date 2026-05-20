using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Folders.Server;

public sealed record FolderCommandRejected(
    string Code,
    string CommandType,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? ActorPrincipalId,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey) : IRejectionEvent;
