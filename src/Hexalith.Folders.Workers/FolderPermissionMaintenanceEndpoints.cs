using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Workers;

/// <summary>Exposes token-authenticated conditional reset of derived permission rows to authorized operators.</summary>
internal static partial class FolderPermissionMaintenanceEndpoints
{
    /// <summary>Maps maintenance without adding a Dapr service caller or topic subscription.</summary>
    internal static void MapFolderPermissionMaintenance(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/maintenance/folder-permissions/reset", ResetAsync)
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);

    private static async Task<IResult> ResetAsync(FolderPermissionResetRequest request,
        IReadModelConditionalEraser eraser, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!FolderStreamName.TryValidateTenant(request.TenantId, out _)
            || request.AggregateId != request.TenantId
                && !FolderStreamName.TryCreate(request.TenantId, request.AggregateId, out _, out _)
            || request.ExpectedEtag is not null && string.IsNullOrWhiteSpace(request.ExpectedEtag))
        {
            return Results.BadRequest();
        }

        bool organization = request.AggregateId == request.TenantId;
        string storeName = organization ? EventStoreOrganizationEffectivePermissionsReadModel.StoreName
            : EventStoreFolderEffectivePermissionsReadModel.StoreName;
        string key = organization ? EventStoreOrganizationEffectivePermissionsReadModel.Key(request.TenantId)
            : EventStoreFolderEffectivePermissionsReadModel.Key(request.TenantId, request.AggregateId);
        ILogger logger = loggerFactory.CreateLogger(typeof(FolderPermissionMaintenanceEndpoints));
        try
        {
            if (request.ExpectedEtag is null)
            {
                (bool present, string etag) = await eraser.TryReadEtagAsync(storeName, key,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { present, etag });
            }

            if (!await eraser.TryEraseAsync(storeName, key, request.ExpectedEtag,
                cancellationToken).ConfigureAwait(false))
            {
                ResetOutcome(logger, request.TenantId, request.AggregateId, "etag_conflict");
                return Results.Conflict();
            }

            (bool remaining, _) = await eraser.TryReadEtagAsync(storeName, key,
                cancellationToken).ConfigureAwait(false);
            ResetOutcome(logger, request.TenantId, request.AggregateId, remaining ? "row_remaining" : "erased");
            return remaining ? Results.Conflict() : Results.NoContent();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            ResetUnavailable(logger, request.TenantId, request.AggregateId, "unavailable");
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information,
        Message = "Permission projection reset tenant={TenantId} aggregate={AggregateId} outcome={Outcome}")]
    private static partial void ResetOutcome(ILogger logger, string tenantId, string aggregateId, string outcome);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Warning,
        Message = "Permission projection reset tenant={TenantId} aggregate={AggregateId} outcome={Outcome}")]
    private static partial void ResetUnavailable(ILogger logger, string tenantId, string aggregateId, string outcome);
}
