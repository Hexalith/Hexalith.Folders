using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

internal sealed class HistoricalMutationAllowingDaprPolicy : IDaprPolicyEvidenceProvider
{
    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DaprPolicyEvidenceResult.Allowed(request.TargetAppId, "policy:1"));
}
