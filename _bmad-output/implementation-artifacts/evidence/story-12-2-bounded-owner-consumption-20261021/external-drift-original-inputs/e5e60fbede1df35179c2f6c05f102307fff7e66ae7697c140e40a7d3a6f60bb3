using System.Globalization;

using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.Services.SupportSafety;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;

namespace Hexalith.Tenants.UI.Services.Gateways;

internal sealed class TenantsBffComposition(
    ITenantCommandGateway commandGateway,
    IHttpContextAccessor? httpContextAccessor = null,
    ITenantConfigurationPrincipalResolver? principalResolver = null,
    TenantConfigurationReadPolicyProvider? policyProvider = null,
    ITenantsReadSurfaceAvailability? readSurface = null,
    IStringLocalizer<TenantsResources>? resourceLocalizer = null,
    IConfiguration? configuration = null) : ITenantsBffComposition {
    /// <summary>Gets every localized string required to render or safely fail the grant-preview interaction.</summary>
    internal static IReadOnlyList<string> RequiredGrantFactKeys { get; } =
    [
        "Tenants.GlobalAdministrators.Availability.Grant.Available",
        "Tenants.GlobalAdministrators.Availability.Grant.Recovery.MissingConsequencePreview",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.AggregateBusy",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingConsequencePreview",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingLifecycleSupport",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingPermission",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.StaleData",
        "Tenants.GlobalAdministrators.Availability.Grant.Unavailable.UnsafeViewport",
        "Tenants.GlobalAdministrators.Availability.Recovery.AggregateBusy",
        "Tenants.GlobalAdministrators.Availability.Recovery.MissingLifecycleSupport",
        "Tenants.GlobalAdministrators.Availability.Recovery.MissingPermission",
        "Tenants.GlobalAdministrators.Availability.Recovery.None",
        "Tenants.GlobalAdministrators.Availability.Recovery.StaleData",
        "Tenants.GlobalAdministrators.Availability.Recovery.UnsafeViewport",
        "Tenants.GlobalAdministrators.Grant.Audit.AuditDelayed",
        "Tenants.GlobalAdministrators.Grant.Audit.AuditPending",
        "Tenants.GlobalAdministrators.Grant.Audit.AuditUnavailable",
        "Tenants.GlobalAdministrators.Grant.Audit.MissingSupport",
        "Tenants.GlobalAdministrators.Grant.Audit.NotStarted",
        "Tenants.GlobalAdministrators.Grant.Available",
        "Tenants.GlobalAdministrators.Grant.Cancel",
        "Tenants.GlobalAdministrators.Grant.Confirm.DidNotConfirm",
        "Tenants.GlobalAdministrators.Grant.Confirm.EvidenceRequired",
        "Tenants.GlobalAdministrators.Grant.Confirm.PageScoped",
        "Tenants.GlobalAdministrators.Grant.Confirm.VersionNotAdvanced",
        "Tenants.GlobalAdministrators.Grant.DeliveryRetry",
        "Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery",
        "Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Label",
        "Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Reason",
        "Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Recovery",
        "Tenants.GlobalAdministrators.Grant.Description",
        "Tenants.GlobalAdministrators.Grant.Lifecycle.Title",
        "Tenants.GlobalAdministrators.Grant.Preview.Acknowledge",
        "Tenants.GlobalAdministrators.Grant.Preview.Audit",
        "Tenants.GlobalAdministrators.Grant.Preview.Audit.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.AuthorityChange",
        "Tenants.GlobalAdministrators.Grant.Preview.AuthorityChange.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.CallerTargetContext",
        "Tenants.GlobalAdministrators.Grant.Preview.CallerTargetContext.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Confirm",
        "Tenants.GlobalAdministrators.Grant.Preview.Counts",
        "Tenants.GlobalAdministrators.Grant.Preview.Counts.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Freshness",
        "Tenants.GlobalAdministrators.Grant.Preview.Freshness.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Invalidated",
        "Tenants.GlobalAdministrators.Grant.Preview.KnownConsequences",
        "Tenants.GlobalAdministrators.Grant.Preview.KnownConsequences.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.KnownUnknowns",
        "Tenants.GlobalAdministrators.Grant.Preview.KnownUnknowns.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Launch",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Authorization",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Target",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.TargetExists",
        "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Scope",
        "Tenants.GlobalAdministrators.Grant.Preview.Scope.Value",
        "Tenants.GlobalAdministrators.Grant.Preview.Target",
        "Tenants.GlobalAdministrators.Grant.Preview.Title",
        "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Authorization",
        "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Evidence",
        "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization",
        "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Target",
        "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.TargetExists",
        "Tenants.GlobalAdministrators.Grant.Refresh",
        "Tenants.GlobalAdministrators.Grant.State.Accepted",
        "Tenants.GlobalAdministrators.Grant.State.Confirmed",
        "Tenants.GlobalAdministrators.Grant.State.Degraded",
        "Tenants.GlobalAdministrators.Grant.State.Failed",
        "Tenants.GlobalAdministrators.Grant.State.Idle",
        "Tenants.GlobalAdministrators.Grant.State.Previewed",
        "Tenants.GlobalAdministrators.Grant.State.ProjectionPending",
        "Tenants.GlobalAdministrators.Grant.State.Rejected",
        "Tenants.GlobalAdministrators.Grant.State.RequestSent",
        "Tenants.GlobalAdministrators.Grant.State.UnableToVerify",
        "Tenants.GlobalAdministrators.Grant.Status.Pending",
        "Tenants.GlobalAdministrators.Grant.Status.PublishFailed",
        "Tenants.GlobalAdministrators.Grant.Status.Rejected",
        "Tenants.GlobalAdministrators.Grant.Status.TimedOut",
        "Tenants.GlobalAdministrators.Grant.Status.Unknown",
        "Tenants.GlobalAdministrators.Grant.Submission.Failed",
        "Tenants.GlobalAdministrators.Grant.Submission.Invalid",
        "Tenants.GlobalAdministrators.Grant.Submission.Rejected",
        "Tenants.GlobalAdministrators.Grant.Submission.Unavailable",
        "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous",
        "Tenants.GlobalAdministrators.Grant.Title",
        "Tenants.GlobalAdministrators.Grant.UnableToVerify.EventEvidence",
        "Tenants.GlobalAdministrators.Grant.UnableToVerify.StatusTimeout",
        "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch",
        "Tenants.GlobalAdministrators.Grant.UnableToVerify.UnsupportedSubmission",
        "Tenants.GlobalAdministrators.Grant.Unavailable.Authorization",
        "Tenants.GlobalAdministrators.Grant.Unavailable.CommandSurface",
        "Tenants.GlobalAdministrators.Grant.Unavailable.Freshness",
        "Tenants.GlobalAdministrators.Grant.Unavailable.InFlight",
        "Tenants.GlobalAdministrators.Grant.Unavailable.ProjectionLifecycle",
        "Tenants.GlobalAdministrators.Grant.Unavailable.ReadSurface",
        "Tenants.GlobalAdministrators.Grant.Unavailable.RemoveDeferred",
        "Tenants.GlobalAdministrators.Grant.Unavailable.TrackedDispatch",
        "Tenants.GlobalAdministrators.Grant.Validation.UserIdInvalid",
        "Tenants.GlobalAdministrators.Grant.Validation.UserIdRequired",
        "Tenants.GlobalAdministrators.Grant.UserId.Help",
        "Tenants.GlobalAdministrators.Grant.UserId.Label",
    ];

    /// <summary>Gets every localized string required to render or safely fail the removal interaction.</summary>
    internal static IReadOnlyList<string> RequiredRemoveFactKeys { get; } =
    [
        "Tenants.GlobalAdministrators.Availability.Remove.Available",
        "Tenants.GlobalAdministrators.Availability.Remove.Recovery.MissingConsequencePreview",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.AggregateBusy",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.IncompletePopulation",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.LastAdministrator",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingConsequencePreview",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingLifecycleSupport",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingPermission",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.StaleData",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.TargetMissing",
        "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.UnsafeViewport",
        "Tenants.GlobalAdministrators.Availability.Recovery.AggregateBusy",
        "Tenants.GlobalAdministrators.Availability.Recovery.IncompletePopulation",
        "Tenants.GlobalAdministrators.Availability.Recovery.LastAdministrator",
        "Tenants.GlobalAdministrators.Availability.Recovery.MissingLifecycleSupport",
        "Tenants.GlobalAdministrators.Availability.Recovery.MissingPermission",
        "Tenants.GlobalAdministrators.Availability.Recovery.None",
        "Tenants.GlobalAdministrators.Availability.Recovery.StaleData",
        "Tenants.GlobalAdministrators.Availability.Recovery.TargetMissing",
        "Tenants.GlobalAdministrators.Availability.Recovery.UnsafeViewport",
        "Tenants.GlobalAdministrators.Remove.Launch",
        "Tenants.GlobalAdministrators.Remove.Title",
        "Tenants.GlobalAdministrators.Remove.Description",
        "Tenants.GlobalAdministrators.Remove.Lifecycle.Title",
        "Tenants.GlobalAdministrators.Remove.State.Idle",
        "Tenants.GlobalAdministrators.Remove.State.Previewed",
        "Tenants.GlobalAdministrators.Remove.State.RequestSent",
        "Tenants.GlobalAdministrators.Remove.State.Accepted",
        "Tenants.GlobalAdministrators.Remove.State.ProjectionPending",
        "Tenants.GlobalAdministrators.Remove.State.Confirmed",
        "Tenants.GlobalAdministrators.Remove.State.Rejected",
        "Tenants.GlobalAdministrators.Remove.State.Failed",
        "Tenants.GlobalAdministrators.Remove.State.Degraded",
        "Tenants.GlobalAdministrators.Remove.State.UnableToVerify",
        "Tenants.GlobalAdministrators.Remove.Audit.NotStarted",
        "Tenants.GlobalAdministrators.Remove.Audit.AuditPending",
        "Tenants.GlobalAdministrators.Remove.Audit.AuditDelayed",
        "Tenants.GlobalAdministrators.Remove.Audit.AuditUnavailable",
        "Tenants.GlobalAdministrators.Remove.Audit.MissingSupport",
        "Tenants.GlobalAdministrators.Remove.Preview.Title",
        "Tenants.GlobalAdministrators.Remove.Preview.Scope",
        "Tenants.GlobalAdministrators.Remove.Preview.Scope.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Target",
        "Tenants.GlobalAdministrators.Remove.Preview.Target.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Counts",
        "Tenants.GlobalAdministrators.Remove.Preview.Counts.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.AuthorityChange",
        "Tenants.GlobalAdministrators.Remove.Preview.AuthorityChange.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Freshness",
        "Tenants.GlobalAdministrators.Remove.Preview.Freshness.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Audit",
        "Tenants.GlobalAdministrators.Remove.Preview.Audit.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext",
        "Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext.Self.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext.Other.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.KnownConsequences",
        "Tenants.GlobalAdministrators.Remove.Preview.KnownConsequences.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.KnownUnknowns",
        "Tenants.GlobalAdministrators.Remove.Preview.KnownUnknowns.Value",
        "Tenants.GlobalAdministrators.Remove.Preview.Acknowledge",
        "Tenants.GlobalAdministrators.Remove.Preview.Confirm",
        "Tenants.GlobalAdministrators.Remove.Cancel",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Authorization",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Target",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Evidence",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.TargetMissing",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.LastAdministrator",
        "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Localization",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Authorization",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Target",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Refresh",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.TargetMissing",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.LastAdministrator",
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Localization",
        "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous",
        "Tenants.GlobalAdministrators.Remove.Refresh",
        "Tenants.GlobalAdministrators.Remove.DeliveryRetry",
        "Tenants.GlobalAdministrators.Remove.DeliveryRetry.Recovery",
        "Tenants.GlobalAdministrators.Remove.UnableToVerify.TrackingMismatch",
        "Tenants.GlobalAdministrators.Remove.UnableToVerify.EventEvidence",
        "Tenants.GlobalAdministrators.Remove.UnableToVerify.StatusTimeout",
        "Tenants.GlobalAdministrators.Remove.UnableToVerify.UnsupportedSubmission",
        "Tenants.GlobalAdministrators.Remove.Status.Pending",
        "Tenants.GlobalAdministrators.Remove.Status.Unknown",
        "Tenants.GlobalAdministrators.Remove.Status.PublishFailed",
        "Tenants.GlobalAdministrators.Remove.Status.Rejected",
        "Tenants.GlobalAdministrators.Remove.Status.Rejected.LastAdministrator",
        "Tenants.GlobalAdministrators.Remove.Status.Rejected.NotFound",
        "Tenants.GlobalAdministrators.Remove.Status.Rejected.Permission",
        "Tenants.GlobalAdministrators.Remove.Status.TimedOut",
        "Tenants.GlobalAdministrators.Remove.Status.Failed",
        "Tenants.GlobalAdministrators.Remove.Recovery.Rejected",
        "Tenants.GlobalAdministrators.Remove.Recovery.Failed",
        "Tenants.GlobalAdministrators.Remove.Recovery.PublishFailed",
        "Tenants.GlobalAdministrators.Remove.Recovery.TimedOut",
        "Tenants.GlobalAdministrators.Remove.Confirm.EvidenceRequired",
        "Tenants.GlobalAdministrators.Remove.Confirm.StillPresent",
        "Tenants.GlobalAdministrators.Remove.Confirm.VersionNotAdvanced",
        "Tenants.GlobalAdministrators.Remove.Projection.UnableToVerify",
    ];

    // Reads the composition decision rather than resolving ITenantQueryGateway, which would close a
    // container cycle (this type -> gateway -> this type) the moment Tenants:BaseAddress is configured.
    // Absence fails closed: an unregistered read surface is not evidence of a connected one.
    public bool IsReadSurfaceConnected => readSurface?.IsConnected == true;

    public bool IsCommandSurfaceConnected => commandGateway is not UnavailableTenantCommandGateway;

    public string? AuditPermissionRecoveryHref => configuration?["Tenants:Audit:PermissionRecoveryHref"];

    public string? AuditEscalationRecoveryHref => configuration?["Tenants:Audit:EscalationRecoveryHref"];

    public bool IsGlobalAdministratorDispatchConnected
        => commandGateway.SupportsGlobalAdministratorDispatch;

    public bool IsGlobalAdministratorStatusConnected
        => commandGateway.SupportsCommandStatusLookup;

    public bool IsGlobalAdministratorRequeryConnected
        => readSurface?.IsConnected == true;

    // The fixed-key completeness walk enumerates the whole resource table twice and swaps the ambient UI
    // culture while it does so. Its inputs -- the compiled satellite resources and this instance's localizer
    // -- cannot change for the lifetime of the composition, but the property is read several times per
    // render of the global-administrators page (availability, confirm-disabled, and the final dispatch arm),
    // so the walk is resolved once and reused. The per-preview overload below still evaluates its own keys.
    private bool? _hasCompleteFixedGrantLocalization;
    private bool? _hasCompleteFixedRemoveLocalization;

    private bool HasCompleteFixedGrantLocalization
        => _hasCompleteFixedGrantLocalization ??= HasCompleteLocalization(RequiredGrantFactKeys);

    public bool IsGlobalAdministratorGrantPreviewReady
        => principalResolver is not null
            && HasCompleteFixedGrantLocalization;

    public string? GlobalAdministratorGrantPreviewUnavailableReasonKey
        => principalResolver is not null && !HasCompleteFixedGrantLocalization
            ? "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization"
            : null;

    public string? GlobalAdministratorGrantPreviewRecoveryKey
        => principalResolver is not null && !HasCompleteFixedGrantLocalization
            ? "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization"
            : null;

    public bool IsGlobalAdministratorRemovePreviewReady
        => principalResolver is not null
            && (_hasCompleteFixedRemoveLocalization ??= HasCompleteLocalization(RequiredRemoveFactKeys));

    public TenantLifecycleAuthorizationReflectionState LifecycleAuthorizationReflection
        => IsCommandSurfaceConnected
            ? TenantsGlobalAdministratorClaims.Evaluate(httpContextAccessor?.HttpContext?.User)
            : TenantLifecycleAuthorizationReflectionState.Indeterminate;

    public TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
        => TenantsGlobalAdministratorClaims.Evaluate(httpContextAccessor?.HttpContext?.User);

    public async ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
        CancellationToken cancellationToken = default) {
        if (principalResolver is null) {
            return TenantLifecycleAuthorizationReflectionState.Indeterminate;
        }

        TenantConfigurationPrincipalEvidence evidence = await principalResolver.ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        return evidence.State switch {
            TenantConfigurationPrincipalEvidenceState.GlobalAdministrator
                => TenantLifecycleAuthorizationReflectionState.Authorized,
            TenantConfigurationPrincipalEvidenceState.NonAdministrator
                => TenantLifecycleAuthorizationReflectionState.MissingPermission,
            _ => TenantLifecycleAuthorizationReflectionState.Indeterminate,
        };
    }

    public async ValueTask<GlobalAdministratorGrantPreview> ComposeGlobalAdministratorGrantPreviewAsync(
        string targetUserId,
        GlobalAdministratorsSnapshot completeSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completeSnapshot);

        // Authority is resolved before the row collection is inspected. This prevents the preview seam from
        // becoming a fixed-scope administrator-presence oracle for an unauthorized caller.
        TenantLifecycleAuthorizationReflectionState authority =
            await ResolveGlobalAdministratorsAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            targetUserId,
            completeSnapshot,
            authority is TenantLifecycleAuthorizationReflectionState.Authorized);
        if (!preview.IsComplete)
        {
            return preview;
        }

        return HasCompleteFixedGrantLocalization
            ? preview
            : GlobalAdministratorGrantPreview.Unavailable(
                targetUserId,
                "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization",
                "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization");
    }

    public async ValueTask<GlobalAdministratorRemovePreview> ComposeGlobalAdministratorRemovePreviewAsync(
        string targetUserId,
        GlobalAdministratorsSnapshot completeSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completeSnapshot);
        if (principalResolver is null)
        {
            return GlobalAdministratorRemovePreview.Unavailable(
                targetUserId,
                "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Authorization",
                "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Authorization");
        }

        TenantConfigurationPrincipalEvidence evidence = await principalResolver
            .ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        GlobalAdministratorRemovePreview preview = GlobalAdministratorRemovePreview.Create(
            targetUserId,
            evidence.Subject,
            completeSnapshot,
            evidence.State is TenantConfigurationPrincipalEvidenceState.GlobalAdministrator);
        if (!preview.IsComplete)
        {
            return preview;
        }

        return HasCompleteLocalization(RequiredRemoveFactKeys)
            ? preview
            : GlobalAdministratorRemovePreview.Unavailable(
                targetUserId,
                "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Localization",
                "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Localization");
    }

    private bool HasCompleteLocalization(IReadOnlyList<string?> requiredFactKeys)
    {
        if (resourceLocalizer is null)
        {
            return false;
        }

        CultureInfo french;
        try
        {
            french = CultureInfo.GetCultureInfo("fr");
        }
        catch (CultureNotFoundException)
        {
            // Globalization-invariant or predefined-cultures-only hosting cannot prove the French facts
            // resolve. This lookup sits outside the per-culture walk's own catch, so it fails closed here
            // instead of throwing out of a property that is read while rendering.
            return false;
        }

        return HasCompleteLocalization(CultureInfo.InvariantCulture, requiredFactKeys)
            && HasCompleteLocalization(french, requiredFactKeys);
    }

    private bool HasCompleteLocalization(
        CultureInfo culture,
        IReadOnlyList<string?> requiredFactKeys)
    {
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            IReadOnlyDictionary<string, LocalizedString> explicitResources = resourceLocalizer!
                .GetAllStrings(includeParentCultures: false)
                .GroupBy(static value => value.Name, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
            foreach (string? key in requiredFactKeys)
            {
                if (string.IsNullOrWhiteSpace(key)
                    || !explicitResources.TryGetValue(key, out LocalizedString? explicitValue)
                    || !IsResolvedResource(key, explicitValue)
                    || !IsResolvedResource(key, resourceLocalizer[key]))
                {
                    return false;
                }

                if ((string.Equals(
                            key,
                            "Tenants.GlobalAdministrators.Grant.Preview.Counts.Value",
                            StringComparison.Ordinal)
                        || string.Equals(
                            key,
                            "Tenants.GlobalAdministrators.Remove.Preview.Counts.Value",
                            StringComparison.Ordinal))
                    && !HasRequiredCountPlaceholders(resourceLocalizer[key].Value, culture))
                {
                    return false;
                }

                if ((string.Equals(
                            key,
                            "Tenants.GlobalAdministrators.Remove.Preview.Target.Value",
                            StringComparison.Ordinal)
                        || string.Equals(
                            key,
                            "Tenants.GlobalAdministrators.Remove.Preview.Acknowledge",
                            StringComparison.Ordinal))
                    && !HasRequiredTargetPlaceholder(resourceLocalizer[key].Value, culture))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static bool IsResolvedResource(string key, LocalizedString value)
        => !value.ResourceNotFound
            && !string.IsNullOrWhiteSpace(value.Value)
            && !string.Equals(value.Value, key, StringComparison.Ordinal);

    private static bool HasRequiredCountPlaceholders(string format, CultureInfo culture)
    {
        const string currentCountMarker = "__current-count__";
        const string resultingCountMarker = "__resulting-count__";
        try
        {
            string rendered = string.Format(culture, format, currentCountMarker, resultingCountMarker);
            return rendered.Contains(currentCountMarker, StringComparison.Ordinal)
                && rendered.Contains(resultingCountMarker, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasRequiredTargetPlaceholder(string format, CultureInfo culture)
    {
        const string targetMarker = "__literal-target__";
        try
        {
            return string.Format(culture, format, targetMarker)
                .Contains(targetMarker, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveLifecycleAuthorizationAsync(
        CancellationToken cancellationToken = default)
        => ResolveGlobalAdministratorsAuthorizationAsync(cancellationToken);

    public async ValueTask<TenantMutationAuthorizationReflection> ResolveTenantMutationAuthorizationAsync(
        string tenantId,
        TenantDetail detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);
        if (principalResolver is null || string.IsNullOrWhiteSpace(tenantId)
            || !TenantAuditSupportSafety.IsSafe(tenantId, SupportSafeCopyValueKind.TenantId)
            || !string.Equals(tenantId, detail.TenantId, StringComparison.Ordinal))
        {
            return TenantMutationAuthorizationReflection.Indeterminate;
        }

        // Resolve the current circuit principal before inspecting scoped membership evidence.
        TenantConfigurationPrincipalEvidence principal = await principalResolver.ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (principal.State is TenantConfigurationPrincipalEvidenceState.Indeterminate
            || string.IsNullOrWhiteSpace(principal.Subject))
        {
            return TenantMutationAuthorizationReflection.Indeterminate;
        }

        IReadOnlyList<TenantMember>? members = detail.Members;
        if (members is null || members.Any(member => member is null
                || string.IsNullOrWhiteSpace(member.UserId)
                || !TenantAuditSupportSafety.IsSafe(member.UserId, SupportSafeCopyValueKind.UserId)
                || member.Role is not (TenantRole.TenantOwner or TenantRole.TenantContributor or TenantRole.TenantReader))
            || members.Select(member => member.UserId).Distinct(StringComparer.Ordinal).Count() != members.Count)
        {
            return TenantMutationAuthorizationReflection.Indeterminate;
        }

        bool global = principal.State is TenantConfigurationPrincipalEvidenceState.GlobalAdministrator;
        TenantRole? role = members.FirstOrDefault(member =>
            string.Equals(member.UserId, principal.Subject, StringComparison.Ordinal))?.Role;
        return new(
            global || role is TenantRole.TenantOwner or TenantRole.TenantContributor
                ? TenantLifecycleAuthorizationReflectionState.Authorized
                : TenantLifecycleAuthorizationReflectionState.MissingPermission,
            global || role is TenantRole.TenantOwner
                ? TenantLifecycleAuthorizationReflectionState.Authorized
                : TenantLifecycleAuthorizationReflectionState.MissingPermission);
    }

    public async ValueTask<TenantCorrectionProjection> ComposeTenantCorrectionProjectionAsync(
        string tenantId,
        string targetUserId,
        TenantDetail rawDetail,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantCorrectionProjection> captures = await ComposeTenantCorrectionProjectionsAsync(
            tenantId, [targetUserId], rawDetail, freshness, lifecycle, provenance, cancellationToken).ConfigureAwait(false);
        return captures[0];
    }

    public async ValueTask<IReadOnlyList<TenantCorrectionProjection>> ComposeTenantCorrectionProjectionsAsync(
        string tenantId,
        IReadOnlyList<string> targetUserIds,
        TenantDetail rawDetail,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDetail);
        ArgumentNullException.ThrowIfNull(targetUserIds);
        TenantCorrectionProjection[] unavailable = targetUserIds.Select(target =>
            TenantCorrectionProjection.Unavailable(tenantId, target)).ToArray();
        if (principalResolver is null || string.IsNullOrWhiteSpace(tenantId)
            || !TenantAuditSupportSafety.IsSafe(tenantId, SupportSafeCopyValueKind.TenantId)
            || targetUserIds.Any(target => string.IsNullOrWhiteSpace(target)
                || !TenantAuditSupportSafety.IsSafe(target, SupportSafeCopyValueKind.UserId))
            || targetUserIds.Distinct(StringComparer.Ordinal).Count() != targetUserIds.Count
            || !string.Equals(rawDetail.TenantId, tenantId, StringComparison.Ordinal))
        {
            return unavailable;
        }

        // Principal resolution precedes membership inspection; raw collections and claims never leave this seam.
        TenantConfigurationPrincipalEvidence authority = await principalResolver.ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        if (authority.State is TenantConfigurationPrincipalEvidenceState.Indeterminate
            || string.IsNullOrWhiteSpace(authority.Subject))
        {
            return unavailable;
        }

        IReadOnlyList<TenantMember>? members = rawDetail.Members;
        if (members is null || members.Any(member => member is null
                || string.IsNullOrWhiteSpace(member.UserId)
                || !string.Equals(TenantAuditSupportSafety.SafeIdentifier(member.UserId,
                    SupportSafeCopyValueKind.UserId), member.UserId, StringComparison.Ordinal)
                || member.Role is not (TenantRole.TenantOwner or TenantRole.TenantContributor or TenantRole.TenantReader))
            || members.Select(member => member.UserId).Distinct(StringComparer.Ordinal).Count() != members.Count)
        {
            return unavailable;
        }

        bool global = authority.State is TenantConfigurationPrincipalEvidenceState.GlobalAdministrator;
        bool authorized = global || members.Any(member => member.Role is TenantRole.TenantOwner
            && string.Equals(member.UserId, authority.Subject, StringComparison.Ordinal));
        if (!authorized)
        {
            return unavailable;
        }

        int ownerCount = members.Count(member => member.Role is TenantRole.TenantOwner);
        return targetUserIds.Select(target => new TenantCorrectionProjection(tenantId, target, rawDetail.Status,
            members.FirstOrDefault(member => string.Equals(member.UserId, target, StringComparison.Ordinal))?.Role,
            members.Count == 0, true, global, true, freshness, lifecycle, provenance)
            { OwnerCount = ownerCount }).ToArray();
    }

    public async ValueTask<TenantConfigurationComposition> ComposeTenantDetailAsync(
        TenantDetail detail,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(detail);

        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(detail.TenantId, cancellationToken)
            .ConfigureAwait(false);
        return TenantConfigurationSafeComposer.Compose(detail, policy);
    }

    public async ValueTask<TenantConfigurationComposition> ReauthorizeTenantDetailAsync(
        TenantDetail sanitizedDetail,
        TenantConfigurationSafeModel safeModel,
        bool degraded,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sanitizedDetail);
        ArgumentNullException.ThrowIfNull(safeModel);

        if (!string.Equals(sanitizedDetail.TenantId, safeModel.TenantId, StringComparison.Ordinal)) {
            return new(
                TenantConfigurationSafeComposer.SanitizeDetail(sanitizedDetail),
                TenantConfigurationSafeModel.Unavailable(sanitizedDetail.TenantId),
                TenantConfigurationManagementContext.Unavailable(sanitizedDetail.TenantId, sanitizedDetail.Status));
        }

        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(sanitizedDetail.TenantId, cancellationToken)
            .ConfigureAwait(false);
        (TenantConfigurationSafeModel safe, TenantConfigurationManagementContext management) =
            TenantConfigurationSafeComposer.Reauthorize(sanitizedDetail, safeModel, policy, degraded);
        return new(TenantConfigurationSafeComposer.SanitizeDetail(sanitizedDetail), safe, management);
    }

    public async ValueTask<TenantConfigurationManagementContext> ReauthorizeConfigurationManagementAsync(
        string tenantId,
        TenantStatus tenantStatus,
        TenantConfigurationSafeModel safeModel,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(safeModel);
        if (!string.Equals(tenantId, safeModel.TenantId, StringComparison.Ordinal)) {
            return TenantConfigurationManagementContext.Unavailable(tenantId, tenantStatus);
        }

        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        return TenantConfigurationSafeComposer.Reauthorize(safeModel, tenantStatus, policy, safeModel.IsDegraded).ManagementContext;
    }

    public async ValueTask<TenantConfigurationManagementContext> ReauthorizeConfigurationManagementAsync(
        TenantDetail sanitizedDetail,
        TenantConfigurationSafeModel safeModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sanitizedDetail);
        ArgumentNullException.ThrowIfNull(safeModel);
        if (!string.Equals(sanitizedDetail.TenantId, safeModel.TenantId, StringComparison.Ordinal))
        {
            return TenantConfigurationManagementContext.Unavailable(
                sanitizedDetail.TenantId,
                sanitizedDetail.Status);
        }

        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(
                sanitizedDetail.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        return TenantConfigurationSafeComposer.Reauthorize(
            sanitizedDetail,
            safeModel,
            policy,
            safeModel.IsDegraded).ManagementContext;
    }

    public async ValueTask<bool> IsConfigurationKeyAuthorizedAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        return policy.IsAvailable
            && (policy.IsGlobalAdministrator
                || policy.AuthorizedPrefixes.Any(prefix => TenantConfigurationManagementContext.IsPrefixMatch(prefix, key)));
    }

    public async ValueTask<TenantSetConfigurationPreview> ComposeSetConfigurationPreviewAsync(
        TenantDetail rawDetail,
        TenantSetConfigurationIntent intent,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        string? projectionVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDetail);
        ArgumentNullException.ThrowIfNull(intent);

        if (!string.Equals(rawDetail.TenantId, intent.TenantId, StringComparison.Ordinal)
            || string.IsNullOrEmpty(intent.NamespacePrefix)
            || string.IsNullOrEmpty(intent.KeySuffix)
            || !string.Equals(
                string.Concat(intent.NamespacePrefix, ".", intent.KeySuffix),
                intent.FullKey,
                StringComparison.Ordinal))
        {
            return TenantSetConfigurationPreview.Unavailable(intent);
        }

        // Resolve and validate current authority before touching the raw configuration dictionary. This
        // ordering is the boundary that prevents the preview seam becoming a key-existence oracle.
        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(intent.TenantId, cancellationToken)
            .ConfigureAwait(false);
        bool namespaceAuthorized = policy.IsAvailable
            && (policy.IsGlobalAdministrator
                || policy.AuthorizedPrefixes.Contains(intent.NamespacePrefix, StringComparer.Ordinal));
        bool hasMutationAuthority = policy.IsGlobalAdministrator
            || !string.IsNullOrWhiteSpace(policy.Subject)
                && ((IReadOnlyList<TenantMember>?)rawDetail.Members ?? []).Any(member => member is not null
                    && string.Equals(member.UserId, policy.Subject, StringComparison.Ordinal)
                    && member.Role is TenantRole.TenantOwner);
        if (!namespaceAuthorized
            || !hasMutationAuthority
            || (!policy.IsGlobalAdministrator
                && !TenantConfigurationManagementContext.IsPrefixMatch(intent.NamespacePrefix, intent.FullKey)))
        {
            return TenantSetConfigurationPreview.Unavailable(intent);
        }

        TenantSetConfigurationCurrentState currentState = rawDetail.Configuration.TryGetValue(
            intent.FullKey,
            out string? currentValue)
            ? string.Equals(
                TenantSetConfigurationValueFingerprint.Create(currentValue),
                intent.ValueFingerprint,
                StringComparison.Ordinal)
                ? TenantSetConfigurationCurrentState.Matching
                : TenantSetConfigurationCurrentState.Different
            : TenantSetConfigurationCurrentState.Absent;

        return TenantSetConfigurationPreview.Create(
            intent,
            rawDetail.Status,
            currentState,
            freshness,
            lifecycle,
            projectionVersion,
            isAuthorized: true);
    }

    public async ValueTask<TenantRemoveConfigurationPreview> ComposeRemoveConfigurationPreviewAsync(
        TenantDetail rawDetail,
        TenantRemoveConfigurationIntent intent,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        string? projectionVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDetail);
        ArgumentNullException.ThrowIfNull(intent);

        if (!string.Equals(rawDetail.TenantId, intent.TenantId, StringComparison.Ordinal)
            || string.IsNullOrEmpty(intent.NamespacePrefix)
            || string.IsNullOrEmpty(intent.FullKey)
            || !TenantConfigurationManagementContext.IsPrefixMatch(intent.NamespacePrefix, intent.FullKey))
        {
            return TenantRemoveConfigurationPreview.Unavailable(intent);
        }

        // Authority is resolved before the dictionary lookup so this seam cannot reveal whether an
        // unauthorized literal key exists.
        TenantConfigurationReadPolicyResolution policy = await ResolvePolicyAsync(intent.TenantId, cancellationToken)
            .ConfigureAwait(false);
        bool namespaceAuthorized = policy.IsAvailable
            && (policy.IsGlobalAdministrator
                || policy.AuthorizedPrefixes.Contains(intent.NamespacePrefix, StringComparer.Ordinal));
        bool hasMutationAuthority = policy.IsGlobalAdministrator
            || !string.IsNullOrWhiteSpace(policy.Subject)
                && ((IReadOnlyList<TenantMember>?)rawDetail.Members ?? []).Any(member => member is not null
                    && string.Equals(member.UserId, policy.Subject, StringComparison.Ordinal)
                    && member.Role is TenantRole.TenantOwner);
        if (!namespaceAuthorized || !hasMutationAuthority)
        {
            return TenantRemoveConfigurationPreview.Unavailable(intent);
        }

        TenantRemoveConfigurationCurrentState currentState = rawDetail.Configuration.ContainsKey(intent.FullKey)
            ? TenantRemoveConfigurationCurrentState.Present
            : TenantRemoveConfigurationCurrentState.Absent;
        return TenantRemoveConfigurationPreview.Create(
            intent,
            rawDetail.Status,
            currentState,
            freshness,
            lifecycle,
            projectionVersion,
            isAuthorized: true);
    }

    private async ValueTask<TenantConfigurationReadPolicyResolution> ResolvePolicyAsync(
        string tenantId,
        CancellationToken cancellationToken) {
        if (principalResolver is null || policyProvider is null) {
            return TenantConfigurationReadPolicyResolution.Unavailable();
        }

        TenantConfigurationPrincipalEvidence principal = await principalResolver.ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        return policyProvider.Resolve(tenantId, principal);
    }
}
