using System.Globalization;

using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.Services.SupportSafety;

namespace Hexalith.Tenants.UI.State.TenantAudit;

/// <summary>Contains the typed, support-safe audit narrative fields used by UI behavior.</summary>
/// <param name="UserId">The support-safe affected user identifier.</param>
/// <param name="ConfigurationKey">The support-safe configuration key.</param>
/// <param name="Role">The supported role carried by the event.</param>
/// <param name="OldRole">The supported role before a role change.</param>
/// <param name="NewRole">The supported role after a role change.</param>
/// <param name="PreviousRole">The supported previous role carried by the event.</param>
/// <param name="Timestamp">The optional support-safe narrative timestamp.</param>
/// <param name="OccurredAt">The optional support-safe occurrence timestamp.</param>
public sealed record TenantAuditNarrative(
    string? UserId = null,
    string? ConfigurationKey = null,
    TenantRole? Role = null,
    TenantRole? OldRole = null,
    TenantRole? NewRole = null,
    TenantRole? PreviousRole = null,
    DateTimeOffset? Timestamp = null,
    DateTimeOffset? OccurredAt = null)
{
    /// <summary>Builds typed narrative evidence from the allow-listed response fields.</summary>
    /// <param name="payload">The structured response narrative.</param>
    /// <returns>Typed support-safe narrative evidence.</returns>
    internal static TenantAuditNarrative FromPayload(IReadOnlyDictionary<string, string>? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return new();
        }

        return new(
            UserId: SafeIdentifier(payload, "userId"),
            ConfigurationKey: SafeConfigurationKey(payload, "key"),
            Role: SafeRole(payload, "role"),
            OldRole: SafeRole(payload, "oldRole"),
            NewRole: SafeRole(payload, "newRole"),
            PreviousRole: SafeRole(payload, "previousRole"),
            Timestamp: SafeTimestamp(payload, "timestamp"),
            OccurredAt: SafeTimestamp(payload, "occurredAt"));
    }

    /// <summary>Formats the sanitized narrative fields for support-safe display only.</summary>
    /// <returns>A display string that is never reparsed for behavior.</returns>
    public string ToDisplayString()
    {
        List<string> fields = [];
        Add(fields, "userId", TenantAuditSupportSafety.SafeIdentifier(UserId, SupportSafeCopyValueKind.UserId));
        Add(fields, "key", TenantAuditSupportSafety.SafeIdentifier(ConfigurationKey, SupportSafeCopyValueKind.ConfigurationKey));
        Add(fields, "role", Role?.ToString());
        Add(fields, "oldRole", OldRole?.ToString());
        Add(fields, "newRole", NewRole?.ToString());
        Add(fields, "previousRole", PreviousRole?.ToString());
        Add(fields, "timestamp", Timestamp?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(fields, "occurredAt", OccurredAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return string.Join("; ", fields);
    }

    private static void Add(ICollection<string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add($"{key}: {value}");
        }
    }

    private static string? SafeIdentifier(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out string? value)
            ? NullIfEmpty(TenantAuditSupportSafety.SafeIdentifier(value, SupportSafeCopyValueKind.UserId))
            : null;

    private static string? SafeConfigurationKey(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out string? value)
            ? NullIfEmpty(TenantAuditSupportSafety.SafeIdentifier(value, SupportSafeCopyValueKind.ConfigurationKey))
            : null;

    private static TenantRole? SafeRole(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out string? value)
            && Enum.TryParse(value, ignoreCase: false, out TenantRole role)
            && role is TenantRole.TenantOwner or TenantRole.TenantContributor or TenantRole.TenantReader
                ? role
                : null;

    private static DateTimeOffset? SafeTimestamp(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out string? value)
            && DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset timestamp)
                ? timestamp
                : null;

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
