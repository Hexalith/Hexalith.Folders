using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetReconciliationStatus projection through the Gateway.</summary>
internal sealed class GetReconciliationStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceStatusQueryHandler handler)
    : GatewayQueryHandler<GetReconciliationStatus>(support, EventuallyConsistent,
        "query_status", static payload => payload.FolderId, historicalAction: WorkspaceStatusQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetReconciliationStatus payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override string? SecondaryEntity(GetReconciliationStatus payload) => payload.ReconciliationId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetReconciliationStatus payload,
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

        if (result.ProviderOutcome is not { } outcome || outcome.ReconciliationReference != payload.ReconciliationId)
        {
            return Denied;
        }

        return Success(new
        {
            payload.ReconciliationId,
            outcome.OperationId,
            state = FoldersDomainServiceEndpoints.ReconciliationStateFor(result.CurrentState),
            finalStateEvidence = FoldersDomainServiceEndpoints.FinalStateEvidenceFor(result.CurrentState),
            escalationRequired = FoldersDomainServiceEndpoints.EscalationRequiredFor(result.CurrentState),
            result.RetryEligibility,
            result.RetryAfter,
        }, result.Freshness);
    }
}
