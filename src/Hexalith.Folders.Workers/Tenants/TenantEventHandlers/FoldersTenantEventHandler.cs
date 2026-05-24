using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Workers.Tenants.TenantEventHandlers;

public sealed class FoldersTenantEventHandler(
    FolderTenantAccessHandler handler,
    FoldersTenantAccessEventMapper mapper,
    IOptions<FoldersTenantEventOptions> options,
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
    public Task HandleAsync(TenantCreated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantCreated, @event.TenantId, context, fingerprintParts: [@event.Name]), cancellationToken);
    }

    public Task HandleAsync(TenantUpdated @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantUpdated, @event.TenantId, context, fingerprintParts: [@event.Name]), cancellationToken);
    }

    public Task HandleAsync(TenantDisabled @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantDisabled, @event.TenantId, context), cancellationToken);
    }

    public Task HandleAsync(TenantEnabled @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return HandleAsync(ToProjectionEvent(FolderTenantAccessEventKind.TenantEnabled, @event.TenantId, context), cancellationToken);
    }

    public Task HandleAsync(UserAddedToTenant @event, TenantEventContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return HandleAsync(ToProjectionEvent(
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
        return HandleAsync(ToProjectionEvent(
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
        return HandleAsync(ToProjectionEvent(
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
        return HandleAsync(ToProjectionEvent(
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
        return HandleAsync(ToProjectionEvent(
            FolderTenantAccessEventKind.TenantConfigurationRemoved,
            @event.TenantId,
            context,
            configurationKey: @event.Key,
            fingerprintParts: [@event.Key]), cancellationToken);
    }

    private Task HandleAsync(FolderTenantAccessEvent @event, CancellationToken cancellationToken)
    {
        if (options.Value.ProjectionWriter != FoldersTenantEventProjectionWriter.Workers)
        {
            logger?.LogDebug(
                "Skipping folders tenant-event projection in Workers host because ProjectionWriter={ProjectionWriter}.",
                options.Value.ProjectionWriter);
            return Task.CompletedTask;
        }

        return handler.HandleAsync(@event, cancellationToken);
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
        => mapper.Map(
            kind,
            eventTenantId,
            context.TenantId,
            context.MessageId,
            context.SequenceNumber,
            context.Timestamp,
            context.CorrelationId,
            principalId,
            role,
            previousRole,
            configurationKey,
            fingerprintParts);
}
