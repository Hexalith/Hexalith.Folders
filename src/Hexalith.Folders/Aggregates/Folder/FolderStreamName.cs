using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed partial record FolderStreamName(string Value)
{
    internal const int MaxSegmentLength = StreamNameSegment.MaxLength;
    public const string ReservedOrganizationPermissionScope = "organization_baseline";

    public static FolderStreamName Create(string managedTenantId, string folderId)
    {
        if (!TryCreate(managedTenantId, folderId, out FolderStreamName? streamName, out FolderResultCode code))
        {
            // Attribute the exception to whichever segment actually failed so callers get
            // a meaningful `ParamName` instead of always seeing `managedTenantId`.
            string paramName = code switch
            {
                FolderResultCode.InvalidFolderId => nameof(folderId),
                _ => nameof(managedTenantId),
            };
            throw new ArgumentException($"Invalid folder stream name: {code}.", paramName);
        }

        return streamName!;
    }

    public static bool TryCreate(
        string? managedTenantId,
        string? folderId,
        out FolderStreamName? streamName,
        out FolderResultCode code)
    {
        streamName = null;

        if (!TryValidateTenant(managedTenantId, out code))
        {
            return false;
        }

        if (!IsValidSegment(folderId)
            || string.Equals(folderId, managedTenantId, StringComparison.Ordinal)
            || ProviderBindingActorId.IsReserved(folderId)
            || string.Equals(folderId, ReservedOrganizationPermissionScope, StringComparison.Ordinal))
        {
            code = FolderResultCode.InvalidFolderId;
            return false;
        }

        streamName = new FolderStreamName($"{managedTenantId}:folders:{folderId}");
        code = FolderResultCode.Created;
        return true;
    }

    /// <summary>Validates a managed tenant without constructing an unrelated aggregate identifier.</summary>
    internal static bool TryValidateTenant(string? managedTenantId, out FolderResultCode code)
    {
        code = IsReservedSystemTenant(managedTenantId) ? FolderResultCode.ReservedTenant
            : !IsValidSegment(managedTenantId) || ProviderBindingActorId.IsReserved(managedTenantId)
                || string.Equals(managedTenantId, ReservedOrganizationPermissionScope, StringComparison.Ordinal)
                ? FolderResultCode.InvalidTenant : FolderResultCode.Created;
        return code == FolderResultCode.Created;
    }

    // Reserved-name check uses ordinal-equals on the raw input so whitespace and casing
    // surface as InvalidTenant via IsValidSegment, not as ReservedTenant via Trim().
    // This keeps the rejection-code surface deterministic and prevents differential
    // disclosure of the reserved-name list through whitespace probing.
    internal static bool IsReservedSystemTenant(string? managedTenantId)
        => !string.IsNullOrEmpty(managedTenantId)
            && string.Equals(managedTenantId, "system", StringComparison.Ordinal);

    internal static bool IsValidSegment(string? value)
        => StreamNameSegment.IsValid(value);
}
