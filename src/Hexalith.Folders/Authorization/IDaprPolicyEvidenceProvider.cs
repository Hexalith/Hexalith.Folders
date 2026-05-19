namespace Hexalith.Folders.Authorization;

public interface IDaprPolicyEvidenceProvider
{
    Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default);
}
