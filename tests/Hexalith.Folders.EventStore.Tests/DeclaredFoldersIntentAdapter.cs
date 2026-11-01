using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>A deliberately changed registration used only to prove refusal before intent creation.</summary>
/// <param name="original">The unchanged legacy intent implementation.</param>
/// <param name="declaration">The deliberately changed owner policy.</param>
internal sealed class DeclaredFoldersIntentAdapter(IIdempotencyIntentAdapter original, CommandIntentDeclaration declaration)
    : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public string CommandType => declaration.Route.MessageType!;
    /// <inheritdoc/>
    public string AdapterId { get; init; } = declaration.AdapterId;
    /// <inheritdoc/>
    public string OperationId { get; init; } = declaration.OperationId;
    /// <inheritdoc/>
    public int DescriptorVersion { get; init; } = declaration.DescriptorSchema.Version;
    /// <inheritdoc/>
    public IdempotencyReplayRetentionTier RetentionTier { get; init; } = declaration.RetentionTier;
    /// <inheritdoc/>
    public CommandIntentDeclaration Declaration => declaration;
    /// <summary>Gets the number of intent creation calls after attempted admission.</summary>
    public int CreateIntentCalls { get; private set; }

    /// <inheritdoc/>
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        CreateIntentCalls++;
        return original.CreateIntent(command);
    }
}
