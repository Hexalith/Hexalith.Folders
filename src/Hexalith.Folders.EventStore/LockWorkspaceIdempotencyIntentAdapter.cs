using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for LockWorkspace.</summary>
internal sealed class LockWorkspaceIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.LockWorkspace";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "lock-workspace";

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
                ["folder_id"] = command.AggregateId,
                ["lock_intent"] = FoldersCanonicalIntentBuilder.ReadString(root, "lockIntent"),
                ["requested_lease_seconds"] = FoldersCanonicalIntentBuilder.ReadString(root, "requestedLeaseSeconds"),
                ["task_id"] = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root),
                ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
            },
            delegatedTaskScope: FoldersCanonicalIntentBuilder.ReadTaskScope(command, root));
    }
}
