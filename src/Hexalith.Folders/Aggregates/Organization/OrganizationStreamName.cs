namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationStreamName(string Value)
{
    public static OrganizationStreamName Create(string managedTenantId, string organizationId)
    {
        if (!TryCreate(managedTenantId, organizationId, out OrganizationStreamName? streamName, out OrganizationAclResultCode code))
        {
            throw new ArgumentException($"Invalid organization stream name: {code}.", nameof(managedTenantId));
        }

        return streamName!;
    }

    public static bool TryCreate(
        string? managedTenantId,
        string? organizationId,
        out OrganizationStreamName? streamName,
        out OrganizationAclResultCode code)
    {
        streamName = null;

        if (IsReservedSystemTenant(managedTenantId))
        {
            code = OrganizationAclResultCode.ReservedTenant;
            return false;
        }

        if (!IsValidSegment(managedTenantId))
        {
            code = OrganizationAclResultCode.InvalidTenant;
            return false;
        }

        if (!IsValidSegment(organizationId))
        {
            code = OrganizationAclResultCode.InvalidOrganization;
            return false;
        }

        streamName = new OrganizationStreamName($"{managedTenantId}:organizations:{organizationId}");
        code = OrganizationAclResultCode.Accepted;
        return true;
    }

    internal static bool IsReservedSystemTenant(string? managedTenantId)
        => !string.IsNullOrWhiteSpace(managedTenantId)
            && string.Equals(managedTenantId.Trim(), "system", StringComparison.OrdinalIgnoreCase);

    internal static bool IsValidSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
            && !value.Any(static character => character == ':' || char.IsControl(character));
}
