using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.ProviderReadiness;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetProviderBinding projection through the Gateway.</summary>
internal sealed class GetProviderBindingGatewayQueryHandler(GatewayQueryExecutionSupport support,
    GetProviderBindingQueryHandler handler)
    : GatewayQueryHandler<GetProviderBinding>(support, EventuallyConsistent,
        GetProviderBindingQueryHandler.ReadActionToken, static payload => payload.ProviderBindingRef, providerScope: true)
{
    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetProviderBinding payload,
        GatewayQueryPage page, CancellationToken token)
    {
        GetProviderBindingQueryResult result = await handler.HandleAsync(new GetProviderBindingQuery(
            query.TenantId, query.UserId, Evidence(query), payload.ProviderBindingRef,
            query.CorrelationId, null), token).ConfigureAwait(false);
        if (result.Code != GetProviderBindingQueryResultCode.Allowed || result.ProviderBindingRef != payload.ProviderBindingRef)
        {
            return result.Code is GetProviderBindingQueryResultCode.ProjectionStale or GetProviderBindingQueryResultCode.ProjectionUnavailable or GetProviderBindingQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new
        {
            result.ProviderBindingRef,
            result.ProviderFamilyRef,
            result.CapabilityProfileRef,
            redaction = "credential_reference_redacted",
        }, Freshness(result.Freshness));
    }
}
