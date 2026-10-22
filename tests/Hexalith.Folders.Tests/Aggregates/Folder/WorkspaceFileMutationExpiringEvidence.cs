using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class WorkspaceFileMutationExpiringEvidence(WorkspaceFileMutationLeaseClock clock) : IWorkspacePathPolicyEvidenceProvider
{
    public int Calls { get; private set; }
    public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(WorkspacePathPolicyEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (++Calls == 2) clock.NowValue = clock.NowValue.AddHours(1);
        return Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.NoEscape) { PolicyVersion = "1.1.0", PolicyDigest = "policy" });
    }
}
