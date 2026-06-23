using System.Globalization;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingResult
{
    public SemanticIndexingResult(
        SemanticIndexingStatus status,
        string reasonCode,
        bool retryable,
        string? publishedEventId = null)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(reasonCode);

        Status = status;
        ReasonCode = reasonCode;
        Retryable = retryable;
        PublishedEventId = publishedEventId;
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
}
