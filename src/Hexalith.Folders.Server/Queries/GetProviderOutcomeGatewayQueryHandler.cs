using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetProviderOutcome projection through the Gateway.</summary>
internal sealed class GetProviderOutcomeGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceStatusQueryHandler handler)
    : GatewayQueryHandler<GetProviderOutcome>(support, EventuallyConsistent,
        "query_status", static payload => payload.FolderId, historicalAction: WorkspaceStatusQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetProviderOutcome payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetProviderOutcome payload,
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

        if (result.ProviderOutcome is not { } outcome || outcome.OperationId != payload.OperationId)
        {
            return Denied;
        }

        return Success(new
        {
            outcome.OperationId,
            outcome.State,
            outcome.SanitizedStatusClass,
            outcome.ProviderCorrelationReference,
            outcome.RetryEligibility,
            outcome.RetryAfter,
        }, outcome.Freshness);
    }
}
