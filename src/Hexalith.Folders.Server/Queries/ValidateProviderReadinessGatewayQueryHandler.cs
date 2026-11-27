using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.ProviderReadiness;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized ValidateProviderReadiness projection through the Gateway.</summary>
internal sealed class ValidateProviderReadinessGatewayQueryHandler(GatewayQueryExecutionSupport support,
    ProviderReadinessValidationService handler)
    : GatewayQueryHandler<ValidateProviderReadiness>(support, SnapshotPerTask,
        "create_folder", static payload => payload.ProviderBindingRef,
        historicalAction: ProviderReadinessValidationService.ReadActionToken, providerScope: true)
{
    /// <inheritdoc/>
    protected override string? SecondaryEntity(ValidateProviderReadiness payload) => payload.ProviderBindingRef;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, ValidateProviderReadiness payload,
        GatewayQueryPage page, CancellationToken token)
    {
        ProviderReadinessRequestedCapability capability = Enum.Parse<ProviderReadinessRequestedCapability>(
            payload.RequestedCapability.ToString());
        ProviderReadinessValidationResult result = await handler.ValidateExistingBindingAsync(new ProviderReadinessValidationRequest(
            query.TenantId, query.UserId, payload.ProviderBindingRef, capability,
            query.CorrelationId, Evidence(query, ProviderReadinessValidationService.ReadActionToken),
            new Dictionary<string, string?>()), token).ConfigureAwait(false);
        if (result.Code != ProviderReadinessResultCode.Allowed)
        {
            return result.Code is ProviderReadinessResultCode.ProjectionStale or ProviderReadinessResultCode.ProjectionUnavailable or ProviderReadinessResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        if (result.ProviderBindingRef != payload.ProviderBindingRef)
        {
            return Denied;
        }

        if (result.CategoryCode is "provider_unavailable" or "provider_transient_failure" or "provider_rate_limited")
        {
            return Unavailable;
        }

        return Success(new
        {
            audience = "authorized_operator",
            result.ProviderBindingRef,
            result.Status,
            result.CapabilityProfileRef,
            result.Evidence,
            sanitizedErrorCategory = result.CategoryCode == "none" ? null : result.CategoryCode,
            result.SafeRemediationCode,
            safeReasonCode = result.ReasonCode,
            result.Retryable,
            retryAfterSeconds = result.RetryAfter is null ? (long?)null : (long)Math.Ceiling(result.RetryAfter.Value.TotalSeconds),
            result.RemediationCategory,
            result.ProviderReference,
            result.CorrelationId,
        }, Freshness(result.Freshness));
    }
}
