namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclCommandValidationResult(
    OrganizationAclResultCode Code,
    IReadOnlyList<OrganizationAclOperation> Operations,
    string IdempotencyFingerprint,
    OrganizationAclOperation? FailingOperation = null)
{
    public bool IsAccepted => Code == OrganizationAclResultCode.Accepted;

    public static OrganizationAclCommandValidationResult Accepted(
        IReadOnlyList<OrganizationAclOperation> operations,
        string fingerprint)
        => new(OrganizationAclResultCode.Accepted, operations, fingerprint);

    public static OrganizationAclCommandValidationResult Rejected(OrganizationAclResultCode code)
        => new(code, [], string.Empty);

    public static OrganizationAclCommandValidationResult Rejected(
        OrganizationAclResultCode code,
        OrganizationAclOperation failingOperation)
        => new(code, [], string.Empty, failingOperation);
}
