namespace Hexalith.Folders.Authorization;

public sealed record EventStoreClaimTransformEvidence(
    string? TenantId,
    string? PrincipalId,
    IReadOnlySet<string> PermissionTokens,
    bool IsPresent,
    bool Malformed)
{
    public static EventStoreClaimTransformEvidence Allowed(
        string? tenantId,
        string? principalId,
        IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return new(
            tenantId,
            principalId,
            permissions
                .Where(static permission => !string.IsNullOrWhiteSpace(permission))
                .Select(static permission => permission.Trim())
                .ToHashSet(StringComparer.Ordinal),
            IsPresent: true,
            Malformed: false);
    }

    public static EventStoreClaimTransformEvidence Missing()
        => new(null, null, new HashSet<string>(StringComparer.Ordinal), IsPresent: false, Malformed: false);

    public static EventStoreClaimTransformEvidence MalformedEvidence()
        => new(null, null, new HashSet<string>(StringComparer.Ordinal), IsPresent: true, Malformed: true);

    public bool HasPermissionFor(string actionToken)
        => PermissionTokens.Contains(actionToken)
            || PermissionTokens.Contains("*")
            || PermissionTokens.Contains("folders:*")
            || PermissionTokens.Contains("commands:*");
}
