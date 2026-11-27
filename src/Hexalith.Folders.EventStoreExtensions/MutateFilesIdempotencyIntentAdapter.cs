using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for Add/Change/Remove file mutations.</summary>
internal sealed class MutateFilesIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
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
    public CommandIntentDeclaration Declaration => FoldersCommandIntentDeclarations.For(CommandType);

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        string? taskScope = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root);

        if (root.TryGetProperty("changes", out JsonElement changes))
        {
            return CreateDecoratedBatchIntent(command, root, changes, taskScope!);
        }

        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["content_hash_reference"] = FoldersCanonicalIntentBuilder.ReadString(root, "contentHashReference"),
                ["file_operation_kind"] = FoldersCanonicalIntentBuilder.ReadString(root, "fileOperationKind"),
                ["operation_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "operationId"),
                ["path_metadata"] = FoldersCanonicalIntentBuilder.ReadCanonicalObject(root, "pathMetadata"),
                ["path_policy_class"] = FoldersCanonicalIntentBuilder.ReadString(root, "pathMetadata", "pathPolicyClass"),
                ["task_id"] = taskScope,
                ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            },
            delegatedTaskScope: taskScope);
    }

    private static IdempotencyCanonicalIntent CreateDecoratedBatchIntent(
        IdempotencyIntentCommand command,
        JsonElement root,
        JsonElement changes,
        string taskScope)
    {
        if (root.TryGetProperty("requestSchemaVersion", out _) || root.TryGetProperty("items", out _))
        {
            throw new JsonException("Decorated MutateFiles canonical intent must not mix changes with requestSchemaVersion or items.");
        }

        FoldersCanonicalIntentBuilder.VerifyRequiredMatchingFolderId(root, command.AggregateId);
        string? workspaceId = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId");
        if (string.IsNullOrWhiteSpace(workspaceId)
            || changes.ValueKind != JsonValueKind.Array
            || changes.GetArrayLength() is < 1 or > 100)
        {
            throw new JsonException("Decorated MutateFiles canonical intent requires a workspaceId and 1..100 changes.");
        }

        if (changes.GetArrayLength() == 1)
        {
            JsonElement single = changes[0];
            if (single.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Decorated MutateFiles change entry must be an object.");
            }

            JsonObject normalizedSingle = BuildSemanticChangeObject(single);
            normalizedSingle["requestSchemaVersion"] = "v1";
            normalizedSingle["workspaceId"] = workspaceId;
            normalizedSingle["taskId"] = taskScope;

            using JsonDocument normalizedGuard = JsonDocument.Parse(normalizedSingle.ToJsonString());
            return FoldersCanonicalIntentBuilder.Create(
                command,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["content_hash_reference"] = FoldersCanonicalIntentBuilder.ReadString(single, "contentHashReference"),
                    ["file_operation_kind"] = FoldersCanonicalIntentBuilder.ReadString(single, "fileOperationKind"),
                    ["operation_id"] = FoldersCanonicalIntentBuilder.ReadString(single, "operationId"),
                    ["path_metadata"] = FoldersCanonicalIntentBuilder.ReadCanonicalObject(single, "pathMetadata"),
                    ["path_policy_class"] = FoldersCanonicalIntentBuilder.ReadString(single, "pathMetadata", "pathPolicyClass"),
                    ["task_id"] = taskScope,
                    ["workspace_id"] = workspaceId,
                },
                delegatedTaskScope: taskScope,
                canonicalGuardRoot: normalizedGuard.RootElement);
        }

        JsonArray normalizedItems = [];
        foreach (JsonElement item in changes.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Decorated MutateFiles change entry must be an object.");
            }

            normalizedItems.Add(BuildSemanticChangeObject(item));
        }

        JsonObject normalizedBatch = new()
        {
            ["requestSchemaVersion"] = "v1",
            ["workspaceId"] = workspaceId,
            ["taskId"] = taskScope,
            ["items"] = normalizedItems,
        };

        using JsonDocument normalizedBatchGuard = JsonDocument.Parse(normalizedBatch.ToJsonString());
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["content_hash_reference"] = null,
                ["file_operation_kind"] = null,
                ["operation_id"] = null,
                ["path_metadata"] = null,
                ["path_policy_class"] = null,
                ["task_id"] = taskScope,
                ["workspace_id"] = workspaceId,
            },
            delegatedTaskScope: taskScope,
            canonicalGuardRoot: normalizedBatchGuard.RootElement);
    }

    private static JsonObject BuildSemanticChangeObject(JsonElement item)
    {
        JsonObject semantic = [];

        CopyIfPresent(item, semantic, "operationId");
        CopyIfPresent(item, semantic, "fileOperationKind");
        CopyIfPresent(item, semantic, "pathMetadata");
        CopyIfPresent(item, semantic, "contentHashReference");

        if (item.TryGetProperty("byteLength", out JsonElement byteLengthElement)
            && byteLengthElement.ValueKind != JsonValueKind.Null)
        {
            semantic["byteLength"] = JsonNode.Parse(byteLengthElement.GetRawText());
        }

        string? mediaType = FoldersCanonicalIntentBuilder.ReadString(item, "inlineContent", "mediaType")
            ?? FoldersCanonicalIntentBuilder.ReadString(item, "streamDescriptor", "mediaType")
            ?? FoldersCanonicalIntentBuilder.ReadString(item, "mediaType");
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            semantic["mediaType"] = mediaType;
        }

        CopyIfPresent(item, semantic, "expectedFileVersion");
        CopyIfPresent(item, semantic, "expectedLockFence");
        CopyIfPresent(item, semantic, "ownerFileId");

        // Deliberately exclude inlineContent (and contentBytes), streamDescriptor, and transportOperation
        // so raw file content bytes never enter canonical intent descriptors and transport retries match.
        return semantic;
    }

    private static void CopyIfPresent(JsonElement source, JsonObject target, string propertyName)
    {
        if (source.TryGetProperty(propertyName, out JsonElement value))
        {
            target[propertyName] = JsonNode.Parse(value.GetRawText());
        }
    }
}
