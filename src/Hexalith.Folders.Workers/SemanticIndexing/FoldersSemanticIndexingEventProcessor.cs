using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed class FoldersSemanticIndexingEventProcessor
{
    private static readonly IReadOnlyDictionary<string, Type> s_eventTypes = SupportedEventTypes()
        .SelectMany(static type => new[]
        {
            new KeyValuePair<string, Type>(type.FullName!, type),
            new KeyValuePair<string, Type>(type.AssemblyQualifiedName!, type),
            new KeyValuePair<string, Type>(type.Name, type),
        })
        .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(static group => group.Key, static group => group.First().Value, StringComparer.Ordinal);

    private readonly SemanticIndexingProcessManager _processManager;
    private readonly ILogger<FoldersSemanticIndexingEventProcessor>? _logger;

    public FoldersSemanticIndexingEventProcessor(
        SemanticIndexingProcessManager processManager,
        ILogger<FoldersSemanticIndexingEventProcessor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(processManager);
        _processManager = processManager;
        _logger = logger;
    }

    public async Task<FoldersSemanticIndexingEventProcessingResult> ProcessAsync(
        EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        // Cross-delivery idempotency is owned by the semantic-indexing bridge projection (stale-result protection):
        // a redelivered event re-applies to the same bridge entry, whose fingerprint short-circuits re-indexing, so a
        // non-stale entry is never sent to Memories twice. No in-process dedup is kept here — a per-request set would
        // reset on every Dapr delivery (the processor is transient) and only give a false sense of idempotency.
        if (string.IsNullOrWhiteSpace(envelope.EventTypeName) || envelope.Payload is null || envelope.Payload.Length == 0)
        {
            LogUnprocessablePayload(envelope);
            return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
        }

        if (!s_eventTypes.TryGetValue(envelope.EventTypeName, out Type? eventType))
        {
            return FoldersSemanticIndexingEventProcessingResult.SkippedUnknownEventType;
        }

        try
        {
            object? deserialized = JsonSerializer.Deserialize(envelope.Payload, eventType);
            if (deserialized is not IFolderEvent folderEvent)
            {
                LogUnprocessablePayload(envelope);
                return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
            }

            await _processManager.ProcessFolderEventsAsync(
                [new FolderProjectionEnvelope(envelope.TenantId, envelope.SequenceNumber, folderEvent)],
                cancellationToken).ConfigureAwait(false);
            return FoldersSemanticIndexingEventProcessingResult.Processed;
        }
        catch (JsonException)
        {
            LogUnprocessablePayload(envelope);
            return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
        }
        catch (NotSupportedException)
        {
            LogUnprocessablePayload(envelope);
            return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
        }
    }

    // A payload that cannot be deserialized or is not an IFolderEvent is a deterministic, permanent failure of THIS
    // message. Record it as a metadata-only warning (no payload, no content) so the dropped event is observable; the
    // endpoint returns a success status for it so Dapr does not redeliver the same poison message forever.
    private void LogUnprocessablePayload(EventStoreDomainEventEnvelope envelope)
        => _logger?.LogWarning(
            "Dropping unprocessable folders semantic-indexing event (metadata-only): MessageId={MessageId}, TenantId={TenantId}, EventType={EventType}, Sequence={Sequence}.",
            envelope.MessageId,
            envelope.TenantId,
            envelope.EventTypeName,
            envelope.SequenceNumber);

    private static Type[] SupportedEventTypes()
        =>
        [
            typeof(WorkspaceFileMutationAccepted),
            typeof(WorkspaceCommitSucceeded),
            typeof(WorkspaceCommitFailed),
            typeof(FolderArchived),
        ];
}

public enum FoldersSemanticIndexingEventProcessingResult
{
    Processed,
    SkippedUnknownEventType,
    FailedInvalidPayload,
}
