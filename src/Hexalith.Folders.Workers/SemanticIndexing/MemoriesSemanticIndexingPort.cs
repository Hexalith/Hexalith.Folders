using System.Globalization;

using Dapr;
using Dapr.Client;

using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Folders.Workers.SemanticIndexing;

/// <summary>
/// Publishes one curated <see cref="SearchIndexEntryChanged"/> CloudEvent per indexed unit to the Memories
/// search index via Dapr pub/sub (component <c>pubsub</c>, topic <c>memories-events</c>). Memories upserts the
/// entry by the composite key (<c>TenantId</c>, <c>AggregateId</c>) into its search index, so re-publishing the
/// same state is harmless. This is the search-index path — it deliberately does NOT call
/// <c>MemoriesClient.IngestAsync</c> (the separate experimental RAG memory-ingestion subsystem). It follows the
/// <c>Hexalith.Tenants</c> <c>MemoriesSearchIndexEventPublisher</c> precedent.
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

        SearchIndexEntryChanged entry = new()
        {
            TenantId = FoldersSemanticIndexingDefaults.IndexTenant,
            AggregateId = FileVersionAggregateId(request),
            Text = BuildText(request),
            Attributes = BuildAttributes(request),
            CorrelationId = request.CorrelationId,
            CausationId = request.TaskId,
        };

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["cloudevent.id"] = cloudEventId,
            ["cloudevent.type"] = nameof(SearchIndexEntryChanged),
            ["cloudevent.source"] = FoldersSemanticIndexingDefaults.CloudEventsSource,
        };

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
                publishedEventId: cloudEventId);
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
    // safe identifier or classification; pathPolicyOutcome is a classification, not a raw path.
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
        };

    // The per-file-version upsert key for the shared `folders-index` tenant. Memories replaces the search-index
    // entry by the composite (TenantId, AggregateId), so this MUST be unique per indexed file version — a
    // folder-level key would collapse every file version in a folder onto one entry (within-folder data loss).
    // The '/' delimiter is intentional: it is excluded from SemanticIndexingBridgeValidation segment ids (which do
    // permit ':'), so no combination of segment values can forge a colliding AggregateId across managed tenants in
    // the shared index.
    private static string FileVersionAggregateId(SemanticIndexingRequest request)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{request.ManagedTenantId}/{request.OrganizationId}/{request.FolderId}/{request.FileVersionId}");
}
