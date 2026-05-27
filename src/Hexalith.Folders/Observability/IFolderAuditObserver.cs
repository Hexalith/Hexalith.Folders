namespace Hexalith.Folders.Observability;

public interface IFolderAuditObserver
{
    ValueTask ObserveAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default);
}
