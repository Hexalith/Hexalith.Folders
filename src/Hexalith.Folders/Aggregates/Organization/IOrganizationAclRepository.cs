namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationAclRepository
{
    OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId);

    OrganizationState Load(OrganizationStreamName streamName);

    OrganizationAclAppendOutcome AppendIfFingerprintAbsent(
        OrganizationStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IOrganizationAclEvent> events);

    bool TryGetIdempotencyFingerprint(
        string managedTenantId,
        string organizationId,
        string idempotencyKey,
        out string? fingerprint);
}
