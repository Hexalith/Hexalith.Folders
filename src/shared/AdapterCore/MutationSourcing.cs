namespace Hexalith.Folders.AdapterCore;

/// <summary>Resolved sourcing values for a mutating SDK invocation.</summary>
/// <param name="IdempotencyKey">The caller-sourced or explicitly opted-in generated key.</param>
/// <param name="CorrelationId">The per-invocation correlation ID.</param>
/// <param name="TaskId">The caller-provided task ID.</param>
internal readonly record struct MutationSourcing(string IdempotencyKey, string CorrelationId, string TaskId);
