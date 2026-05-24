namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationProviderBindingRepository
{
    OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId);

    OrganizationState Load(OrganizationStreamName streamName);

    OrganizationAclAppendOutcome AppendIfFingerprintAbsent(
        OrganizationStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IOrganizationEvent> events);

    bool TryGetIdempotencyFingerprint(
        OrganizationStreamName streamName,
        string idempotencyKey,
        out string? fingerprint);
}
