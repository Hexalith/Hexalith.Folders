using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed partial record FolderStreamName(string Value)
{
    internal const int MaxSegmentLength = 256;

    public static FolderStreamName Create(string managedTenantId, string folderId)
    {
        if (!TryCreate(managedTenantId, folderId, out FolderStreamName? streamName, out FolderResultCode code))
        {
            throw new ArgumentException($"Invalid folder stream name: {code}.", nameof(managedTenantId));
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

        if (IsReservedSystemTenant(managedTenantId))
        {
            code = FolderResultCode.ReservedTenant;
            return false;
        }

        if (!IsValidSegment(managedTenantId))
        {
            code = FolderResultCode.InvalidTenant;
            return false;
        }

        if (!IsValidSegment(folderId))
        {
            code = FolderResultCode.InvalidFolderId;
            return false;
        }

        streamName = new FolderStreamName($"{managedTenantId}:folders:{folderId}");
        code = FolderResultCode.Created;
        return true;
    }

    internal static bool IsReservedSystemTenant(string? managedTenantId)
        => !string.IsNullOrWhiteSpace(managedTenantId)
            && string.Equals(managedTenantId.Trim(), "system", StringComparison.OrdinalIgnoreCase);

    internal static bool IsValidSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxSegmentLength
            && CanonicalSegmentPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSegmentPattern();
}
