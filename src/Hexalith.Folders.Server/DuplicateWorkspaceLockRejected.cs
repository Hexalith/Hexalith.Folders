using System.Diagnostics;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Folders.Server;

public sealed record DuplicateWorkspaceLockRejected(
    string Code,
    string CommandType,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey) : IRejectionEvent
{
    public static DuplicateWorkspaceLockRejected Create(
        string commandType,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        string? canonicalCorrelationId = FolderCommandRejected.CanonicalIdentifierOrNull(correlationId);
        string? canonicalTaskId = FolderCommandRejected.CanonicalIdentifierOrNull(taskId);
        string? canonicalIdempotencyKey = FolderCommandRejected.CanonicalIdentifierOrNull(idempotencyKey);

        if (!string.IsNullOrWhiteSpace(correlationId) && canonicalCorrelationId is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.correlation_id_dropped", true);
        }

        if (!string.IsNullOrWhiteSpace(taskId) && canonicalTaskId is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.task_id_dropped", true);
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && canonicalIdempotencyKey is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.idempotency_key_dropped", true);
        }

        return new(
            nameof(Aggregates.Folder.FolderResultCode.LockConflict),
            FolderCommandRejected.NormalizeCommandTypeForRejection(commandType),
            canonicalCorrelationId,
            canonicalTaskId,
            canonicalIdempotencyKey);
    }
}
