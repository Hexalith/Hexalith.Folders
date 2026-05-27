namespace Hexalith.Folders.Aggregates.Folder;

public sealed class UnavailableWorkspacePathPolicyEvidenceProvider : IWorkspacePathPolicyEvidenceProvider
{
    public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
        WorkspacePathPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.Unavailable));
}
