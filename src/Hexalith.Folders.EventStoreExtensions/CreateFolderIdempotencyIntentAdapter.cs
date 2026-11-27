using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for CreateFolder.</summary>
internal sealed class CreateFolderIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
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
    public CommandIntentDeclaration Declaration => FoldersCommandIntentDeclarations.For(CommandType);

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        bool isDecoratedV2 = FoldersCanonicalIntentBuilder.IsDecoratedV2Payload(root);
        using JsonDocument? normalized = isDecoratedV2
            ? FoldersCanonicalIntentBuilder.BuildNormalizedV2Guard(root, command.AggregateId, stripFolderId: false)
            : null;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["folder_metadata.display_name"] = FoldersCanonicalIntentBuilder.ReadString(root, "folderMetadata", "displayName"),
                ["parent_folder_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "parentFolderId"),
                ["request_schema_version"] = isDecoratedV2
                    ? "v1"
                    : FoldersCanonicalIntentBuilder.ReadString(root, "requestSchemaVersion"),
            },
            canonicalGuardRoot: normalized?.RootElement);
    }
}
