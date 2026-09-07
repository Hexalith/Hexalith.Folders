using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for BindRepository.</summary>
internal sealed class BindRepositoryIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.BindRepository";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "bind-repository";

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
                ["branch_ref_policy.policy_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "policyRef"),
                ["external_repository_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "externalRepositoryRef"),
                ["folder_id"] = command.AggregateId,
                ["provider_binding_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "providerBindingRef"),
            });
    }
}
