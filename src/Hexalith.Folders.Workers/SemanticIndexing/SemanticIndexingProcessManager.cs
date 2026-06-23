using System.Security.Cryptography;
using System.Text;

using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed class SemanticIndexingProcessManager
{
    private readonly ISemanticIndexingBridgeWriter _bridgeWriter;
    private readonly ISemanticIndexingContentMaterializer _contentMaterializer;
    private readonly ISemanticIndexingPolicyEvaluator _policyEvaluator;
    private readonly ISemanticIndexingPort _semanticIndexingPort;
    private readonly TimeProvider _timeProvider;

    public SemanticIndexingProcessManager(
        ISemanticIndexingBridgeWriter bridgeWriter,
        ISemanticIndexingPolicyEvaluator policyEvaluator,
        ISemanticIndexingContentMaterializer contentMaterializer,
        ISemanticIndexingPort semanticIndexingPort,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(bridgeWriter);
        ArgumentNullException.ThrowIfNull(policyEvaluator);
        ArgumentNullException.ThrowIfNull(contentMaterializer);
        ArgumentNullException.ThrowIfNull(semanticIndexingPort);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _bridgeWriter = bridgeWriter;
        _policyEvaluator = policyEvaluator;
        _contentMaterializer = contentMaterializer;
        _semanticIndexingPort = semanticIndexingPort;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ProcessFolderEventsAsync(
        IReadOnlyCollection<FolderProjectionEnvelope> envelopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        IReadOnlyList<SemanticIndexingBridgeEntry> applied = await _bridgeWriter
            .ApplyFolderEventsAsync(envelopes, cancellationToken)
            .ConfigureAwait(false);
        List<SemanticIndexingBridgeEntry> recorded = [];

        foreach (SemanticIndexingBridgeEntry entry in applied
            .Where(static entry => entry.Status is SemanticIndexingBridgeStatus.Stale or SemanticIndexingBridgeStatus.Tombstoned)
            .OrderBy(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal))
        {
            // Stale entries keep the Story 10.3 create/update upsert behavior; tombstoned entries are routed to the
            // removal (hard delete) / archive (soft delete) egress instead of being silently skipped (Story 10.4 AC1).
            SemanticIndexingBridgeEntry? result = entry.Status == SemanticIndexingBridgeStatus.Tombstoned
                ? await ProcessTombstoneAsync(entry, cancellationToken).ConfigureAwait(false)
                : await ProcessStaleAsync(entry, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                recorded.Add(result);
            }
        }

        return recorded;
    }

    private async Task<SemanticIndexingBridgeEntry?> ProcessStaleAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
    {
        // A stale entry without a content-hash reference (e.g. a metadata-only mutation) has nothing to index.
        if (entry.Identity.ContentHashReference is null)
        {
            return null;
        }

        return await ProcessEntryAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SemanticIndexingBridgeEntry?> ProcessTombstoneAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
    {
        // Emit only for previously-indexed units (decision (B)): a tombstoned entry with no prior index-publish
        // evidence has no document in the search index, so the egress is a metadata-only no-op (removal_not_required).
        // PublishedEventId is the stable source URI the upsert used as its cloudevent.id (decision (C)); reusing it for
        // the removal/archive guarantees byte-identical targeting under the composite (TenantId, AggregateId) key. The
        // null-guard here ensures a never-indexed removal never dereferences a missing source identity.
        string? indexedEventId = entry.Evidence.PublishedEventId;
        if (indexedEventId is null)
        {
            return await RecordRemovalAsync(entry, "removal_not_required", retryable: false, publishedEventId: null, cancellationToken).ConfigureAwait(false);
        }

        // The tombstone reason mirrors the projection's literals: folder_file_removed -> hard delete; folder_archived
        // -> soft delete. Any other reason is left untouched (defensive; the projection only sets these two).
        SemanticIndexingResult result;
        if (string.Equals(entry.ReasonCode, "folder_file_removed", StringComparison.Ordinal))
        {
            result = await _semanticIndexingPort
                .RemoveFileVersionAsync(BuildRemovalRequest(entry, indexedEventId), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (string.Equals(entry.ReasonCode, "folder_archived", StringComparison.Ordinal))
        {
            result = await _semanticIndexingPort
                .SoftDeleteFileVersionAsync(BuildArchiveRequest(entry, indexedEventId), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            return null;
        }

        return await RecordRemovalAsync(entry, result.ReasonCode, result.Retryable, result.PublishedEventId, cancellationToken).ConfigureAwait(false);
    }

    private static SemanticIndexingRemovalRequest BuildRemovalRequest(SemanticIndexingBridgeEntry entry, string indexedEventId)
        => new(
            entry.Identity.ManagedTenantId,
            entry.Identity.OrganizationId,
            entry.Identity.FolderId,
            entry.Identity.FileVersionId,
            indexedEventId,
            entry.CorrelationId,
            entry.TaskId);

    private static SemanticIndexingArchiveRequest BuildArchiveRequest(SemanticIndexingBridgeEntry entry, string indexedEventId)
        => new(
            entry.Identity.ManagedTenantId,
            entry.Identity.OrganizationId,
            entry.Identity.FolderId,
            entry.Identity.FileVersionId,
            indexedEventId,
            entry.Evidence.IndexedText,
            entry.Evidence.IndexedAttributes,
            entry.CorrelationId,
            entry.TaskId);

    private Task<SemanticIndexingBridgeEntry?> RecordRemovalAsync(
        SemanticIndexingBridgeEntry entry,
        string reasonCode,
        bool retryable,
        string? publishedEventId,
        CancellationToken cancellationToken)
        => _bridgeWriter.RecordRemovalEvidenceAsync(
            new SemanticIndexingRemovalEvidenceUpdate(
                entry.Identity,
                reasonCode,
                retryable,
                entry.CorrelationId,
                entry.TaskId,
                publishedEventId,
                DeriveResultFingerprint(entry.Identity, SemanticIndexingBridgeStatus.Tombstoned, reasonCode, publishedEventId),
                entry.Freshness.Watermark,
                _timeProvider.GetUtcNow()),
            cancellationToken);

    private async Task<SemanticIndexingBridgeEntry?> ProcessEntryAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
    {
        SemanticIndexingPolicyEvaluationResult policy = await _policyEvaluator
            .EvaluateAsync(entry, cancellationToken)
            .ConfigureAwait(false);
        if (!policy.IsAllowed)
        {
            return await RecordAsync(entry, MapPolicyStatus(policy), policy.ReasonCode, policy.Retryable, null, cancellationToken).ConfigureAwait(false);
        }

        SemanticIndexingContentMaterializationResult materialized = await _contentMaterializer
            .MaterializeAsync(
                new SemanticIndexingContentMaterializationRequest(
                    entry.Identity,
                    entry.Identity.ContentHashReference!,
                    entry.Evidence.PathPolicyClass,
                    entry.Evidence.ByteLength,
                    entry.Evidence.MediaType,
                    entry.Evidence.TransportEvidenceKind,
                    entry.Evidence.ObservedByteLength,
                    entry.CorrelationId,
                    entry.TaskId),
                cancellationToken)
            .ConfigureAwait(false);
        if (materialized.Status != SemanticIndexingContentMaterializationStatus.Available)
        {
            return await RecordAsync(
                entry,
                materialized.Status == SemanticIndexingContentMaterializationStatus.Skipped
                    ? SemanticIndexingBridgeStatus.Skipped
                    : SemanticIndexingBridgeStatus.Failed,
                materialized.ReasonCode,
                materialized.Retryable,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        if (materialized.LengthBytes > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes
            || materialized.ContentBytes!.LongLength > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes)
        {
            return await RecordAsync(
                entry,
                SemanticIndexingBridgeStatus.Skipped,
                "content_too_large",
                retryable: false,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        if (!IsSupportedInlineContentType(materialized.ContentType!))
        {
            return await RecordAsync(
                entry,
                SemanticIndexingBridgeStatus.Skipped,
                "content_type_unsupported",
                retryable: false,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        // A stored source URI that cannot be parsed into a stable source identity is a deterministic data problem,
        // not a transient outage. Record it as reconciliation_required rather than throwing past the event
        // processor: an unguarded throw here would surface as a 500 and Dapr would redeliver the same poison entry.
        SemanticIndexingSourceIdentity? source = TrySourceFrom(entry.Identity.SourceUri);
        if (source is null)
        {
            return await RecordAsync(
                entry,
                SemanticIndexingBridgeStatus.ReconciliationRequired,
                "source_identity_invalid",
                retryable: false,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        SemanticIndexingRequest request = BuildRequest(entry, policy, materialized, source);
        SemanticIndexingResult result = await _semanticIndexingPort
            .IndexFileVersionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return await RecordAsync(
            entry,
            MapIndexingStatus(result),
            result.ReasonCode,
            result.Retryable,
            result.PublishedEventId,
            cancellationToken,
            result.IndexedText,
            result.IndexedAttributes).ConfigureAwait(false);
    }

    private static SemanticIndexingRequest BuildRequest(
        SemanticIndexingBridgeEntry entry,
        SemanticIndexingPolicyEvaluationResult policy,
        SemanticIndexingContentMaterializationResult materialized,
        SemanticIndexingSourceIdentity source)
        => new(
            entry.Identity.ManagedTenantId,
            entry.Identity.OrganizationId,
            entry.Identity.FolderId,
            entry.Identity.FileVersionId,
            entry.Identity.ContentHashReference!,
            source,
            new SemanticIndexingContentDescriptor(
                materialized.ReasonCode,
                materialized.LengthBytes,
                materialized.ContentType!,
                materialized.SizeClassification,
                materialized.TypeClassification),
            new SemanticIndexingPolicyOutcome(
                authorizedForIndexing: true,
                policy.SensitivityClassification,
                policy.PathPolicyOutcome),
            entry.CorrelationId,
            entry.TaskId);

    private Task<SemanticIndexingBridgeEntry?> RecordAsync(
        SemanticIndexingBridgeEntry entry,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        bool retryable,
        string? publishedEventId,
        CancellationToken cancellationToken,
        string? indexedText = null,
        IReadOnlyDictionary<string, string>? indexedAttributes = null)
        => _bridgeWriter.RecordIndexingResultAsync(
            new SemanticIndexingResultUpdate(
                entry.Identity,
                status,
                reasonCode,
                retryable,
                entry.CorrelationId,
                entry.TaskId,
                publishedEventId,
                DeriveResultFingerprint(entry.Identity, status, reasonCode, publishedEventId),
                _timeProvider.GetUtcNow(),
                indexedText,
                indexedAttributes),
            cancellationToken);

    private static SemanticIndexingBridgeStatus MapPolicyStatus(SemanticIndexingPolicyEvaluationResult policy)
        => policy.Status == SemanticIndexingPolicyEvaluationStatus.Failed
            ? SemanticIndexingBridgeStatus.Failed
            : SemanticIndexingBridgeStatus.Skipped;

    private static SemanticIndexingBridgeStatus MapIndexingStatus(SemanticIndexingResult result)
        => result.Status switch
        {
            SemanticIndexingStatus.Accepted => SemanticIndexingBridgeStatus.Indexed,
            SemanticIndexingStatus.Skipped => SemanticIndexingBridgeStatus.Skipped,
            SemanticIndexingStatus.Deferred => SemanticIndexingBridgeStatus.Failed,
            SemanticIndexingStatus.Failed => SemanticIndexingBridgeStatus.Failed,
            _ => SemanticIndexingBridgeStatus.ReconciliationRequired,
        };

    private static bool IsSupportedInlineContentType(string contentType)
        => contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/x-yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/markdown", StringComparison.OrdinalIgnoreCase);

    private static SemanticIndexingSourceIdentity? TrySourceFrom(string sourceUri)
    {
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        try
        {
            return new SemanticIndexingSourceIdentity(
                uri.Scheme,
                uri.Authority,
                uri.AbsolutePath.TrimStart('/'));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string DeriveResultFingerprint(
        SemanticIndexingFileVersionIdentity identity,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        string? publishedEventId)
        => "sir-" + Hash(
            identity.ManagedTenantId,
            identity.FolderId,
            identity.FileVersionId,
            identity.ContentHashReference ?? "no-content-hash",
            identity.SourceUri,
            status.ToStatusCode(),
            reasonCode,
            publishedEventId ?? string.Empty);

    private static string Hash(params string[] parts)
    {
        string material = string.Join('\u001f', parts.Select(static part => part.Normalize(NormalizationForm.FormC)));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
