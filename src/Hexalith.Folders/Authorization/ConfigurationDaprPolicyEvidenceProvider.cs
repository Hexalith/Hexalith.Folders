namespace Hexalith.Folders.Authorization;

public sealed class ConfigurationDaprPolicyEvidenceProvider(DaprPolicyEvidenceOptions options) : IDaprPolicyEvidenceProvider
{
    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool required = request.RequiresPolicyEvidence || options.RequirePolicyEvidence;
        if (!required)
        {
            return Task.FromResult(DaprPolicyEvidenceResult.Allowed(request.TargetAppId, "dapr_policy_not_required"));
        }

        if (!options.Enabled)
        {
            return Task.FromResult(DaprPolicyEvidenceResult.Unavailable("dapr_policy_disabled"));
        }

        if (!Contains(options.AllowedTargetAppIds, request.TargetAppId)
            || !Contains(options.AllowedServiceInvocationClasses, request.ServiceInvocationClass))
        {
            return Task.FromResult(DaprPolicyEvidenceResult.Denied(request.TargetAppId));
        }

        return Task.FromResult(DaprPolicyEvidenceResult.Allowed(request.TargetAppId, "dapr_policy_configured"));
    }

    private static bool Contains(IEnumerable<string> values, string value)
        => values.Any(candidate => string.Equals(candidate, value, StringComparison.Ordinal));
}
