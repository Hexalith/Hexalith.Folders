using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;

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

    private readonly ConcurrentDictionary<string, byte> _processedMessageIds = new(StringComparer.Ordinal);
    private readonly SemanticIndexingProcessManager _processManager;

    public FoldersSemanticIndexingEventProcessor(SemanticIndexingProcessManager processManager)
    {
        ArgumentNullException.ThrowIfNull(processManager);
        _processManager = processManager;
    }

    public async Task<FoldersSemanticIndexingEventProcessingResult> ProcessAsync(
        EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_processedMessageIds.TryAdd(envelope.MessageId, 0))
        {
            return FoldersSemanticIndexingEventProcessingResult.Duplicate;
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
                _processedMessageIds.TryRemove(envelope.MessageId, out _);
                return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
            }

            await _processManager.ProcessFolderEventsAsync(
                [new FolderProjectionEnvelope(envelope.TenantId, envelope.SequenceNumber, folderEvent)],
                cancellationToken).ConfigureAwait(false);
            return FoldersSemanticIndexingEventProcessingResult.Processed;
        }
        catch (JsonException)
        {
            _processedMessageIds.TryRemove(envelope.MessageId, out _);
            return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
        }
        catch (NotSupportedException)
        {
            _processedMessageIds.TryRemove(envelope.MessageId, out _);
            return FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            _processedMessageIds.TryRemove(envelope.MessageId, out _);
            throw;
        }
    }

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
    Duplicate,
    SkippedUnknownEventType,
    FailedInvalidPayload,
}
