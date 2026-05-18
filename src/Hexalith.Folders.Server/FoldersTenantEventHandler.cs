using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Contracts.Events;

namespace Hexalith.Folders.Server;

public sealed class FoldersTenantEventHandler(FolderTenantAccessHandler handler) :
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
    public Task HandleAsync(TenantCreated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantCreated, @event.TenantId, context, payloadFingerprint: @event.Name), cancellationToken);
    }

    public Task HandleAsync(TenantUpdated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return handler.HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantUpdated, @event.TenantId, context, payloadFingerprint: @event.Name), cancellationToken);
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
            payloadFingerprint: string.Join('|', @event.UserId, @event.Role)), cancellationToken);
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
            payloadFingerprint: @event.UserId), cancellationToken);
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
            payloadFingerprint: string.Join('|', @event.UserId, @event.OldRole, @event.NewRole)), cancellationToken);
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
            payloadFingerprint: @event.Key), cancellationToken);
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
            payloadFingerprint: @event.Key), cancellationToken);
    }

    private static FolderTenantAccessEvent ToProjectionEvent(
        FolderTenantAccessEventKind kind,
        string eventTenantId,
        TenantEventContext context,
        string? principalId = null,
        string? role = null,
        string? previousRole = null,
        string? configurationKey = null,
        string? payloadFingerprint = null)
        => new(
            kind,
            string.Equals(context.TenantId, eventTenantId, StringComparison.Ordinal) ? eventTenantId : string.Empty,
            context.MessageId,
            context.SequenceNumber,
            context.Timestamp,
            context.CorrelationId,
            principalId,
            role,
            previousRole,
            configurationKey,
            payloadFingerprint ?? eventTenantId);
}
