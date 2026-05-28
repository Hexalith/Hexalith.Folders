namespace Hexalith.Folders.Queries.Audit;

public interface IOperationTimelineEntryReadModel
{
    Task<OperationTimelineEntryReadModelResult> GetAsync(
        OperationTimelineEntryReadModelRequest request,
        CancellationToken cancellationToken = default);
}
