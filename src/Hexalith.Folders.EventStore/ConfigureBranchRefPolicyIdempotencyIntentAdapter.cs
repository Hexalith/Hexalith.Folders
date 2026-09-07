using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for ConfigureBranchRefPolicy.</summary>
internal sealed class ConfigureBranchRefPolicyIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.ConfigureBranchRefPolicy";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "configure-branch-ref-policy";

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
                ["branch_ref_policy.allowed_ref_patterns"] = FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "branchRefPolicy", "allowedRefPatterns"),
                ["branch_ref_policy.default_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "defaultRef"),
                ["branch_ref_policy.policy_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "policyRef"),
                ["branch_ref_policy.protected_ref_patterns"] = FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "branchRefPolicy", "protectedRefPatterns"),
                ["folder_id"] = command.AggregateId,
                ["repository_binding_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "repositoryBindingId"),
            });
    }
}
