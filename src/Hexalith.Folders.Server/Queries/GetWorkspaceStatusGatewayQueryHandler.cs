using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetWorkspaceStatus projection through the Gateway.</summary>
internal sealed class GetWorkspaceStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceStatusQueryHandler handler)
    : GatewayQueryHandler<GetWorkspaceStatus>(support, ReadYourWrites,
        WorkspaceStatusQueryHandler.ActionToken, static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetWorkspaceStatus payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetWorkspaceStatus payload,
        GatewayQueryPage page, CancellationToken token)
    {
        WorkspaceStatusQueryResult result = await handler.HandleAsync(new WorkspaceStatusQuery(
            payload.FolderId, payload.WorkspaceId, query.TenantId, query.UserId,
            Evidence(query), query.CorrelationId, null, null, null), token).ConfigureAwait(false);
        if (result.Code != WorkspaceStatusQueryResultCode.Allowed)
        {
            return result.Code is WorkspaceStatusQueryResultCode.ProjectionStale or WorkspaceStatusQueryResultCode.ProjectionUnavailable or WorkspaceStatusQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        if (result.ProviderOutcome is not { } outcome)
        {
            return Unavailable;
        }

        return Success(new
        {
            result.FolderId,
            result.WorkspaceId,
            result.CurrentState,
            result.AcceptedCommandState,
            result.ProjectedState,
            providerOutcome = new
            {
                outcome.OperationId,
                outcome.State,
                outcome.SanitizedStatusClass,
                outcome.ProviderCorrelationReference,
                outcome.RetryEligibility,
                outcome.RetryAfter,
                outcome.Freshness,
            },
            result.RetryEligibility,
            result.RetryAfter,
            result.ProjectionLag,
            result.LastFailureCategory,
        }, result.Freshness);
    }
}
