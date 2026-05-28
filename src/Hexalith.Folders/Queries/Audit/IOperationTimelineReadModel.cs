namespace Hexalith.Folders.Queries.Audit;

public interface IOperationTimelineReadModel
{
    Task<OperationTimelineReadModelResult> GetAsync(
        OperationTimelineReadModelRequest request,
        CancellationToken cancellationToken = default);
}
