using System.Collections;
using System.Globalization;
using System.Resources;
using System.Security.Claims;

using AngleSharp.Dom;

using Bunit;

using Hexalith.FrontComposer.Shell.Components.Layout;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Components.Tenants;
using Hexalith.Tenants.UI.Components.Users;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.UI.Tests.Components;
using Hexalith.Tenants.UI.State.UserTenants;
using Hexalith.FrontComposer.Shell.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests;

public sealed partial class TenantsWorkspaceTests : BunitContext
{
    public TenantsWorkspaceTests()
    {
        // The workspace now renders Fluent UI v5 components (FluentSelect/FluentTextInput/FluentButton)
        // which import their JS modules in OnAfterRenderAsync. Loose JSInterop lets bUnit no-op those
        // imports instead of throwing under the default Strict mode.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Protected search paging is a required scoped circuit service; the workspace fails loudly without it.
        Services.AddScoped<TenantSearchPagingState>();
        Services.AddScoped<TenantCreateAuditReturnState>();
        Services.AddScoped<TenantAggregateCommandAdmissionGate>();
        Services.AddScoped<TenantCreateAttemptTracker>();
        Services.AddScoped(provider => provider.GetRequiredService<TenantCreateAttemptTracker>().Attempts);
        Services.AddLocalization();
        var caller = Substitute.For<Hexalith.FrontComposer.Contracts.Rendering.IUserContextAccessor>();
        caller.UserId.Returns("operator.alpha");
        Services.AddSingleton(caller);

#if HFC_ROUTE_OPTIONS
        Services.AddScoped<NavigationFailureNotifier>();
#endif
    }

    [Fact]
    public void Workspace_renders_gateway_error_without_mock_tenant_data()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Error()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-list-error']");

        cut.Find("[data-testid='tenants-list-error']").GetAttribute("role").ShouldBe("alert");
        cut.Markup.ShouldContain("The authorized tenant list could not be loaded");
        cut.Markup.ShouldNotContain("tenant-1", Case.Insensitive);
        cut.Markup.ShouldNotContain("sample tenant", Case.Insensitive);
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Workspace_exposes_keyboard_reachable_controls()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-list-refresh']");

        // Controls are Fluent UI v5 components (no raw HTML controls), so they render as the
        // corresponding custom elements. Asserting the tag also guards against regressing to raw HTML.
        cut.Find("[data-testid='tenants-list-refresh']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-list-reset']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-list-search']").NodeName.ShouldBe("FLUENT-TEXT-INPUT");
    }

    [Fact]
    public void Workspace_default_route_renders_associated_tenant_panel_with_complete_body()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-workspace-tabs']");

        // The workspace composes the shared FcPageTabs/FcPageTab contract: Fluent UI owns tab
        // semantics, selection, and the "${id}-panel" association from FcPageTab.ChildContent, so the
        // generated tenants panel carries the real surface. Browser tests own Fluent's JavaScript
        // selection, visibility, and reciprocal accessible-name association.
        FcPageTabs pageTabs = cut.FindComponent<FcPageTabs>().Instance;
        pageTabs.ActiveTabId.ShouldBe("tenants");

        IElement tabsRoot = cut.Find("[data-testid='tenants-workspace-tabs']");
        tabsRoot.GetAttribute("aria-label").ShouldBe("Tenant workspace sections");

        IElement tenantTab = cut.Find("#tenants");
        IElement tenantPanel = cut.Find("#tenants-panel");
        tenantTab.GetAttribute("aria-controls").ShouldBe("tenants-panel");
        tenantPanel.GetAttribute("role").ShouldBe("tabpanel");
        tenantPanel.QuerySelector("[data-testid='tenants-workspace-tenants-panel-content']").ShouldNotBeNull();
        tenantPanel.QuerySelector("[data-testid='tenants-list-refresh']").ShouldNotBeNull();
        tenantPanel.QuerySelector("[data-testid='tenants-list-search']").ShouldNotBeNull();
        tenantPanel.QuerySelector("[data-testid='tenants-workspace-scope']").ShouldNotBeNull();
        tenantPanel.QuerySelector("[data-testid='tenants-create-flow']").ShouldNotBeNull();
        cut.FindAll("#tenants-panel").ShouldHaveSingleItem();
        IElement pageHeader = cut.Find("[data-testid='tenants-list-page-header']");
        pageHeader.QuerySelector("[data-testid='tenants-workspace-tabs']").ShouldBeNull();
        pageHeader.QuerySelector("[role='tabpanel']").ShouldBeNull();
        pageHeader.QuerySelector("[data-testid='tenants-create-flow']").ShouldBeNull();

        cut.Find("[data-testid='tenants-workspace-tabs']").TextContent.ShouldContain("Tenants");
        cut.Find("[data-testid='tenants-workspace-tabs']").TextContent.ShouldContain("Users");
        cut.Find("[data-testid='tenants-list-refresh']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-list-search']").NodeName.ShouldBe("FLUENT-TEXT-INPUT");
        cut.Find("[data-testid='tenants-workspace-scope']").NodeName.ShouldBe("FLUENT-DROPDOWN");
        cut.FindAll("[data-testid='tenants-user-lookup-input']").ShouldBeEmpty();
        gateway.DidNotReceive()
            .GetUserTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(
        TenantWorkspaceState.TenantsTab,
        TenantWorkspaceState.AllScope,
        TenantWorkspaceState.AllScope,
        "[data-testid='tenants-list-refresh']")]
    [InlineData(
        TenantWorkspaceState.TenantsTab,
        TenantWorkspaceState.MyScope,
        TenantWorkspaceState.MyScope,
        "[data-testid='tenants-my-list']")]
    [InlineData(
        TenantWorkspaceState.UsersTab,
        TenantWorkspaceState.AllScope,
        TenantWorkspaceState.AllScope,
        "[data-testid='tenants-user-lookup-input']")]
    [InlineData(
        TenantWorkspaceState.UsersTab,
        TenantWorkspaceState.MyScope,
        TenantWorkspaceState.AllScope,
        "[data-testid='tenants-user-lookup-input']")]
    public void WorkspaceCanonicalStateIdentifiersActivateTheMatchingTabAndSurface(
        string tab,
        string scope,
        string expectedNormalizedScope,
        string expectedSurfaceSelector)
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [MembershipRow("tenant.alpha", "Alpha", TenantRole.TenantOwner)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/tenants?tab={tab}&scope={scope}");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement(expectedSurfaceSelector);

        cut.FindComponent<FcPageTabs>().Instance.ActiveTabId.ShouldBe(
            tab == TenantWorkspaceState.UsersTab ? "workspace-users" : tab);
        cut.Find(expectedSurfaceSelector).ShouldNotBeNull();
        TenantWorkspaceState normalizedState = PrivateField<TenantWorkspaceState>(cut.Instance, "_workspaceState");
        normalizedState.Tab.ShouldBe(tab);
        normalizedState.Scope.ShouldBe(expectedNormalizedScope);

        if (tab == TenantWorkspaceState.UsersTab)
        {
            Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/tenants/workspace-users");
            cut.FindAll("[data-testid='tenants-my-list']").ShouldBeEmpty();
        }

        if (tab == TenantWorkspaceState.TenantsTab)
        {
            cut.FindAll("[data-testid='tenants-workspace-scope'] fluent-option")
                .Select(option => option.GetAttribute("value"))
                .ShouldBe([TenantWorkspaceState.AllScope, TenantWorkspaceState.MyScope]);

            string nextScope = scope == TenantWorkspaceState.AllScope
                ? TenantWorkspaceState.MyScope
                : TenantWorkspaceState.AllScope;
            string expectedUrl = nextScope == TenantWorkspaceState.MyScope
                ? "http://localhost/tenants/tenants?scope=mine"
                : "http://localhost/tenants/tenants";
            string nextSurfaceSelector = nextScope == TenantWorkspaceState.MyScope
                ? "[data-testid='tenants-my-list']"
                : "[data-testid='tenants-list-refresh']";

            FluentSelectInterop.ChangeFluentSelect(cut, "tenants-workspace-scope", nextScope);

            cut.WaitForElement(nextSurfaceSelector);
            Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(expectedUrl);
            PrivateField<TenantWorkspaceState>(cut.Instance, "_workspaceState").Scope.ShouldBe(nextScope);
        }
    }

    [Fact]
    public async Task Route_backed_workspace_tabs_select_the_matching_panel_and_navigate_between_urls()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants/workspace-users");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-user-lookup-input']");
        FluentTabs tabs = cut.FindComponent<FluentTabs>().Instance;
        UserMembershipLookupPanel retainedUsers = cut.FindComponent<UserMembershipLookupPanel>().Instance;
        tabs.ActiveTabId.ShouldBe("workspace-users");
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/tenants/workspace-users");

        await cut.InvokeAsync(() => tabs.ActiveTabIdChanged.InvokeAsync(TenantWorkspaceState.TenantsTab));
        cut.WaitForElement("[data-testid='tenants-list-refresh']");
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/tenants/tenants");
        cut.FindComponent<FluentTabs>().Instance.ActiveTabId.ShouldBe(TenantWorkspaceState.TenantsTab);
        JSInterop.Invocations["prepareTabNavigation"].Single().Arguments.ShouldBe(new object?[] { "/tenants/tenants", "tenants" });
        cut.FindComponent<UserMembershipLookupPanel>().Instance.ShouldBeSameAs(retainedUsers);

        await cut.InvokeAsync(() => tabs.ActiveTabIdChanged.InvokeAsync("workspace-users"));
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/tenants/workspace-users");
        cut.FindComponent<UserMembershipLookupPanel>().Instance.ShouldBeSameAs(retainedUsers);
        JSInterop.Invocations["prepareTabNavigation"].Count.ShouldBe(2);
    }

    [Fact]
    public void Workspace_shows_one_authorized_contextual_global_administrator_entry_with_safe_return_context()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?search=alpha&sort=name&desc=True");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        string? href = cut.Find("[data-testid='tenants-global-administrators-entry']").GetAttribute("href");
        href.ShouldBe("/global-administrators?returnUrl=%2Ftenants%2Ftenants%3Fsearch%3Dalpha%26sort%3Dname%26desc%3DTrue");
        cut.FindAll("[data-testid='tenants-global-administrators-entry']").Count.ShouldBe(1);
    }

    [Fact]
    public void Global_administrator_return_context_suppresses_the_active_workspace_cursor_after_paging()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        List<TenantListRequest> requests = [];
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantListRequest request = call.ArgAt<TenantListRequest>(0)!;
                requests.Add(request);
                return Task.FromResult(TenantListSnapshot.Ready(
                    [],
                    nextCursor: request.Cursor is null ? "opaque-page-2" : null,
                    hasMore: request.Cursor is null,
                    eTag: "\"etag\"",
                    ReadModelFreshnessState.Current,
                    isDegraded: false));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-list-next']");
        cut.Find("[data-testid='tenants-list-next']").Click();
        cut.WaitForAssertion(() => requests[^1].Cursor.ShouldBe("opaque-page-2"));

        Services.GetRequiredService<NavigationManager>().Uri.ShouldContain("cursor=opaque-page-2");
        cut.Find("[data-testid='tenants-global-administrators-entry']")
            .GetAttribute("href")
            .ShouldBe("/global-administrators?returnUrl=%2Ftenants%2Ftenants");
    }

    [Fact]
    public void Workspace_hides_global_administrator_entry_when_authority_is_not_confirmed()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Indeterminate));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
    }

    [Fact]
    public void Workspace_without_bff_resolves_audit_focus_fail_closed()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.WaitForElement("[data-audit-focus-page-terminal]");
        cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
        PrivateField<bool>(cut.Instance, "_auditAuthorityResolved").ShouldBeTrue();
    }

    [Fact]
    public void Missing_audit_return_handoff_shows_notice_and_requests_heading_focus()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?auditReturnUnavailable=true");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.Find("[data-testid='tenants-audit-return-notice']").TextContent.ShouldNotBeNullOrWhiteSpace();
        Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith("/tenants/tenants?auditReturnUnavailable=true");
        string headingReference = HeadingReferenceId(cut);
        cut.WaitForAssertion(() => HeadingFocusRequests(headingReference).ShouldBe(1));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-return-notice']").ShouldBeEmpty());
    }

    [Fact]
    public void Workspace_audit_focus_interop_failure_shows_missing_origin_notice()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        module.Setup<string>("restoreAuditFocus", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Focus helper unavailable."));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?auditFocus=tenant-row-tenant.alpha");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.WaitForElement("[data-testid='tenants-audit-return-notice']");
        cut.Find("[data-testid='tenants-list-page-header']");
    }

    [Fact]
    public void Users_tab_shows_missing_origin_and_partial_return_notices_outside_inactive_tenants_panel()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetUserTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Empty(true, ReadModelFreshnessState.Current, null, "user.alpha")));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            "/tenants?tab=users&userId=user.alpha&auditReturnUnavailable=true&auditPartialReturn=true");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-user-lookup']");
        cut.Find("[data-testid='tenants-audit-return-notice']")
            .Closest("[data-testid='tenants-workspace-tenants-panel-content']").ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-partial-return']")
            .Closest("[data-testid='tenants-workspace-tenants-panel-content']").ShouldBeNull();
    }

    [Fact]
    public void Current_workspace_row_hides_audit_column_only_for_proven_denial()
    {
        TenantListRow row = TenantListRow.FromSummary(new Hexalith.Tenants.Contracts.Queries.TenantSummary(
            "tenant.alpha", "Alpha", TenantStatus.Active)) with
        {
            Freshness = ReadModelFreshnessState.Current,
            Lifecycle = ProjectionLifecycleState.Current,
        };
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Ready(
                [row], nextCursor: null, hasMore: false, eTag: null,
                ReadModelFreshnessState.Current, isDegraded: false) with { Lifecycle = ProjectionLifecycleState.Current }));
        var authentication = new MutableAuthenticationStateProvider(AdministratorPrincipal());
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-list-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-list-audit-entrypoint']").ShouldBeEmpty());

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Indeterminate;
        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-list-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull());
    }

    [Fact]
    public async Task Workspace_authentication_events_clear_pending_authority_restore_entry_and_ignore_late_disposed_completion()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        var authentication = new MutableAuthenticationStateProvider(NonAdministratorPrincipal());
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.Find("[data-testid='tenants-global-administrators-entry']");

        TaskCompletionSource<AuthenticationState> pendingRevocation = authentication.NotifyPending();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty());
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        pendingRevocation.SetResult(new AuthenticationState(NonAdministratorPrincipal()));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty());

        // The event principal is only a transition signal. A raw administrator claim must not bypass the
        // composition seam's corroborated circuit-principal decision.
        authentication.Set(AdministratorPrincipal());
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty());
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Authorized;
        authentication.Set(AdministratorPrincipal());
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-administrators-entry']"));
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
            PrivateField<bool>(cut.Instance, "_canReviewGlobalAdministrators").ShouldBeFalse();
        });
        await Task.Yield();
        await cut.InvokeAsync(() => { });

        TaskCompletionSource<AuthenticationState> lateRestore = authentication.NotifyPending();
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Authorized;
        TenantsWorkspace instance = cut.Instance;
        cut.Dispose();
        // bUnit's rendered-handle disposal removes inspection immediately; invoke the component's
        // IDisposable lifecycle explicitly so the late completion is observed after production teardown.
        instance.Dispose();
        PrivateField<bool>(instance, "_disposed").ShouldBeTrue();
        long authorizationVersionAtDisposal = PrivateField<long>(instance, "_authorizationVersion");
        lateRestore.SetResult(new AuthenticationState(AdministratorPrincipal()));

        // A fixed 20 ms sleep made this a silent-pass: whenever the continuation landed later than the
        // sleep -- routine on a loaded agent -- the disposal guard could be deleted and the assertion still
        // held. Poll for the flag to flip instead, up to a second. With the guard removed the continuation
        // sets it within milliseconds and the loop exits early, so the regression fails loudly; with the
        // guard in place the loop runs to its deadline and the absence is real.
        for (int attempt = 0;
            attempt < 100 && !PrivateField<bool>(instance, "_canReviewGlobalAdministrators");
            attempt++)
        {
            await Task.Yield();
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }

        PrivateField<bool>(instance, "_canReviewGlobalAdministrators").ShouldBeFalse();

        authentication.Set(AdministratorPrincipal());
        await Task.Yield();
        PrivateField<long>(instance, "_authorizationVersion").ShouldBe(authorizationVersionAtDisposal);
    }

    [Fact]
    public void Workspace_pending_optional_authorization_does_not_block_the_tenant_list()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        var pendingAuthorization = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            resolver: () => new ValueTask<TenantLifecycleAuthorizationReflectionState>(pendingAuthorization.Task)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.Find("[data-testid='tenants-list-refresh']");
        cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();

        pendingAuthorization.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.WaitForElement("[data-testid='tenants-global-administrators-entry']");
    }

    [Fact]
    public async Task Workspace_superseded_load_keeps_its_cancellation_source_alive_until_completion()
    {
        TenantListSnapshot ready = TenantListSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current);
        int calls = 0;
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSupersededCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(
                Arg.Any<TenantListRequest>(),
                Arg.Any<TenantListSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int callNumber = Interlocked.Increment(ref calls);
                return callNumber == 2
                    ? CompleteSupersededLoadAsync(
                        call.ArgAt<CancellationToken>(2),
                        ready,
                        cancellationObserved,
                        allowSupersededCompletion)
                    : Task.FromResult(ready);
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForAssertion(() => Volatile.Read(ref calls).ShouldBe(1));

        Task supersededRefresh = cut.Find("[data-testid='tenants-list-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => Volatile.Read(ref calls).ShouldBe(2));

        await cut.Find("[data-testid='tenants-list-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowSupersededCompletion.SetResult();
        await supersededRefresh;

        Volatile.Read(ref calls).ShouldBe(3);

        static async Task<TenantListSnapshot> CompleteSupersededLoadAsync(
            CancellationToken cancellationToken,
            TenantListSnapshot result,
            TaskCompletionSource cancellationObserved,
            TaskCompletionSource allowCompletion)
        {
            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
                cancellationObserved.SetResult);
            await cancellationObserved.Task;
            await allowCompletion.Task;
            _ = cancellationToken.WaitHandle;
            using CancellationTokenRegistration registration = cancellationToken.Register(static () => { });
            return result;
        }
    }

    [Fact]
    public void Workspace_source_assigns_cancellation_disposal_to_the_load_operation_owner()
    {
        string source = File.ReadAllText(Path.Combine(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
                "src",
                "Hexalith.Tenants.UI",
                "Components",
                "Pages",
                "TenantsWorkspace.razor"))
            .ReplaceLineEndings("\n");

        source.ShouldContain("using PendingLoadOperation operation = BeginLoad();");
        source.ShouldNotContain(
            "_loadCancellation?.Cancel();\n        _loadCancellation?.Dispose();\n        _loadCancellation = new CancellationTokenSource();");
    }

    [Fact]
    public void Workspace_optional_authorization_fault_fails_closed_without_aborting_the_tenant_list()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            resolver: static () => ValueTask.FromException<TenantLifecycleAuthorizationReflectionState>(
                new InvalidOperationException("unsafe provider detail"))));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.Find("[data-testid='tenants-list-refresh']");
        cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("unsafe provider detail");
    }

    [Fact]
    public void Workspace_faulted_authentication_event_clears_entry_without_harming_ordinary_workspace()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        var authentication = new MutableAuthenticationStateProvider(AdministratorPrincipal());
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.Find("[data-testid='tenants-global-administrators-entry']");

        authentication.NotifyFault(new InvalidOperationException("unsafe event detail"));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-list-refresh']");
            cut.Markup.ShouldNotContain("unsafe event detail");
        });
    }

    [Fact]
    public void Synchronous_initial_caller_failure_requires_reacquisition_before_workspace_authority()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(true, ReadModelFreshnessState.Current)));
        AuthenticationStateProvider authentication = Substitute.For<AuthenticationStateProvider>();
        int callerReads = 0;
        authentication.GetAuthenticationStateAsync().Returns(_ => ++callerReads == 1
            ? throw new InvalidOperationException("Authentication is unavailable.")
            : Task.FromResult(new AuthenticationState(AdministratorPrincipal())));
        Services.AddSingleton(authentication);
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.FindAll("[data-testid='tenants-global-administrators-entry']").ShouldBeEmpty();
        callerReads.ShouldBe(1);

        cut.Find("[data-testid='tenants-list-refresh']").Click();
        cut.WaitForElement("[data-testid='tenants-global-administrators-entry']");
        callerReads.ShouldBe(2);
    }

    [Theory]
    [InlineData(ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Current)]
    [InlineData(ReadModelFreshnessState.Current, ProjectionLifecycleState.Unknown)]
    public void Current_tenant_row_cannot_open_audit_from_uncertain_list_page(
        ReadModelFreshnessState pageFreshness,
        ProjectionLifecycleState pageLifecycle)
    {
        TenantListRow row = TenantListRow.FromSummary(new Hexalith.Tenants.Contracts.Queries.TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))
            with { Freshness = ReadModelFreshnessState.Current, Lifecycle = ProjectionLifecycleState.Current };
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Ready([row], null, false, "etag", pageFreshness, false) with
            {
                Lifecycle = pageLifecycle,
            }));
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-list-audit-entrypoint']");
        cut.Find("[data-testid='tenants-list-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
    }

    [Fact]
    public void Workspace_unknown_tab_query_normalizes_to_tenants_tab()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?tab=unknown");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-list-refresh']");

        cut.Find("[data-testid='tenants-workspace-tabs']").TextContent.ShouldContain("Tenants");
        cut.FindComponent<FcPageTabs>().Instance.ActiveTabId.ShouldBe(TenantWorkspaceState.TenantsTab);
        cut.FindAll("#tenants-panel").ShouldHaveSingleItem()
            .QuerySelector("[data-testid='tenants-list-refresh']").ShouldNotBeNull();
        cut.FindAll("[data-testid='tenants-user-lookup-input']").ShouldBeEmpty();
        gateway.Received(1)
            .ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Workspace_tenants_tab_mine_scope_uses_self_audit_gateway_without_client_filtering_list_page()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [MembershipRow("tenant.alpha", "Alpha", TenantRole.TenantOwner)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?tab=tenants&scope=mine");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-my-list']");

        gateway.DidNotReceive()
            .ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>());
        gateway.Received(1)
            .GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>());
        cut.Find("[data-testid='tenants-my-tenant-id']").TextContent.ShouldContain("tenant.alpha");
        cut.Find("[data-testid='tenants-audit-entrypoint']").GetAttribute("href").ShouldBeNull();
    }

    [Fact]
    public void Workspace_mine_scope_restores_return_context_after_a_detail_drill_in()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [MembershipRow("tenant.alpha", "Alpha", TenantRole.TenantOwner)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        // Simulate returning from the shared tenant-detail route: scope=mine plus the restored selection and
        // return-focus anchor (AC5).
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?tab=tenants&scope=mine&selected=tenant.alpha&anchor=tenants-my-row-id-dGVuYW50LmFscGhh");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-my-list']");

        // The scope=mine return-context banner renders (distinct testid from the scope=all banner) and names
        // the restored selection.
        cut.Find("[data-testid='tenants-my-return-context']").TextContent.ShouldContain("tenant.alpha");
        cut.FindAll("[data-testid='tenants-list-return-context']").ShouldBeEmpty();

        // The canonical URL preserves the selection/anchor so a redirect does not strip the restored context.
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(
            "http://localhost/tenants/tenants?scope=mine&selected=tenant.alpha&anchor=tenants-my-row-id-dGVuYW50LmFscGhh");
    }

    [Fact]
    public void Workspace_users_tab_prefills_lookup_from_query_without_claiming_all_users_inventory()
    {
        List<UserTenantMembershipRequest> requests = [];
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetUserTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requests.Add(call.ArgAt<UserTenantMembershipRequest>(0));
                return Task.FromResult(UserTenantMembershipSnapshot.Ready(
                    [MembershipRow("tenant.beta", "Beta", TenantRole.TenantReader)],
                    nextCursor: null,
                    hasMore: false,
                    eTag: "\"etag\"",
                    freshness: ReadModelFreshnessState.Current,
                    targetUserId: "USER.Target-01"));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/tenants?tab=users&userId={Uri.EscapeDataString("USER.Target-01")}&sort=role");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-user-lookup-results']");

        gateway.DidNotReceive()
            .ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>());
        requests.ShouldHaveSingleItem().TargetUserId.ShouldBe("USER.Target-01");
        IElement usersTab = cut.Find("#workspace-users");
        IElement usersPanel = cut.FindAll("#workspace-users-panel").ShouldHaveSingleItem();
        cut.FindComponent<FcPageTabs>().Instance.ActiveTabId.ShouldBe("workspace-users");
        usersTab.GetAttribute("aria-controls").ShouldBe("workspace-users-panel");
        usersPanel.GetAttribute("role").ShouldBe("tabpanel");
        usersPanel.QuerySelector("[data-testid='tenants-user-lookup-results']").ShouldNotBeNull();
        cut.FindAll("[data-testid='tenants-workspace-tenants-panel-content']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-user-lookup-input']").GetAttribute("value").ShouldBe("USER.Target-01");
        cut.Markup.ShouldContain("lookup", Case.Insensitive);
        cut.Markup.ShouldNotContain("all users", Case.Insensitive);
    }

    [Fact]
    public void Workspace_hosts_create_flow_in_a_collapsed_accordion_without_a_duplicate_title()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-accordion']");

        // The create block is grouped in a FluentAccordion item (the "Page sections" UX rule), and the
        // create form is hosted inside it rather than as a tall card pushing the list down.
        cut.Find("[data-testid='tenants-create-accordion']").NodeName.ShouldBe("FLUENT-ACCORDION-ITEM");
        cut.Find("[data-testid='tenants-create-flow']");

        // Collapsed by default: the item must not be expanded, so the tenant list stays the primary content.
        string? expanded = cut.Find("[data-testid='tenants-create-accordion']").GetAttribute("expanded");
        (string.IsNullOrEmpty(expanded) || expanded == "false").ShouldBeTrue();

        // The accordion header already shows the title, so the inner <h2> must not be rendered (no duplicate).
        cut.FindAll("#tenants-create-heading").ShouldBeEmpty();
    }

    [Fact]
    public void Audit_return_restores_create_lifecycle_inside_expanded_accordion()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        TenantCreateCommandSnapshot snapshot = TenantCreateCommandSnapshot.Idle()
            .RequestSent(new CreateTenant("tenant.alpha", "Alpha", null), null, true)
            .Accepted(TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-123"));
        Services.GetRequiredService<TenantCreateAuditReturnState>()
            .Remember(snapshot, "tenant.alpha", "Alpha", null);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?auditFocus=tenants-create-lifecycle");
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));

        IRenderedComponent<Router> cut = Render<Router>(parameters => parameters
            .Add(component => component.AppAssembly, typeof(TenantsWorkspace).Assembly)
            .Add(component => component.Found, routeData => builder =>
            {
                builder.OpenComponent<RouteView>(0);
                builder.AddAttribute(1, "RouteData", routeData);
                builder.CloseComponent();
            }));

        cut.WaitForElement("[data-testid='tenants-create-accordion']");
        cut.FindComponent<TenantsWorkspace>().Instance.QueryAuditFocus.ShouldBeNull();
        cut.Find("[data-testid='tenants-create-accordion']").HasAttribute("expanded").ShouldBeTrue();
        CreateTenantFlow restored = cut.FindComponent<CreateTenantFlow>().Instance;

        // The one-shot marker is consumed after focus restoration and the workspace re-renders without it;
        // the accordion stays latched open around the restored lifecycle instead of collapsing.
        restored.RestoreAuditReturn.ShouldBeFalse();
        restored.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Find("[data-testid='tenants-create-lifecycle']");
    }

    [Fact]
    public void Create_audit_return_keeps_list_view_and_rearms_retention_after_the_marker_is_consumed()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        // Events are stored, so the audit dimension is pending and the shared control hosts the entry point.
        TenantCreateCommandSnapshot snapshot = TenantCreateCommandSnapshot.Idle()
            .RequestSent(new CreateTenant("tenant.alpha", "Alpha", null), null, true)
            .Accepted(TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-123"))
            .ApplyStatus(new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true));
        TenantCreateAuditReturnState returnState = Services.GetRequiredService<TenantCreateAuditReturnState>();
        returnState.Remember(snapshot, "tenant.alpha", "Alpha", null);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?status=Active&sort=name&desc=True&auditFocus=tenants-create-lifecycle");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        string href = cut.WaitForElement("fluent-anchor-button[data-testid='tenants-audit-entrypoint']")
            .GetAttribute("href").ShouldNotBeNull();
        href.ShouldContain("returnUrl=%2Ftenants%3Fstatus%3DActive%26sort%3Dname%26desc%3DTrue");
        href.ShouldContain("returnFocus=tenants-create-lifecycle");
        cut.FindComponent<CreateTenantFlow>().Instance.RestoreAuditReturn.ShouldBeFalse();
        cut.Find("[data-testid='tenants-create-accordion']").HasAttribute("expanded").ShouldBeTrue();

        // A second audit trip from the restored flow must find the latest create state again.
        var retained = returnState.Take();
        retained.ShouldNotBeNull();
        retained.Value.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
    }

    [Fact]
    public void Workspace_partial_return_keeps_notice_when_marker_cleanup_fails()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler cleanup = module.SetupVoid("consumeReturnMarkers");
        cleanup.SetException(new Microsoft.JSInterop.JSException("Marker cleanup unavailable."));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?auditPartialReturn=true");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.WaitForAssertion(() => cleanup.Invocations.Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-audit-partial-return']");
        cut.Find("[data-testid='tenants-list-refresh']");
    }

    [Fact]
    public void Workspace_mine_scope_opens_audit_for_an_authorized_current_row()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [CurrentMembershipRow("tenant.alpha", "Alpha", TenantRole.TenantOwner)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current) with { Lifecycle = ProjectionLifecycleState.Current }));
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?tab=tenants&scope=mine");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        string href = cut.WaitForElement("fluent-anchor-button[data-testid='tenants-audit-entrypoint']")
            .GetAttribute("href").ShouldNotBeNull();
        href.ShouldStartWith("/tenants/tenant.alpha/audit?");
        href.ShouldContain("source=my-tenants");
        href.ShouldContain("returnUrl=" + Uri.EscapeDataString("/tenants/tenants?scope=mine"));
        href.ShouldContain("returnFocus=tenants-my-row-id-dGVuYW50LmFscGhh");
    }

    [Fact]
    public void Workspace_users_tab_opens_audit_for_an_authorized_current_row_with_the_user_hint()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetUserTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [CurrentMembershipRow("tenant.beta", "Beta", TenantRole.TenantReader)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current,
                targetUserId: "user.target-01") with { Lifecycle = ProjectionLifecycleState.Current }));
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?tab=users&userId=user.target-01");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        string href = cut.WaitForElement("fluent-anchor-button[data-testid='tenants-audit-entrypoint']")
            .GetAttribute("href").ShouldNotBeNull();
        href.ShouldStartWith("/tenants/tenant.beta/audit?");
        href.ShouldContain("targetUserId=user.target-01");
        href.ShouldContain("source=user-lookup");
        href.ShouldContain("returnUrl=" + Uri.EscapeDataString("/tenants/workspace-users?userId=user.target-01"));
        href.ShouldContain("returnFocus=tenants-user-row-id-dGVuYW50LmJldGE");
    }

    [Fact]
    public void Workspace_mine_detail_return_focuses_the_detail_link_without_heading_focus()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetMyTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [MembershipRow("tenant.alpha", "Alpha", TenantRole.TenantOwner)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<string> detailFocus = module.Setup<string>("restoreDetailFocus", _ => true);
        detailFocus.SetResult("found");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?tab=tenants&scope=mine");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        string detailHref = cut.WaitForElement("[data-testid='tenants-my-detail-link']").GetAttribute("href").ShouldNotBeNull();
        string listReturn = Uri.UnescapeDataString(detailHref.Split("returnUrl=", StringSplitOptions.None)[1]);
        listReturn.ShouldContain("anchor=tenants-my-detail-id-dGVuYW50LmFscGhh");

        Services.GetRequiredService<NavigationManager>().NavigateTo(listReturn);

        cut.WaitForAssertion(() => detailFocus.Invocations.Count.ShouldBe(1));
        detailFocus.Invocations.Single().Arguments.ShouldBe(
            ["tenants-my-detail-id-dGVuYW50LmFscGhh", "tenants-my-page", "tenants-list-heading"]);
        string headingReference = HeadingReferenceId(cut);
        HeadingFocusRequests(headingReference).ShouldBe(0);
        cut.FindAll("[data-testid='tenants-audit-return-notice']").ShouldBeEmpty();
    }

    [Fact]
    public void Route_backed_users_path_opens_the_users_tab_and_keeps_its_route()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        gateway.GetUserTenantsAsync(Arg.Any<UserTenantMembershipRequest>(), Arg.Any<UserTenantMembershipSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UserTenantMembershipSnapshot.Ready(
                [MembershipRow("tenant.beta", "Beta", TenantRole.TenantReader)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current,
                targetUserId: "user.target-01")));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants/workspace-users?userId=user.target-01");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.WaitForElement("[data-testid='tenants-user-lookup-input']");
        cut.FindComponent<FluentTabs>().Instance.ActiveTabId.ShouldBe("workspace-users");
        gateway.DidNotReceive()
            .ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>());
        gateway.Received(1).GetUserTenantsAsync(
            Arg.Is<UserTenantMembershipRequest>(request => request.TargetUserId == "user.target-01"),
            Arg.Any<UserTenantMembershipSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Route_backed_tenants_path_opens_the_tenants_tab_and_keeps_its_route()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants/tenants?status=Active&sort=name");

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();

        cut.WaitForElement("[data-testid='tenants-list-refresh']");
        cut.FindComponent<FluentTabs>().Instance.ActiveTabId.ShouldBe(TenantWorkspaceState.TenantsTab);
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/tenants/tenants?status=Active&sort=name");
    }

    [Fact]
    public void Workspace_allows_create_flow_only_for_authoritatively_empty_unknown_list()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        // Unknown freshness is the empty/first-tenant bootstrap state (an empty index has no persisted
        // ProjectedAt, so no staleness header is emitted). It must NOT block creation, otherwise the first
        // tenant can never be created. Only an authoritative Stale classification blocks the command.
        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-create-unavailable-reason']").ShouldBeEmpty();
    }

    [Fact]
    public void Workspace_blocks_create_flow_for_ambiguous_unknown_list()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: false, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-create-unavailable-reason']")
            .TextContent.ShouldContain("cannot prove an empty first-tenant state");
    }

    [Fact]
    public void Workspace_blocks_create_flow_for_non_empty_unknown_list()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Ready(
                [TenantListRow.FromSummary(new Hexalith.Tenants.Contracts.Queries.TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Unknown,
                isDegraded: false)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-create-unavailable-reason']")
            .TextContent.ShouldContain("cannot prove an empty first-tenant state");
    }

    [Fact]
    public void Workspace_blocks_create_flow_when_list_freshness_is_stale()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Stale)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        // A definite Stale classification must still gate the create command behind a Refresh.
        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-create-unavailable-reason']")
            .TextContent.ShouldContain("cannot prove an empty first-tenant state");
    }

    [Fact]
    public void Workspace_blocks_create_when_command_surface_is_disconnected()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized, commandSurfaceConnected: false));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-create-unavailable-reason']")
            .TextContent.ShouldContain("Tenant command support is unavailable.");
    }

    [Fact]
    public void Workspace_hands_the_create_flow_its_list_projection_baseline_and_absence_proof()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        // Pins the composition seam that carries the whole provenance guarantee. Dropping the
        // BaselineProjectionVersion binding, or returning a null version from the evidence provider,
        // leaves every component-level create test green while confirmation silently loses its baseline.
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Current) with
            {
                ProjectionVersion = "tenant-index-7",
            }));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        CreateTenantFlow flow = cut.FindComponent<CreateTenantFlow>().Instance;
        flow.BaselineProjectionVersion.ShouldBe("tenant-index-7");
        flow.BaselineTenantAbsent.ShouldBeTrue();
        flow.IsCommandSurfaceAvailable.ShouldBeTrue();
        flow.IsFresh.ShouldBeTrue();
    }

    [Fact]
    public void Workspace_does_not_treat_a_page_past_the_end_as_an_authoritative_first_tenant_list()
    {
        // A cursor page with no rows also reports Empty. Granting it the first-tenant exception would
        // both open create on an Unknown-freshness list and hand the flow a false absence proof.
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown) with
            {
                ProjectionVersion = "tenant-index-7",
                RequestCursor = "cursor-page-2",
            }));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        CreateTenantFlow flow = cut.FindComponent<CreateTenantFlow>().Instance;
        flow.BaselineTenantAbsent.ShouldBeFalse();
        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Workspace_blocks_create_when_the_projection_lifecycle_is_ambiguous()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        // Unknown freshness is only the "no first write yet" case when the projection itself is not
        // rebuilding, degraded, or unavailable.
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown) with
            {
                Lifecycle = ProjectionLifecycleState.Rebuilding,
            }));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-create-unavailable-reason']")
            .TextContent.ShouldContain("cannot prove an empty first-tenant state");
    }

    [Fact]
    public void Workspace_names_the_first_tenant_bootstrap_exception_when_create_stays_available()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(isAuthorizationScoped: true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForElement("[data-testid='tenants-create-submit']");

        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-create-availability-note']")
            .TextContent.ShouldContain("awaiting its first projection write");
    }

    [Fact]
    public void Workspace_localizer_uses_the_active_production_bundle_and_fails_closed_for_unknown_keys()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo activeCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            CultureInfo.CurrentCulture = activeCulture;
            CultureInfo.CurrentUICulture = activeCulture;
            var resources = new ResourceManager(typeof(TenantsResources));
            var localizer = new StubTenantsLocalizer();

            string shippedTitle = resources.GetString("Tenants.List.Title", activeCulture).ShouldNotBeNull();
            string shippedPageSizeFormat = resources.GetString("Tenants.List.PageSizeOption", activeCulture).ShouldNotBeNull();

            localizer["Tenants.List.Title"].Value.ShouldBe(shippedTitle);
            localizer["Tenants.List.PageSizeOption", 1234].Value
                .ShouldBe(string.Format(activeCulture, shippedPageSizeFormat, 1234));
            localizer.GetAllStrings(includeParentCultures: false)
                .Single(value => value.Name == "Tenants.List.Title")
                .Value
                .ShouldBe(shippedTitle);

            Should.Throw<KeyNotFoundException>(() => _ = localizer["Tenants.Unknown.Key"]);
            Should.Throw<KeyNotFoundException>(() => _ = localizer["Tenants.Unknown.Key", 1234]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public async Task CreateListProofRefreshCannotReuseMatchingLastConfirmedCache()
    {
        string scope = TenantQueryGateway.AuditCallerScope("operator.alpha");
        TenantListSnapshot cached = TenantCommandProofFixtures.ListProof(
            new Hexalith.Tenants.Contracts.Queries.TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active), "tenant-sequence:42")! with { CallerScope = scope };
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>()).Returns(cached);
        var caller = Substitute.For<Hexalith.FrontComposer.Contracts.Rendering.IUserContextAccessor>();
        caller.UserId.Returns("operator.alpha");
        Services.AddSingleton(caller);
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForAssertion(() => PrivateField<TenantListSnapshot>(cut.Instance, "_snapshot").Rows.Count.ShouldBe(1));
        TenantListSnapshot failure = TenantListSnapshot.Error() with { CallerScope = scope };
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>()).Returns(failure);
        gateway.ClearReceivedCalls();
        var method = typeof(TenantsWorkspace).GetMethod("FindTenantProjectionEvidenceAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        TenantListSnapshot? proof = null;
        await cut.InvokeAsync(async () => proof = await (Task<TenantListSnapshot?>)method.Invoke(cut.Instance, ["tenant.alpha"])!);
        proof.ShouldBeSameAs(failure);
        proof.ShouldNotBeNull().Kind.ShouldBe(TenantListSurfaceKind.Error);
        _ = gateway.Received(1).ListTenantsAsync(Arg.Is<TenantListRequest>(request => request.ETag == null), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Pending audit-return authority retains drafts and exact tracking; denial or caller change clears it.</summary>
    [Theory]
    [InlineData("authorized")]
    [InlineData("denied")]
    [InlineData("caller-changed")]
    public async Task ReviewCreateAuditReturnWaitsForResolvedAuthority(string outcome)
    {
        var authentication = new MutableAuthenticationStateProvider(AdministratorPrincipal());
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        var pending = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int resolutions = 0;
        ITenantQueryGateway reads = Substitute.For<ITenantQueryGateway>();
        reads.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(true, ReadModelFreshnessState.Unknown)));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        Services.AddSingleton(reads);
        Services.AddSingleton(commands);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(resolver: () => ++resolutions == 1
            ? new(pending.Task) : ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        TenantCreateCommandSnapshot previous = TenantCreateCommandSnapshot.Idle()
            .RequestSent(new CreateTenant("tenant.alpha", "Submitted original name", "Submitted original description"), "tenant-sequence:1", true)
            .Accepted(TenantCommandSubmissionResult.Accepted("original-return-message", "original-return-correlation"));
        TenantCreateAuditReturnState retained = Services.GetRequiredService<TenantCreateAuditReturnState>();
        retained.Remember(previous, "tenant.next-draft", "Draft retained name", "Draft retained description");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants?auditFocus=tenants-create-lifecycle");
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IRenderedComponent<Router> cut = Render<Router>(p => p
            .Add(x => x.AppAssembly, typeof(TenantsWorkspace).Assembly)
            .Add(x => x.Found, routeData => builder =>
            {
                builder.OpenComponent<RouteView>(0);
                builder.AddAttribute(1, "RouteData", routeData);
                builder.CloseComponent();
            }));
        cut.WaitForAssertion(() => resolutions.ShouldBe(1));
        CreateTenantFlow initial = cut.FindComponent<CreateTenantFlow>().Instance;
        initial.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        initial.IsAuthorized.ShouldBeFalse();
        object? held = typeof(TenantCreateAuditReturnState).GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(retained);
        held.ShouldNotBeNull();
        held.GetType().GetField("Item1")!.GetValue(held).ShouldBeSameAs(previous);
        held.GetType().GetField("Item2")!.GetValue(held).ShouldBe("tenant.next-draft");
        held.GetType().GetField("Item3")!.GetValue(held).ShouldBe("Draft retained name");
        held.GetType().GetField("Item4")!.GetValue(held).ShouldBe("Draft retained description");
        if (outcome is "caller-changed")
        {
            Services.GetRequiredService<Hexalith.FrontComposer.Contracts.Rendering.IUserContextAccessor>().UserId.Returns("operator.next");
            await cut.InvokeAsync(() => authentication.Set(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator.next"), new Claim("global_admin", "true"), new Claim("eventstore:tenant", "system")], "test"))));
            typeof(TenantCreateAuditReturnState).GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(retained).ShouldBeNull();
        }
        pending.SetResult(outcome is "denied" ? TenantLifecycleAuthorizationReflectionState.MissingPermission : TenantLifecycleAuthorizationReflectionState.Authorized);
        if (outcome is "authorized")
        {
            cut.WaitForAssertion(() => cut.FindComponent<CreateTenantFlow>().Instance.Snapshot.MessageId.ShouldBe(previous.MessageId));
            CreateTenantFlow restored = cut.FindComponent<CreateTenantFlow>().Instance;
            restored.Snapshot.CorrelationId.ShouldBe(previous.CorrelationId);
            restored.Snapshot.Intent.ShouldBe(previous.Intent);
            restored.Snapshot.State.ShouldBe(previous.State);
            cut.Find("[data-testid='tenants-create-tenant-id']").GetAttribute("value").ShouldBe("tenant.next-draft");
            cut.Find("[data-testid='tenants-create-name']").GetAttribute("value").ShouldBe("Draft retained name");
            cut.FindComponents<FluentTextArea>().Single(area => area.Instance.Name == "tenant-description").Instance.Value.ShouldBe("Draft retained description");
        }
        else
        {
            cut.WaitForAssertion(() => typeof(TenantCreateAuditReturnState).GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(retained).ShouldBeNull());
            cut.FindComponent<CreateTenantFlow>().Instance.Snapshot.MessageId.ShouldBeNull();
            cut.Find("[data-testid='tenants-create-name']").GetAttribute("value").ShouldNotBe("Draft retained name");
        }
        commands.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Authoritative empty Unknown freshness never substitutes for resolved GA authority.</summary>
    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate)]
    public async Task LiveWorkspaceCreateRefusesUnprovenAuthorityEvenForFirstTenant(TenantLifecycleAuthorizationReflectionState authority)
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway reads = Substitute.For<ITenantQueryGateway>();
        reads.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(true, ReadModelFreshnessState.Unknown)));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        Services.AddSingleton(reads);
        Services.AddSingleton(commands);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(authority));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        IRenderedComponent<CreateTenantFlow> create = cut.FindComponent<CreateTenantFlow>();
        cut.WaitForAssertion(() => create.Instance.AuthorizationReflection.ShouldBe(authority));
        create.Instance.IsFirstTenantBootstrap.ShouldBeTrue();
        create.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
        create.Find("[data-testid='tenants-create-tenant-id']").Change("first-literal-tenant");
        create.Find("[data-testid='tenants-create-name']").Change("First tenant");
        await create.Find("form").SubmitAsync();
        commands.ReceivedCalls().ShouldBeEmpty();
        create.Find("[data-testid='tenants-create-unavailable-reason']").TextContent.ShouldContain(authority is TenantLifecycleAuthorizationReflectionState.MissingPermission
            ? "not authorized" : "could not be verified");
    }

    /// <summary>The resolved GA first-tenant path dispatches once with unchanged exact-attempt semantics.</summary>
    [Fact]
    public async Task LiveWorkspaceResolvedGlobalAdministratorCreatesAuthoritativeFirstTenant()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider(AdministratorPrincipal()));
        ITenantQueryGateway reads = Substitute.For<ITenantQueryGateway>();
        reads.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(true, ReadModelFreshnessState.Unknown)));
        reads.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantDetailSnapshot.NotFound("first-literal-tenant")));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.CreateTenantAsync(Arg.Any<CreateTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "first-tracking")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)));
        Services.AddSingleton(reads);
        Services.AddSingleton(commands);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        IRenderedComponent<CreateTenantFlow> create = cut.FindComponent<CreateTenantFlow>();
        cut.WaitForAssertion(() => create.Instance.IsAuthorized.ShouldBeTrue());
        create.Instance.IsFirstTenantBootstrap.ShouldBeTrue();
        create.Find("[data-testid='tenants-create-tenant-id']").Change("first-literal-tenant");
        create.Find("[data-testid='tenants-create-name']").Change("First tenant");
        await create.Find("form").SubmitAsync();
        _ = commands.Received(1).CreateTenantAsync(Arg.Is<CreateTenant>(request => request.TenantId == "first-literal-tenant"), Arg.Any<string>(), Arg.Any<CancellationToken>());
        create.Instance.Snapshot.MessageId.ShouldNotBeNullOrWhiteSpace();
        create.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        create.Instance.Snapshot.CorrelationId.ShouldBe("first-tracking");
    }

    /// <summary>A superseded caller lookup cannot turn create back on after a newer refusal.</summary>
    [Fact]
    public async Task LiveWorkspaceIgnoresSupersededCreateAuthority()
    {
        var authentication = new MutableAuthenticationStateProvider(AdministratorPrincipal());
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        var old = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int resolutions = 0;
        ITenantQueryGateway reads = Substitute.For<ITenantQueryGateway>();
        reads.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantListSnapshot.Empty(true, ReadModelFreshnessState.Unknown)));
        Services.AddSingleton(reads);
        Services.AddSingleton<ITenantCommandGateway>(Substitute.For<ITenantCommandGateway>());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(resolver: () => ++resolutions == 1
            ? new(old.Task) : ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.MissingPermission)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        IRenderedComponent<TenantsWorkspace> cut = RenderWorkspace();
        cut.WaitForAssertion(() => resolutions.ShouldBe(1));
        cut.FindComponent<CreateTenantFlow>().Instance.IsAuthorized.ShouldBeFalse();
        await cut.InvokeAsync(() => authentication.Set(NonAdministratorPrincipal()));
        cut.WaitForAssertion(() => cut.FindComponent<CreateTenantFlow>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission));
        old.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.FindComponent<CreateTenantFlow>().Instance.IsAuthorized.ShouldBeFalse();
        cut.Find("[data-testid='tenants-create-submit']").HasAttribute("disabled").ShouldBeTrue();
    }

    private static UserTenantMembershipRow MembershipRow(string tenantId, string name, TenantRole role)
        => new(tenantId, name, TenantStatus.Active, role, ReadModelFreshnessState.Current);

    private static UserTenantMembershipRow CurrentMembershipRow(string tenantId, string name, TenantRole role)
        => new(tenantId, name, TenantStatus.Active, role, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current);

    private int HeadingFocusRequests(string headingReference)
        => JSInterop.Invocations
            .Count(invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus"
                && invocation.Arguments.Count > 0
                && invocation.Arguments[0] is ElementReference element
                && element.Id == headingReference);

    // SetRendererInfo initializes the service provider, so it must run after every registration. The
    // workspace only restores retained protected paging on an interactive render pass.
    private IRenderedComponent<TenantsWorkspace> RenderWorkspace()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return Render<TenantsWorkspace>();
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly ResourceManager _resourceManager = new(typeof(TenantsResources));

        public LocalizedString this[string name]
            => new(name, Resolve(name), resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(
                CultureInfo.CurrentCulture,
                Resolve(name),
                arguments),
                resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            // Both shipped bundles have key parity, so the resolved production set is the complete key
            // space whether or not the caller asks to include parent cultures. Values still flow through
            // the same lookup as the indexers so enumeration cannot drift from active-culture resolution.
            ResourceSet resourceSet = _resourceManager.GetResourceSet(
                CultureInfo.CurrentUICulture,
                createIfNotExists: true,
                tryParents: true)
                ?? throw new MissingManifestResourceException("The Tenants production resource set was not found.");

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Key is string name && entry.Value is string)
                {
                    yield return this[name];
                }
            }
        }

        private static string Resolve(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _resourceManager.GetString(name, CultureInfo.CurrentUICulture)
                ?? throw new KeyNotFoundException($"The Tenants resource key '{name}' is not shipped.");
        }
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandStatusResult.Unknown("Tenant command status is unavailable."));
    }

    private static ClaimsPrincipal AdministratorPrincipal()
        => new(new ClaimsIdentity(
        [
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("global_admin", "true"),
        ], "test"));

    private static ClaimsPrincipal NonAdministratorPrincipal()
        => new(new ClaimsIdentity([new Claim("sub", "operator.alpha")], "test"));

    private static string HeadingReferenceId(IRenderedComponent<TenantsWorkspace> cut)
    {
        FcPageHeader header = cut.FindComponent<FcPageHeader>().Instance;
        return ((ElementReference)(typeof(FcPageHeader)
            .GetField("_headingElement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(header)
            ?? throw new InvalidOperationException("The list heading reference was not captured."))).Id;
    }

    private static T PrivateField<T>(TenantsWorkspace instance, string name)
        => (T)(typeof(TenantsWorkspace)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(instance)
            ?? throw new InvalidOperationException($"Field {name} was not found."));

    private sealed class StubTenantsBffComposition(
        TenantLifecycleAuthorizationReflectionState globalAdministratorReflection = TenantLifecycleAuthorizationReflectionState.Indeterminate,
        Func<ValueTask<TenantLifecycleAuthorizationReflectionState>>? resolver = null,
        bool commandSurfaceConnected = true,
        Func<CancellationToken, ValueTask<TenantLifecycleAuthorizationReflectionState>>? cancellableResolver = null) : ITenantsBffComposition
    {
        private TenantLifecycleAuthorizationReflectionState _reflection = globalAdministratorReflection;

        public bool IsReadSurfaceConnected => true;

        public bool IsCommandSurfaceConnected => commandSurfaceConnected;

        public TenantLifecycleAuthorizationReflectionState Reflection
        {
            get => _reflection;
            set => _reflection = value;
        }

        public TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
            => Reflection;

        public ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
            CancellationToken cancellationToken = default)
            => cancellableResolver?.Invoke(cancellationToken) ?? resolver?.Invoke() ?? ValueTask.FromResult(Reflection);
    }

    private sealed class MutableAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(principal);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);

        public void Set(ClaimsPrincipal updated)
        {
            _state = new AuthenticationState(updated);
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }

        public TaskCompletionSource<AuthenticationState> NotifyPending()
        {
            var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
            NotifyAuthenticationStateChanged(pending.Task);
            return pending;
        }

        public void NotifyFault(Exception exception)
            => NotifyAuthenticationStateChanged(Task.FromException<AuthenticationState>(exception));
    }
}
