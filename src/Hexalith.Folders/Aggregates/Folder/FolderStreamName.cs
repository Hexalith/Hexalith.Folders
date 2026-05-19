using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed partial record FolderStreamName(string Value)
{
    internal const int MaxSegmentLength = 256;

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

    // Reserved-name check uses ordinal-equals on the raw input so whitespace and casing
    // surface as InvalidTenant via IsValidSegment, not as ReservedTenant via Trim().
    // This keeps the rejection-code surface deterministic and prevents differential
    // disclosure of the reserved-name list through whitespace probing.
    internal static bool IsReservedSystemTenant(string? managedTenantId)
        => !string.IsNullOrEmpty(managedTenantId)
            && string.Equals(managedTenantId, "system", StringComparison.Ordinal);

    internal static bool IsValidSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxSegmentLength
            && CanonicalSegmentPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSegmentPattern();
}
