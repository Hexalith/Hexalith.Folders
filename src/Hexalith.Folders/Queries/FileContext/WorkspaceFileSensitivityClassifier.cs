using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

public sealed class WorkspaceFileSensitivityClassifier : IWorkspaceFileSensitivityClassifier
{
    public Task<WorkspacePathSensitivityResult> ClassifyAsync(
        PathMetadata path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (path.PathPolicyClass.Contains("secret", StringComparison.Ordinal)
            || path.PathPolicyClass.Contains("credential", StringComparison.Ordinal)
            || path.PathPolicyClass.Contains("redacted", StringComparison.Ordinal))
        {
            return Task.FromResult(WorkspacePathSensitivityResult.Redacted());
        }

        return Task.FromResult(WorkspacePathSensitivityResult.Allowed());
    }
}
