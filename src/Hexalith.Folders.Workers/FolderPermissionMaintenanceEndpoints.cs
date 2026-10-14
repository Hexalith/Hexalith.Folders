using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Workers;

/// <summary>Exposes token-authenticated conditional reset of derived permission rows to authorized operators.</summary>
internal static class FolderPermissionMaintenanceEndpoints
{
    /// <summary>Maps maintenance without adding a Dapr service caller or topic subscription.</summary>
    internal static void MapFolderPermissionMaintenance(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/maintenance/folder-permissions/reset", ResetAsync)
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);

    private static async Task<IResult> ResetAsync(FolderPermissionResetRequest request,
        IReadModelConditionalEraser eraser, CancellationToken cancellationToken)
    {
        if (!FolderStreamName.TryCreate(request.TenantId, request.TenantId == "a" ? "b" : "a", out _, out _)
            || request.AggregateId != request.TenantId
                && !FolderStreamName.TryCreate(request.TenantId, request.AggregateId, out _, out _)
            || request.ExpectedEtag is not null && string.IsNullOrWhiteSpace(request.ExpectedEtag))
        {
            return Results.BadRequest();
        }

        string key = request.AggregateId == request.TenantId
            ? "projection:folders:organization-permissions:" + request.TenantId
            : "projection:folders:folder-permissions:" + request.TenantId + ":" + request.AggregateId;
        try
        {
            if (request.ExpectedEtag is null)
            {
                (bool present, string etag) = await eraser.TryReadEtagAsync("statestore", key,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { present, etag });
            }

            if (!await eraser.TryEraseAsync("statestore", key, request.ExpectedEtag,
                cancellationToken).ConfigureAwait(false))
            {
                return Results.Conflict();
            }

            (bool remaining, _) = await eraser.TryReadEtagAsync("statestore", key,
                cancellationToken).ConfigureAwait(false);
            return remaining ? Results.Conflict() : Results.NoContent();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
