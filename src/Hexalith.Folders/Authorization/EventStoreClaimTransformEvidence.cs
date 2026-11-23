using System.Collections.Frozen;

namespace Hexalith.Folders.Authorization;

public sealed record EventStoreClaimTransformEvidence(
    string? TenantId,
    string? PrincipalId,
    IReadOnlySet<string> PermissionTokens,
    bool IsPresent,
    bool Malformed)
{
    /// <summary>Gets a snapshot of the permissions using ordinal token comparison.</summary>
    public IReadOnlySet<string> PermissionTokens
    {
        get;
        init => field = value.ToFrozenSet(StringComparer.Ordinal);
    } = PermissionTokens.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the verified delegating principal's claims when this is a delegated transport identity.</summary>
    public EventStoreClaimTransformEvidence? Delegator { get; init; }

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
                .ToFrozenSet(StringComparer.Ordinal),
            IsPresent: true,
            Malformed: false);
    }

    public static EventStoreClaimTransformEvidence Missing()
        => new(null, null, FrozenSet<string>.Empty, IsPresent: false, Malformed: false);

    public static EventStoreClaimTransformEvidence MalformedEvidence()
        => new(null, null, FrozenSet<string>.Empty, IsPresent: true, Malformed: true);

    public bool HasPermissionFor(string actionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);
        return PermissionTokens.Contains(actionToken.Trim());
    }
}
