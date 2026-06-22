namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Result of an ops-console diagnostics query. Metadata-only; <paramref name="Payload"/> is present only
/// when <paramref name="Code"/> is <see cref="DiagnosticReadResultCode.Allowed"/> (safe denial otherwise).
/// </summary>
/// <typeparam name="TPayload">The diagnostic view payload type.</typeparam>
/// <param name="Code">Outcome code.</param>
/// <param name="Payload">The diagnostic view, present only when allowed.</param>
/// <param name="CorrelationId">Safe correlation id echoed to the caller.</param>
public sealed record OpsConsoleDiagnosticReadResult<TPayload>(
    DiagnosticReadResultCode Code,
    TPayload? Payload,
    string CorrelationId)
    where TPayload : class;
