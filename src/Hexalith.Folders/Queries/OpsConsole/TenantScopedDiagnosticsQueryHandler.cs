using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Reads the two tenant-scoped ops-console diagnostics (readiness, projection-freshness). These have no
/// folder/workspace selector, so authorization is tenant-access-before-observation (mirroring
/// <see cref="ProviderReadiness.GetProviderBindingQueryHandler"/>): authentication, reserved-tenant and
/// client-controlled-mismatch fail-closed, claim-transform evidence, then a bounded-stale diagnostic tenant
/// read. Metadata-only; a diagnostic owned by another tenant is indistinguishable from a missing one.
/// </summary>
public sealed partial class TenantScopedDiagnosticsQueryHandler(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IOpsConsoleDiagnosticsReadModel readModel,
    ILogger<TenantScopedDiagnosticsQueryHandler>? logger = null)
{
    /// <summary>Claim-transform action token required to read a tenant-scoped ops-console diagnostic.</summary>
    public const string ReadActionToken = "tenant-context-and-ops-console-diagnostic-read";

    private readonly TenantAccessAuthorizer _tenantAccessAuthorizer = tenantAccessAuthorizer ?? throw new ArgumentNullException(nameof(tenantAccessAuthorizer));
    private readonly IOpsConsoleDiagnosticsReadModel _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
    private readonly ILogger<TenantScopedDiagnosticsQueryHandler> _logger = logger ?? NullLogger<TenantScopedDiagnosticsQueryHandler>.Instance;

    /// <summary>Reads readiness diagnostics for the authoritative tenant.</summary>
    public async Task<OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView>> GetReadinessAsync(
        string? authoritativeTenantId,
        string? principalId,
        EventStoreClaimTransformEvidence claimTransformEvidence,
        string? correlationId,
        IReadOnlyDictionary<string, string?>? clientControlledTenantValues,
        CancellationToken cancellationToken = default)
    {
        (DiagnosticAuthorization authorization, string safeCorrelationId) = await AuthorizeAsync(
            authoritativeTenantId, principalId, claimTransformEvidence, correlationId, clientControlledTenantValues, cancellationToken).ConfigureAwait(false);

        if (!authorization.Allowed)
        {
            return new OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView>(authorization.DeniedCode, null, safeCorrelationId);
        }

        ReadinessDiagnosticsView? view;
        try
        {
            view = await _readModel.GetReadinessAsync(authorization.ManagedTenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadModelFailure(ex, ex.GetType().FullName);
            return new OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView>(DiagnosticReadResultCode.ReadModelUnavailable, null, safeCorrelationId);
        }

        return view is null || !string.Equals(view.ManagedTenantId, authorization.ManagedTenantId, StringComparison.Ordinal)
            ? new OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView>(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId)
            : new OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView>(DiagnosticReadResultCode.Allowed, view, safeCorrelationId);
    }

    /// <summary>Reads projection-freshness diagnostics for the authoritative tenant.</summary>
    public async Task<OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView>> GetProjectionFreshnessAsync(
        string? authoritativeTenantId,
        string? principalId,
        EventStoreClaimTransformEvidence claimTransformEvidence,
        string? correlationId,
        IReadOnlyDictionary<string, string?>? clientControlledTenantValues,
        CancellationToken cancellationToken = default)
    {
        (DiagnosticAuthorization authorization, string safeCorrelationId) = await AuthorizeAsync(
            authoritativeTenantId, principalId, claimTransformEvidence, correlationId, clientControlledTenantValues, cancellationToken).ConfigureAwait(false);

        if (!authorization.Allowed)
        {
            return new OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView>(authorization.DeniedCode, null, safeCorrelationId);
        }

        ProjectionFreshnessDiagnosticsView? view;
        try
        {
            view = await _readModel.GetProjectionFreshnessAsync(authorization.ManagedTenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadModelFailure(ex, ex.GetType().FullName);
            return new OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView>(DiagnosticReadResultCode.ReadModelUnavailable, null, safeCorrelationId);
        }

        return view is null || !string.Equals(view.ManagedTenantId, authorization.ManagedTenantId, StringComparison.Ordinal)
            ? new OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView>(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId)
            : new OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView>(DiagnosticReadResultCode.Allowed, view, safeCorrelationId);
    }

    private async Task<(DiagnosticAuthorization Authorization, string SafeCorrelationId)> AuthorizeAsync(
        string? authoritativeTenantId,
        string? principalId,
        EventStoreClaimTransformEvidence claimTransformEvidence,
        string? correlationId,
        IReadOnlyDictionary<string, string?>? clientControlledTenantValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);
        cancellationToken.ThrowIfCancellationRequested();

        string safeCorrelationId = SafeCorrelationId(correlationId);

        if (string.IsNullOrWhiteSpace(authoritativeTenantId) || string.IsNullOrWhiteSpace(principalId))
        {
            return (DiagnosticAuthorization.Denied(DiagnosticReadResultCode.AuthenticationRequired), safeCorrelationId);
        }

        string managedTenantId = authoritativeTenantId.Trim();
        string principal = principalId.Trim();
        if (string.Equals(managedTenantId, "system", StringComparison.OrdinalIgnoreCase)
            || HasClientControlledMismatch(managedTenantId, clientControlledTenantValues)
            || !IsClaimTransformEvidenceValid(claimTransformEvidence, managedTenantId, principal))
        {
            return (DiagnosticAuthorization.Denied(DiagnosticReadResultCode.AuthorizationDenied), safeCorrelationId);
        }

        TenantAccessAuthorizationResult tenantAccess = await _tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(
            new TenantAccessAuthorizationContext(managedTenantId, principal, RequestedTenantId: managedTenantId),
            cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            DiagnosticReadResultCode deniedCode = tenantAccess.Outcome switch
            {
                TenantAccessOutcome.StaleProjection => DiagnosticReadResultCode.ProjectionStale,
                TenantAccessOutcome.UnavailableProjection => DiagnosticReadResultCode.ProjectionUnavailable,
                _ => DiagnosticReadResultCode.AuthorizationDenied,
            };
            return (DiagnosticAuthorization.Denied(deniedCode), safeCorrelationId);
        }

        return (DiagnosticAuthorization.Allow(managedTenantId), safeCorrelationId);
    }

    private void LogReadModelFailure(Exception ex, string? exceptionType)
        => _logger.LogWarning(
            ex,
            "Ops-console diagnostics read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
            exceptionType);

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

    private readonly record struct DiagnosticAuthorization(bool Allowed, DiagnosticReadResultCode DeniedCode, string ManagedTenantId)
    {
        public static DiagnosticAuthorization Allow(string managedTenantId)
            => new(true, DiagnosticReadResultCode.Allowed, managedTenantId);

        public static DiagnosticAuthorization Denied(DiagnosticReadResultCode code)
            => new(false, code, string.Empty);
    }
}
