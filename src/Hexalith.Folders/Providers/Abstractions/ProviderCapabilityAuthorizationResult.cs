namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityAuthorizationResult(
    bool IsAllowed,
    ProviderAuthorizationEvidenceSnapshot? Snapshot,
    ProviderFailureCategory FailureCategory,
    string ReasonCode)
{
    public static ProviderCapabilityAuthorizationResult Allowed(ProviderAuthorizationEvidenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(true, snapshot, ProviderFailureCategory.None, "allowed");
    }

    public static ProviderCapabilityAuthorizationResult Denied(
        ProviderFailureCategory failureCategory,
        string reasonCode)
        => new(false, null, failureCategory, reasonCode);
}
