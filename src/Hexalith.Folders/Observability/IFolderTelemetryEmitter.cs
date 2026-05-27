namespace Hexalith.Folders.Observability;

public interface IFolderTelemetryEmitter
{
    ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default);
}
