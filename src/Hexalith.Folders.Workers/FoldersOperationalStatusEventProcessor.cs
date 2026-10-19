using Hexalith.EventStore.Client.Subscriptions;
using Microsoft.Extensions.Logging;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Workers;

/// <summary>Persists operational completion before the existing authenticated subscription acknowledges delivery.</summary>
public sealed partial class FoldersOperationalStatusEventProcessor(OperationalStatusProjectionWriter writer,
    ILogger<FoldersOperationalStatusEventProcessor>? logger = null)
{
    /// <summary>Processes folder actor positions, leaving organization streams to their existing projections.</summary>
    public async Task ProcessAsync(EventStoreDomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Domain != "folders" || envelope.AggregateId == envelope.TenantId
            || ProviderBindingActorId.IsReserved(envelope.AggregateId))
        {
            return;
        }
        OperationalStatusEvent source = OperationalStatusEventReader.ReadStored(envelope);
        var result = await writer.ApplyAsync(envelope.TenantId, envelope.AggregateId, envelope.MessageId,
            [source], cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            if (logger is not null)
                IncompletePersistence(logger, result.Status.ToString(), result.ConflictKind.ToString(), result.RecoveryReason);
            throw new InvalidOperationException("Operational status persistence is incomplete.");
        }
    }

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning,
        Message = "Operational batch incomplete status={Status} conflict_kind={ConflictKind} recovery_reason={RecoveryReason}")]
    private static partial void IncompletePersistence(ILogger logger, string status, string conflictKind, string? recoveryReason);
}
