using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetCommitEvidence projection through the Gateway.</summary>
internal sealed class GetCommitEvidenceGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceStatusQueryHandler handler)
    : GatewayQueryHandler<GetCommitEvidence>(support, EventuallyConsistent,
        "query_status", static payload => payload.FolderId, historicalAction: WorkspaceStatusQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetCommitEvidence payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override string? SecondaryEntity(GetCommitEvidence payload) => payload.OperationId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetCommitEvidence payload,
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
            commitResultStatus = result.CurrentState,
            commitReferenceClassification = outcome.CommitReferenceClassification
                ?? FoldersDomainServiceEndpoints.CommitReferenceClassification(result.CurrentState),
            changedPathMetadataDigest = outcome.ChangedPathMetadataDigest ?? "digest_unavailable",
            outcome.ProviderCorrelationReference,
            redaction = new { visibility = "metadata_only", reasonCode = "not_redacted" },
            auditMetadataKeys = new[]
            {
                "folder_id", "workspace_id", "operation_id", "changed_path_metadata_digest",
                "provider_correlation_reference", "sensitive_metadata_tier", "correlation_id",
            },
        }, result.Freshness);
    }
}
