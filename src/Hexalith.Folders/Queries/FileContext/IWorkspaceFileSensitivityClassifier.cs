using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

public interface IWorkspaceFileSensitivityClassifier
{
    Task<WorkspacePathSensitivityResult> ClassifyAsync(
        PathMetadata path,
        CancellationToken cancellationToken = default);
}
