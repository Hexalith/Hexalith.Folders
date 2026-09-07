using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for Add/Change/Remove file mutations.</summary>
internal sealed class MutateFilesIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.MutateFiles";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "mutate-files";

    /// <inheritdoc />
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["content_hash_reference"] = FoldersCanonicalIntentBuilder.ReadString(root, "contentHashReference"),
                ["file_operation_kind"] = FoldersCanonicalIntentBuilder.ReadString(root, "fileOperationKind"),
                ["operation_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "operationId"),
                ["path_metadata"] = FoldersCanonicalIntentBuilder.ReadCanonicalObject(root, "pathMetadata"),
                ["path_policy_class"] = FoldersCanonicalIntentBuilder.ReadString(root, "pathMetadata", "pathPolicyClass"),
                ["task_id"] = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root),
                ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            },
            delegatedTaskScope: FoldersCanonicalIntentBuilder.ReadTaskScope(command, root));
    }
}
