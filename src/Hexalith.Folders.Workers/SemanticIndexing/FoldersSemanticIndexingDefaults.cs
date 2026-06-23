namespace Hexalith.Folders.Workers.SemanticIndexing;

public static class FoldersSemanticIndexingDefaults
{
    public const long MaxInlineIngestionBytes = 262144;

    public const string CloudEventsSource = "hexalith-folders";

    public const string IndexTenant = "folders-index";

    public const string DomainEventsRoute = "/folders/events";

    public const string DomainEventsTopicName = "folders.events";

    public const string PubSubName = "pubsub";

    public const string EventsTopicName = "memories-events";

    /// <summary>
    /// The metadata-only search-index attribute key that records whether a Folders unit is live or archived, so the
    /// Story 10.5 query facade can filter soft-deleted (archived) units without re-evaluating folder state. The key
    /// and its values are stable, ordinal, and lowercase.
    /// </summary>
    public const string StatusAttributeKey = "folders.status";

    /// <summary>The <see cref="StatusAttributeKey"/> value emitted on the live <c>SearchIndexEntryChanged</c> upsert path.</summary>
    public const string StatusActive = "active";

    /// <summary>The <see cref="StatusAttributeKey"/> value emitted on the archive soft-delete <c>SearchIndexEntryChanged</c> re-send.</summary>
    public const string StatusArchived = "archived";
}
