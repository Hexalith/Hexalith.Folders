namespace Hexalith.Folders.Queries.Audit;

public interface IAuditTrailReadModel
{
    Task<AuditTrailReadModelResult> GetAsync(
        AuditTrailReadModelRequest request,
        CancellationToken cancellationToken = default);
}
