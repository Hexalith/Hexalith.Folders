namespace Hexalith.Folders.Observability;

public sealed class NoOpFolderAuditObserver : IFolderAuditObserver
{
    public ValueTask ObserveAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return ValueTask.CompletedTask;
    }
}
