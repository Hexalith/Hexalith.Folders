using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for ConfigureBranchRefPolicy.</summary>
internal sealed class ConfigureBranchRefPolicyIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
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
    public CommandIntentDeclaration Declaration => FoldersCommandIntentDeclarations.For(CommandType);

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        bool hasNestedPolicy = root.TryGetProperty("branchRefPolicy", out _);
        if (hasNestedPolicy && FoldersCanonicalIntentBuilder.IsDecoratedV2Payload(root))
        {
            throw new JsonException("ConfigureBranchRefPolicy with nested branchRefPolicy requires requestSchemaVersion.");
        }

        using JsonDocument? normalized = !hasNestedPolicy && FoldersCanonicalIntentBuilder.IsDecoratedV2Payload(root)
            ? FoldersCanonicalIntentBuilder.BuildNormalizedV2Guard(root, command.AggregateId, stripFolderId: true)
            : null;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["branch_ref_policy.allowed_ref_patterns"] = hasNestedPolicy
                    ? FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "branchRefPolicy", "allowedRefPatterns")
                    : FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "allowedRefPatterns"),
                ["branch_ref_policy.default_ref"] = hasNestedPolicy
                    ? FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "defaultRef")
                    : FoldersCanonicalIntentBuilder.ReadString(root, "defaultRef"),
                ["branch_ref_policy.policy_ref"] = hasNestedPolicy
                    ? FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "policyRef")
                    : FoldersCanonicalIntentBuilder.ReadString(root, "policyRef"),
                ["branch_ref_policy.protected_ref_patterns"] = hasNestedPolicy
                    ? FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "branchRefPolicy", "protectedRefPatterns")
                    : FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "protectedRefPatterns"),
                ["folder_id"] = command.AggregateId,
                ["repository_binding_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "repositoryBindingId"),
            },
            canonicalGuardRoot: normalized?.RootElement);
    }
}
