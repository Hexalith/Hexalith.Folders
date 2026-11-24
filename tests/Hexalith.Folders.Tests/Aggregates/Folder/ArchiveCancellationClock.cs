namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class ArchiveCancellationClock : TimeProvider
{
    internal Action? OnRead { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        OnRead?.Invoke();
        return DateTimeOffset.UtcNow;
    }
}
