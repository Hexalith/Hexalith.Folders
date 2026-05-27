using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryTaskStatusReadModel(IUtcClock clock) : ITaskStatusReadModel
{
    private readonly ConcurrentDictionary<string, TaskStatusReadModelSnapshot> _snapshots = new(StringComparer.Ordinal);

    public void Save(TaskStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshots[Key(snapshot.ManagedTenantId, snapshot.TaskId)] = snapshot;
    }

    public Task<TaskStatusReadModelResult> GetAsync(
        TaskStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _snapshots.TryGetValue(Key(request.ManagedTenantId, request.TaskId), out TaskStatusReadModelSnapshot? snapshot)
                ? TaskStatusReadModelResult.Available(snapshot)
                : TaskStatusReadModelResult.NotFound(new FolderLifecycleFreshness(
                    request.ReadConsistency,
                    clock.UtcNow,
                    null,
                    Stale: false,
                    ReasonCode: null)));
    }

    private static string Key(string managedTenantId, string taskId)
        => $"{managedTenantId}|{taskId}";
}
