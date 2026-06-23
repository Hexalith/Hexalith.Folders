using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingEvidence
{
    public SemanticIndexingEvidence(
        string? publishedEventId = null,
        string? resultFingerprint = null,
        string? pathPolicyClass = null,
        long? byteLength = null,
        string? mediaType = null,
        string? transportEvidenceKind = null,
        long? observedByteLength = null,
        string? indexedText = null,
        IReadOnlyDictionary<string, string>? indexedAttributes = null)
    {
        if (byteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "Content length evidence must not be negative.");
        }

        if (observedByteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observedByteLength), observedByteLength, "Observed content length evidence must not be negative.");
        }

        // PublishedEventId is the SearchIndexEntryChanged CloudEvent id (= the stable source URI), recorded as the
        // post-publish traceability handle (Story 10.3 design decision (c)). It replaces the former IngestAsync
        // WorkflowId/MemoryUnitId pair; the search-index pub/sub path returns no Memories-side workflow/memory-unit id.
        PublishedEventId = SemanticIndexingBridgeValidation.RequireOptionalValue(publishedEventId, nameof(publishedEventId));
        ResultFingerprint = SemanticIndexingBridgeValidation.RequireOptionalValue(resultFingerprint, nameof(resultFingerprint));
        PathPolicyClass = SemanticIndexingBridgeValidation.RequireOptionalValue(pathPolicyClass, nameof(pathPolicyClass));
        ByteLength = byteLength;
        MediaType = SemanticIndexingBridgeValidation.RequireOptionalValue(mediaType, nameof(mediaType));
        TransportEvidenceKind = SemanticIndexingBridgeValidation.RequireOptionalValue(transportEvidenceKind, nameof(transportEvidenceKind));
        ObservedByteLength = observedByteLength;

        // IndexedText / IndexedAttributes retain the exact metadata-only curated document that was published on the
        // SearchIndexEntryChanged upsert. The Memories upsert is a destructive full-field overwrite (Story 10.4
        // decision (A)), so the archive soft-delete must re-send the complete document with only folders.status
        // flipped to archived. These are already C9-safe (descriptor-derived text, classification attributes).
        IndexedText = SemanticIndexingBridgeValidation.RequireOptionalValue(indexedText, nameof(indexedText));
        IndexedAttributes = indexedAttributes;
    }

    public string? PublishedEventId { get; init; }

    public string? ResultFingerprint { get; init; }

    public string? PathPolicyClass { get; init; }

    public long? ByteLength { get; init; }

    public string? MediaType { get; init; }

    public string? TransportEvidenceKind { get; init; }

    public long? ObservedByteLength { get; init; }

    public string? IndexedText { get; init; }

    public IReadOnlyDictionary<string, string>? IndexedAttributes { get; init; }

    public static SemanticIndexingEvidence FromMutation(WorkspaceFileMutationAccepted accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        return new SemanticIndexingEvidence(
            pathPolicyClass: accepted.PathPolicyClass,
            byteLength: accepted.ByteLength,
            mediaType: accepted.MediaType,
            transportEvidenceKind: accepted.TransportEvidenceKind,
            observedByteLength: accepted.ObservedByteLength);
    }
}
