using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class WorkspaceFileMutationLeaseClock(DateTimeOffset initial) : TimeProvider
{
    public DateTimeOffset NowValue { get; set; } = initial;
    public override DateTimeOffset GetUtcNow() => NowValue;
}
