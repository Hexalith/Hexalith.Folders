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
}
