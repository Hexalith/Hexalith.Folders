using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Server;

public sealed class FoldersTenantEventHandler(
    FolderTenantAccessHandler handler,
    ILogger<FoldersTenantEventHandler>? logger = null) :
    ITenantEventHandler<TenantCreated>,
    ITenantEventHandler<TenantUpdated>,
    ITenantEventHandler<TenantDisabled>,
    ITenantEventHandler<TenantEnabled>,
    ITenantEventHandler<UserAddedToTenant>,
    ITenantEventHandler<UserRemovedFromTenant>,
    ITenantEventHandler<UserRoleChanged>,
    ITenantEventHandler<TenantConfigurationSet>,
    ITenantEventHandler<TenantConfigurationRemoved>
{
    private const char FieldSeparator = '';
    private const string NullMarker = "null";

    public Task HandleAsync(TenantCreated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantCreated, @event.TenantId, context, fingerprintParts: [@event.Name]), cancellationToken);
    }

    public Task HandleAsync(TenantUpdated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantUpdated, @event.TenantId, context, fingerprintParts: [@event.Name]), cancellationToken);
    }

    public Task HandleAsync(TenantDisabled @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantDisabled, @event.TenantId, context), cancellationToken);
    }

    public Task HandleAsync(TenantEnabled @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantEnabled, @event.TenantId, context), cancellationToken);
    }

    public Task HandleAsync(UserAddedToTenant @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.UserAddedToTenant,
            @event.TenantId,
            context,
            principalId: @event.UserId,
            role: @event.Role.ToString(),
            fingerprintParts: [@event.UserId, @event.Role.ToString()]), cancellationToken);
    }

    public Task HandleAsync(UserRemovedFromTenant @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.UserRemovedFromTenant,
            @event.TenantId,
            context,
            principalId: @event.UserId,
            fingerprintParts: [@event.UserId]), cancellationToken);
    }

    public Task HandleAsync(UserRoleChanged @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.UserRoleChanged,
            @event.TenantId,
            context,
            principalId: @event.UserId,
            role: @event.NewRole.ToString(),
            previousRole: @event.OldRole.ToString(),
            fingerprintParts: [@event.UserId, @event.OldRole.ToString(), @event.NewRole.ToString()]), cancellationToken);
    }

    public Task HandleAsync(TenantConfigurationSet @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.TenantConfigurationSet,
            @event.TenantId,
            context,
            configurationKey: @event.Key,
            fingerprintParts: [@event.Key]), cancellationToken);
    }

    public Task HandleAsync(TenantConfigurationRemoved @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.TenantConfigurationRemoved,
            @event.TenantId,
            context,
            configurationKey: @event.Key,
            fingerprintParts: [@event.Key]), cancellationToken);
    }

    private FolderTenantAccessEvent ToProjectionEvent(
        FolderTenantAccessEventKind kind,
        string eventTenantId,
        TenantEventContext context,
        string? principalId = null,
        string? role = null,
        string? previousRole = null,
        string? configurationKey = null,
        string?[]? fingerprintParts = null)
    {
        string projectionTenantId = eventTenantId;
        if (!string.Equals(context.TenantId, eventTenantId, StringComparison.Ordinal))
        {
            logger?.LogWarning(
                "Tenant envelope mismatch: envelope TenantId={EnvelopeTenantId} differs from payload TenantId={PayloadTenantId} for event {EventKind} (MessageId={MessageId}); event will be dropped.",
                context.TenantId, eventTenantId, kind, context.MessageId);
            projectionTenantId = string.Empty;
        }

        return new FolderTenantAccessEvent(
            kind,
            projectionTenantId,
            context.MessageId,
            context.SequenceNumber,
            context.Timestamp,
            context.CorrelationId,
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
