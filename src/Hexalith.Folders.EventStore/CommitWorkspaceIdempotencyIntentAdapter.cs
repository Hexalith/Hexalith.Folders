using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for CommitWorkspace.</summary>
internal sealed class CommitWorkspaceIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.CommitWorkspace";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "commit-workspace";

    /// <inheritdoc />
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Commit;

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["author_metadata_reference"] = FoldersCanonicalIntentBuilder.ReadString(root, "authorMetadataReference"),
                ["branch_ref_target"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefTarget"),
                ["changed_path_metadata_digest"] = FoldersCanonicalIntentBuilder.ReadString(root, "changedPathMetadataDigest"),
                ["commit_message_classification"] = FoldersCanonicalIntentBuilder.ReadString(root, "commitMessageClassification"),
                ["operation_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "operationId"),
                ["task_id"] = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root),
                ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            },
            delegatedTaskScope: FoldersCanonicalIntentBuilder.ReadTaskScope(command, root));
    }
}
