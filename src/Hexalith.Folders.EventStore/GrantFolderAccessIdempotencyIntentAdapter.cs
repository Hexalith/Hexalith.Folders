using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for GrantFolderAccess.</summary>
internal sealed class GrantFolderAccessIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public CommandIntentDeclaration Declaration => FoldersCommandCatalog.Commands.Single(declaration => declaration.Route.MessageType == CommandType);

    /// <inheritdoc/>
    public string CommandType => "Hexalith.Folders.Commands.GrantFolderAccess";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "grant-folder-access";

    /// <inheritdoc />
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
        => FolderAccessIdempotencyIntent.Create(command, effect: "grant");
}
