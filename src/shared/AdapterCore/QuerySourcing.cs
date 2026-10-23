namespace Hexalith.Folders.AdapterCore;

/// <summary>Resolved sourcing values for a query SDK invocation.</summary>
/// <param name="CorrelationId">The per-invocation correlation ID.</param>
/// <param name="TaskId">The task ID, when the operation requires or accepts it.</param>
internal readonly record struct QuerySourcing(string CorrelationId, string? TaskId);
