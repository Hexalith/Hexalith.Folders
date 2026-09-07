using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for CreateRepositoryBackedFolder.</summary>
internal sealed class CreateRepositoryBackedFolderIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.CreateRepositoryBackedFolder";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "create-repository-backed-folder";

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
                ["repository_binding_id"] = FoldersCanonicalIntentBuilder.ReadString(root, "branchRefPolicy", "repositoryBindingId"),
                ["folder_id"] = command.AggregateId,
                ["folder_metadata.display_name"] = FoldersCanonicalIntentBuilder.ReadString(root, "folderMetadata", "displayName"),
                ["provider_binding_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "providerBindingRef"),
                ["repository_profile_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "repositoryProfileRef"),
            },
            credentialScope: FoldersCanonicalIntentBuilder.ReadString(root, "credentialScopeClass"));
    }
}
