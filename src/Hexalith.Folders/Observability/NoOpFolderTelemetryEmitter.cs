namespace Hexalith.Folders.Observability;

public sealed class NoOpFolderTelemetryEmitter : IFolderTelemetryEmitter
{
    public ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return ValueTask.CompletedTask;
    }
}
