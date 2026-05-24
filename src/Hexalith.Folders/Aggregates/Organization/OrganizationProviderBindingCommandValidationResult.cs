namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationProviderBindingCommandValidationResult(
    bool IsAccepted,
    OrganizationProviderBindingResultCode Code,
    string IdempotencyFingerprint)
{
    public static OrganizationProviderBindingCommandValidationResult Accepted(string idempotencyFingerprint)
        => new(true, OrganizationProviderBindingResultCode.Accepted, idempotencyFingerprint);

    public static OrganizationProviderBindingCommandValidationResult Rejected(OrganizationProviderBindingResultCode code)
        => new(false, code, string.Empty);
}
