using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Tests.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

internal static class OperationalStatusFixture
{
    public static IReadOnlyList<IFolderEvent> SuccessfulLifecycle()
    {
        // The older general-purpose fixture hard-codes a release ID independently of
        // its aggregate-generated lock. Construct a consistent synthetic source here;
        // no retained fixture or historical event bytes are changed.
        IFolderEvent[] history = FolderLifecycleReplayFixture.SuccessfulLifecycle().ToArray();
        WorkspaceLockAcquired acquired = history.OfType<WorkspaceLockAcquired>().Single();
        history[^1] = ((WorkspaceLockReleased)history[^1]) with { LockId = acquired.LockId };
        return history;
    }
}
