using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Server.Authorization;

/// <summary>Emits candidate authorization decisions through the host's observable logging pipeline.</summary>
internal sealed partial class LoggingPd10AuthorizationAuditSink(ILogger<LoggingPd10AuthorizationAuditSink> logger)
    : IPd10AuthorizationAuditSink
{
    /// <inheritdoc />
    public ValueTask WriteAsync(Pd10AuthorizationAuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        AuthorizationDecision(
            logger,
            record.Actor,
            record.Tenant,
            record.Operation,
            record.OperationFamily,
            record.Result,
            record.CorrelationId);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Information,
        Message = "PD10 authorization decision actor={Actor} tenant={Tenant} operation={Operation} operation_family={OperationFamily} result={Result} correlation_id={CorrelationId}")]
    private static partial void AuthorizationDecision(
        ILogger logger,
        string actor,
        string tenant,
        string operation,
        string operationFamily,
        string result,
        string correlationId);
}
