namespace Hexalith.Folders.Queries.Audit;

public interface IAuditRecordReadModel
{
    Task<AuditRecordReadModelResult> GetAsync(
        AuditRecordReadModelRequest request,
        CancellationToken cancellationToken = default);
}
