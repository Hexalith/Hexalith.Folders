namespace Hexalith.Folders.Projections.TenantAccess;

public static class FoldersTenantEventSubscription
{
    public const string AppId = "folders-workers";

    public const string Route = "/tenants/events";

    public const string PubSubName = "pubsub";

    public const string TopicName = "system.tenants.events";
}
