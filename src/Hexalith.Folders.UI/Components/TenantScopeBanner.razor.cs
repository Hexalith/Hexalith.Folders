using System.Globalization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;
using Hexalith.FrontComposer.Contracts.Rendering;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / UX-DR6 — the authoritative Tenant Scope Banner. Renders the tenant identifier,
/// effective-access state, principal / delegated-actor summary, policy scope, and last authorization
/// check that anchor every diagnostic page (scope-before-evidence, UX-DR4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant provenance (concern #12):</b> the tenant identifier comes from the authenticated context
/// via <see cref="IUserContextAccessor.TenantId"/> (the <c>tenant_id</c> claim) — <b>never</b> from the
/// <c>{FolderId}</c> route value or any response payload. The effective-access state derives from the
/// <see cref="EffectivePermissions"/> evidence via <see cref="TenantScopeStateMapper"/>.
/// </para>
/// <para>
/// <b>Safe denial (UX-DR21):</b> a denied state shows only that access is denied for the tenant scope;
/// it must not reveal whether any folder or workspace behind the route exists.
/// </para>
/// </remarks>
public partial class TenantScopeBanner : ComponentBase
{
    private const string UnknownText = "Unknown";

    // The authenticated-context bridge (IUserContextAccessor) exposes the tenant and the principal only;
    // this auth model carries no on-behalf-of / act-as delegation claim. The banner therefore states the
    // delegation dimension honestly (the principal is acting directly) rather than silently omitting it.
    private const string DirectAccessText = "Direct — no delegated actor";

    private TenantAccessState _accessState;
    private BadgeSlot _accessSlot;
    private string _accessLabel = string.Empty;
    private string _tenantId = UnknownText;
    private string _principal = UnknownText;
    private string _delegatedActor = DirectAccessText;
    private string _policyScope = UnknownText;
    private string _lastAuthCheck = UnknownText;

    /// <summary>The authenticated-context bridge supplying the authoritative tenant and principal.</summary>
    [Inject]
    private IUserContextAccessor UserContext { get; set; } = default!;

    /// <summary>
    /// Gets or sets the effective-permissions evidence for the current scope, fetched by the host page.
    /// <see langword="null"/> renders the <see cref="TenantAccessState.Unknown"/> posture.
    /// </summary>
    [Parameter]
    public EffectivePermissions? Permissions { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? tenant = UserContext.TenantId;
        _tenantId = string.IsNullOrWhiteSpace(tenant) ? UnknownText : tenant!;

        string? principal = UserContext.UserId;
        _principal = string.IsNullOrWhiteSpace(principal) ? UnknownText : principal!;

        _accessState = TenantScopeStateMapper.Resolve(Permissions);
        _accessSlot = TenantScopeStateMapper.ResolveSlot(_accessState);
        _accessLabel = TenantScopeStateMapper.ResolveLabel(_accessState);
        _policyScope = ResolvePolicyScope(Permissions);
        _lastAuthCheck = ResolveLastAuthCheck(Permissions);
    }

    private static string ResolvePolicyScope(EffectivePermissions? permissions)
    {
        if (permissions?.Permissions is null || permissions.Permissions.Count == 0)
        {
            return UnknownText;
        }

        return string.Join(", ", permissions.Permissions.Select(static level => level.ToString()));
    }

    private static string ResolveLastAuthCheck(EffectivePermissions? permissions)
    {
        // A null freshness or an unpopulated default timestamp must read "Unknown", never a fabricated
        // 0001-01-01 date.
        if (permissions?.Freshness is not { ObservedAt: var observedAt } || observedAt == default)
        {
            return UnknownText;
        }

        return observedAt.ToString("u", CultureInfo.InvariantCulture);
    }
}
