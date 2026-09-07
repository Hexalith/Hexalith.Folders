using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for CreateFolder.</summary>
internal sealed class CreateFolderIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.CreateFolder";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "create-folder";

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
                ["folder_metadata.display_name"] = FoldersCanonicalIntentBuilder.ReadString(root, "folderMetadata", "displayName"),
                ["parent_folder_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "parentFolderId"),
                ["request_schema_version"] = FoldersCanonicalIntentBuilder.ReadString(root, "requestSchemaVersion"),
            });
    }
}
