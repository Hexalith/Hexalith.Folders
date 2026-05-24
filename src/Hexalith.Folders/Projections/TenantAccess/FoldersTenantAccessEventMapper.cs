using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FoldersTenantAccessEventMapper(ILogger<FoldersTenantAccessEventMapper>? logger = null)
{
    private const char FieldSeparator = '\u001F';
    private const string NullMarker = "\u0001null\u0001";

    public FolderTenantAccessEvent Map(
        FolderTenantAccessEventKind kind,
        string eventTenantId,
        string envelopeTenantId,
        string messageId,
        long sequenceNumber,
        DateTimeOffset timestamp,
        string correlationId,
        string? principalId = null,
        string? role = null,
        string? previousRole = null,
        string? configurationKey = null,
        string?[]? fingerprintParts = null)
    {
        string projectionTenantId = eventTenantId;
        if (!string.Equals(envelopeTenantId, eventTenantId, StringComparison.Ordinal))
        {
            logger?.LogWarning(
                "Tenant envelope mismatch: envelope TenantId={EnvelopeTenantId} differs from payload TenantId={PayloadTenantId} for event {EventKind} (MessageId={MessageId}); event will be dropped.",
                envelopeTenantId,
                eventTenantId,
                kind,
                messageId);
            projectionTenantId = string.Empty;
        }

        return new FolderTenantAccessEvent(
            kind,
            projectionTenantId,
            messageId,
            sequenceNumber,
            timestamp,
            correlationId,
            principalId,
            role,
            previousRole,
            configurationKey,
            FingerprintHash(fingerprintParts));
    }

    private static string FingerprintHash(string?[]? parts)
    {
        if (parts is null || parts.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder canonical = new();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                _ = canonical.Append(FieldSeparator);
            }

            _ = canonical.Append(parts[i] is null ? NullMarker : parts[i]);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
