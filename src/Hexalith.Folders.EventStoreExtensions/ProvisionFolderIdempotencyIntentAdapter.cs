using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Server.Provisioning;

namespace Hexalith.Folders.EventStore;

/// <summary>Describes immutable owner intent and leaves the live lease fence out of replay identity.</summary>
internal sealed class ProvisionFolderIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => FolderProvisioningRequest.CommandType;

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "provision-folder";

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
        return FoldersCanonicalIntentBuilder.Create(command, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["task_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "taskId"),
            ["step_key"] = FoldersCanonicalIntentBuilder.ReadString(root, "stepKey"),
            ["admission_operation"] = FoldersCanonicalIntentBuilder.ReadString(root, "admissionOperation"),
            ["request_sha256"] = FoldersCanonicalIntentBuilder.ReadString(root, "requestSha256"),
            ["expected_version"] = FoldersCanonicalIntentBuilder.ReadString(root, "expectedVersion"),
            ["display_name"] = FoldersCanonicalIntentBuilder.ReadString(root, "displayName")?.Trim(),
        });
    }
}
