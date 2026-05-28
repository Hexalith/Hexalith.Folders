using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #2 — the Tenant Scope Banner renders authoritative tenant scope from the
/// authenticated context (not the route) and stays read-only.
/// </summary>
public sealed class TenantScopeBannerTests
{
    [Fact]
    public void TenantId_ComesFromAuthenticatedContext_NotRoute()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create(tenantId: "ctx-tenant", userId: "ctx-user");
        using BunitContext _ctx = ctx;

        IRenderedComponent<TenantScopeBanner> rendered = ctx.Render<TenantScopeBanner>();

        rendered.Find("[data-testid=\"tenant-scope-tenant-id\"]").TextContent.ShouldBe("ctx-tenant");
        rendered.Find("[data-testid=\"tenant-scope-principal\"]").TextContent.ShouldBe("ctx-user");
    }

    [Fact]
    public void AllowedAdministerPermissions_RenderAllowedAccess()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        EffectivePermissions permissions = new()
        {
            FolderId = "folder-1",
            AuthorizationOutcome = EffectivePermissionsAuthorizationOutcome.Allowed,
            Permissions = [FolderPermissionLevel.Administer],
            Freshness = Fresh(),
        };

        IRenderedComponent<TenantScopeBanner> rendered = ctx.Render<TenantScopeBanner>(p => p
            .Add(b => b.Permissions, permissions));

        rendered.Markup.ShouldContain("Access allowed");
    }

    [Fact]
    public void DeniedPermissions_RenderDenied_WithoutLeakingExistence()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        EffectivePermissions permissions = new()
        {
            FolderId = "folder-1",
            AuthorizationOutcome = EffectivePermissionsAuthorizationOutcome.Denied_safe,
            Permissions = [],
            Freshness = Fresh(),
        };

        IRenderedComponent<TenantScopeBanner> rendered = ctx.Render<TenantScopeBanner>(p => p
            .Add(b => b.Permissions, permissions));

        rendered.Markup.ShouldContain("Access denied");
    }

    [Fact]
    public void RendersDelegatedActorSummary()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<TenantScopeBanner> rendered = ctx.Render<TenantScopeBanner>();

        // AC #2: the banner summarizes the principal/delegated-actor dimension. This auth model carries no
        // on-behalf-of claim, so the delegated-actor row states direct access rather than omitting it.
        rendered.Find("[data-testid=\"tenant-scope-delegated-actor\"]").TextContent.ShouldContain("Direct");
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<TenantScopeBanner> rendered = ctx.Render<TenantScopeBanner>();

        rendered.ShouldHaveNoMutationAffordances();
    }

    private static FreshnessMetadata Fresh()
        => new()
        {
            Stale = false,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };
}
