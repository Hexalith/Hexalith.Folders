using System.Globalization;
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
            .Where(static entry => entry.Status == SemanticIndexingBridgeStatus.Stale)
            .OrderBy(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal))
        {
            if (entry.Identity.ContentHashReference is null)
            {
                continue;
            }

            SemanticIndexingBridgeEntry? result = await ProcessEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                recorded.Add(result);
            }
        }

        return recorded;
    }

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

        SemanticIndexingRequest request = BuildRequest(entry, policy, materialized);
        SemanticIndexingResult result = await _semanticIndexingPort
            .IndexFileVersionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return await RecordAsync(
            entry,
            MapIndexingStatus(result),
            result.ReasonCode,
            result.Retryable,
            result.PublishedEventId,
            cancellationToken).ConfigureAwait(false);
    }

    private static SemanticIndexingRequest BuildRequest(
        SemanticIndexingBridgeEntry entry,
        SemanticIndexingPolicyEvaluationResult policy,
        SemanticIndexingContentMaterializationResult materialized)
    {
        SemanticIndexingSourceIdentity source = SourceFrom(entry.Identity.SourceUri);
        return new SemanticIndexingRequest(
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
            entry.TaskId,
            DeriveIdempotencyKey(entry.Identity));
    }

    private Task<SemanticIndexingBridgeEntry?> RecordAsync(
        SemanticIndexingBridgeEntry entry,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        bool retryable,
        string? publishedEventId,
        CancellationToken cancellationToken)
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
                _timeProvider.GetUtcNow()),
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

    private static SemanticIndexingSourceIdentity SourceFrom(string sourceUri)
    {
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("Semantic indexing source URI must be absolute.", nameof(sourceUri));
        }

        return new SemanticIndexingSourceIdentity(
            uri.Scheme,
            uri.Authority,
            uri.AbsolutePath.TrimStart('/'));
    }

    private static string DeriveIdempotencyKey(SemanticIndexingFileVersionIdentity identity)
        => "semantic-indexing-" + Hash(
            identity.ManagedTenantId,
            identity.OrganizationId,
            identity.FolderId,
            identity.WorkspaceId,
            identity.FileVersionId,
            identity.ContentHashReference ?? "no-content-hash",
            identity.SourceUri);

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
        return Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
    }
}
