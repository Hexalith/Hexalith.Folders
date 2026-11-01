using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for RevokeFolderAccess.</summary>
internal sealed class RevokeFolderAccessIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.RevokeFolderAccess";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "revoke-folder-access";

    /// <inheritdoc />
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    /// <inheritdoc />
    public CommandIntentDeclaration Declaration => FoldersCommandIntentDeclarations.For(CommandType);

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
        => FolderAccessIdempotencyIntent.Create(command, effect: "revoke");
}
