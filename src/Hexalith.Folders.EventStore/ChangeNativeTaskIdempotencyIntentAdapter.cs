using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Canonical admission for the internal native lease/completion command.</summary>
internal sealed class ChangeNativeTaskIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    public string CommandType => "Hexalith.Folders.Commands.ChangeNativeTask";
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;
    public string OperationId => "change-native-task";
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using var document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        var root = document.RootElement;
        return FoldersCanonicalIntentBuilder.Create(command, new Dictionary<string, string?>
        {
            ["folder_id"] = command.AggregateId,
            ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            ["task_id"] = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root),
            ["expected_head"] = FoldersCanonicalIntentBuilder.ReadString(root, "expectedHead"),
            ["native_request"] = FoldersCanonicalIntentBuilder.ReadCanonicalObject(root, "request"),
        }, delegatedTaskScope: FoldersCanonicalIntentBuilder.ReadTaskScope(command, root));
    }
}
