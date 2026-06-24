using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public static class FoldersSemanticIndexingDefaults
{
    public const long MaxInlineIngestionBytes = 262144;

    public const string CloudEventsSource = "hexalith-folders";

    /// <summary>The shared physical Memories index tenant; sourced from the cross-layer search-index contract.</summary>
    public const string IndexTenant = FoldersSemanticIndexingAttributes.IndexTenant;

    public const string DomainEventsRoute = "/folders/events";

    public const string DomainEventsTopicName = "folders.events";

    public const string PubSubName = "pubsub";

    public const string EventsTopicName = "memories-events";

    /// <summary>
    /// The metadata-only search-index attribute key that records whether a Folders unit is live or archived, so the
    /// Story 10.5 query facade can filter soft-deleted (archived) units without re-evaluating folder state. Sourced
    /// from the shared <see cref="FoldersSemanticIndexingAttributes"/> contract so the producer and facade cannot drift.
    /// </summary>
    public const string StatusAttributeKey = FoldersSemanticIndexingAttributes.StatusAttribute;

    /// <summary>The <see cref="StatusAttributeKey"/> value emitted on the live <c>SearchIndexEntryChanged</c> upsert path.</summary>
    public const string StatusActive = FoldersSemanticIndexingAttributes.StatusActive;

    /// <summary>The <see cref="StatusAttributeKey"/> value emitted on the archive soft-delete <c>SearchIndexEntryChanged</c> re-send.</summary>
    public const string StatusArchived = FoldersSemanticIndexingAttributes.StatusArchived;
}
