namespace Hexalith.Folders.Queries.Folders;

public interface ITaskStatusReadModel
{
    Task<TaskStatusReadModelResult> GetAsync(
        TaskStatusReadModelRequest request,
        CancellationToken cancellationToken = default);
}
