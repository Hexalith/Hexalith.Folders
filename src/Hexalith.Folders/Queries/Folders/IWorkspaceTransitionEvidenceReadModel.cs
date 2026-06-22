namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Read model exposing the most recent workspace lifecycle transition evidence. Production hosts populate
/// it from a transition projection; the in-memory implementation is seeded directly for dev/test.
/// </summary>
public interface IWorkspaceTransitionEvidenceReadModel
{
    /// <summary>
    /// Reads the transition evidence for a workspace.
    /// </summary>
    /// <param name="request">The read-model request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null when no evidence exists for the workspace.</returns>
    Task<WorkspaceTransitionEvidenceSnapshot?> GetAsync(
        WorkspaceTransitionEvidenceReadModelRequest request,
        CancellationToken cancellationToken = default);
}
