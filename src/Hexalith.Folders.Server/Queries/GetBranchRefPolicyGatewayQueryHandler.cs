using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetBranchRefPolicy projection through the Gateway.</summary>
internal sealed class GetBranchRefPolicyGatewayQueryHandler(GatewayQueryExecutionSupport support,
    BranchRefPolicyQueryHandler handler)
    : GatewayQueryHandler<GetBranchRefPolicy>(support, EventuallyConsistent,
        "read_branch_ref_policy", static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetBranchRefPolicy payload,
        GatewayQueryPage page, CancellationToken token)
    {
        BranchRefPolicyQueryResult result = await handler.HandleAsync(new BranchRefPolicyQuery(
            payload.FolderId, query.TenantId, query.UserId, Evidence(query),
            query.CorrelationId, null, null, null), token).ConfigureAwait(false);
        if (result.Code != BranchRefPolicyQueryResultCode.Allowed)
        {
            return result.Code is BranchRefPolicyQueryResultCode.ProjectionStale or BranchRefPolicyQueryResultCode.ProjectionUnavailable or BranchRefPolicyQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        if (string.IsNullOrWhiteSpace(result.RepositoryBindingId) || string.IsNullOrWhiteSpace(result.PolicyRef)
            || string.IsNullOrWhiteSpace(result.DefaultRef) || result.AllowedRefPatterns.Count == 0)
        {
            return Unavailable;
        }

        return Success(new
        {
            requestSchemaVersion = "v2",
            result.RepositoryBindingId,
            result.PolicyRef,
            result.DefaultRef,
            result.AllowedRefPatterns,
            result.ProtectedRefPatterns,
        }, result.Freshness);
    }
}
