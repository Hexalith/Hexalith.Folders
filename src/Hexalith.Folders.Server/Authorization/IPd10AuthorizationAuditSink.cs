namespace Hexalith.Folders.Server.Authorization;

/// <summary>Writes direct PD10 candidate authorization decisions.</summary>
public interface IPd10AuthorizationAuditSink
{
    /// <summary>Writes exactly one decision record.</summary>
    ValueTask WriteAsync(Pd10AuthorizationAuditRecord record, CancellationToken cancellationToken);
}
