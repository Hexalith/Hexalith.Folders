using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Server;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetEffectivePermissions projection through the Gateway.</summary>
internal sealed class GetEffectivePermissionsGatewayQueryHandler(GatewayQueryExecutionSupport support,
    EffectivePermissionsQueryHandler handler)
    : GatewayQueryHandler<GetEffectivePermissions>(support, ReadYourWrites,
        "read_metadata", static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? TaskScope(GetEffectivePermissions payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetEffectivePermissions payload,
        GatewayQueryPage page, CancellationToken token)
    {
        EffectivePermissionsQuery read = new(payload.FolderId, query.TenantId, query.UserId, query.CorrelationId, payload.TaskId);
        EffectivePermissionsQueryResult result = payload.TaskId is null
            ? await handler.HandleAsync(read, token).ConfigureAwait(false)
            : await handler.HandleVerifiedTaskAsync(read, query.CallerAuthority!.Permissions, token).ConfigureAwait(false);
        if (result.Code != EffectivePermissionsResultCode.Allowed)
        {
            return result.Code is EffectivePermissionsResultCode.ProjectionStale or EffectivePermissionsResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new
        {
            result.FolderId,
            permissions = result.Permissions.Select(FoldersDomainServiceEndpoints.PermissionToken),
            result.AuthorizationOutcome,
            taskId = result.TaskContextId,
        }, Freshness(result.Freshness));
    }
}
