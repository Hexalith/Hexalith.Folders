using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>An otherwise valid legacy registration that lacks declared admission.</summary>
/// <param name="original">The unchanged legacy intent implementation.</param>
internal sealed class UndeclaredFoldersIntentAdapter(IIdempotencyIntentAdapter original) : IIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public string CommandType => original.CommandType;
    /// <inheritdoc/>
    public string AdapterId => original.AdapterId;
    /// <inheritdoc/>
    public string OperationId => original.OperationId;
    /// <inheritdoc/>
    public int DescriptorVersion => original.DescriptorVersion;
    /// <inheritdoc/>
    public IdempotencyReplayRetentionTier RetentionTier => original.RetentionTier;
    /// <inheritdoc/>
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command) => original.CreateIntent(command);
}
