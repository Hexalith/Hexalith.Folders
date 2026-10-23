using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Aggregates.Organization;

public sealed partial record OrganizationStreamName(string Value)
{
    internal const int MaxSegmentLength = 256;

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

        if (!IsValidManagedTenant(managedTenantId))
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

    /// <summary>Validates the tenant namespace shared by organization stream and command validators.</summary>
    internal static bool IsValidManagedTenant(string? value)
        => IsValidSegment(value) && !ProviderBindingActorId.IsReserved(value)
            && !string.Equals(value, FolderStreamName.ReservedOrganizationPermissionScope, StringComparison.Ordinal);

    internal static bool IsValidSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxSegmentLength
            && (CanonicalSegmentPattern().IsMatch(value)
                || PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(value));

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSegmentPattern();
}
