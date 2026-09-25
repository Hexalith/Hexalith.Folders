using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class FinalAclAllowingDapr : IDaprPolicyEvidenceProvider
{
    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
}
