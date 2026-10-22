using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.Folders.EventStore;

/// <summary>Admits the private source-generation erasure grant with an exact metadata-only intent.</summary>
internal sealed class AuthorizeContentErasureIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    public string CommandType => "Hexalith.Folders.Commands.AuthorizeWorkspaceContentErasure";
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;
    public string OperationId => "authorize-content-erasure";
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using var document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        var root = document.RootElement;
        return FoldersCanonicalIntentBuilder.Create(command, new Dictionary<string, string?>
        {
            ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            ["receipt_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "receiptId"),
            ["assigned_task_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "assignedTaskId"),
            ["expected_generation"] = FoldersCanonicalIntentBuilder.ReadString(root, "expectedGeneration"),
        });
    }
}
