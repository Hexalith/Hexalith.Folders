using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed partial class ProviderSupportEvidenceQueryHandler(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IProviderSupportEvidenceReadModel readModel,
    IUtcClock clock,
    ILogger<ProviderSupportEvidenceQueryHandler>? logger = null)
{
    public const string ReadActionToken = "tenant-context-and-provider-support-read";

    private const string EventuallyConsistent = "eventually_consistent";
    private const string DeniedSafe = "denied_safe";

    private readonly TenantAccessAuthorizer _tenantAccessAuthorizer = tenantAccessAuthorizer ?? throw new ArgumentNullException(nameof(tenantAccessAuthorizer));
    private readonly IProviderSupportEvidenceReadModel _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly ILogger<ProviderSupportEvidenceQueryHandler> _logger = logger ?? NullLogger<ProviderSupportEvidenceQueryHandler>.Instance;

    public async Task<ProviderSupportEvidenceQueryResult> HandleAsync(
        ProviderSupportEvidenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.ClaimTransformEvidence);
        cancellationToken.ThrowIfCancellationRequested();

        string correlationId = SafeCorrelationId(query.CorrelationId);
        ProviderReadinessFreshness deniedFreshness = new(EventuallyConsistent, _clock.UtcNow, null, Stale: true);
        ProviderSupportEvidencePage emptyPage = new(null, query.Limit, IsTruncated: false, TruncatedReason: null);

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(ProviderSupportEvidenceQueryResultCode.AuthenticationRequired, "authentication_required", deniedFreshness, emptyPage, correlationId);
        }

        string managedTenantId = query.AuthoritativeTenantId.Trim();
        string principalId = query.PrincipalId.Trim();
        if (string.Equals(managedTenantId, "system", StringComparison.OrdinalIgnoreCase)
            || HasClientControlledMismatch(managedTenantId, query.ClientControlledTenantValues)
            || !IsClaimTransformEvidenceValid(query.ClaimTransformEvidence, managedTenantId, principalId))
        {
            return SafeResult(ProviderSupportEvidenceQueryResultCode.AuthorizationDenied, "provider_support_read_denied", deniedFreshness, emptyPage, correlationId);
        }

        TenantAccessAuthorizationResult tenantAccess = await _tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(
            new TenantAccessAuthorizationContext(managedTenantId, principalId, RequestedTenantId: managedTenantId),
            cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return TenantDeniedResult(tenantAccess, deniedFreshness, emptyPage, correlationId);
        }

        ProviderSupportEvidenceReadModelResult readModelResult;
        try
        {
            readModelResult = await _readModel.QueryAsync(
                new ProviderSupportEvidenceReadModelRequest(
                    managedTenantId,
                    principalId,
                    ReadActionToken,
                    query.Cursor,
                    query.Limit,
                    correlationId,
                    tenantAccess.ProjectionWatermark,
                    EventuallyConsistent,
                    _clock.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Provider support evidence read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable,
                "read_model_unavailable",
                deniedFreshness,
                emptyPage,
                correlationId);
        }

        ProviderSupportEvidencePage page = new(
            readModelResult.NextCursor,
            query.Limit,
            readModelResult.NextCursor is not null,
            readModelResult.NextCursor is null ? null : "result_count_limit");

        return readModelResult.Status switch
        {
            ProviderSupportEvidenceReadModelStatus.Available => new(
                ProviderSupportEvidenceQueryResultCode.Allowed,
                readModelResult.Items,
                page,
                readModelResult.Freshness,
                correlationId,
                "success"),
            ProviderSupportEvidenceReadModelStatus.Stale => SafeResult(
                ProviderSupportEvidenceQueryResultCode.ProjectionStale,
                "projection_stale",
                readModelResult.Freshness,
                emptyPage,
                correlationId),
            ProviderSupportEvidenceReadModelStatus.Unavailable => SafeResult(
                ProviderSupportEvidenceQueryResultCode.ProviderUnavailable,
                "provider_unavailable",
                readModelResult.Freshness,
                emptyPage,
                correlationId),
            ProviderSupportEvidenceReadModelStatus.Malformed => SafeResult(
                ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable,
                "projection_malformed",
                readModelResult.Freshness,
                emptyPage,
                correlationId),
            _ => SafeResult(
                ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable,
                "read_model_unavailable",
                readModelResult.Freshness,
                emptyPage,
                correlationId),
        };
    }

    private static ProviderSupportEvidenceQueryResult TenantDeniedResult(
        TenantAccessAuthorizationResult tenantAccess,
        ProviderReadinessFreshness deniedFreshness,
        ProviderSupportEvidencePage emptyPage,
        string correlationId)
    {
        ProviderSupportEvidenceQueryResultCode code = tenantAccess.Outcome switch
        {
            TenantAccessOutcome.StaleProjection => ProviderSupportEvidenceQueryResultCode.ProjectionStale,
            TenantAccessOutcome.UnavailableProjection => ProviderSupportEvidenceQueryResultCode.ProjectionUnavailable,
            _ => ProviderSupportEvidenceQueryResultCode.AuthorizationDenied,
        };

        string reasonCode = tenantAccess.Outcome switch
        {
            TenantAccessOutcome.StaleProjection => "projection_stale",
            TenantAccessOutcome.UnavailableProjection => "projection_unavailable",
            TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => "authorization_evidence_malformed",
            _ => "tenant_access_denied",
        };

        return SafeResult(
            code,
            reasonCode,
            deniedFreshness with { ProjectionWatermark = tenantAccess.ProjectionWatermark },
            emptyPage,
            correlationId);
    }

    private static ProviderSupportEvidenceQueryResult SafeResult(
        ProviderSupportEvidenceQueryResultCode code,
        string reasonCode,
        ProviderReadinessFreshness freshness,
        ProviderSupportEvidencePage page,
        string correlationId)
        => new(code, [], page, freshness with { Stale = code != ProviderSupportEvidenceQueryResultCode.Allowed }, correlationId, reasonCode);

    private static bool IsClaimTransformEvidenceValid(
        EventStoreClaimTransformEvidence evidence,
        string authoritativeTenantId,
        string principalId)
        => evidence.IsPresent
            && !evidence.Malformed
            && string.Equals(evidence.TenantId?.Trim(), authoritativeTenantId, StringComparison.Ordinal)
            && string.Equals(evidence.PrincipalId?.Trim(), principalId, StringComparison.Ordinal)
            && evidence.HasPermissionFor(ReadActionToken);

    private static bool HasClientControlledMismatch(
        string authoritativeTenantId,
        IReadOnlyDictionary<string, string?>? comparisonValues)
    {
        if (comparisonValues is null || comparisonValues.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<string, string?> value in comparisonValues)
        {
            if (value.Value is null)
            {
                continue;
            }

            string trimmed = value.Value.Trim();
            if (trimmed.Length == 0 || !string.Equals(trimmed, authoritativeTenantId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string SafeCorrelationId(string? value)
        => !string.IsNullOrWhiteSpace(value) && CanonicalIdentifierPattern().IsMatch(value.Trim())
            ? value.Trim()
            : $"correlation_{Guid.NewGuid():N}";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();
}
