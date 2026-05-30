using System.Collections.Concurrent;

using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.LoadTests.Scenarios;

public sealed class LifecycleCapacityRunRecorder
{
    private readonly ConcurrentDictionary<string, byte> _tenants = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _folders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _workspaces = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _tasks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _operations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _idempotencyKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _observedStepCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _resultCodes = new(StringComparer.Ordinal);

    public int TenantCount => _tenants.Count;

    public int FolderCount => _folders.Count;

    public int WorkspaceCount => _workspaces.Count;

    public int TaskCount => _tasks.Count;

    public int OperationCount => _operations.Count;

    public int IdempotencyKeyCount => _idempotencyKeys.Count;

    public IReadOnlyDictionary<string, int> ResultCodes
        => _resultCodes.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ObservedStepCounts
        => _observedStepCounts.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    public IReadOnlyList<string> MeasuredSteps
        => _observedStepCounts.Keys.Order(StringComparer.Ordinal).ToArray();

    public void RecordIteration(LifecycleCapacityIteration iteration)
    {
        ArgumentNullException.ThrowIfNull(iteration);
        _tenants.TryAdd(iteration.TenantId, 0);
        _folders.TryAdd($"{iteration.TenantId}:{iteration.FolderId}", 0);
        _workspaces.TryAdd($"{iteration.TenantId}:{iteration.FolderId}:{iteration.WorkspaceId}", 0);
        _tasks.TryAdd($"{iteration.TenantId}:{iteration.FolderId}:{iteration.WorkspaceId}:{iteration.TaskId}", 0);
    }

    public void RecordOperation(LifecycleCapacityIteration iteration, string operationId, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(iteration);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        _operations.TryAdd($"{iteration.TenantId}:{iteration.FolderId}:{iteration.WorkspaceId}:{operationId}", 0);
        _idempotencyKeys.TryAdd($"{iteration.TenantId}:{iteration.FolderId}:{idempotencyKey}", 0);
    }

    public void RecordMeasuredStep(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        _observedStepCounts.AddOrUpdate(stepName, 1, static (_, count) => count + 1);
    }

    public void RecordResult(FolderResultCode code)
        => RecordResult(code.ToString());

    public void RecordResult(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        _resultCodes.AddOrUpdate(code, 1, static (_, count) => count + 1);
    }
}
