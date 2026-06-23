using System.Globalization;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingResult
{
    public SemanticIndexingResult(
        SemanticIndexingStatus status,
        string reasonCode,
        bool retryable,
        string? publishedEventId = null,
        string? indexedText = null,
        IReadOnlyDictionary<string, string>? indexedAttributes = null)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(reasonCode);

        Status = status;
        ReasonCode = reasonCode;
        Retryable = retryable;
        PublishedEventId = publishedEventId;
        IndexedText = indexedText;
        IndexedAttributes = indexedAttributes;
    }

    public SemanticIndexingStatus Status { get; init; }

    public string StatusCode => Status.ToString().ToLower(CultureInfo.InvariantCulture);

    public string ReasonCode { get; init; }

    public bool Retryable { get; init; }

    /// <summary>
    /// Gets the published <c>SearchIndexEntryChanged</c> CloudEvent id (= the stable source URI) recorded as the
    /// post-publish traceability handle. The search-index pub/sub path returns no Memories-side workflow/memory-unit id.
    /// </summary>
    public string? PublishedEventId { get; init; }

    /// <summary>
    /// Gets the exact metadata-only curated text that was published on the upsert, persisted into bridge evidence so a
    /// later archive soft-delete can re-send the complete document (the Memories upsert is a destructive full-field
    /// overwrite — Story 10.4 decision (A)). Null on paths that publish nothing.
    /// </summary>
    public string? IndexedText { get; init; }

    /// <summary>
    /// Gets the exact metadata-only attribute set that was published on the upsert, persisted into bridge evidence for
    /// the archive soft-delete full-document re-send (Story 10.4 decision (A)). Null on paths that publish nothing.
    /// </summary>
    public IReadOnlyDictionary<string, string>? IndexedAttributes { get; init; }
}
