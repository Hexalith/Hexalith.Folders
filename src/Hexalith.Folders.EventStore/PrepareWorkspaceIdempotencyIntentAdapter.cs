using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for PrepareWorkspace.</summary>
internal sealed class PrepareWorkspaceIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.PrepareWorkspace";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "prepare-workspace";

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
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["branch_ref_policy_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicyRef"),
                ["folder_id"] = command.AggregateId,
                ["repository_binding_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "repositoryBindingId"),
                ["task_id"] = FoldersCanonicalIntentBuilder.ReadTaskScope(command, root),
                ["workspace_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspaceId"),
                ["workspace_policy_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "workspacePolicyRef"),
            },
            delegatedTaskScope: FoldersCanonicalIntentBuilder.ReadTaskScope(command, root));
    }
}
