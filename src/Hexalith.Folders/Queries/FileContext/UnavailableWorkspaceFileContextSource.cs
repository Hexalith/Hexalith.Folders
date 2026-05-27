using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.FileContext;

public sealed class UnavailableWorkspaceFileContextSource(IUtcClock clock) : IWorkspaceFileContextSource
{
    public Task<WorkspaceFileContextSourceResult> QueryAsync(
        WorkspaceFileContextSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(WorkspaceFileContextSourceResult.Unavailable(clock.UtcNow));
    }
}
