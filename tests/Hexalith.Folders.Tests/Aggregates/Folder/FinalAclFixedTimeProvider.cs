namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class FinalAclFixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
