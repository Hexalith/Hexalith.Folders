using System.Text.Json.Serialization;

namespace Hexalith.Folders.Authorization;

public sealed record TenantAccessAuthorizationResult(
    TenantAccessOutcome Outcome,
    string Code,
    string? TenantId,
    string? ProjectionWatermark,
    DateTimeOffset? LastEventTimestamp,
    TimeSpan? ProjectionAge,
    TenantProjectionFreshnessStatus FreshnessStatus,
    string Source)
{
    [JsonIgnore]
    public bool IsAllowed => Outcome == TenantAccessOutcome.Allowed;
}
