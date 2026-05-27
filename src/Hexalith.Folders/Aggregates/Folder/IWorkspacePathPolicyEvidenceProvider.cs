namespace Hexalith.Folders.Aggregates.Folder;

public interface IWorkspacePathPolicyEvidenceProvider
{
    Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
        WorkspacePathPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default);
}
