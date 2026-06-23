using System.Globalization;

using Dapr;
using Dapr.Client;

using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Folders.Workers.SemanticIndexing;

/// <summary>
/// Publishes curated search-index CloudEvents for indexed units to the Memories search index via Dapr pub/sub
/// (component <c>pubsub</c>, topic <c>memories-events</c>). The create/update path publishes
/// <see cref="SearchIndexEntryChanged"/> (Memories upserts by the composite key (<c>TenantId</c>, <c>AggregateId</c>)
/// via <c>HashSetAsync</c>); the removal path publishes <see cref="SearchIndexEntryRemoved"/> (hard delete via
/// <c>KeyDeleteAsync</c>); the archive soft-delete re-publishes the full <see cref="SearchIndexEntryChanged"/> with
/// <c>folders.status = archived</c>. All three are idempotent under at-least-once delivery. This is the search-index
/// path — it deliberately does NOT call <c>MemoriesClient.IngestAsync</c> (the separate experimental RAG
/// memory-ingestion subsystem). It follows the <c>Hexalith.Tenants</c> <c>MemoriesSearchIndexEventPublisher</c>
/// precedent (soft-delete via a status attribute), extended with the Folders hybrid hard-delete for file removals.
/// </summary>
internal sealed class MemoriesSemanticIndexingPort : ISemanticIndexingPort
{
    private readonly DaprClient _daprClient;

    public MemoriesSemanticIndexingPort(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        _daprClient = daprClient;
    }

    public async ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
        SemanticIndexingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The CloudEvent id is the stable per-unit source URI; Memories echoes it back verbatim as
        // ScoredResult.SourceUri and it is the post-publish traceability handle (PublishedEventId).
        string cloudEventId = request.Source.ToUriString();
        string text = BuildText(request);
        Dictionary<string, string> attributes = BuildAttributes(request);

        SearchIndexEntryChanged entry = new()
        {
            TenantId = FoldersSemanticIndexingDefaults.IndexTenant,
            AggregateId = FileVersionAggregateId(request.ManagedTenantId, request.OrganizationId, request.FolderId, request.FileVersionId),
            Text = text,
            Attributes = attributes,
            CorrelationId = request.CorrelationId,
            CausationId = request.TaskId,
        };

        Dictionary<string, string> metadata = BuildMetadata(cloudEventId, nameof(SearchIndexEntryChanged));

        try
        {
            await _daprClient.PublishEventAsync(
                FoldersSemanticIndexingDefaults.PubSubName,
                FoldersSemanticIndexingDefaults.EventsTopicName,
                entry,
                metadata,
                cancellationToken).ConfigureAwait(false);

            // Return the exact published text/attributes so the bridge can persist them as evidence; a later archive
            // soft-delete re-sends this complete document (the Memories upsert is a destructive full-field overwrite).
            return new SemanticIndexingResult(
                SemanticIndexingStatus.Accepted,
                "memories_accepted",
                retryable: false,
                publishedEventId: cloudEventId,
                indexedText: text,
                indexedAttributes: attributes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // A timeout not tied to the caller's token is a transient remote outage, not a rejected Folders
            // operation; record it as retryable so the bridge entry stays eligible for re-evaluation.
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
        catch (DaprException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
    }

    public async ValueTask<SemanticIndexingResult> RemoveFileVersionAsync(
        SemanticIndexingRemovalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The removal targets exactly the upserted document: AggregateId is reconstructed identically to the upsert
        // and cloudevent.id is the same stable source URI the upsert used (Story 10.4 AC5). Memories KeyDeleteAsync is
        // idempotent (deleting a missing key is a no-op), so re-delivery is harmless.
        SearchIndexEntryRemoved removed = new()
        {
            TenantId = FoldersSemanticIndexingDefaults.IndexTenant,
            AggregateId = FileVersionAggregateId(request.ManagedTenantId, request.OrganizationId, request.FolderId, request.FileVersionId),
            CorrelationId = request.CorrelationId,
            CausationId = request.TaskId,
        };

        Dictionary<string, string> metadata = BuildMetadata(request.IndexedEventId, nameof(SearchIndexEntryRemoved));

        try
        {
            await _daprClient.PublishEventAsync(
                FoldersSemanticIndexingDefaults.PubSubName,
                FoldersSemanticIndexingDefaults.EventsTopicName,
                removed,
                metadata,
                cancellationToken).ConfigureAwait(false);

            return new SemanticIndexingResult(
                SemanticIndexingStatus.Accepted,
                "memories_accepted",
                retryable: false,
                publishedEventId: request.IndexedEventId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
        catch (DaprException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
    }

    public async ValueTask<SemanticIndexingResult> SoftDeleteFileVersionAsync(
        SemanticIndexingArchiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Re-send the COMPLETE document (the Memories upsert is a destructive full-field overwrite — decision (A)) with
        // only folders.status flipped to archived, reusing the original text/attributes/cloudevent.id so the soft
        // delete overwrites exactly the upserted document.
        string text = string.IsNullOrWhiteSpace(request.IndexedText)
            ? ArchiveFallbackText(request.FileVersionId)
            : request.IndexedText;
        Dictionary<string, string> attributes = BuildArchivedAttributes(request);

        SearchIndexEntryChanged entry = new()
        {
            TenantId = FoldersSemanticIndexingDefaults.IndexTenant,
            AggregateId = FileVersionAggregateId(request.ManagedTenantId, request.OrganizationId, request.FolderId, request.FileVersionId),
            Text = text,
            Attributes = attributes,
            CorrelationId = request.CorrelationId,
            CausationId = request.TaskId,
        };

        Dictionary<string, string> metadata = BuildMetadata(request.IndexedEventId, nameof(SearchIndexEntryChanged));

        try
        {
            await _daprClient.PublishEventAsync(
                FoldersSemanticIndexingDefaults.PubSubName,
                FoldersSemanticIndexingDefaults.EventsTopicName,
                entry,
                metadata,
                cancellationToken).ConfigureAwait(false);

            return new SemanticIndexingResult(
                SemanticIndexingStatus.Accepted,
                "memories_accepted",
                retryable: false,
                publishedEventId: request.IndexedEventId,
                indexedText: text,
                indexedAttributes: attributes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
        catch (DaprException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_publish_error", retryable: true);
        }
    }

    // The curated, C9-safe searchable text: descriptor + non-sensitive identity tokens + type classification.
    // Never raw bytes, never raw path segments. Empty descriptor falls back to "{typeClassification} {fileVersionId}".
    private static string BuildText(SemanticIndexingRequest request)
    {
        string descriptor = request.Content.IndexingTextDescriptor;
        string typeClassification = request.Content.TypeClassification;
        string fileVersionId = request.FileVersionId;

        return string.IsNullOrWhiteSpace(descriptor)
            ? string.Create(CultureInfo.InvariantCulture, $"{typeClassification} {fileVersionId}")
            : string.Create(CultureInfo.InvariantCulture, $"{descriptor} {fileVersionId} {typeClassification}");
    }

    // Flat, exactly-matched string attributes (BM25 has no metadata origin/confidence). Every value is a metadata-
    // safe identifier or classification; pathPolicyOutcome is a classification, not a raw path. folders.status records
    // the live/archived lifecycle so the Story 10.5 facade can filter soft-deleted units without inferring state.
    private static Dictionary<string, string> BuildAttributes(SemanticIndexingRequest request)
        => new(StringComparer.Ordinal)
        {
            ["folders.managedTenantId"] = request.ManagedTenantId,
            ["folders.organizationId"] = request.OrganizationId,
            ["folders.folderId"] = request.FolderId,
            ["folders.fileVersionId"] = request.FileVersionId,
            ["folders.contentHash"] = request.ContentHash,
            ["folders.contentDescriptor"] = request.Content.IndexingTextDescriptor,
            ["folders.sizeClassification"] = request.Content.SizeClassification,
            ["folders.typeClassification"] = request.Content.TypeClassification,
            ["folders.sensitivityClassification"] = request.Policy.SensitivityClassification,
            ["folders.pathPolicyOutcome"] = request.Policy.PathPolicyOutcome,
            [FoldersSemanticIndexingDefaults.StatusAttributeKey] = FoldersSemanticIndexingDefaults.StatusActive,
        };

    // Clone the original index-time attributes and overwrite only folders.status -> archived. When the preserved
    // evidence carries no attributes (legacy entries), reconstruct the metadata-safe subset available from identity.
    private static Dictionary<string, string> BuildArchivedAttributes(SemanticIndexingArchiveRequest request)
    {
        Dictionary<string, string> attributes = request.IndexedAttributes is { Count: > 0 }
            ? new Dictionary<string, string>(request.IndexedAttributes, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["folders.managedTenantId"] = request.ManagedTenantId,
                ["folders.organizationId"] = request.OrganizationId,
                ["folders.folderId"] = request.FolderId,
                ["folders.fileVersionId"] = request.FileVersionId,
            };

        attributes[FoldersSemanticIndexingDefaults.StatusAttributeKey] = FoldersSemanticIndexingDefaults.StatusArchived;
        return attributes;
    }

    // C9-safe fallback when the index-time text was not preserved (legacy entries): a stable descriptor that leaks no
    // raw path/content. The loss of the original rich searchable text is an accepted, documented archive consequence.
    private static string ArchiveFallbackText(string fileVersionId)
        => string.Create(CultureInfo.InvariantCulture, $"{FoldersSemanticIndexingDefaults.StatusArchived} {fileVersionId}");

    private static Dictionary<string, string> BuildMetadata(string cloudEventId, string cloudEventType)
        => new(StringComparer.Ordinal)
        {
            ["cloudevent.id"] = cloudEventId,
            ["cloudevent.type"] = cloudEventType,
            ["cloudevent.source"] = FoldersSemanticIndexingDefaults.CloudEventsSource,
        };

    // The per-file-version upsert key for the shared `folders-index` tenant. Memories replaces the search-index
    // entry by the composite (TenantId, AggregateId), so this MUST be unique per indexed file version — a
    // folder-level key would collapse every file version in a folder onto one entry (within-folder data loss).
    // The '/' delimiter is intentional: it is excluded from SemanticIndexingBridgeValidation segment ids (which do
    // permit ':'), so no combination of segment values can forge a colliding AggregateId across managed tenants in
    // the shared index. Reconstructed identically for upsert, hard delete, and archive so all three target the same
    // document under the composite key (Story 10.4 AC5).
    private static string FileVersionAggregateId(
        string managedTenantId,
        string organizationId,
        string folderId,
        string fileVersionId)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{managedTenantId}/{organizationId}/{folderId}/{fileVersionId}");
}
