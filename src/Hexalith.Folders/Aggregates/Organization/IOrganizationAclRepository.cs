namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationAclRepository
{
    OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId);

    OrganizationState Load(OrganizationStreamName streamName);

    void Append(OrganizationStreamName streamName, IReadOnlyList<IOrganizationAclEvent> events);

    bool TryGetIdempotencyFingerprint(
        string managedTenantId,
        string organizationId,
        string idempotencyKey,
        out string? fingerprint);
}
