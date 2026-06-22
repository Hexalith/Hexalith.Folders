using System.Text.RegularExpressions;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>
/// Reads a single redacted provider-binding metadata record. Tenant-scoped, authorization-before-read,
/// metadata-only: the credential reference is never surfaced — only a redaction marker is reported by the
/// transport layer.
/// </summary>
public sealed partial class GetProviderBindingQueryHandler(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IProviderReadinessBindingReader bindingReader,
    IUtcClock clock,
    ILogger<GetProviderBindingQueryHandler>? logger = null)
{
    /// <summary>Claim-transform action token required to read a provider binding.</summary>
    public const string ReadActionToken = "tenant-context-and-provider-binding-read";

    /// <summary>
    /// Defaulted capability profile reference. The <see cref="ConfigureProviderBinding"/> command does not
    /// persist a capability profile, so reads report a stable canonical default (see story 8.1 DD4).
    /// </summary>
    public const string DefaultCapabilityProfileRef = "default";

    private const string EventuallyConsistent = "eventually_consistent";

    private readonly TenantAccessAuthorizer _tenantAccessAuthorizer = tenantAccessAuthorizer ?? throw new ArgumentNullException(nameof(tenantAccessAuthorizer));
    private readonly IProviderReadinessBindingReader _bindingReader = bindingReader ?? throw new ArgumentNullException(nameof(bindingReader));
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly ILogger<GetProviderBindingQueryHandler> _logger = logger ?? NullLogger<GetProviderBindingQueryHandler>.Instance;

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The redacted binding metadata or a safe denial.</returns>
    public async Task<GetProviderBindingQueryResult> HandleAsync(
        GetProviderBindingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.ClaimTransformEvidence);
        cancellationToken.ThrowIfCancellationRequested();

        string correlationId = SafeCorrelationId(query.CorrelationId);
        ProviderReadinessFreshness deniedFreshness = new(EventuallyConsistent, _clock.UtcNow, null, Stale: true);

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return Safe(GetProviderBindingQueryResultCode.AuthenticationRequired, "authentication_required", deniedFreshness, correlationId);
        }

        if (string.IsNullOrWhiteSpace(query.ProviderBindingRef))
        {
            return Safe(GetProviderBindingQueryResultCode.NotFoundSafe, "not_found", deniedFreshness, correlationId);
        }

        string managedTenantId = query.AuthoritativeTenantId.Trim();
        string principalId = query.PrincipalId.Trim();
        if (string.Equals(managedTenantId, "system", StringComparison.OrdinalIgnoreCase)
            || HasClientControlledMismatch(managedTenantId, query.ClientControlledTenantValues)
            || !IsClaimTransformEvidenceValid(query.ClaimTransformEvidence, managedTenantId, principalId))
        {
            return Safe(GetProviderBindingQueryResultCode.AuthorizationDenied, "provider_binding_read_denied", deniedFreshness, correlationId);
        }

        TenantAccessAuthorizationResult tenantAccess = await _tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(
            new TenantAccessAuthorizationContext(managedTenantId, principalId, RequestedTenantId: managedTenantId),
            cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            GetProviderBindingQueryResultCode deniedCode = tenantAccess.Outcome switch
            {
                TenantAccessOutcome.StaleProjection => GetProviderBindingQueryResultCode.ProjectionStale,
                TenantAccessOutcome.UnavailableProjection => GetProviderBindingQueryResultCode.ProjectionUnavailable,
                _ => GetProviderBindingQueryResultCode.AuthorizationDenied,
            };
            return Safe(
                deniedCode,
                "tenant_access_denied",
                deniedFreshness with { ProjectionWatermark = tenantAccess.ProjectionWatermark },
                correlationId);
        }

        OrganizationProviderBinding? binding;
        try
        {
            binding = await _bindingReader.GetAsync(
                new ProviderReadinessBindingReadRequest(managedTenantId, query.ProviderBindingRef.Trim(), correlationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Provider binding read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return Safe(GetProviderBindingQueryResultCode.ReadModelUnavailable, "read_model_unavailable", deniedFreshness, correlationId);
        }

        // Safe denial: a binding owned by a different tenant is indistinguishable from a nonexistent one.
        if (binding is null || !string.Equals(binding.ManagedTenantId, managedTenantId, StringComparison.Ordinal))
        {
            return Safe(GetProviderBindingQueryResultCode.NotFoundSafe, "not_found", deniedFreshness, correlationId);
        }

        return new GetProviderBindingQueryResult(
            GetProviderBindingQueryResultCode.Allowed,
            binding.ProviderBindingRef,
            binding.ProviderKind,
            DefaultCapabilityProfileRef,
            new ProviderReadinessFreshness(EventuallyConsistent, _clock.UtcNow, tenantAccess.ProjectionWatermark, Stale: false),
            correlationId,
            "success");
    }

    private static GetProviderBindingQueryResult Safe(
        GetProviderBindingQueryResultCode code,
        string reasonCode,
        ProviderReadinessFreshness freshness,
        string correlationId)
        => new(
            code,
            ProviderBindingRef: null,
            ProviderFamilyRef: null,
            CapabilityProfileRef: null,
            freshness with { Stale = code != GetProviderBindingQueryResultCode.Allowed },
            correlationId,
            reasonCode);

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
