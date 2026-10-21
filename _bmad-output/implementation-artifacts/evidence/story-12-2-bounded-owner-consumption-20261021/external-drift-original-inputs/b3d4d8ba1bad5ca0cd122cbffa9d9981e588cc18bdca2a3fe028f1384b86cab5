using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Collections;
using System.Collections.Concurrent;
using System.Resources;
using System.Xml.Linq;

using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;

using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.FrontComposer.Shell.State.Navigation;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Components.Tenants.Audit;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantUsers;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.UI.State.UserTenants;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class TenantAuditPageTests : BunitContext
{
    private bool _rendererInfoConfigured;

    [Fact]
    public async Task Notification_refreshing_affordance_retains_rows_until_the_authoritative_read_completes()
    {
        TenantAuditSnapshot confirmed = ReadySnapshot([Row("event-confirmed", AuditEventCategory.Access)]);
        StubTenantQueryGateway gateway = RegisterServices(confirmed);
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-audit-projection-lifecycle-status']")
            .GetAttribute("role").ShouldBe("status");
        cut.Find("[data-testid='tenants-audit-projection-lifecycle-status-badge']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("projection-lifecycle-badge--current");
        cut.Find("[data-testid='tenants-audit-projection-lifecycle-status-badge']")
            .TextContent.Trim().ShouldBe("Current");
        cut.Find("[data-testid='tenants-audit-row-projection-lifecycle']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("projection-lifecycle-badge--current");
        cut.Find("[data-testid='tenants-audit-row-projection-lifecycle']")
            .TextContent.Trim().ShouldBe("Current");
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());
        gateway.QueueResponse(pending.Task);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-notification-refreshing']")
                .GetAttribute("role").ShouldBe("status");
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-confirmed");
        });

        pending.SetResult(ReadySnapshot([Row("event-refreshed", AuditEventCategory.Access)]));
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='tenants-audit-notification-refreshing']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-refreshed");
        });
    }

    [Fact]
    public async Task Notification_does_not_submit_pending_filters_before_apply()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-confirmed", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>());
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-audit-filter-category", AuditEventCategory.Administrative.ToString());

        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantReadRefreshSubscription refreshSubscription = Services.GetRequiredService<TenantReadRefreshSubscription>();
        await using TenantReadRefreshLease marker = await refreshSubscription.SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            () => { processed.SetResult(); return Task.CompletedTask; });
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha");
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-filter-pending']").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-audit-row']")
            .GetAttribute("data-audit-reference").ShouldBe("event-confirmed");
    }

    [Fact]
    public async Task Tenant_rebinding_disposes_the_previous_subscription_and_only_the_new_scope_refreshes()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-alpha", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-beta", AuditEventCategory.Access)], nextCursor: "beta-next", hasMore: true),
            ReadySnapshot([Row("event-beta-refreshed", AuditEventCategory.Access)]));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());

        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-audit-filter-category", AuditEventCategory.Access.ToString());
        cut.Find("[data-testid='tenants-audit-filter-pending']").ShouldNotBeNull();
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));

        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(2);
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-beta");
            cut.FindAll("[data-testid='tenants-audit-filter-pending']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-audit-next']").HasAttribute("disabled").ShouldBeFalse();
        });
        await subscription.Received(1).UnsubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.beta",
            Arg.Any<CancellationToken>());

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha");

        // The stale-scope nudge must produce NO read, and the matching nudge that follows is what proves it.
        // Draining the dispatcher was not a settling point: `OnProjectionChanged` dispatches via
        // `_ = RunRefreshLoopAsync(key)`, and a fire-and-forget task whose first await is the gateway call
        // has not reached a dispatcher hop yet -- so the drain could complete before the nudge did anything,
        // and the absence assertion passed for a read that simply had not started.
        //
        // Both nudges go through the same renderer dispatcher, which preserves FIFO order, so once the
        // matching nudge's read has landed the stale one has provably been processed. The count is then
        // conclusive: it would be 4, not 3, had the stale-scope nudge issued a read of its own.
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType,
            "tenant.beta");
        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(3);
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-beta-refreshed");
        });

        gateway.Requests.Count.ShouldBe(3, "The stale-scope nudge must not have issued a read of its own.");

        // Exactly one alpha read ever happened: the initial load, before the rebind. A stale-scope nudge
        // that slipped through would show up here as a second one.
        gateway.Requests.Count(request => request.TenantId == "tenant.alpha").ShouldBe(1);
    }

    [Fact]
    public async Task Tenant_arriving_during_subscription_setup_is_handed_to_the_setup_owner()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-alpha", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-beta", AuditEventCategory.Access)]));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var alphaSetup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        subscription
            .SubscribeAsync(GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(alphaSetup.Task);
        subscription
            .SubscribeAsync(GetTenantAuditQuery.ProjectionType, "tenant.beta", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());

        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));
        alphaSetup.SetResult();

        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.WaitForAssertion(() => subscription.ReceivedCalls()
            .Count(call => string.Equals(call.GetArguments()[1] as string, "tenant.beta", StringComparison.Ordinal))
            .ShouldBe(1));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.beta",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Same_tenant_parameter_pass_retries_a_failed_refresh_subscription()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-alpha", AuditEventCategory.Access)]));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        subscription.SubscribeAsync(
                GetTenantAuditQuery.ProjectionType,
                "tenant.alpha",
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException(new InvalidOperationException("transient setup failure")),
                Task.CompletedTask);
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>());

        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));

        await subscription.Received(2).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>());
        gateway.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Newer_notification_refresh_rejects_a_late_cursor_result_and_preserves_cursor_history()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot(
                [Row("event-first", AuditEventCategory.Access)],
                nextCursor: "protected-next",
                hasMore: true),
            ReadySnapshot([Row("event-newest", AuditEventCategory.Access)]));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        var latePage = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());
        gateway.QueueResponse(latePage.Task);

        Task nextNavigation = cut.Find("[data-testid='tenants-audit-next']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha");
        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(3);
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-newest");
        });

        latePage.SetResult(ReadySnapshot([Row("event-late", AuditEventCategory.Access)]));
        await nextNavigation;

        cut.Find("[data-testid='tenants-audit-row']")
            .GetAttribute("data-audit-reference").ShouldBe("event-newest");
        cut.Markup.ShouldNotContain("event-late");
        gateway.Requests[1].Cursor.ShouldBe("protected-next");
        gateway.Requests[2].Cursor.ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Tenant_audit_page_renders_grid_filters_paging_and_support_safe_rows()
    {
        const string visibleReference = "event-safe-reference";
        TenantAuditSnapshot snapshot = ReadySnapshot(
            [
                Row("event-safe-reference", AuditEventCategory.Access, "userId: target-user; role: TenantReader"),
            ],
            nextCursor: "next-cursor",
            hasMore: true);
        StubTenantQueryGateway gateway = RegisterServices(snapshot);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsClipboard.js");
        JSRuntimeInvocationHandler writeHandler = module.SetupVoid("writeText", visibleReference).SetVoidResult();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        gateway.Requests.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        cut.Find("[data-testid='tenants-audit-filter-category']").GetAttribute("value").ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("type").ShouldBe("datetime-local");
        cut.Find("[data-testid='tenants-audit-filter-to']").GetAttribute("type").ShouldBe("datetime-local");
        cut.Find("[data-testid='tenants-audit-refresh']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-next']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-previous']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-row']").GetAttribute("data-audit-reference").ShouldBe("event-safe-reference");
        cut.Find("[data-testid='tenants-audit-copy-reference']").GetAttribute("data-copy-kind").ShouldBe("ApprovedReference");
        cut.Find("[data-testid='tenants-audit-row-reference'] > .audit-data-grid__wrap").TextContent.ShouldBe(visibleReference);
        cut.Find("[data-testid='tenants-audit-row-context']").TextContent
            .ShouldBe("userId: target-user; role: TenantReader");
        cut.Find("[data-testid='tenants-audit-receipt-open']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Markup.ShouldContain("target-user");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("EventStore metadata", Case.Insensitive);

        cut.Find("[data-surface-testid='tenants-audit-copy-reference']").Click();
        cut.WaitForAssertion(() => writeHandler.Invocations.Count.ShouldBe(1));
        writeHandler.Invocations.Single().Arguments[0].ShouldBe(visibleReference);
    }

    [Fact]
    public void RestoredAuditContextRejectsUnsafeIdentity()
    {
        RegisterServices(ReadySnapshot([Row("event-safe-reference", AuditEventCategory.Access)]));
        const string target = "  target\u034F\uFE0F\\{U+200D}  ";
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("targetUserId", target));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(component => component.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-audit-context']").ShouldBeEmpty();
    }

    [Fact]
    public void Tenant_audit_page_omits_grid_copy_for_an_unsafe_raw_event_reference()
    {
        RegisterServices(ReadySnapshot([Row("Bearer raw-token", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-audit-copy-reference']").ShouldBeEmpty();
        cut.FindAll("[data-surface-testid='tenants-audit-copy-reference']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-row-reference'] > .audit-data-grid__wrap")
            .TextContent.ShouldBe("Reference unavailable");
        cut.Find("[data-testid='tenants-audit-row']").HasAttribute("data-audit-reference").ShouldBeFalse();
        cut.Markup.ShouldNotContain("raw-token", Case.Insensitive);
    }

    // A composed "{reference} - {context}" literal can pass the policy while the reference itself is
    // missing, which would render and copy a literal carrying no audit reference at all.
    [Fact]
    public void Tenant_audit_page_omits_reference_surfaces_when_only_the_context_is_present()
    {
        RegisterServices(ReadySnapshot(
            [
                Row(string.Empty, AuditEventCategory.Access, referenceContext: "userId: target-user"),
            ]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-row-reference'] > .audit-data-grid__wrap")
            .TextContent.ShouldBe("Reference unavailable");
        cut.FindAll("[data-testid='tenants-audit-copy-reference']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-row']").HasAttribute("data-audit-reference").ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-role']").ShouldBeEmpty();
    }

    [Fact]
    public void Tenant_audit_page_keeps_the_safe_reference_but_omits_unsafe_narrative_context_and_correction()
    {
        RegisterServices(ReadySnapshot(
            [
                Row(
                    "event-safe-correction",
                    AuditEventCategory.Access,
                    referenceContext: "userId: Bearer raw-token; role: TenantReader",
                    eventType: "UserRemovedFromTenant"),
            ]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-row-reference'] > .audit-data-grid__wrap")
            .TextContent.ShouldBe("event-safe-correction");
        cut.Find("[data-testid='tenants-audit-copy-reference']");
        cut.Find("[data-testid='tenants-audit-row']").GetAttribute("data-audit-reference")
            .ShouldBe("event-safe-correction");
        cut.FindAll("[data-testid='tenants-audit-row-context']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-role']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("raw-token", Case.Insensitive);
        cut.Markup.ShouldNotContain("Bearer", Case.Insensitive);
    }

    [Fact]
    public void Tenant_audit_page_opens_receipt_from_loaded_row_without_extra_backend_query()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-safe-reference", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?supportSafeCommandReference=command-safe-reference");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();

        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-safe-reference");
        cut.Find("[data-testid='tenants-audit-receipt-copy']").GetAttribute("data-copy-kind").ShouldBe("ApprovedReference");
        cut.Find("[data-testid='tenants-audit-receipt']").TextContent.ShouldNotContain("command-safe-reference");
        cut.Markup.ShouldContain("command-safe-reference");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("EventStore metadata", Case.Insensitive);
    }

    [Fact]
    public void Tenant_audit_page_receipt_reference_query_fails_closed_when_row_is_not_loaded()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-loaded", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-not-loaded");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");

        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("not loaded");
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldBe("-");
        cut.Find("[data-testid='tenants-audit-receipt-scope'] dd").TextContent.ShouldBe("-");
        cut.Markup.ShouldNotContain("event-not-loaded");
        // Visible text only — avoids the Fluent success-color token false positive (see VisibleText).
        cut.VisibleText().ShouldNotContain("Success", Case.Insensitive);
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Unavailable)]
    [InlineData(TenantAuditSurfaceKind.Error)]
    public void Unavailable_audit_read_escalates_once_from_the_page_never_again_from_its_receipt(TenantAuditSurfaceKind kind)
    {
        RegisterServices(SnapshotFor(kind));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-not-loaded");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        if (kind is TenantAuditSurfaceKind.Unavailable)
        {
            cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-audit-availability']")
                .GetAttribute("data-state").ShouldBe("unavailable");
        }

        cut.Find("[data-testid='tenants-audit-recovery-escalate']").GetAttribute("href").ShouldBe("/support/audit-incident");

        // The page already escalates the failed read, and hands the receipt the very condition it rendered the
        // link from, so the receipt never offers a second escalation for the same failure.
        cut.FindComponent<AuditEvidenceReceipt>().Instance.HostEscalatesFailedRead.ShouldBeTrue();
        cut.FindAll("[data-testid='tenants-audit-receipt-recovery-escalate']").ShouldBeEmpty();
        cut.FindAll("[href='/support/audit-incident']").Count.ShouldBe(1);
    }

    [Fact]
    public void Tenant_audit_page_receipt_reference_query_opens_loaded_row_without_extra_backend_query()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-safe-reference", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-safe-reference&supportSafeCommandReference=command-safe-reference");

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-safe-reference");
        cut.Find("[data-testid='tenants-audit-receipt-copy']").GetAttribute("data-copy-kind").ShouldBe("ApprovedReference");
        cut.Find("[data-testid='tenants-audit-receipt']").TextContent.ShouldNotContain("command-safe-reference");
        cut.Markup.ShouldContain("command-safe-reference");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
    }

    [Fact]
    public void Receipt_url_changes_use_loaded_rows_and_a_manual_choice_survives_refresh_and_close()
    {
        TenantAuditSnapshot rows = ReadySnapshot([
            Row("event-a", AuditEventCategory.Access),
            Row("event-b", AuditEventCategory.Access),
        ]);
        StubTenantQueryGateway gateway = RegisterServices(rows, rows, rows);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-a");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldContain("event-a"));

        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-b");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-b");
        gateway.Requests.Count.ShouldBe(1);

        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-a");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.FindAll("[data-testid='tenants-audit-receipt-open']")[1].Click();
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-b");

        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-b");
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-reset']").Click();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public void Changed_receipt_url_during_initial_read_does_not_refocus_its_heading_when_ready_replaces_loading()
    {
        StubTenantQueryGateway gateway = RegisterServices();
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(pending.Task);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> receiptFocus = module.Setup<bool>("isFocusInsideAuditReceipt");
        receiptFocus.SetResult(false);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-a");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(1));
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));

        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-b");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));

        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldBe("-");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("loading", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-audit-receipt-missing']").ShouldBeEmpty();
        cut.FindAll("[data-surface-testid='tenants-audit-receipt-copy']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(1);
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(2));
        int loadingHeadingFocusCount = focus.Invocations.Count;

        pending.SetResult(ReadySnapshot([
            Row("event-a", AuditEventCategory.Access),
            Row("event-b", AuditEventCategory.Access),
        ]));
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-b");
            cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
        });
        receiptFocus.Invocations.ShouldNotBeEmpty();
        focus.Invocations.Count.ShouldBe(loadingHeadingFocusCount);
        gateway.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public void Receipt_heading_focus_cancellation_is_treated_as_renderer_teardown()
    {
        RegisterServices(ReadySnapshot([Row("event-safe", AuditEventCategory.Access)]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>(
            "focusElementById",
            "tenants-audit-receipt-heading");
        focus.SetException(new TaskCanceledException("Renderer teardown."));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-safe");

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-state']")
            .TextContent.ShouldContain("ready", Case.Insensitive));
        focus.Invocations.Count.ShouldBe(1);
    }

    [Fact]
    public void Dismissed_deep_link_with_the_same_reference_opens_again_for_a_new_tenant()
    {
        TenantAuditRow betaRow = Row("event-shared", AuditEventCategory.Access) with
        {
            TenantId = "tenant.beta",
            Scope = "tenant.beta",
        };
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-shared", AuditEventCategory.Access)]),
            ReadySnapshot([betaRow]));
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-shared");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-state']")
            .TextContent.ShouldContain("ready"));
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();

        navigation.NavigateTo("/tenants/tenant.beta/audit?receiptReference=event-shared");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
            cut.Find("[data-testid='tenants-audit-receipt-scope']").TextContent.ShouldContain("tenant.beta");
        });
        gateway.Requests.Select(request => request.TenantId).ShouldBe(["tenant.alpha", "tenant.beta"]);
    }

    [Fact]
    public async Task Queued_launcher_callback_uses_the_matching_current_row_after_refresh()
    {
        TenantAuditRow oldRow = Row("event-shared", AuditEventCategory.Access) with { ActorId = "old-actor" };
        TenantAuditRow currentRow = Row("event-shared", AuditEventCategory.Access,
            eventType: "UserRemovedFromTenant") with { ActorId = "current-actor" };
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([oldRow]), ReadySnapshot([currentRow]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        EventCallback<TenantAuditRow> queuedLauncher = cut.FindComponent<AuditDataGrid>().Instance.OnOpenReceipt;

        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(2);
            cut.Find("[data-testid='tenants-audit-row-actor']").TextContent.ShouldBe("current-actor");
        });
        await cut.InvokeAsync(() => queuedLauncher.InvokeAsync(oldRow));

        cut.Find("[data-testid='tenants-audit-receipt-actor']").TextContent.ShouldContain("current-actor");
        cut.Markup.ShouldNotContain("old-actor");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
        cut.FindComponent<AuditEvidenceReceipt>().Instance.CorrectionIntent!.OutcomeType
            .ShouldBe("UserRemovedFromTenant");
    }

    [Fact]
    public void Receipt_close_uses_the_current_row_launcher_and_falls_back_when_focus_fails()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-a", AuditEventCategory.Access), Row("event-b", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-b", AuditEventCategory.Access), Row("event-a", AuditEventCategory.Access)]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(false);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.FindAll("[data-testid='tenants-audit-receipt-open']")[1].Click();
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));

        cut.FindAll("[data-testid='tenants-audit-receipt-open']")[0]
            .GetAttribute("id").ShouldBe("tenants-audit-receipt-launcher-0");
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBeGreaterThanOrEqualTo(3));
        focus.Invocations.ToArray()[^2].Arguments[0].ShouldBe("tenants-audit-receipt-launcher-0");
        focus.Invocations.ToArray()[^1].Arguments[0].ShouldBe("tenant-audit-heading");
    }

    [Fact]
    public void Removing_an_unsafe_url_receipt_returns_focus_to_the_page_heading()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-safe", AuditEventCategory.Access)]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=Bearer%20raw-token");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBeGreaterThan(0));
        int beforeRemoval = focus.Invocations.Count;

        navigation.NavigateTo("/tenants/tenant.alpha/audit");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));

        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(beforeRemoval + 1));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenant-audit-heading");
        gateway.Requests.Count.ShouldBe(1);
        cut.Markup.ShouldNotContain("raw-token");
    }

    [Fact]
    public void Removing_a_loaded_url_receipt_returns_focus_to_its_row_launcher()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([
                Row("event-first", AuditEventCategory.Access),
                Row("event-safe", AuditEventCategory.Access),
            ]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-safe");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        cut.WaitForAssertion(() => focus.Invocations.ShouldNotBeEmpty());
        int beforeRemoval = focus.Invocations.Count;

        navigation.NavigateTo("/tenants/tenant.alpha/audit");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));

        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(beforeRemoval + 1));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-launcher-1");
        gateway.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Static_render_skips_receipt_focus_probes()
    {
        RegisterServices(ReadySnapshot([Row("event-safe", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-safe");
        ConfigureRenderer(new RendererInfo("Static", isInteractive: false));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("ready", Case.Insensitive);
        JSInterop.Invocations.Any(invocation => invocation.Identifier is "isFocusInsideAuditReceipt")
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Delayed_focus_module_import_does_not_focus_an_open_receipt_after_close()
    {
        RegisterServices(ReadySnapshot([Row("event-safe", AuditEventCategory.Access)]));
        var focusRuntime = new DelayedFocusJsRuntime();
        Services.AddSingleton<IJSRuntime>(focusRuntime);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        await focusRuntime.ImportRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();

        await focusRuntime.SecondImportRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        focusRuntime.SecondImportRelease.SetResult();
        await focusRuntime.LauncherFocused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        focusRuntime.FirstImportRelease.SetResult();
        await focusRuntime.FirstImportReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => Task.CompletedTask);

        focusRuntime.FocusTargets.ToArray().ShouldBe(["tenants-audit-receipt-launcher-0"]);
    }

    [Fact]
    public void Receipt_invalidation_preserves_focus_outside_receipt_and_hands_off_focus_inside_receipt()
    {
        TenantAuditSnapshot rows = ReadySnapshot([Row("event-a", AuditEventCategory.Access)]);
        RegisterServices(rows,
            TenantAuditSnapshot.Error(new TenantAuditRequest("tenant.alpha")),
            TenantAuditSnapshot.Unavailable(new TenantAuditRequest("tenant.alpha")));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> inside = module.Setup<bool>("isFocusInsideAuditReceipt");
        inside.SetResult(false);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-a");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-state']")
            .TextContent.ShouldContain("ready"));
        int focusCount = focus.Invocations.Count;

        cut.Find("[data-testid='tenants-audit-filter-from']").Change("2026-01-01T00:00");
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-state']")
            .TextContent.ShouldContain("failed"));
        focus.Invocations.Count.ShouldBe(focusCount);
        inside.Invocations.ShouldNotBeEmpty();

        inside.SetResult(true);
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-state']")
            .TextContent.ShouldContain("unavailable"));
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBeGreaterThan(focusCount));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Viewport_removal_hands_off_only_a_focused_receipt_correction_control(bool correctionHasFocus)
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        RegisterServices(viewport, ReadySnapshot([Row("event-role-change", AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader", eventType: "UserRoleChanged")]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> insideCorrection = module.Setup<bool>("isFocusInsideAuditReceiptCorrection");
        insideCorrection.SetResult(correctionHasFocus);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        int priorFocus = focus.Invocations.Count;

        viewport.Observe(ViewportTier.Phone);

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']")
            .ShouldBeEmpty());
        cut.WaitForAssertion(() => insideCorrection.Invocations.Count.ShouldBe(1));
        if (correctionHasFocus)
        {
            cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(priorFocus + 1));
            focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
        }
        else
        {
            focus.Invocations.Count.ShouldBe(priorFocus);
        }
    }

    [Fact]
    public async Task Same_state_notification_probes_focus_only_when_a_receipt_correction_control_disappears()
    {
        TenantAuditRow correctable = Row("event-role-change", AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader", eventType: "UserRoleChanged");
        TenantAuditRow uncorrectable = Row("event-role-change", AuditEventCategory.Access,
            eventType: "UserAddedToTenant");
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([correctable]), ReadySnapshot([correctable]), ReadySnapshot([uncorrectable]));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> insideCorrection = module.Setup<bool>("isFocusInsideAuditReceiptCorrection");
        insideCorrection.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>());
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        int priorFocus = focus.Invocations.Count;

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha");
        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(2);
            cut.FindAll("[data-testid='tenants-audit-notification-refreshing']").ShouldBeEmpty();
        });
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        insideCorrection.Invocations.ShouldBeEmpty();
        focus.Invocations.Count.ShouldBe(priorFocus);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetTenantAuditQuery.ProjectionType, "tenant.alpha");
        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(3);
            cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']")
                .ShouldBeEmpty();
            insideCorrection.Invocations.Count.ShouldBeGreaterThan(0);
            focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
        });
    }

    [Fact]
    public async Task Delayed_receipt_focus_probe_cannot_refocus_the_receipt_after_close()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-a", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-a", AuditEventCategory.Access)]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> pendingProbe = module.Setup<bool>("isFocusInsideAuditReceipt");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => pendingProbe.Invocations.Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        pendingProbe.SetResult(true);
        await refresh;
        cut.WaitForAssertion(() => focus.Invocations.Last().Arguments[0]
            .ShouldBe("tenant-audit-heading"));
        focus.Invocations.Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Loading_receipt_renders_while_focus_probe_and_authoritative_read_are_pending()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-a", AuditEventCategory.Access)]));
        var pendingRead = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        module.Setup<bool>("focusElementById", _ => true).SetResult(true);
        JSRuntimeInvocationHandler<bool> pendingProbe = module.Setup<bool>("isFocusInsideAuditReceipt");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']");
        gateway.QueueResponse(pendingRead.Task);
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => pendingProbe.Invocations.Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("loading", Case.Insensitive);
        cut.FindAll("[data-surface-testid='tenants-audit-receipt-copy']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(1);

        pendingProbe.SetResult(false);
        // No render occurs while the authoritative read is pending; observe the request itself.
        SpinWait.SpinUntil(() => gateway.Requests.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("loading", Case.Insensitive);

        pendingRead.SetResult(ReadySnapshot([Row("event-a", AuditEventCategory.Access)]));
        await refresh;
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("ready", Case.Insensitive);
    }

    // The Loading write replaces the receipt instance before the focus probe returns, and it removes Copy and
    // the recovery actions. A probe that found focus inside the receipt must still move focus to the heading
    // immediately, not after the authoritative read completes.
    [Fact]
    public async Task Loading_receipt_probe_that_found_receipt_focus_moves_it_to_the_heading_before_the_read_completes()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-a", AuditEventCategory.Access)]));
        var pendingRead = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        JSRuntimeInvocationHandler<bool> pendingProbe = module.Setup<bool>("isFocusInsideAuditReceipt");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        gateway.QueueResponse(pendingRead.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => pendingProbe.Invocations.Count.ShouldBe(1));
        pendingProbe.SetResult(true);

        // The heading focus runs in OnAfterRenderAsync after the probe's render, and no later render occurs
        // while the read is pending, so wait on the invocation itself rather than a render-driven assertion.
        SpinWait.SpinUntil(() => focus.Invocations.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("loading", Case.Insensitive);
        SpinWait.SpinUntil(() => gateway.Requests.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();

        pendingRead.SetResult(ReadySnapshot([Row("event-a", AuditEventCategory.Access)]));
        await refresh;
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Stale, true)]
    [InlineData(TenantAuditSurfaceKind.Degraded, true)]
    [InlineData(TenantAuditSurfaceKind.Error, false)]
    [InlineData(TenantAuditSurfaceKind.Unavailable, false)]
    [InlineData(TenantAuditSurfaceKind.Unauthorized, false)]
    [InlineData(TenantAuditSurfaceKind.InvalidCursor, false)]
    public void Missing_url_reference_discloses_absence_only_on_a_checked_page(
        TenantAuditSurfaceKind kind, bool checkedPage)
    {
        RegisterServices(SnapshotFor(kind));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-not-loaded");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.FindAll("[data-testid='tenants-audit-receipt-missing']").Count.ShouldBe(checkedPage ? 1 : 0);
        cut.FindAll("[data-surface-testid='tenants-audit-receipt-copy']").ShouldBeEmpty();
    }

    [Fact]
    public void Tenant_audit_page_fails_closed_for_membership_correction_when_intended_role_is_missing()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot(
            [
                Row(
                    "event-removed-member",
                    AuditEventCategory.Access,
                    referenceContext: "userId: target-user",
                    eventType: "UserRemovedFromTenant"),
            ]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent.ShouldContain("Choose the intended role");
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(1);
        cut.Markup.ShouldNotContain("POST /api/v1/commands", Case.Insensitive);
        // Visible text only — "undo" also appears inside the Fluent token --colorNeutralForegroundOnBrand.
        cut.VisibleText().ShouldNotContain("undo", Case.Insensitive);
        cut.Markup.ShouldNotContain("rollback", Case.Insensitive);
        cut.Markup.ShouldNotContain("hidden edit", Case.Insensitive);
    }

    [Fact]
    public void Tenant_audit_page_receipt_flow_keeps_original_evidence_visible_when_correction_is_blocked()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot(
            [
                Row(
                    "event-removed-member",
                    AuditEventCategory.Access,
                    referenceContext: "userId: target-user",
                    eventType: "UserRemovedFromTenant"),
            ]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();

        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldContain("event-removed-member");
        cut.FindAll("[data-testid='tenants-correction-unavailable-reason']")
            .Any(reason => reason.TextContent.Contains("Choose the intended role", StringComparison.Ordinal))
            .ShouldBeTrue();
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("GlobalAdministratorRemoved", "grid")]
    [InlineData("GlobalAdministratorRemoved", "receipt")]
    [InlineData("GlobalAdministratorSet", "grid")]
    [InlineData("GlobalAdministratorSet", "receipt")]
    public void TenantAuditPageOffersGlobalAdministratorCorrectionWithAuthority(string eventType, string origin)
    {
        RegisterGlobalAdminServices(
            authorized: true,
            eventType == "GlobalAdministratorSet"
                ? GlobalAdmins("admin-user", "other-admin")
                : GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot(eventType, "admin-user"));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.WaitForElement("[data-testid='tenants-correction-start']");
        if (origin == "receipt")
        {
            cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
            cut.FindComponent<AuditEvidenceReceipt>().Find("[data-testid='tenants-correction-start']").Click();
        }
        else
        {
            cut.Find("[data-testid='tenants-correction-start']").Click();
        }
        cut.WaitForElement("[data-testid='tenants-correction-panel']");
        cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!.CommandType.ShouldBe(
            eventType == "GlobalAdministratorSet"
                ? TenantCorrectionCommandType.RemoveGlobalAdministrator
                : TenantCorrectionCommandType.SetGlobalAdministrator);
        cut.FindAll("[data-testid='tenants-correction-role']").ShouldBeEmpty();
    }

    [Fact]
    public void Open_global_correction_refreshes_preview_when_paging_past_global_audit_rows()
    {
        TenantAuditSnapshot first = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user") with
        {
            NextCursor = "next-global",
            HasMore = true,
        };
        TenantAuditRow unrelated = first.Rows[0] with
        {
            EventReference = "event-unrelated",
            EventType = "TenantCreated",
            Outcome = "TenantCreated",
        };
        TenantAuditSnapshot second = first with
        {
            Rows = [unrelated],
            RequestCursor = "next-global",
            NextCursor = "next-unrelated",
        };
        TenantAuditSnapshot third = second with
        {
            RequestCursor = "next-unrelated",
            NextCursor = null,
            HasMore = false,
        };
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), first, second, third);
        bool projectionChanged = false;
        gateway.GlobalAdminProvider = _ => projectionChanged
            ? GlobalAdmins("other-admin", "third-admin")
            : GlobalAdmins("other-admin");

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("1");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
        int readsBeforePaging = gateway.GlobalAdminRequests.Count;
        projectionChanged = true;

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-unrelated");
            cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("2");
            cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
        });
        gateway.GlobalAdminRequests.Count.ShouldBeGreaterThan(readsBeforePaging);
        gateway.Requests[1].Cursor.ShouldBe("next-global");

        gateway.GlobalAdminFault = new InvalidOperationException("projection unavailable");
        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("-");
            cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        });
        gateway.Requests[2].Cursor.ShouldBe("next-unrelated");
    }

    [Theory]
    [InlineData("GlobalAdministratorRemoved", true, false, true)]
    [InlineData("GlobalAdministratorRemoved", false, true, false)]
    [InlineData("GlobalAdministratorSet", false, true, true)]
    [InlineData("GlobalAdministratorSet", true, false, false)]
    public void Global_correction_start_requires_only_its_selected_tracked_command(
        string eventType, bool grantSupported, bool removeSupported, bool expectedAvailable)
    {
        TenantAuditSnapshot audit = GlobalAdminAuditSnapshot(eventType, "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true,
            eventType == "GlobalAdministratorSet"
                ? GlobalAdmins("admin-user", "other-admin")
                : GlobalAdmins("other-admin"),
            audit);
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        commands.SupportsTrackedGlobalAdministratorDispatch = grantSupported;
        commands.SupportsTrackedGlobalAdministratorRemoveDispatch = removeSupported;

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => gateway.GlobalAdminRequests.Count.ShouldBeGreaterThan(0));
        if (expectedAvailable)
        {
            cut.WaitForElement("[data-testid='tenants-correction-start']");
        }
        else
        {
            cut.WaitForAssertion(() => cut.FindComponent<AuditDataGrid>().Instance
                .CorrectionIntentProvider!(audit.Rows[0]).UnavailableReasons
                .ShouldContain(TenantCorrectionUnavailableReason.GlobalAdministratorCommandSupportUnavailable));
            cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        }
    }

    [Fact]
    public void Legacy_unordered_global_projection_cannot_start_correction()
    {
        TenantAuditSnapshot audit = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true,
            GlobalAdmins("other-admin") with { ProjectionVersion = "legacy-projection" },
            audit);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));

        cut.WaitForAssertion(() => gateway.GlobalAdminRequests.Count.ShouldBeGreaterThan(0));
        cut.WaitForAssertion(() => cut.FindComponent<AuditDataGrid>().Instance
            .CorrectionIntentProvider!(audit.Rows[0]).UnavailableReasons
            .ShouldContain(TenantCorrectionUnavailableReason.CurrentProjectionUnavailable));
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("GlobalAdministratorRemoved", "grid")]
    [InlineData("GlobalAdministratorSet", "grid")]
    [InlineData("GlobalAdministratorRemoved", "receipt")]
    [InlineData("GlobalAdministratorSet", "receipt")]
    public async Task GlobalAdministratorAuditCannotArmCorrectionEvenThroughADirectCallback(
        string eventType, string origin)
    {
        TenantAuditSnapshot audit = GlobalAdminAuditSnapshot(eventType, "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(false, GlobalAdmins("other-admin"), audit);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        IRenderedComponent<AuditDataGrid> grid = cut.FindComponent<AuditDataGrid>();
        TenantCorrectionStartIntent intent = grid.Instance.CorrectionIntentProvider!(audit.Rows[0]);
        intent.IsAvailable.ShouldBeFalse();
        intent.UnavailableReasons.ShouldContain(TenantCorrectionUnavailableReason.AuthorizationIndeterminate);
        // A stale child callback cannot bypass a fresh authorization decision.
        TenantCorrectionStartIntent staleAvailableIntent = intent with { UnavailableReasons = [] };
        if (origin == "receipt")
        {
            cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
            cut.WaitForElement("[data-testid='tenants-audit-receipt']");
            await cut.InvokeAsync(() => cut.FindComponent<AuditEvidenceReceipt>().Instance
                .OnStartCorrection.InvokeAsync(staleAvailableIntent));
        }
        else
        {
            await cut.InvokeAsync(() => grid.Instance.OnStartCorrection.InvokeAsync(staleAvailableIntent));
        }

        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-unavailable-reason']").ShouldNotBeEmpty();
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        commands.TrackedMessageIds.ShouldBeEmpty();
        commands.StatusHandles.ShouldBeEmpty();
        gateway.GlobalAdminRequests.ShouldNotBeEmpty();
    }

    [Fact]
    public void Correction_panel_does_not_remount_after_its_source_audit_row_disappears()
    {
        TenantAuditSnapshot evidence = ReadySnapshot([Row("event-global-admin", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway gateway = RegisterServices(
            evidence,
            TenantAuditSnapshot.Empty(true, ReadModelFreshnessState.Current, "\"etag\"", new TenantAuditRequest("tenant.alpha")),
            evidence);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-start-panel']");

        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Grid_started_correction_is_invalidated_on_loading_and_does_not_remount_on_the_same_row()
    {
        TenantAuditSnapshot evidence = ReadySnapshot([Row("event-global-admin", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway gateway = RegisterServices( evidence);
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop focusModule = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = focusModule.Setup<bool>("focusCorrectionLauncher", "event-global-admin", "grid");
        focus.SetResult(false);
        JSRuntimeInvocationHandler<bool> fallback = focusModule.Setup<bool>("focusElementById", "tenant-audit-heading");
        fallback.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-start-panel']");
        gateway.QueueResponse(pending.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForElement("[data-testid='tenants-audit-loading']");
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        cut.WaitForAssertion(() => fallback.Invocations.Count.ShouldBe(1));
        pending.SetResult(evidence);
        await refresh;

        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        gateway.Requests.Count.ShouldBe(2);
    }

    // With a receipt open, an invalidated correction panel whose launchers are not rendered (Loading) returns
    // focus to the still-rendered receipt heading, not the page heading.
    [Fact]
    public async Task Invalidated_correction_with_an_open_receipt_falls_back_to_the_receipt_heading()
    {
        TenantAuditSnapshot evidence = ReadySnapshot([Row("event-global-admin", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway gateway = RegisterServices( evidence);
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop focusModule = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> launcher = focusModule.Setup<bool>("focusCorrectionLauncher", "event-global-admin", "grid");
        launcher.SetResult(false);
        JSRuntimeInvocationHandler<bool> focus = focusModule.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-audit-grid'] [data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-start-panel']");
        int focusCount = focus.Invocations.Count;
        gateway.QueueResponse(pending.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForElement("[data-testid='tenants-audit-loading']");
        cut.WaitForAssertion(() => launcher.Invocations.Count.ShouldBe(1));
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(focusCount + 1));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");

        pending.SetResult(evidence);
        await refresh;
    }

    // Cancellation is not listed: Blazor already treats a canceled after-render task as non-fatal.
    [Theory]
    [InlineData("js")]
    [InlineData("disposed")]
    public async Task Correction_focus_return_failures_do_not_fault_the_audit_page(string failure)
    {
        TenantAuditSnapshot evidence = ReadySnapshot([Row("event-global-admin", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway gateway = RegisterServices( evidence);
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop focusModule = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> launcher = focusModule.Setup<bool>("focusCorrectionLauncher", "event-global-admin", "grid");
        launcher.SetException<Exception>(failure is "js"
            ? new Microsoft.JSInterop.JSException("Focus module failed.")
            : new ObjectDisposedException("tenantsFocus"));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-start-panel']");
        gateway.QueueResponse(pending.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForElement("[data-testid='tenants-audit-loading']");
        cut.WaitForAssertion(() => launcher.Invocations.Count.ShouldBe(1));
        pending.SetResult(evidence);
        await refresh;

        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        gateway.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pending_correction_open_does_not_mount_when_source_row_is_removed_or_no_longer_ready(
        bool removeSource)
    {
        TenantAuditRow row = Row(
            "event-role-change",
            AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader",
            eventType: "UserRoleChanged");
        TenantAuditSnapshot refreshed = removeSource
            ? TenantAuditSnapshot.Empty(
                true,
                ReadModelFreshnessState.Current,
                "\"etag-empty\"",
                new TenantAuditRequest("tenant.alpha"))
            : ReadySnapshot([row with { Freshness = ReadModelFreshnessState.Stale }]);
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([row]), refreshed);
        var pendingProjection = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-correction-start']");
        gateway.QueueDetailResponse(pendingProjection.Task);

        Task open = cut.Find("[data-testid='tenants-correction-start']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(2));
        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        await refresh;

        pendingProjection.SetResult(DetailSnapshot(TenantRole.TenantContributor));
        await open;

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Pending_correction_open_renders_the_refreshed_unavailable_reason()
    {
        TenantAuditRow row = Row(
            "event-role-change",
            AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader",
            eventType: "UserRoleChanged");
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([row]));
        var pendingProjection = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-correction-start']");
        gateway.QueueDetailResponse(pendingProjection.Task);

        Task open = cut.Find("[data-testid='tenants-correction-start']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(2));
        pendingProjection.SetResult(DetailSnapshot(TenantRole.TenantReader));
        await open;

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']")
            .TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    // The receipt caches its correction intent, unlike the grid, which recomputes on every render. A receipt
    // Start correction whose refreshed intent is unavailable must be replaced by the reason, and the focus it
    // held must move to the receipt heading instead of dropping to <body>.
    [Fact]
    public async Task Receipt_started_correction_that_becomes_unavailable_shows_its_reason_and_focuses_the_receipt_heading()
    {
        TenantAuditRow row = Row(
            "event-role-change",
            AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader",
            eventType: "UserRoleChanged");
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([row]));
        var pendingProjection = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        module.Setup<bool>("isFocusInsideAuditReceiptCorrection").SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForElement("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        gateway.QueueDetailResponse(pendingProjection.Task);

        Task open = cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(2));
        pendingProjection.SetResult(DetailSnapshot(TenantRole.TenantReader));
        await open;

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-unavailable-reason']")
            .TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(2));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
    }

    [Fact]
    public async Task Enrichment_that_removes_a_focused_receipt_correction_returns_focus_to_receipt_heading()
    {
        TenantAuditRow row = Row(
            "event-role-change",
            AuditEventCategory.Access,
            "userId: target-user; oldRole: TenantReader",
            eventType: "UserRoleChanged");
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([row]),
            ReadySnapshot([row]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        module.Setup<bool>("isFocusInsideAuditReceipt").SetResult(false);
        JSRuntimeInvocationHandler<bool> correctionFocus = module.Setup<bool>("isFocusInsideAuditReceiptCorrection");
        correctionFocus.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForElement("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        gateway.QueueDetailResponse(Task.FromResult(DetailSnapshot(TenantRole.TenantReader)));

        cut.Find("[data-testid='tenants-audit-refresh']").Click();

        cut.WaitForAssertion(() => cut.FindAll(
            "[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").ShouldBeEmpty());
        cut.WaitForAssertion(() => correctionFocus.Invocations.Count.ShouldBe(1));
        cut.WaitForAssertion(() => focus.Invocations.Last().Arguments[0]
            .ShouldBe("tenants-audit-receipt-heading"));
    }

    [Fact]
    public async Task Invalid_tenant_route_clears_the_previous_receipt_and_correction_selection()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-global-admin", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-global-admin");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        await cut.InvokeAsync(() => cut.FindComponent<AuditDataGrid>().Instance.OnRoleSelected
            .InvokeAsync(new TenantCorrectionRoleSelection("event-global-admin", TenantRole.TenantReader)));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-start-panel']");
        cut.Find("[data-testid='tenants-audit-receipt']");

        navigation.NavigateTo("/tenants/Bearer%20raw-token/audit?receiptReference=event-global-admin");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "Bearer raw-token"));

        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("event-global-admin");
        gateway.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task NewestTenantCorrectionProjectionWinsAnOutOfOrderOpenRace()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([
            Row("event-role-change", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        var older = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newer = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueDetailResponse(older.Task);
        gateway.QueueDetailResponse(newer.Task);
        Task olderOpen = cut.Find("[data-testid='tenants-correction-start']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(2));
        Task newerOpen = cut.Find("[data-testid='tenants-correction-start']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(3));
        newer.SetResult(DetailSnapshot(TenantRole.TenantOwner));
        await newerOpen;
        older.SetResult(DetailSnapshot(TenantRole.TenantReader));
        await olderOpen;
        cut.Find("[data-testid='tenants-correction-start-current-role']").TextContent.ShouldContain("Tenant owner");
        cut.Find("[data-testid='tenants-correction-start-panel']");
    }

    [Fact]
    public async Task Verified_global_pair_remains_navigable_after_panel_is_closed_and_is_removed_when_requery_fails()
    {
        TenantAuditSnapshot originalAudit = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), originalAudit);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        DateTimeOffset originalTime = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture);
        TenantAuditRow corrective = new TenantAuditRow(
            "event-corrective", "GlobalAdministratorSet", AuditEventCategory.Access,
            "actor-user", originalTime.AddMinutes(5), "system", "admin-user", "system",
            "GlobalAdministratorSet", "userId: admin-user", ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked,
            new TenantAuditNarrative(UserId: "admin-user")) with
        {
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        TenantCorrectionStartIntent intent = new(
            "event-global-admin", "system", "admin-user", "GlobalAdministratorRemoved",
            "Current global administrator projection is available.",
            TenantCorrectionCommandDomain.GlobalAdministrators,
            TenantCorrectionCommandType.SetGlobalAdministrator, null, [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "admin-user",
                ["originalTimestamp"] = originalTime.ToString("O", CultureInfo.InvariantCulture),
            });
        GlobalAdministratorCorrectionSnapshot completed = GlobalAdministratorCorrectionSnapshot
            .FromIntent(intent, GlobalAdmins("other-admin")) with
        {
            CorrelationId = "tracking-safe",
            CommittedEventSequence = 2,
            SubmittedAtUtc = originalTime.AddMinutes(1),
            LifecycleState = TenantCommandLifecycleState.Confirmed,
            AuditState = TenantCommandAuditState.AuditAvailable,
            ProofLink = new TenantCorrectionProofLink(
                "event-global-admin", "event-corrective", originalTime, corrective.Timestamp, corrective.ReferenceContext),
        };
        registry.Remember(completed);
        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        gateway.ProofAuditProvider = request => originalAudit with
        {
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/system/audit?receiptReference=event-corrective");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldBe("event-corrective"));
        cut.Find("[data-testid='tenants-audit-receipt-paired']").TextContent.ShouldContain("event-global-admin");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants/system/audit");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldContain("event-corrective"));
        cut.Find("[data-testid='tenants-audit-receipt-paired']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldContain("event-global-admin"));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/system/audit?receiptReference=event-corrective");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldBe("event-corrective"));
        cut.Find("[data-testid='tenants-audit-receipt-paired']").TextContent.ShouldContain("event-global-admin");
        cut.FindComponent<AuditEvidenceReceipt>().FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        var proofReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedProof = new TaskCompletionSource<GlobalAdministratorAuditProof?>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.AsyncGlobalProofProvider = () =>
        {
            proofReadEntered.TrySetResult();
            return delayedProof.Task;
        };
        gateway.QueueResponse(Task.FromResult(originalAudit));
        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await proofReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldBe("event-corrective");
        cut.Find("[data-testid='tenants-audit-receipt-paired']");
        delayedProof.SetResult(new GlobalAdministratorAuditProof(corrective));
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.AsyncGlobalProofProvider = null;
        gateway.GlobalProof = null;
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty());

        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']");
        gateway.QueueResponse(Task.FromResult(ReadySnapshot([Row("tenant-event", AuditEventCategory.Access)])));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-corrective");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("event-corrective"));
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent
            .ShouldContain("not loaded in the current tenant-scoped", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void Paired_global_receipt_requires_the_retained_sequence_and_original_protected_row(
        long retainedSequence, bool originalAvailable)
    {
        TenantAuditSnapshot originalAudit = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), originalAudit);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        DateTimeOffset originalTime = originalAudit.Rows[0].Timestamp;
        TenantAuditRow corrective = originalAudit.Rows[0] with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = originalTime.AddMinutes(5),
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        TenantCorrectionStartIntent intent = new(
            "event-global-admin", "system", "admin-user", "GlobalAdministratorRemoved",
            "Current global administrator projection is available.",
            TenantCorrectionCommandDomain.GlobalAdministrators,
            TenantCorrectionCommandType.SetGlobalAdministrator, null, [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "admin-user",
                ["originalTimestamp"] = originalTime.ToString("O", CultureInfo.InvariantCulture),
            });
        registry.Remember(GlobalAdministratorCorrectionSnapshot.FromIntent(intent, GlobalAdmins("other-admin")) with
        {
            CorrelationId = "tracking-safe",
            CommittedEventSequence = retainedSequence,
            SubmittedAtUtc = originalTime.AddMinutes(1),
            LifecycleState = TenantCommandLifecycleState.Confirmed,
            AuditState = TenantCommandAuditState.AuditAvailable,
            ProofLink = new TenantCorrectionProofLink(
                "event-global-admin", "event-corrective", originalTime, corrective.Timestamp, corrective.ReferenceContext),
        });
        int proofReads = 0;
        int originalReads = 0;
        gateway.GlobalProofProvider = _ =>
        {
            proofReads++;
            return new GlobalAdministratorAuditProof(corrective);
        };
        gateway.ProofAuditProvider = request =>
        {
            originalReads++;
            return originalAudit with
            {
                Rows = originalAvailable ? originalAudit.Rows : [],
                From = request.From,
                To = request.To,
                RequestCursor = request.Cursor,
                RequestPageSize = request.PageSize,
                Stream = request.Stream,
            };
        };

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => proofReads.ShouldBeGreaterThan(0));
        if (retainedSequence == 2)
        {
            originalReads.ShouldBeGreaterThan(0);
        }
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ready", true)]
    [InlineData("wrong-category", false)]
    [InlineData("opaque-version", false)]
    [InlineData("wrong-stream", false)]
    [InlineData("version-drift", false)]
    public void Original_global_receipt_on_second_protected_page_requires_access_and_ordered_version(
        string originalState, bool shouldPair)
    {
        TenantAuditSnapshot loaded = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        TenantAuditRow original = loaded.Rows[0];
        TenantAuditRow corrective = original with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = original.Timestamp.AddMinutes(5),
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("admin-user", "other-admin") with { ProjectionVersion = "tenant-sequence:2" }, loaded);
        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        gateway.ProofAuditProvider = request => loaded with
        {
            Rows = request.Cursor is null
                ? [original with { EventReference = "event-unrelated" }]
                : [original with { Category = originalState == "wrong-category"
                    ? AuditEventCategory.Administrative : AuditEventCategory.Access }],
            HasMore = request.Cursor is null,
            NextCursor = request.Cursor is null ? "second-original-page" : null,
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = originalState == "wrong-stream" ? "tenant" : request.Stream,
            ProjectionVersion = originalState switch
            {
                "opaque-version" => "legacy-audit",
                "version-drift" when request.Cursor is not null => "tenant-sequence:3",
                _ => "tenant-sequence:2",
            },
        };
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        TenantCorrectionStartIntent intent = new(
            original.EventReference, "system", "admin-user", original.EventType,
            "Current global administrator projection is available.",
            TenantCorrectionCommandDomain.GlobalAdministrators,
            TenantCorrectionCommandType.SetGlobalAdministrator, null, [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "admin-user",
                ["originalTimestamp"] = original.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            });
        registry.Remember(GlobalAdministratorCorrectionSnapshot.FromIntent(intent) with
        {
            CorrelationId = "tracking-safe",
            CommittedEventSequence = 2,
            SubmittedAtUtc = original.Timestamp.AddMinutes(1),
            LifecycleState = TenantCommandLifecycleState.Confirmed,
            AuditState = TenantCommandAuditState.AuditAvailable,
            ProofLink = new TenantCorrectionProofLink(
                original.EventReference, corrective.EventReference, original.Timestamp, corrective.Timestamp,
                corrective.ReferenceContext),
        });

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/system/audit?receiptReference=event-corrective");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));

        cut.WaitForAssertion(() => gateway.Requests.Count(request => request.From == original.Timestamp)
            .ShouldBeGreaterThanOrEqualTo(originalState is "opaque-version" or "wrong-stream" ? 1 : 2));
        gateway.Requests.Where(request => request.From == original.Timestamp)
            .ShouldAllBe(request => request.Stream == "global-administrators");
        if (shouldPair)
        {
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
                .TextContent.ShouldBe("event-corrective"));
            cut.Find("[data-testid='tenants-audit-receipt-paired']").TextContent.ShouldContain(original.EventReference);
        }
        else
        {
            cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();
        }
    }

    [Fact]
    public void Sibling_completion_pairs_only_its_retained_source_after_authorized_audit_return()
    {
        TenantAuditSnapshot loaded = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        TenantAuditRow original = loaded.Rows[0];
        TenantAuditRow otherSource = original with { EventReference = "event-other-source" };
        TenantAuditRow corrective = original with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = original.Timestamp.AddMinutes(5),
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        // A later valid removal has reversed the target state before this audit return.
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin") with { ProjectionVersion = "tenant-sequence:3" },
            loaded with { Rows = [original, otherSource] });
        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        gateway.ProofAuditProvider = request => loaded with
        {
            Rows = [original, otherSource],
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            "admin-user", GlobalAdmins("other-admin") with { IsCompleteEvidence = true }, isAuthorized: true);
        preview.IsComplete.ShouldBeTrue();
        registry.RememberCompletion(new GlobalAdministratorReconciliationState(
            GlobalAdministratorActionKind.Grant, "admin-user", "message-safe", "tracking-safe",
            TenantCommandLifecycleState.Confirmed, GrantPreview: preview, HasCommandEventEvidence: true)
        {
            SourceAuditReference = original.EventReference,
            SourceAuditTimestampUtc = original.Timestamp,
            SubmittedAtUtc = original.Timestamp.AddMinutes(1),
            CommittedEventSequence = 2,
            ConfirmedProjectionEvidence = GlobalAdmins("admin-user", "other-admin") with
            {
                ProjectionVersion = "tenant-sequence:2",
                IsCompleteEvidence = true,
            },
        });
        registry.PendingCompletions().Count.ShouldBe(1);

        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        gateway.Requests.Any(request => request.From == original.Timestamp).ShouldBeFalse();
        registry.PendingCompletions().Count.ShouldBe(1);

        navigation.NavigateTo("/tenants/system/audit?receiptReference=event-other-source");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => gateway.Requests.Any(request => request.From == original.Timestamp)
            .ShouldBeTrue());
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();

        navigation.NavigateTo("/tenants/system/audit?receiptReference=event-global-admin");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']").TextContent
            .ShouldContain(corrective.EventReference);
        registry.FindAll(otherSource.EventReference).ShouldBeEmpty();
        registry.FindAll(original.EventReference).Count.ShouldBe(1);
        registry.PendingCompletions().ShouldBeEmpty();
    }

    [Fact]
    public void Principal_change_clears_off_page_global_receipt_and_pair_before_reauthorization()
    {
        TenantAuditSnapshot loaded = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        TenantAuditRow original = loaded.Rows[0];
        TenantAuditRow corrective = original with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = original.Timestamp.AddMinutes(5),
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), loaded);
        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        gateway.ProofAuditProvider = request => loaded with
        {
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        TenantCorrectionStartIntent intent = new(
            original.EventReference, "system", "admin-user", original.EventType,
            "Current global administrator projection is available.",
            TenantCorrectionCommandDomain.GlobalAdministrators,
            TenantCorrectionCommandType.SetGlobalAdministrator, null, [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "admin-user",
                ["originalTimestamp"] = original.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            });
        registry.Remember(GlobalAdministratorCorrectionSnapshot.FromIntent(intent) with
        {
            CorrelationId = "tracking-safe",
            CommittedEventSequence = 2,
            SubmittedAtUtc = original.Timestamp.AddMinutes(1),
            LifecycleState = TenantCommandLifecycleState.Confirmed,
            AuditState = TenantCommandAuditState.AuditAvailable,
            ProofLink = new TenantCorrectionProofLink(original.EventReference, corrective.EventReference,
                original.Timestamp, corrective.Timestamp, corrective.ReferenceContext),
        });

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/system/audit?receiptReference=event-corrective");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']");
        StubBffComposition authorization = (StubBffComposition)Services.GetRequiredService<ITenantsBffComposition>();
        StubAuthenticationStateProvider authentication = (StubAuthenticationStateProvider)Services
            .GetRequiredService<AuthenticationStateProvider>();
        gateway.QueueResponse(Task.FromResult(TenantAuditSnapshot.Unauthorized(new TenantAuditRequest("system"))));
        authorization.Authorized = false;
        var pendingPrincipal = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        authentication.ChangePrincipal(pendingPrincipal.Task);

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();
        ((Dictionary<string, TenantAuditRow>)typeof(TenantAuditPage)
            .GetField("_verifiedGlobalRows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).ShouldBeEmpty();
        pendingPrincipal.SetResult(new AuthenticationState(new ClaimsPrincipal()));
        cut.WaitForElement("[data-testid='tenants-audit-unauthorized']");
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']").ShouldBeEmpty();
    }

    [Fact]
    public void Faulted_authentication_transition_shows_recoverable_unavailable_state()
    {
        RegisterGlobalAdminServices(true, GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        StubAuthenticationStateProvider authentication = (StubAuthenticationStateProvider)Services
            .GetRequiredService<AuthenticationStateProvider>();

        authentication.ChangePrincipal(Task.FromException<AuthenticationState>(new InvalidOperationException("auth failed")));

        cut.WaitForElement("[data-testid='tenants-audit-unavailable']");
        cut.Find("[data-testid='tenants-audit-recovery-refresh']");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Authentication_transition_replaces_system_read_subscription()
    {
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(true, GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => typeof(TenantAuditPage)
            .GetField("_subscriptionTenantId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance).ShouldBe("system"));

        StubAuthenticationStateProvider authentication = (StubAuthenticationStateProvider)Services
            .GetRequiredService<AuthenticationStateProvider>();
        gateway.QueueResponse(Task.FromResult(GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user")));
        authentication.ChangePrincipal(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));

        cut.WaitForAssertion(() =>
        {
            _ = subscription.Received(1).UnsubscribeAsync(
                GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
            _ = subscription.Received(2).SubscribeAsync(
                GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Disposed_audit_page_does_not_react_to_principal_change()
    {
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(true, GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
        int readsBeforeDispose = gateway.Requests.Count;

        await cut.Instance.DisposeAsync();
        FieldInfo transitionVersionField = typeof(TenantAuditPage).GetField(
            "_authenticationTransitionVersion", BindingFlags.Instance | BindingFlags.NonPublic)!;
        long transitionVersionBeforeChange = (long)transitionVersionField.GetValue(cut.Instance)!;
        StubAuthenticationStateProvider authentication = (StubAuthenticationStateProvider)Services
            .GetRequiredService<AuthenticationStateProvider>();
        authentication.ChangePrincipal(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));

        ((long)transitionVersionField.GetValue(cut.Instance)!).ShouldBe(transitionVersionBeforeChange);
        gateway.Requests.Count.ShouldBe(readsBeforeDispose);
        await subscription.Received(1).UnsubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authentication_transition_invalidates_global_correction_open_waiting_on_authorization()
    {
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(true, GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-correction-start']");
        StubBffComposition authorization = (StubBffComposition)Services.GetRequiredService<ITenantsBffComposition>();
        var pendingAuthorization = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        authorization.GlobalAuthorizationTask = pendingAuthorization.Task;
        int callsBeforeOpen = authorization.GlobalAuthorizationCalls;
        Task open = cut.Find("[data-testid='tenants-correction-start']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => authorization.GlobalAuthorizationCalls.ShouldBeGreaterThan(callsBeforeOpen));

        StubAuthenticationStateProvider authentication = (StubAuthenticationStateProvider)Services
            .GetRequiredService<AuthenticationStateProvider>();
        var pendingPrincipal = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        authentication.ChangePrincipal(pendingPrincipal.Task);
        pendingAuthorization.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await open.WaitAsync(TimeSpan.FromSeconds(5));

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        ((GlobalAdministratorsSnapshot?)typeof(TenantAuditPage)
            .GetField("_globalAdministratorsProjection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)).ShouldBeNull();
        gateway.QueueResponse(Task.FromResult(TenantAuditSnapshot.Unauthorized(new TenantAuditRequest("system"))));
        authorization.GlobalAuthorizationTask = null;
        authorization.Authorized = false;
        pendingPrincipal.SetResult(new AuthenticationState(new ClaimsPrincipal()));
        cut.WaitForElement("[data-testid='tenants-audit-unauthorized']");
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
    }

    [Fact]
    public void Mounted_global_panel_notifies_page_when_a_new_proven_pair_becomes_available()
    {
        TenantAuditSnapshot originalAudit = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), originalAudit);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(DateTimeOffset.Parse("2026-06-01T10:01:00Z", CultureInfo.InvariantCulture));
        Services.AddSingleton(clock);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        commands.Status = new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
        {
            CommittedEventSequence = 2,
        };
        gateway.GlobalAdminProvider = _ => commands.StatusHandles.Count == 0
            ? GlobalAdmins("other-admin")
            : GlobalAdmins("admin-user", "other-admin") with { ProjectionVersion = "tenant-sequence:2" };
        DateTimeOffset originalTime = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture);
        TenantAuditRow corrective = new TenantAuditRow(
            "event-corrective", "GlobalAdministratorSet", AuditEventCategory.Access,
            "actor-user", originalTime.AddMinutes(5), "system", "admin-user", "system",
            "GlobalAdministratorSet", "userId: admin-user", ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked,
            new TenantAuditNarrative(UserId: "admin-user")) with
        {
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        gateway.GlobalProof = new GlobalAdministratorAuditProof(corrective);
        gateway.AsyncGlobalProofProvider = () => Task.Run(async () =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            return (GlobalAdministratorAuditProof?)new GlobalAdministratorAuditProof(corrective);
        });
        gateway.ProofAuditProvider = request => originalAudit with
        {
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.FindComponent<AuditEvidenceReceipt>().Find("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']");
        cut.WaitForAssertion(() => cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!
            .LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Find("[data-testid='tenants-correction-proof-link']");
        registry.FindAll("event-global-admin").Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-receipt-paired']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-receipt-reference']")
            .TextContent.ShouldContain("event-corrective"));
    }

    [Fact]
    public void Confirmed_global_panel_refresh_notifies_page_when_proof_arrives_off_dispatcher()
    {
        TenantAuditSnapshot originalAudit = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"), originalAudit);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(DateTimeOffset.Parse("2026-06-01T10:01:00Z", CultureInfo.InvariantCulture));
        Services.AddSingleton(clock);
        Services.AddSingleton(new GlobalAdministratorCorrectionProofRegistry());
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        commands.Status = new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
        {
            CommittedEventSequence = 2,
        };
        gateway.GlobalAdminProvider = _ => commands.StatusHandles.Count == 0
            ? GlobalAdmins("other-admin")
            : GlobalAdmins("admin-user", "other-admin") with { ProjectionVersion = "tenant-sequence:2" };
        DateTimeOffset originalTime = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture);
        TenantAuditRow corrective = originalAudit.Rows[0] with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = originalTime.AddMinutes(5),
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };
        gateway.ProofAuditProvider = request => originalAudit with
        {
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.FindComponent<AuditEvidenceReceipt>().Find("[data-testid='tenants-correction-start']").Click();
        cut.WaitForElement("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!
            .LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
        gateway.AsyncGlobalProofProvider = () => Task.Run(async () =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            return (GlobalAdministratorAuditProof?)new GlobalAdministratorAuditProof(corrective);
        });

        cut.Find("[data-testid='tenants-correction-refresh']").Click();

        cut.WaitForElement("[data-testid='tenants-correction-proof-link']");
        cut.WaitForElement("[data-testid='tenants-audit-receipt-paired']");
        cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public void Shared_receipt_shows_both_independently_reverified_successive_pairs()
    {
        DateTimeOffset timeA = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture);
        TenantAuditRow rowA = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user").Rows[0] with
        {
            EventReference = "event-a",
        };
        TenantAuditRow rowB = rowA with
        {
            EventReference = "event-b",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Timestamp = timeA.AddMinutes(5),
            CommandCorrelationId = "tracking-one",
            AggregateSequence = 2,
        };
        TenantAuditRow rowC = rowA with
        {
            EventReference = "event-c",
            Timestamp = timeA.AddMinutes(10),
            CommandCorrelationId = "tracking-two",
            AggregateSequence = 2,
        };
        TenantAuditSnapshot selectedAudit = GlobalAdminAuditSnapshot("GlobalAdministratorSet", "admin-user") with
        {
            Rows = [rowB],
        };
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("admin-user", "other-admin"), selectedAudit);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        gateway.GlobalProofProvider = correlation => new GlobalAdministratorAuditProof(
            correlation == "tracking-one" ? rowB : rowC);
        gateway.ProofAuditProvider = request => selectedAudit with
        {
            Rows = request.To == timeA ? [rowA] : [rowB],
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        };
        foreach ((TenantAuditRow original, TenantAuditRow corrective, string correlation,
            TenantCorrectionCommandType commandType) in new[]
            {
                (rowA, rowB, "tracking-one", TenantCorrectionCommandType.SetGlobalAdministrator),
                (rowB, rowC, "tracking-two", TenantCorrectionCommandType.RemoveGlobalAdministrator),
            })
        {
            TenantCorrectionStartIntent intent = new(
                original.EventReference, "system", "admin-user", original.EventType,
                "Current global administrator projection is available.",
                TenantCorrectionCommandDomain.GlobalAdministrators, commandType, null, [],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["userId"] = "admin-user",
                    ["originalTimestamp"] = original.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                });
            registry.Remember(GlobalAdministratorCorrectionSnapshot.FromIntent(intent) with
            {
                CorrelationId = correlation,
                CommittedEventSequence = 2,
                SubmittedAtUtc = original.Timestamp.AddMinutes(1),
                LifecycleState = TenantCommandLifecycleState.Confirmed,
                AuditState = TenantCommandAuditState.AuditAvailable,
                ProofLink = new TenantCorrectionProofLink(original.EventReference, corrective.EventReference,
                    original.Timestamp, corrective.Timestamp, corrective.ReferenceContext),
            });
        }

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-receipt-paired']").Count.ShouldBe(2));
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']")[0].TextContent.ShouldContain("event-a");
        cut.FindAll("[data-testid='tenants-audit-receipt-paired']")[1].TextContent.ShouldContain("event-c");

        gateway.GlobalProofProvider = correlation => correlation == "tracking-one"
            ? throw new HttpRequestException("first protected proof read failed")
            : new GlobalAdministratorAuditProof(rowC);
        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-receipt-paired']").Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-audit-receipt-paired']").TextContent.ShouldContain("event-c");
    }

    [Fact]
    public void Tenant_audit_page_keeps_global_administrator_correction_fail_closed_when_unauthorized()
    {
        RegisterGlobalAdminServices(
            authorized: false,
            GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldContain("Current access could not be verified");
        cut.VisibleText().ShouldNotContain("Success", Case.Insensitive);
    }

    [Fact]
    public async Task Request_sent_global_correction_keeps_its_intent_and_message_during_reload_and_second_start()
    {
        TenantAuditSnapshot original = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        TenantAuditRow second = original.Rows[0] with
        {
            EventReference = "event-second",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Target = "other-admin",
            Narrative = new TenantAuditNarrative(UserId: "other-admin"),
            ReferenceContext = "userId: other-admin",
        };
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin", "third-admin"),
            original with { Rows = [original.Rows[0], second] });
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        var pendingSubmit = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.SubmissionTask = pendingSubmit.Task;
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-start']").Count.ShouldBe(2));
        cut.FindAll("[data-testid='tenants-correction-start']")[0].Click();
        cut.WaitForElement("[data-testid='tenants-correction-confirm']");
        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => commands.TrackedMessageIds.Count.ShouldBe(1));
        string messageId = commands.TrackedMessageIds[0];
        cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.RequestSent);

        gateway.QueueResponse(Task.FromResult(original with { Rows = [second] }));
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-start']").Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-correction-start']").Click();
        GlobalAdministratorCorrectionSnapshot tracked = cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!;
        tracked.OriginalAuditReference.ShouldBe("event-global-admin");
        tracked.MessageId.ShouldBe(messageId);

        pendingSubmit.SetResult(TenantCommandSubmissionResult.Accepted(messageId, "tracking-safe"));
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Failed_uncorrelated_global_correction_allows_a_new_start()
    {
        TenantAuditSnapshot original = GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user");
        TenantAuditRow second = original.Rows[0] with
        {
            EventReference = "event-second",
            EventType = "GlobalAdministratorSet",
            Outcome = "GlobalAdministratorSet",
            Target = "other-admin",
            Narrative = new TenantAuditNarrative(UserId: "other-admin"),
            ReferenceContext = "userId: other-admin",
        };
        RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin", "third-admin"),
            original with { Rows = [original.Rows[0], second] });
        StubTenantCommandGateway commands = (StubTenantCommandGateway)Services.GetRequiredService<ITenantCommandGateway>();
        commands.SubmissionTask = Task.FromResult(TenantCommandSubmissionResult.Failed("Delivery failed."));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-start']").Count.ShouldBe(2));
        cut.FindAll("[data-testid='tenants-correction-start']")[0].Click();
        cut.WaitForElement("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!
            .LifecycleState.ShouldBe(TenantCommandLifecycleState.Failed));

        cut.FindAll("[data-testid='tenants-correction-start']")[1].Click();

        cut.WaitForAssertion(() => cut.FindComponent<GlobalAdministratorCorrectionPanel>().Instance.Snapshot!
            .OriginalAuditReference.ShouldBe("event-second"));
    }

    [Fact]
    public void Tenant_audit_page_renders_timestamps_in_utc_independent_of_host_timezone()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        // Row timestamp is 2026-06-01T10:00:00Z; rendering must stay UTC, not shift to the server's tz.
        cut.Find("[data-testid='tenants-audit-row-timestamp']").TextContent.ShouldBe("2026-06-01 10:00:00 UTC");
    }

    [Fact]
    public void Tenant_audit_page_exposes_keyboard_native_controls_with_accessible_labels()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "next-cursor", hasMore: true));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("label").Count.ShouldBeGreaterThanOrEqualTo(3);
        cut.Find("[data-testid='tenants-audit-filter-from']").ParentElement!.TextContent.ShouldContain("From");
        cut.Find("[data-testid='tenants-audit-filter-to']").ParentElement!.TextContent.ShouldContain("To");
        cut.Find("[data-testid='tenants-audit-filter-category']").ParentElement!.TextContent.ShouldContain("Category");
        cut.Find("[data-testid='tenants-audit-refresh']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-reset']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-next']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-previous']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-audit-next']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Date_and_category_filters_trigger_server_side_audit_query_and_clear_cursor()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-2", AuditEventCategory.Administrative)]),
            ReadySnapshot([Row("event-3", AuditEventCategory.Administrative)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        gateway.Requests[1].Cursor.ShouldBe("opaque-next");

        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-audit-filter-category", AuditEventCategory.Administrative.ToString());
        cut.Find("[data-testid='tenants-audit-filter-pending']").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-audit-next']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-audit-apply']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));

        gateway.Requests[2].Cursor.ShouldBeNull();
        gateway.Requests[2].Category.ShouldBe(AuditEventCategory.Administrative);
        cut.FindAll("[data-testid='tenants-audit-filter-pending']").ShouldBeEmpty();
    }

    [Fact]
    public void Refresh_with_pending_filter_applies_it_from_page_one_without_reusing_the_validator()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)], requestCursor: "opaque-next"),
            ReadySnapshot([Row("event-3", AuditEventCategory.Administrative)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-audit-filter-category", AuditEventCategory.Administrative.ToString());
        cut.Find("[data-testid='tenants-audit-refresh']").Click();

        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));
        gateway.Requests[2].Category.ShouldBe(AuditEventCategory.Administrative);
        gateway.Requests[2].Cursor.ShouldBeNull();
        gateway.Requests[2].ETag.ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Date_filters_pass_absolute_values_to_gateway()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-3", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-from']").Change("2026-06-01T10:15");
        cut.Find("[data-testid='tenants-audit-filter-to']").Change("2026-06-02T11:45");
        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-apply']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));

        gateway.Requests[1].From.ShouldNotBeNull();
        gateway.Requests[1].To.ShouldNotBeNull();
    }

    [Fact]
    public void Malformed_to_filter_is_field_associated_and_does_not_query()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-to']").Change("not-a-utc-date");

        gateway.Requests.Count.ShouldBe(1);
        IElement to = cut.Find("[data-testid='tenants-audit-filter-to']");
        to.GetAttribute("aria-invalid").ShouldBe("true");
        to.GetAttribute("aria-describedby").ShouldBe("tenants-audit-filter-to-error");
        cut.Find("#tenants-audit-filter-to-error").GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-invalid").ShouldBe("false");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid='tenants-audit-ready']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-recovery-refresh']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-recovery-reset']").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Malformed_from_filter_is_field_associated_and_does_not_query()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-from']").Change("not-a-utc-date");

        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-audit-filter-from-error");
        cut.Find("#tenants-audit-filter-from-error").GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-audit-filter-to']").GetAttribute("aria-invalid").ShouldBe("false");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
    }

    [Theory]
    [InlineData("from", "0001-01-01T00:00")]
    [InlineData("to", "0001-01-01T00:00")]
    [InlineData("from", "0001-01-01T00:00Z")]
    [InlineData("to", "0001-01-01T00:00Z")]
    public void Default_year_one_utc_filter_is_rejected_locally_without_query(string field, string value)
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find($"[data-testid='tenants-audit-filter-{field}']").Change(value);

        gateway.Requests.Count.ShouldBe(1);
        cut.Find($"[data-testid='tenants-audit-filter-{field}']").GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find($"[data-testid='tenants-audit-filter-{field}']").GetAttribute("aria-describedby")
            .ShouldBe($"tenants-audit-filter-{field}-error");
        cut.Find($"#tenants-audit-filter-{field}-error").GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
    }

    [Fact]
    public void Reversed_date_range_marks_both_fields_and_does_not_query()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-from']").Change("2026-06-03T00:00");
        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-filter-to']").Change("2026-06-02T00:00");

        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-audit-filter-from-error");
        cut.Find("[data-testid='tenants-audit-filter-to']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-audit-filter-to-error");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-ready']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("2026-06-01T10:15Z", "2026-06-01T10:15:00+00:00")]
    [InlineData("2026-06-01T12:15+02:00", "2026-06-01T10:15:00+00:00")]
    public void Pasted_iso_8601_from_instant_is_normalized_to_utc(string value, string expected)
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)]),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-from']").Change(value);
        cut.Find("[data-testid='tenants-audit-apply']").Click();

        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        gateway.Requests[1].From.ShouldBe(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Audit_date_filters_explicitly_label_the_absolute_utc_contract()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-label").ShouldNotBeNull().ShouldContain("UTC");
        cut.Find("[data-testid='tenants-audit-filter-to']").GetAttribute("aria-label").ShouldNotBeNull().ShouldContain("UTC");
    }

    [Fact]
    public void Cursor_paging_passes_opaque_cursor_and_previous_history()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)], requestCursor: "opaque-next"),
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        gateway.Requests[1].Cursor.ShouldBe("opaque-next");

        cut.Find("[data-testid='tenants-audit-previous']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));
        gateway.Requests[2].Cursor.ShouldBeNull();
    }

    [Fact]
    public async Task Previous_completion_tolerates_history_cleared_during_its_dispatcher_hop()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-2", AuditEventCategory.Access)], requestCursor: "opaque-next"),
            // Consumed by the filter change below, which issues its own unpaged read.
            ReadySnapshot([Row("event-filtered", AuditEventCategory.Access)]),
            // Consumed by the closing refresh that proves the circuit is still live.
            ReadySnapshot([Row("event-filtered", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeFalse());

        var previousPage = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(previousPage.Task);
        Task previousNavigation = cut.Find("[data-testid='tenants-audit-previous']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));

        // Applying the staged filter clears paging while the Previous read is in flight.
        // Reaching into `_cursorHistory` by reflection reproduced the same state, but pinned a private field
        // name rather than the behaviour, and mutated it from the test thread -- the very cross-thread access
        // the production code is being asserted to survive.
        cut.Find("[data-testid='tenants-audit-filter-from']").Change("2026-01-01T00:00");
        cut.Find("[data-testid='tenants-audit-apply']").Click();

        previousPage.SetResult(ReadySnapshot(
            [Row("event-1-returned", AuditEventCategory.Access)],
            nextCursor: "opaque-next",
            hasMore: true));
        await previousNavigation;

        // The filter's read is newer, so it owns the surface: the superseded Previous result must not be
        // written over it, and its commit must not throw on the history the filter already emptied.
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-row']")
            .GetAttribute("data-audit-reference").ShouldBe("event-filtered"));
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();

        // The circuit survived: the page still responds to input rather than having faulted on the commit.
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(5));
    }

    [Fact]
    public void Metadata_degraded_requested_page_advances_cursor_and_can_return_to_page_one()
    {
        TenantAuditSnapshot degradedPage = TenantAuditSnapshot.Degraded(
            [Row("event-2", AuditEventCategory.Access)],
            TenantAuditReason.ProjectionDegraded,
            new TenantAuditRequest("tenant.alpha", Cursor: "opaque-next"));
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            degradedPage,
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-grid']").TextContent.ShouldContain("event-2"));

        cut.Find("[data-testid='tenants-audit-previous']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));
        gateway.Requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-next", null]);
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Loading, "tenants-audit-loading")]
    [InlineData(TenantAuditSurfaceKind.Empty, "tenants-audit-empty")]
    [InlineData(TenantAuditSurfaceKind.FilteredEmpty, "tenants-audit-filtered-empty")]
    [InlineData(TenantAuditSurfaceKind.Stale, "tenants-audit-stale")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "tenants-audit-degraded")]
    [InlineData(TenantAuditSurfaceKind.Unauthorized, "tenants-audit-unauthorized")]
    [InlineData(TenantAuditSurfaceKind.InvalidCursor, "tenants-audit-invalid-cursor")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "tenants-audit-list-refreshed")]
    [InlineData(TenantAuditSurfaceKind.Unavailable, "tenants-audit-unavailable")]
    [InlineData(TenantAuditSurfaceKind.Error, "tenants-audit-error")]
    public void Tenant_audit_page_renders_distinct_accessible_states(TenantAuditSurfaceKind kind, string selector)
    {
        RegisterServices(SnapshotFor(kind));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement($"[data-testid='{selector}']");

        cut.Find($"[data-testid='{selector}']").GetAttribute("role").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-audit-live-region']").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Empty, "refresh")]
    [InlineData(TenantAuditSurfaceKind.FilteredEmpty, "reset,refresh")]
    [InlineData(TenantAuditSurfaceKind.Stale, "refresh,continue")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "refresh,continue")]
    [InlineData(TenantAuditSurfaceKind.Unauthorized, "permission")]
    [InlineData(TenantAuditSurfaceKind.InvalidCursor, "refresh")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "refresh,continue")]
    [InlineData(TenantAuditSurfaceKind.Unavailable, "refresh,escalate")]
    [InlineData(TenantAuditSurfaceKind.Error, "refresh,escalate")]
    public void Tenant_audit_states_render_exact_recovery_actions_and_refresh_requeries(
        TenantAuditSurfaceKind kind,
        string expectedActions)
    {
        ArgumentNullException.ThrowIfNull(expectedActions);
        TenantAuditSnapshot state = SnapshotFor(kind);
        StubTenantQueryGateway gateway = RegisterServices(state, state);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-recovery']");
        string[] candidates = ["reset", "refresh", "continue", "permission", "escalate"];
        string[] actual = candidates
            .Where(action => cut.FindAll($"[data-testid='tenants-audit-recovery-{action}']").Count > 0)
            .ToArray();

        actual.ShouldBe(expectedActions.Split(','));
        cut.Find("[data-testid='tenants-audit-recovery-description']").TextContent.ShouldNotBeNullOrWhiteSpace();
        if (actual.Contains("permission", StringComparer.Ordinal))
        {
            cut.Find("[data-testid='tenants-audit-recovery-permission']").GetAttribute("href")
                .ShouldBe("/support/audit-access");
        }

        if (actual.Contains("escalate", StringComparer.Ordinal))
        {
            cut.Find("[data-testid='tenants-audit-recovery-escalate']").GetAttribute("href")
                .ShouldBe("/support/audit-incident");
        }

        if (actual.Contains("refresh", StringComparer.Ordinal))
        {
            cut.Find("[data-testid='tenants-audit-recovery-refresh']").Click();
            cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        }
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Stale, "from")]
    [InlineData(TenantAuditSurfaceKind.Stale, "to")]
    [InlineData(TenantAuditSurfaceKind.Stale, "category")]
    [InlineData(TenantAuditSurfaceKind.Stale, "range")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "from")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "to")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "category")]
    [InlineData(TenantAuditSurfaceKind.Degraded, "range")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "from")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "to")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "category")]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed, "range")]
    public void ReadOnlyRecoveryTargetTracksInvalidFiltersAndReset(
        TenantAuditSurfaceKind kind,
        string invalidFilter)
    {
        TenantAuditSnapshot snapshot = SnapshotFor(kind);
        TenantAuditSnapshot resetSnapshot = snapshot with
        {
            Rows = [Row("event-after-reset", AuditEventCategory.Access)],
        };
        StubTenantQueryGateway gateway = RegisterServices(snapshot, resetSnapshot);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-recovery-continue']");
        string target = cut.Find("[data-testid='tenants-audit-recovery-continue']")
            .GetAttribute("href").ShouldNotBeNull();
        target.ShouldBe("#tenant-audit-grid-heading");
        cut.Find(target).TagName.ShouldBe("H2");
        cut.FindAll("[data-testid='tenants-audit-row']").Count.ShouldBe(1);

        switch (invalidFilter)
        {
            case "from":
                cut.Find("[data-testid='tenants-audit-filter-from']").Change("not-a-utc-date");
                break;
            case "to":
                cut.Find("[data-testid='tenants-audit-filter-to']").Change("not-a-utc-date");
                break;
            case "category":
                FluentSelectInterop.ChangeFluentSelect(cut, "tenants-audit-filter-category", "unknown-category");
                break;
            case "range":
                cut.Find("[data-testid='tenants-audit-filter-from']").Change("2026-06-03T00:00");
                cut.Find("[data-testid='tenants-audit-filter-to']").Change("2026-06-02T00:00");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidFilter));
        }

        gateway.Requests.Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid='tenants-audit-recovery-continue']").ShouldBeEmpty();
        cut.FindAll(target).ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-recovery-refresh']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-recovery-reset']").Click();

        cut.WaitForAssertion(() =>
        {
            gateway.Requests.Count.ShouldBe(2);
            cut.FindAll("[data-testid='tenants-audit-invalid-filters']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-audit-recovery-continue']").GetAttribute("href").ShouldBe(target);
            cut.Find(target).TagName.ShouldBe("H2");
            cut.FindAll("[data-testid='tenants-audit-row']").Count.ShouldBe(1);
            cut.Find("[data-testid='tenants-audit-row']")
                .GetAttribute("data-audit-reference").ShouldBe("event-after-reset");
        });
        gateway.Requests[1].From.ShouldBeNull();
        gateway.Requests[1].To.ShouldBeNull();
        gateway.Requests[1].Category.ShouldBeNull();
        gateway.Requests[1].Cursor.ShouldBeNull();
        gateway.Requests[1].ETag.ShouldBeNull();
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Stale)]
    [InlineData(TenantAuditSurfaceKind.Degraded)]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed)]
    public void ReadOnlyRecoveryHasNoTargetWithoutRows(TenantAuditSurfaceKind kind)
    {
        StubTenantQueryGateway gateway = RegisterServices(SnapshotFor(kind) with { Rows = [] });
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-recovery']");

        gateway.Requests.Count.ShouldBe(1);
        cut.FindAll("[data-testid='tenants-audit-recovery-continue']").ShouldBeEmpty();
        cut.FindAll("#tenant-audit-grid-heading").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Stale)]
    [InlineData(TenantAuditSurfaceKind.Degraded)]
    [InlineData(TenantAuditSurfaceKind.ListRefreshed)]
    public async Task ReadOnlyRecoveryStaysHiddenAfterInvalidFiltersSupersedeLateCompletion(
        TenantAuditSurfaceKind kind)
    {
        TenantAuditSnapshot snapshot = SnapshotFor(kind);
        StubTenantQueryGateway gateway = RegisterServices(snapshot);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-recovery-continue']");
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(pending.Task);
        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.Find("[data-testid='tenants-audit-filter-from']").Change("invalid-date");

        pending.SetResult(snapshot with { Rows = [Row("event-late", AuditEventCategory.Access)] });
        await refresh;

        gateway.Requests.Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid='tenants-audit-recovery-continue']").ShouldBeEmpty();
        cut.FindAll("#tenant-audit-grid-heading").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TenantAuditSurfaceKind.Unauthorized, null, "/support/audit-incident", "permission")]
    [InlineData(TenantAuditSurfaceKind.Unavailable, "/support/audit-access", null, "escalate")]
    [InlineData(TenantAuditSurfaceKind.Unauthorized, "https://external.example/access", "/support/audit-incident", "permission")]
    [InlineData(TenantAuditSurfaceKind.Error, "/support/audit-access", "/support/%2573ecret", "escalate")]
    public void Recovery_links_fail_closed_without_safe_composition_destinations(
        TenantAuditSurfaceKind kind,
        string? permissionHref,
        string? escalationHref,
        string absentAction)
    {
        RegisterServices(SnapshotFor(kind));
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition(
            permissionHref: permissionHref,
            escalationHref: escalationHref));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-recovery']");

        cut.FindAll($"[data-testid='tenants-audit-recovery-{absentAction}']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-recovery-description']").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("%252F%252Fevil.example", "/tenants?auditReturnUnavailable=true")]
    [InlineData("%2568%2574%2574%2570%2573%253A%252F%252Fevil.example", "/tenants?auditReturnUnavailable=true")]
    [InlineData("%252Ftenants%252F..%252Fadmin", "/tenants?auditReturnUnavailable=true")]
    [InlineData("/tenants/%2561ccess_token", "/tenants?auditReturnUnavailable=true")]
    [InlineData("/tenants/tenant.beta?filter=%2561uthorization", "/tenants?auditReturnUnavailable=true")]
    [InlineData("%2Ftenants%2Ftenant.beta%3Fkey%3D%2573ecret", "/tenants?auditReturnUnavailable=true")]
    [InlineData("%252Ftenants%252Ftenant.beta%253Ftab%253Daudit", "/tenants?auditReturnUnavailable=true")]
    [InlineData("/tenants?cursor=opaque-next&selected=tenant.beta", "/tenants?selected=tenant.beta&auditPartialReturn=true")]
    public void Tenant_audit_return_navigation_canonicalizes_safe_paths_and_rejects_encoded_unsafe_urls(
        string returnUrl,
        string expectedHref)
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("returnUrl", returnUrl));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-back']").GetAttribute("href").ShouldBe(expectedHref);
    }

    [Fact]
    public void Audit_page_without_return_context_returns_to_its_tenant_detail()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-back']").GetAttribute("href")
            .ShouldBe("/tenants/tenant.alpha");
        cut.Find("[data-testid='tenants-audit-back']").TextContent.ShouldContain("tenant detail");
    }

    [Theory]
    [InlineData("/tenants/my", "tenants-my-row-tenant.alpha", "your tenants")]
    [InlineData("/tenants/users?userId=user.alpha", "tenants-user-row-tenant.alpha", "user lookup")]
    [InlineData("/tenants?tab=users&userId=user.alpha", "tenants-user-row-tenant.alpha", "user lookup")]
    [InlineData("/tenants?tab=tenants&scope=mine", "tenants-my-row-tenant.alpha", "your tenants")]
    [InlineData("/tenants?search=alpha", "tenant-row-tenant.alpha", "the tenant list")]
    [InlineData("/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha", "tenants-member-user.alpha", "tenant detail")]
    public void Audit_back_and_return_context_name_the_validated_origin(string returnUrl, string returnFocus, string origin)
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            "/tenants/tenant.alpha/audit?returnUrl=" + Uri.EscapeDataString(returnUrl)
            + "&returnFocus=" + Uri.EscapeDataString(returnFocus));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-back']").TextContent.Trim().ShouldBe($"Back to {origin}");
        string context = cut.Find("[data-testid='tenants-audit-return-context']").TextContent.Trim();
        context.ShouldStartWith($"Return to {origin}.");
        context.ShouldNotContain(returnFocus);
    }

    [Fact]
    public void Matching_list_anchor_and_explicit_return_focus_restore_the_audit_launcher()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            "/tenants/tenant.alpha/audit?returnUrl="
            + Uri.EscapeDataString("/tenants?selected=tenant.alpha&anchor=tenant-row-tenant.alpha")
            + "&returnFocus=tenant-row-tenant.alpha");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        string href = cut.Find("[data-testid='tenants-audit-back']").GetAttribute("href").ShouldNotBeNull();
        href.ShouldBe("/tenants?selected=tenant.alpha&anchor=tenant-row-tenant.alpha&auditFocus=tenant-row-tenant.alpha");
        cut.Find("[data-testid='tenants-audit-return-context']").TextContent.ShouldContain("focus");
    }

    [Fact]
    public void Focusless_detail_return_preserves_a_stripped_cursor_notice()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            "/tenants/tenant.alpha/audit?returnUrl="
            + Uri.EscapeDataString("/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fcursor%3Dprotected%26selected%3Dtenant.alpha"));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        string href = cut.Find("[data-testid='tenants-audit-back']").GetAttribute("href").ShouldNotBeNull();
        href.ShouldContain("auditPartialReturn=true");
        href.ShouldNotContain("protected");
        cut.Find("[data-testid='tenants-audit-return-context']").TextContent.ShouldNotContain("focus");
    }

    [Fact]
    public void Older_audit_link_reports_partial_return_when_a_nested_list_cursor_is_stripped()
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter(
            "returnUrl", "/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fcursor%3Dopaque-next%26search%3Dalpha"));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        string href = cut.Find("[data-testid='tenants-audit-back']").GetAttribute("href").ShouldNotBeNull();
        href.ShouldContain("auditPartialReturn=true");
        href.ShouldNotContain("cursor");
    }

    [Fact]
    public void Invalid_cursor_recovery_after_page_two_clears_history_and_disables_previous()
    {
        TenantAuditRequest refreshedRequest = new("tenant.alpha");
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-page-one", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            TenantAuditSnapshot.ListRefreshed(
                [Row("event-refreshed", AuditEventCategory.Access)],
                null,
                false,
                "etag-refreshed",
                ReadModelFreshnessState.Current,
                refreshedRequest));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();

        cut.WaitForElement("[data-testid='tenants-audit-list-refreshed']");
        gateway.Requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-next"]);
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-audit-row']").GetAttribute("data-audit-reference").ShouldBe("event-refreshed");
    }

    [Fact]
    public void List_refreshed_after_ready_page_two_clears_paging_history()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-page-one", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-page-two", AuditEventCategory.Access)], nextCursor: "opaque-third", hasMore: true, requestCursor: "opaque-next"),
            TenantAuditSnapshot.ListRefreshed(
                [Row("event-refreshed", AuditEventCategory.Access)], null, false, "etag-refreshed",
                ReadModelFreshnessState.Current, new TenantAuditRequest("tenant.alpha")));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeFalse());
        cut.Find("[data-testid='tenants-audit-refresh']").Click();

        cut.WaitForElement("[data-testid='tenants-audit-list-refreshed']");
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
        gateway.Requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-next", "opaque-next"]);
    }

    [Fact]
    public async Task Recovered_first_page_clears_history_before_supplementary_detail_read_completes()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-page-one", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-page-two", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")], requestCursor: "opaque-next"),
            TenantAuditSnapshot.ListRefreshed(
                [Row("event-refreshed", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")], null, false, "etag-refreshed",
                ReadModelFreshnessState.Current, new TenantAuditRequest("tenant.alpha")));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeFalse());
        var pendingDetail = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueDetailResponse(pendingDetail.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.DetailRequests.Count.ShouldBe(3));

        cut.Find("[data-testid='tenants-audit-list-refreshed']").GetAttribute("role").ShouldBe("status");
        cut.Find("[data-testid='tenants-audit-row']").GetAttribute("data-audit-reference")
            .ShouldBe("event-refreshed");
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();

        pendingDetail.SetResult(TenantDetailSnapshot.Unavailable("tenant.alpha"));
        await refresh;
    }

    [Fact]
    public void Invalid_cursor_after_ready_page_two_refreshes_with_a_null_cursor()
    {
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-page-one", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            ReadySnapshot([Row("event-page-two", AuditEventCategory.Access)], requestCursor: "opaque-next"),
            TenantAuditSnapshot.InvalidCursor(new TenantAuditRequest("tenant.alpha", Cursor: "opaque-next")),
            ReadySnapshot([Row("event-recovered", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeFalse());
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForElement("[data-testid='tenants-audit-invalid-cursor']");
        cut.Find("[data-testid='tenants-audit-recovery-refresh']").Click();

        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(4));
        gateway.Requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-next", "opaque-next", null]);
        cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Invalid_local_filter_discards_an_in_flight_ready_completion()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-old", AuditEventCategory.Access)]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(pending.Task);

        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.Find("[data-testid='tenants-audit-filter-from']").Change("invalid-date");
        pending.SetResult(ReadySnapshot([Row("event-late", AuditEventCategory.Access)]));
        await refresh;

        gateway.Requests.Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-audit-invalid-filters']").GetAttribute("role").ShouldBe("alert");
        cut.FindAll("[data-testid='tenants-audit-grid']").ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_filter_receipt_reset_clears_filters_and_restarts_the_audit_read()
    {
        TenantAuditSnapshot rows = ReadySnapshot([Row("event-a", AuditEventCategory.Access)]);
        StubTenantQueryGateway gateway = RegisterServices(rows, rows);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?receiptReference=event-a");
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");

        cut.Find("[data-testid='tenants-audit-filter-from']").Change("invalid-date");
        cut.Find("[data-testid='tenants-audit-receipt-recovery-reset']").Click();

        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));
        cut.Find("[data-testid='tenants-audit-filter-from']").GetAttribute("aria-invalid").ShouldBe("false");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
    }

    [Fact]
    public void Administrative_entry_with_user_and_configuration_key_uses_the_typed_user_as_receipt_target()
    {
        TenantAuditEntry entry = new(
            "event-configuration",
            "TenantConfigurationSet",
            AuditEventCategory.Administrative,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "affected-user",
                ["key"] = "billing.mode",
            });
        TenantAuditRow row = TenantAuditRow.FromEntry(entry, ReadModelFreshnessState.Current) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        RegisterServices(ReadySnapshot([row]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-row-target']").TextContent.ShouldBe("affected-user");
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.Find("[data-testid='tenants-audit-receipt-target']").TextContent.ShouldContain("affected-user");
        cut.Find("[data-testid='tenants-audit-receipt-target']").TextContent.ShouldNotContain("billing.mode");
        cut.Find("[data-testid='tenants-audit-receipt-state']").TextContent.ShouldContain("ready");
    }

    [Fact]
    public void RoleChangeEvidenceRequiresDeliberateSelectionEvenWhenHistoricalRoleExists()
    {
        TenantAuditEntry entry = new(
            "event-role-change",
            "UserRoleChanged",
            AuditEventCategory.Access,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["userId"] = "target-user",
                ["oldRole"] = "TenantReader",
                ["newRole"] = "TenantContributor",
            });
        TenantAuditRow row = TenantAuditRow.FromEntry(entry, ReadModelFreshnessState.Current) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        RegisterServices(ReadySnapshot([row]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-role']");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-role")]
    public void User_role_changed_without_an_enumerable_old_role_requires_a_picker(string? oldRole)
    {
        var narrative = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["userId"] = "target-user",
            ["newRole"] = "TenantContributor",
        };
        if (oldRole is not null)
        {
            narrative["oldRole"] = oldRole;
        }

        TenantAuditRow row = TenantAuditRow.FromEntry(new TenantAuditEntry(
            "event-role-change",
            "UserRoleChanged",
            AuditEventCategory.Access,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "tenant.alpha",
            narrative), ReadModelFreshnessState.Current) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        RegisterServices(ReadySnapshot([row]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-role']").GetAttribute("aria-label")
            .ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("target-user; oldRole: TenantOwner")]
    public void Access_rows_without_a_safe_typed_user_never_offer_a_correction(string? userId)
    {
        var narrative = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = "configuration.mode",
            ["oldRole"] = "TenantReader",
        };
        if (userId is not null)
        {
            narrative["userId"] = userId;
        }

        TenantAuditRow row = TenantAuditRow.FromEntry(new TenantAuditEntry(
            "event-role-change",
            "UserRoleChanged",
            AuditEventCategory.Access,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "tenant.alpha",
            narrative), ReadModelFreshnessState.Current) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        row.Target.ShouldBe("configuration.mode");
        RegisterServices(ReadySnapshot([row]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']")
            .TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Phone_transition_suppresses_corrections_and_viewport_observation_after_disposal_is_ignored()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        RegisterServices(
            viewport,
            ReadySnapshot([Row(
                "event-role-change",
                AuditEventCategory.Access,
                "userId: target-user; oldRole: TenantReader",
                eventType: "UserRoleChanged")]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.Find("[data-testid='tenants-correction-start']").TextContent.ShouldNotBeNullOrWhiteSpace();

        viewport.Observe(ViewportTier.Phone);

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty());
        cut.Find("[data-testid='tenants-correction-mobile-read-only']").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();

        cut.Dispose();

        Should.NotThrow(() => viewport.Observe(ViewportTier.Desktop));
    }

    [Fact]
    public void Tenant_audit_components_do_not_call_backend_or_use_browser_token_storage()
    {
        string projectRoot = ProjectRoot();
        string[] componentFiles =
        [
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "TenantAuditPage.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "AuditDataGrid.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "AuditEvidenceReceipt.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "CorrectionStartPanel.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "GlobalAdministratorCorrectionPanel.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "State", "TenantAudit", "TenantAuditReceipt.cs"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "State", "TenantAudit", "TenantCorrectionStartIntent.cs"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "State", "TenantAudit", "GlobalAdministratorCorrectionSnapshot.cs"),
        ];
        string combined = string.Join('\n', componentFiles.Select(File.ReadAllText));

        combined.ShouldNotContain("GET /api/tenants", Case.Insensitive);
        combined.ShouldNotContain("POST /api/v1/commands", Case.Insensitive);
        combined.ShouldNotContain("GET /api/v1/commands/status", Case.Insensitive);
        combined.ShouldNotContain("HttpClient", Case.Insensitive);
        combined.ShouldNotContain("localStorage", Case.Insensitive);
        combined.ShouldNotContain("sessionStorage", Case.Insensitive);
        combined.ShouldNotContain("access_token", Case.Insensitive);
        combined.ShouldNotContain("raw payload", Case.Insensitive);
        combined.ShouldNotContain("EventStore metadata", Case.Insensitive);
    }

    [Fact]
    public void Tenant_correction_copy_uses_forward_recovery_language_and_omits_diagnostic_markers()
    {
        string projectRoot = ProjectRoot();
        string[] files =
        [
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "AuditDataGrid.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "AuditEvidenceReceipt.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "CorrectionStartPanel.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Tenants", "Audit", "GlobalAdministratorCorrectionPanel.razor"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "State", "TenantAudit", "TenantCorrectionStartIntent.cs"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Resources", "TenantsResources.resx"),
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Resources", "TenantsResources.fr.resx"),
        ];
        string combined = string.Join('\n', files.Select(path =>
        {
            string content = File.ReadAllText(path);
            int codeBlock = string.Equals(Path.GetExtension(path), ".razor", StringComparison.OrdinalIgnoreCase)
                ? content.IndexOf("@code", StringComparison.Ordinal)
                : -1;
            return codeBlock >= 0 ? content[..codeBlock] : content;
        }));

        combined.ShouldNotContain("undo", Case.Insensitive);
        combined.ShouldNotContain("rollback", Case.Insensitive);
        combined.ShouldNotContain("hidden edit", Case.Insensitive);
        combined.ShouldNotContain("Bearer ", Case.Insensitive);
        combined.ShouldNotContain("JWT", Case.Insensitive);
        combined.ShouldNotContain("stack trace", Case.Insensitive);
        combined.ShouldNotContain("correlation id", Case.Insensitive);
        combined.ShouldNotContain("MessageId", Case.Insensitive);
        combined.ShouldNotContain("protected cursor", Case.Insensitive);
        combined.ShouldNotContain("ETag", Case.Insensitive);
    }

    [Fact]
    public void Audit_styles_preserve_responsive_safety_and_accessibility_hooks()
    {
        string projectRoot = ProjectRoot();
        string styles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "AuditDataGrid.razor.css"));
        string pageStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Pages",
            "TenantAuditPage.razor.css"));

        styles.ShouldContain("overflow-x: auto");
        styles.ShouldContain("min-width:");
        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain("tenants-audit-critical");
        styles.ShouldContain("grid-template-columns: minmax(0, 1fr) auto");
        pageStyles.ShouldContain(":focus-visible");
        pageStyles.ShouldContain("@media (forced-colors: active)");
        pageStyles.ShouldContain("white-space: pre-wrap");

        string receiptStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "AuditEvidenceReceipt.razor.css"));

        receiptStyles.ShouldContain(":focus-visible");
        receiptStyles.ShouldContain("@media (forced-colors: active)");
        receiptStyles.ShouldContain("@media (prefers-reduced-motion: reduce)");
        receiptStyles.ShouldContain("grid-template-columns: repeat(auto-fit");

        string correctionStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "CorrectionStartPanel.razor.css"));

        correctionStyles.ShouldContain(":focus-visible");
        correctionStyles.ShouldContain("@media (forced-colors: active)");
        correctionStyles.ShouldContain("@media (prefers-reduced-motion: reduce)");

        string globalAdminCorrectionStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "GlobalAdministratorCorrectionPanel.razor.css"));

        globalAdminCorrectionStyles.ShouldContain(":focus-visible");
        globalAdminCorrectionStyles.ShouldContain("@media (forced-colors: active)");
        globalAdminCorrectionStyles.ShouldContain("@media (prefers-reduced-motion: reduce)");
    }

    [Fact]
    public void Audit_resource_keys_have_english_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> english = ResourceKeys(Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Resources", "TenantsResources.resx"));
        HashSet<string> french = ResourceKeys(Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Resources", "TenantsResources.fr.resx"));
        string[] auditKeys = english
            .Where(key => key.StartsWith("Tenants.Audit.", StringComparison.Ordinal)
                || key.StartsWith("Tenants.Correction.", StringComparison.Ordinal))
            .ToArray();

        auditKeys.ShouldNotBeEmpty();
        foreach (string key in auditKeys)
        {
            french.ShouldContain(key);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Tenant_audit_page_uses_localized_fallback_heading_for_blank_tenant_id(string blankTenantId)
    {
        RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, blankTenantId));
        cut.WaitForElement("[data-testid='tenants-audit-unavailable']");

        // A blank/whitespace TenantId must render the localized fallback, never a dangling
        // "Audit trail for " heading (AC8 — cosmetic, not a crash fix).
        cut.Markup.ShouldContain("Audit trail for this tenant");
        cut.Markup.ShouldNotContain("Audit trail for <", Case.Insensitive);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("tenant/other")]
    [InlineData("tenant\u200dalpha")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c")]
    public void UnsafeDirectTenantRouteNeverQueriesAudit(string tenantId)
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, tenantId));

        cut.Find("[data-testid='tenants-audit-unavailable']").ShouldNotBeNull();
        cut.Find("#tenant-audit-heading").TextContent.ShouldContain("Audit trail for this tenant");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void UnpairedSurrogateDirectRouteUsesFallbackHeadingWithoutReadingAudit()
    {
        StubTenantQueryGateway gateway = RegisterServices(ReadySnapshot([Row("event-1", AuditEventCategory.Access)]));
        string unsafeTenantId = "tenant" + new string((char)0xD800, 1) + "alpha";

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, unsafeTenantId));

        cut.Find("#tenant-audit-heading").TextContent.ShouldContain("Audit trail for this tenant");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void TenantAuditPageDoesNotReadGlobalAuthorityForReadOnlyGlobalEvidence()
    {
        // Global correction is read-only on this tenant page, so an unavailable global authority
        // service must not add a read dependency to displaying its audit evidence.
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubTenantQueryGateway gateway = new(GlobalAdminAuditSnapshot("GlobalAdministratorSet", "admin-user"))
        {
            GlobalAdminFault = new HttpRequestException("projection read failed"),
        };
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition(authorized: true));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        gateway.GlobalAdminRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("projection")]
    [InlineData("authorization")]
    public void System_audit_stays_readable_when_global_correction_evidence_fails(string failure)
    {
        StubTenantQueryGateway gateway = RegisterGlobalAdminServices(
            authorized: true, GlobalAdmins("other-admin"),
            GlobalAdminAuditSnapshot("GlobalAdministratorRemoved", "admin-user"));
        if (failure == "projection")
        {
            gateway.GlobalAdminFault = new HttpRequestException("projection read failed");
        }
        else
        {
            Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition(authorized: true)
            {
                GlobalAuthorizationFault = new HttpRequestException("authorization read failed"),
            });
        }

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "system"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldBe("event-global-admin");
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("grid")]
    [InlineData("receipt")]
    public void DeliberateStartAndSeparatePreviewHandoffNeverSubmitOrPoll(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user; oldRole: TenantOwner", eventType: "UserRoleChanged")]));
        BunitJSModuleInterop focusModule = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> headingFocus = focusModule.Setup<bool>("focusElementById", "tenants-correction-title");
        headingFocus.SetResult(true);
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        if (origin == "receipt")
        {
            cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
            FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditEvidenceReceipt>(),
                "tenants-correction-role", TenantRole.TenantReader.ToString());
        }
        else
        {
            FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        }
        string container = origin == "receipt" ? "tenants-audit-receipt" : "tenants-audit-grid";
        cut.Find($"[data-testid='{container}'] [data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-panel']");
        cut.FindAll("[data-testid='tenants-correction-confirm']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-start-intended-role']").TextContent.ShouldBe("Tenant reader");
        query.DetailRequests.Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-panel']");
        cut.Find("[data-testid='tenants-correction-original-evidence']").TextContent.ShouldBe("event-correction");
        cut.WaitForAssertion(() => headingFocus.Invocations.Count.ShouldBe(1));
        focusModule.Invocations["focusElementById"].Last().Arguments[0].ShouldBe("tenants-correction-title");
        focusModule.Invocations["focusCorrectionLauncher"].ShouldBeEmpty();
        string previewText = cut.Find("[data-testid='tenants-correction-panel']").TextContent;
        previewText.ShouldNotContain("tenant-projection-current");
        System.Text.RegularExpressions.Regex.IsMatch(previewText, @"\btrue\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .ShouldBeFalse();
        query.DetailRequests.Count.ShouldBe(3);
        commands.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "get_SupportsCommandStatusLookup");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartAndHandoffRecheckAuthorityAndCommandSupport(bool supportLoss)
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-panel']");
        if (supportLoss) query.CorrectionSupport = false;
        else query.CorrectionAuthorized = false;
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-receipt-reference']").TextContent.ShouldBe("event-correction");
        string receiptReason = cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-unavailable-reason']")
            .TextContent.Trim();
        if (!supportLoss)
        {
            receiptReason.ShouldBe("Current access could not be verified. Refresh or request permission before starting correction.");
            cut.Find("[data-testid='tenants-audit-grid'] [data-testid='tenants-correction-unavailable-reason']")
                .TextContent.Trim().ShouldBe("Current access could not be verified. Refresh or request permission before starting correction.");
        }
    }

    [Fact]
    public void DisconnectedCommandSurfaceBlocksStartAndHandoff()
    {
        RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition(commandConnected: false));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-audit-grid'] [data-testid='tenants-correction-unavailable-reason']").TextContent.Trim()
            .ShouldBe("The tenant correction command path is not connected.");
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
    }

    [Fact]
    public void ClosingReceiptLaunchedStartReturnsFocusToReceiptLauncher()
    {
        RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusCorrectionLauncher", "event-correction", "receipt");
        focus.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditEvidenceReceipt>(),
            "tenants-correction-role", TenantRole.TenantReader.ToString());
        var launcher = cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']");
        launcher.GetAttribute("data-correction-origin").ShouldBe("receipt");
        launcher.Click();
        cut.Find("[data-testid='tenants-correction-start-cancel']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        module.Invocations["focusCorrectionLauncher"].Count.ShouldBe(1);
    }

    [Fact]
    public async Task RoleSelectionChangeInvalidatesAPendingStart()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        var pending = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        query.QueueDetailResponse(pending.Task);
        Task opening = cut.Find("[data-testid='tenants-correction-start']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => query.DetailRequests.Count.ShouldBe(2));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantOwner.ToString());
        pending.SetResult(DetailSnapshot(TenantRole.TenantContributor));
        await opening;
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
    }

    [Fact]
    public void AuditRefreshCapturesMultipleTargetsWithOneUnconditionalDetailRead()
    {
        TenantAuditRow first = Row("event-first", AuditEventCategory.Access, "userId: target-user", eventType: "UserRemovedFromTenant");
        TenantAuditRow second = first with { EventReference = "event-second", Target = "another-user", Narrative = new TenantAuditNarrative(UserId: "another-user") };
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([first, second]), ReadySnapshot([first, second]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        query.DetailRequests.ShouldHaveSingleItem().ETag.ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        query.DetailRequests.Count.ShouldBe(2);
        query.DetailRequests.ShouldAllBe(request => request.ETag == null);
    }

    [Fact]
    public void PreviewHandoffRetainsFreshCaptureWhenEarlierDetailDisagrees()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRemovedFromTenant")]));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        query.QueueDetailResponse(Task.FromResult(DetailSnapshot(TenantRole.TenantOwner)));
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        CorrectionStartPanel preview = cut.FindComponent<CorrectionStartPanel>().Instance;
        preview.CurrentProjection.ShouldBeNull();
        preview.StartProjection!.CurrentRole.ShouldBe(TenantRole.TenantOwner);
        preview.Snapshot!.CurrentRole.ShouldBe(TenantRole.TenantOwner);
        preview.Snapshot.IntendedRole.ShouldBe(TenantRole.TenantReader);
        preview.Snapshot.Intent.IntendedCommandType.ShouldBe(TenantCorrectionCommandType.ChangeUserRole);
        preview.Snapshot.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
    }

    [Fact]
    public async Task PendingHandoffKeepsStartEvidenceAndCancelAndRejectsItsLateResult()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        var pending = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        query.QueueDetailResponse(pending.Task);
        Task handoff = cut.Find("[data-testid='tenants-correction-start-handoff']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => query.DetailRequests.Count.ShouldBe(3));
        cut.Find("[data-testid='tenants-correction-start-reference']").TextContent.ShouldBe("event-correction");
        cut.Find("[data-testid='tenants-correction-start-pending']").GetAttribute("role").ShouldBe("status");
        cut.Find("[data-testid='tenants-correction-start-handoff']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-start-cancel']").Click();
        pending.SetResult(DetailSnapshot(TenantRole.TenantContributor));
        await handoff;
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        commands.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task NotificationSupersedingHandoffRetainsTheStartAndRequiresAnotherDeliberateHandoff()
    {
        TenantAuditSnapshot audit = ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway query = RegisterServices(audit, audit);
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(Substitute.For<IProjectionSubscription>());
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        var pending = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        query.QueueDetailResponse(pending.Task);
        Task handoff = cut.Find("[data-testid='tenants-correction-start-handoff']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => query.DetailRequests.Count.ShouldBe(3));
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(GetTenantAuditQuery.ProjectionType, "tenant.alpha");
        cut.WaitForAssertion(() => query.DetailRequests.Count.ShouldBe(4));
        pending.SetResult(DetailSnapshot(TenantRole.TenantOwner));
        await handoff;
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-start-panel']");
        cut.Find("[data-testid='tenants-correction-start-current-role']").TextContent.ShouldBe("Tenant contributor");
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-panel']");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotificationRefreshBlocksAnOpenPreviewWithoutLosingOriginalEvidence(bool stale)
    {
        TenantAuditSnapshot audit = ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]);
        StubTenantQueryGateway query = RegisterServices(audit, audit);
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton(Substitute.For<IProjectionSubscription>());
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        CorrectionStartPanel preview = cut.FindComponent<CorrectionStartPanel>().Instance;
        preview.Snapshot!.CanSubmit.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
        if (stale)
        {
            query.QueueDetailResponse(Task.FromResult(TenantDetailSnapshot.Ready(
                DetailSnapshot(TenantRole.TenantContributor).Detail!, null, ReadModelFreshnessState.Stale)));
        }
        else
        {
            query.CorrectionAuthorized = false;
        }
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(GetTenantAuditQuery.ProjectionType, "tenant.alpha");
        cut.WaitForAssertion(() => preview.Snapshot!.CanSubmit.ShouldBeFalse());
        cut.FindComponent<CorrectionStartPanel>().Instance.ShouldBeSameAs(preview);
        preview.Snapshot!.OriginalAuditReference.ShouldBe("event-correction");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        if (!stale)
        {
            cut.Find("[data-testid='tenants-correction-panel'] [data-testid='tenants-correction-unavailable-reason']")
                .TextContent.Trim().ShouldBe("Current access could not be verified. Refresh or request permission before starting correction.");
        }
    }

    [Fact]
    public void RoleSelectionAndAnotherStartPreserveASubmittedPreviewAndItsTrackingHandle()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        Services.AddSingleton(new TenantCorrectionAttemptTracker());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1,
                HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        CorrectionStartPanel preview = cut.FindComponent<CorrectionStartPanel>().Instance;
        cut.WaitForAssertion(() => preview.Snapshot!.HasCommandTracking.ShouldBeTrue());
        preview.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        string? messageId = preview.Snapshot!.MessageId;
        int detailReads = query.DetailRequests.Count;
        FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditDataGrid>(), "tenants-correction-role", TenantRole.TenantOwner.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.ShouldBeSameAs(preview);
        cut.FindComponent<CorrectionStartPanel>().Instance.RetainedAttemptMessageId.ShouldBe(messageId);
        preview.Snapshot!.MessageId.ShouldBe(messageId);
        preview.Snapshot.IntendedRole.ShouldBe(TenantRole.TenantReader);
        query.DetailRequests.Count.ShouldBe(detailReads);
        cut.FindAll("[data-testid='tenants-correction-aggregate-busy']").ShouldBeEmpty();
    }

    [Fact]
    public void QueryCaptureConfirmationRendersTheOutcomeAndConsumesTheCommittedSequenceRead()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1,
                HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        int readsBeforeConfirm = query.DetailRequests.Count;
        query.QueueDetailResponse(Task.FromResult(DetailSnapshot(TenantRole.TenantContributor)));
        query.QueueDetailResponse(Task.FromResult(TenantDetailSnapshot.Ready(
            DetailSnapshot(TenantRole.TenantReader).Detail!,
            "\"detail-etag\"",
            ReadModelFreshnessState.Current,
            projectionVersion: "tenant-sequence:2")));
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-state']").TextContent
            .ShouldContain("Projection confirms the intended state; corrective audit association is unavailable. Inspect audit evidence."));
        (query.DetailRequests.Count - readsBeforeConfirm).ShouldBe(2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Original_receipt_returns_exact_focus_to_preview_or_safe_fallback(bool loseSourceRow, bool closePreview)
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        RegisterServices(ReadySnapshot([source]), ReadySnapshot([]));
        Services.AddSingleton(new TenantCorrectionAttemptTracker());
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Pending")));
        Services.AddSingleton(commands);
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        if (loseSourceRow)
        {
            cut.Find("[data-testid='tenants-audit-refresh']").Click();
            cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-row']").ShouldBeEmpty());
        }
        cut.Find("[data-testid='tenants-correction-original-receipt']").GetAttribute("id")
            .ShouldBe("tenants-correction-original-receipt");
        cut.Find("[data-testid='tenants-correction-original-receipt']").Click();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-role']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").ShouldBeEmpty();
        if (closePreview) cut.Find("[data-testid='tenants-correction-cancel']").Click();
        int previousFocusCalls = focus.Invocations.Count;

        cut.Find("[data-testid='tenants-audit-receipt-close']").Click();

        string expectedTarget = !closePreview ? "tenants-correction-original-receipt"
            : loseSourceRow ? "tenant-audit-heading" : "tenants-audit-receipt-launcher-0";
        cut.WaitForAssertion(() => {
            focus.Invocations.Count.ShouldBe(previousFocusCalls + 1);
            focus.Invocations.Last().Arguments[0].ShouldBe(expectedTarget);
        });
        cut.Find($"#{expectedTarget}").ShouldNotBeNull();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public void OriginalReceiptHidesCorrectionControlsDuringUnsubmittedPreviewAndRestoresThemAfterCancel()
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        RegisterServices(ReadySnapshot([source]));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        CorrectionStartPanel preview = cut.FindComponent<CorrectionStartPanel>().Instance;
        preview.HasSubmitted.ShouldBeFalse();

        cut.Find("[data-testid='tenants-correction-original-receipt']").Click();

        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldHaveSingleItem();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-role']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").ShouldBeEmpty();
        cut.FindComponent<CorrectionStartPanel>().Instance.ShouldBeSameAs(preview);
        preview.Snapshot!.OriginalAuditReference.ShouldBe("event-correction");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();

        cut.Find("[data-testid='tenants-correction-cancel']").Click();

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-role']").ShouldHaveSingleItem();
        cut.FindAll("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Pending_correction_can_resume_after_cancel_row_loss_and_narrow_viewport()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        StubTenantQueryGateway query = RegisterServices(viewport, ReadySnapshot([source]), ReadySnapshot([]));
        TenantCorrectionAttemptTracker tracker = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Status is still pending.")));
        Services.AddSingleton(commands);

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        CorrectionStartPanel panel = cut.FindComponent<CorrectionStartPanel>().Instance;
        cut.WaitForAssertion(() => panel.Snapshot!.HasCommandTracking.ShouldBeTrue());
        string messageId = panel.Snapshot!.MessageId!;

        cut.Find("[data-testid='tenants-correction-cancel']").Click();
        cut.Find("[data-testid='tenants-correction-resume']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId);

        TaskCompletionSource<TenantAuditSnapshot> pendingAudit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        query.QueueResponse(pendingAudit.Task);
        Task refresh = cut.Find("[data-testid='tenants-audit-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-correction-resume']").ShouldBeEmpty();
        pendingAudit.SetResult(TenantAuditSnapshot.Unauthorized(new TenantAuditRequest("tenant.alpha")));
        await refresh;
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-resume']").ShouldBeEmpty();
        tracker.Find("tenant.alpha")!.MessageId.ShouldBe(messageId);

        cut.Find("[data-testid='tenants-audit-refresh']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-row']").ShouldBeEmpty());
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId);
        int auditReads = query.Requests.Count;
        cut.Find("[data-testid='tenants-correction-original-receipt']").Click();
        cut.Find("[data-testid='tenants-audit-receipt']").TextContent.ShouldContain("not loaded");
        query.Requests.Count.ShouldBe(auditReads);
        viewport.Observe(ViewportTier.Phone);
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId);
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(),
            Arg.Is<string>(id => id == messageId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Remount_restores_pending_attempt_on_a_stale_authorized_audit_surface()
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        TenantAuditSnapshot stale = TenantAuditSnapshot.Stale([source with {
            Freshness = ReadModelFreshnessState.Stale,
        }], null, false, "\"etag\"", new TenantAuditRequest("tenant.alpha"));
        RegisterServices(ReadySnapshot([source]), stale, ReadySnapshot([source]));
        TenantCorrectionAttemptTracker tracker = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Status is still pending.")));
        Services.AddSingleton(commands);
        BunitJSModuleInterop focusModule = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> launcherFocus = focusModule.Setup<bool>("focusCorrectionLauncher", "event-correction", "grid");
        launcherFocus.SetResult(true);
        IRenderedComponent<TenantAuditPage> first = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(first, "tenants-correction-role", TenantRole.TenantReader.ToString());
        first.Find("[data-testid='tenants-correction-start']").Click();
        first.Find("[data-testid='tenants-correction-start-handoff']").Click();
        first.Find("[data-testid='tenants-correction-confirm']").Click();
        first.WaitForAssertion(() => first.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.HasCommandTracking.ShouldBeTrue());
        string messageId = tracker.Find("tenant.alpha")!.MessageId;
        first.Dispose();

        IRenderedComponent<TenantAuditPage> resumed = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        resumed.WaitForAssertion(() => resumed.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId));
        resumed.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeFalse();
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(),
            Arg.Is<string>(id => id == messageId), Arg.Any<CancellationToken>());

        resumed.Find("[data-testid='tenants-correction-close']").Click();
        resumed.WaitForAssertion(() => launcherFocus.Invocations.Count.ShouldBe(1));
        resumed.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        resumed.Find("[data-testid='tenants-correction-resume']").Click();

        tracker.TryUpdate("tenant.alpha", messageId,
            tracker.Find("tenant.alpha")!.Snapshot with {
                LifecycleState = TenantCommandLifecycleState.Rejected,
            }).ShouldBeTrue();
        resumed.Dispose();
        IRenderedComponent<TenantAuditPage> later = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        later.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        later.Find("[data-testid='tenants-correction-resume']").Click();
        later.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId);
    }

    [Fact]
    public void TenantRebindingHidesOtherTenantRetainedCorrectionAndRestoresItOnReturn()
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([source]), ReadySnapshot([source]));
        using TenantCorrectionAttemptTracker tracker = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Status is still pending.")));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.HasCommandTracking.ShouldBeTrue());
        string messageId = tracker.Find("tenant.alpha")!.MessageId;

        TaskCompletionSource<TenantAuditSnapshot> pendingBeta = new(TaskCreationOptions.RunContinuationsAsynchronously);
        query.QueueResponse(pendingBeta.Task);
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() =>
        {
            query.Requests.Last().TenantId.ShouldBe("tenant.beta");
            cut.FindAll("[data-testid='tenants-correction-preview']").ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-correction-refresh']").ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-correction-resume']").ShouldBeEmpty();
        });

        TenantAuditRow betaRow = Row("event-beta", AuditEventCategory.Access) with
        {
            TenantId = "tenant.beta",
            Scope = "tenant.beta",
        };
        pendingBeta.SetResult(ReadySnapshot([betaRow]) with { TenantId = "tenant.beta" });
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-row']").GetAttribute("data-audit-reference").ShouldBe("event-beta");
            cut.FindAll("[data-testid='tenants-correction-preview']").ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-correction-refresh']").ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-correction-resume']").ShouldBeEmpty();
        });
        tracker.Find("tenant.beta").ShouldBeNull();
        tracker.Find("tenant.alpha")!.MessageId.ShouldBe(messageId);

        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() =>
        {
            TenantCorrectionPreviewSnapshot restored = cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!;
            restored.TenantId.ShouldBe("tenant.alpha");
            restored.OriginalAuditReference.ShouldBe("event-correction");
            restored.MessageId.ShouldBe(messageId);
            cut.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeFalse();
        });
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(),
            Arg.Is<string>(id => id == messageId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_another_row_resumes_the_pending_attempt_with_a_busy_reason()
    {
        TenantAuditRow firstRow = Row("event-first", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        TenantAuditRow secondRow = Row("event-second", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        RegisterServices(ReadySnapshot([firstRow, secondRow]));
        TenantCorrectionAttemptTracker tracker = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Status is still pending.")));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        foreach (FluentSelect<string, string> select in cut.FindComponents<FluentSelect<string, string>>()
            .Select(component => component.Instance)
            .Where(select => select.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? value)
                && Equals(value, "tenants-correction-role")))
        {
            await cut.InvokeAsync(() => select.ValueChanged.InvokeAsync(TenantRole.TenantReader.ToString()));
        }
        cut.FindAll("[data-testid='tenants-correction-start']")[0].Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.HasCommandTracking.ShouldBeTrue());
        string messageId = tracker.Find("tenant.alpha")!.MessageId;

        cut.FindAll("[data-testid='tenants-correction-start']")[1].Click();

        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(messageId);
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldContain("Another tenant command is being reconciled");
        cut.Find("[data-testid='tenants-correction-aggregate-busy']").TextContent
            .ShouldContain("Another tenant command is being reconciled");
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(),
            Arg.Is<string>(id => id == messageId), Arg.Any<CancellationToken>());

        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Rejected,
                RejectionCode: "InsufficientPermissions", HasVerifiedCommandIdentity: true)));
        cut.Find("[data-testid='tenants-correction-refresh']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Rejected));
        cut.FindAll("[data-testid='tenants-correction-aggregate-busy']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldNotContain("Another tenant command");
        cut.Find("[data-testid='tenants-correction-close']").Click();
        cut.FindAll("[data-testid='tenants-correction-start']")[1].Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.FindAll("[data-testid='tenants-correction-aggregate-busy']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Resume_closes_another_rows_start_panel_before_showing_the_retained_preview()
    {
        TenantAuditRow firstRow = Row("event-first", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        TenantAuditRow secondRow = firstRow with { EventReference = "event-second" };
        RegisterServices(ReadySnapshot([firstRow, secondRow]));
        TenantCorrectionAttemptTracker tracker = new();
        TenantAggregateCommandAdmissionGate gate = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(gate);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelect<string, string> firstSelect = cut.FindComponents<FluentSelect<string, string>>()
            .Select(component => component.Instance).First(select => select.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? value) && Equals(value, "tenants-correction-role"));
        await cut.InvokeAsync(() => firstSelect.ValueChanged.InvokeAsync(TenantRole.TenantReader.ToString()));
        cut.FindAll("[data-testid='tenants-correction-start']")[0].Click();
        TenantCorrectionStartIntent firstIntent = cut.FindComponent<TenantCorrectionStartPanel>().Instance.Intent!;
        cut.Find("[data-testid='tenants-correction-start-cancel']").Click();
        FluentSelect<string, string> secondSelect = cut.FindComponents<FluentSelect<string, string>>()
            .Select(component => component.Instance).Last(select => select.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? value) && Equals(value, "tenants-correction-role"));
        await cut.InvokeAsync(() => secondSelect.ValueChanged.InvokeAsync(TenantRole.TenantReader.ToString()));
        cut.FindAll("[data-testid='tenants-correction-start']")[1].Click();
        cut.FindComponent<TenantCorrectionStartPanel>().Instance.Intent!.OriginalAuditReference.ShouldBe("event-second");
        tracker.TryBegin(TenantCorrectionPreviewSnapshot.FromIntent(firstIntent), gate, out TenantCorrectionAttempt? retained)
            .ShouldBeTrue();
        cut.Render();

        cut.Find("[data-testid='tenants-correction-resume']").Click();

        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldHaveSingleItem();
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(retained!.MessageId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expired_attempt_is_resumable_but_does_not_redirect_or_block_a_fresh_correction(bool hasCorrelation)
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        RegisterServices(ReadySnapshot([source]), ReadySnapshot([source]));
        DateTimeOffset now = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
        using TenantCorrectionAttemptTracker tracker = new(() => now);
        TenantAggregateCommandAdmissionGate gate = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(gate);
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "new-tracking-safe")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Pending("Status pending")));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> first = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(first, "tenants-correction-role", TenantRole.TenantReader.ToString());
        first.Find("[data-testid='tenants-correction-start']").Click();
        first.Find("[data-testid='tenants-correction-start-handoff']").Click();
        tracker.TryBegin(first.FindComponent<CorrectionStartPanel>().Instance.Snapshot!, gate,
            out TenantCorrectionAttempt? retained).ShouldBeTrue();
        if (hasCorrelation) tracker.TryUpdate("tenant.alpha", retained!.MessageId,
            retained.Snapshot.Accepted(TenantCommandSubmissionResult.Accepted(retained.MessageId, "old-tracking-safe")))
            .ShouldBeTrue();
        first.Dispose();
        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));

        cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-resume']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBe(retained!.MessageId);
        cut.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBe(!hasCorrelation);
        if (hasCorrelation)
        {
            cut.Find("[data-testid='tenants-correction-refresh']").Click();
            _ = commands.Received(1).GetStatusAsync(Arg.Is<TenantCommandTrackingHandle>(h => h.MessageId == retained.MessageId),
                Arg.Any<CancellationToken>());
        }
        cut.Find("[data-testid='tenants-correction-close']").Click();
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBeNull();
        cut.FindAll("[data-testid='tenants-correction-aggregate-busy']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => tracker.Find("tenant.alpha")!.MessageId.ShouldNotBe(retained.MessageId));
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(),
            Arg.Is<string>(id => id != retained.MessageId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpiredAttemptDoesNotKeepFreshPreviewVisibleAfterRowLoss(bool stale)
    {
        TenantAuditRow source = Row("event-correction", AuditEventCategory.Access,
            "userId: target-user", eventType: "UserRoleChanged");
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([source]), ReadySnapshot([source]));
        DateTimeOffset now = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
        using TenantCorrectionAttemptTracker tracker = new(() => now);
        TenantAggregateCommandAdmissionGate gate = new();
        Services.AddSingleton(tracker);
        Services.AddSingleton(gate);
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> first = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(first, "tenants-correction-role", TenantRole.TenantReader.ToString());
        first.Find("[data-testid='tenants-correction-start']").Click();
        first.Find("[data-testid='tenants-correction-start-handoff']").Click();
        tracker.TryBegin(first.FindComponent<CorrectionStartPanel>().Instance.Snapshot!, gate,
            out TenantCorrectionAttempt? retained).ShouldBeTrue();
        first.Dispose();
        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters =>
            parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.Snapshot!.MessageId.ShouldBeNull();
        tracker.Find("tenant.alpha")!.MessageId.ShouldBe(retained!.MessageId);

        TenantAuditSnapshot withoutRow = stale
            ? TenantAuditSnapshot.Stale([], null, false, "\"etag\"", new TenantAuditRequest("tenant.alpha"))
            : ReadySnapshot([]);
        query.QueueResponse(Task.FromResult(withoutRow));
        cut.Find("[data-testid='tenants-audit-refresh']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-correction-confirm']").ShouldBeEmpty();
        if (stale)
        {
            cut.FindAll("[data-testid='tenants-audit-stale']").ShouldHaveSingleItem();
        }
        else
        {
            cut.FindAll("[data-testid='tenants-audit-ready']").ShouldHaveSingleItem();
        }
        tracker.Find("tenant.alpha")!.MessageId.ShouldBe(retained.MessageId);
    }

    [Fact]
    public void FailedSubmittedPreviewRemainsMountedWhenRoleAndStartAreTriedAgain()
    {
        StubTenantQueryGateway query = RegisterServices(ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.SupportsCommandStatusLookup.Returns(true);
        commands.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Failed("Command outcome could not be verified.")));
        Services.AddSingleton(commands);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-handoff']").Click();
        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        CorrectionStartPanel preview = cut.FindComponent<CorrectionStartPanel>().Instance;
        cut.WaitForAssertion(() => preview.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Failed));
        preview.HasSubmitted.ShouldBeTrue();
        int detailReads = query.DetailRequests.Count;
        FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditDataGrid>(), "tenants-correction-role", TenantRole.TenantOwner.ToString());
        cut.Find("[data-testid='tenants-correction-start']").Click();
        cut.FindComponent<CorrectionStartPanel>().Instance.ShouldBeSameAs(preview);
        preview.Snapshot!.IntendedRole.ShouldBe(TenantRole.TenantReader);
        query.DetailRequests.Count.ShouldBe(detailReads);
        _ = commands.Received(1).ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NarrowViewportReturnsFocusedReceiptCorrectionToItsHeadingWithoutAnOpenPanel()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        RegisterServices(viewport, ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        module.Setup<bool>("isFocusInsideAuditReceiptCorrection").SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(1));
        viewport.Observe(ViewportTier.Phone);
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(2));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
        cut.FindAll("[data-testid='tenants-correction-start']").ShouldBeEmpty();
    }

    [Fact]
    public void NarrowViewportIgnoresStaleCorrectionReferenceAfterRoleSelectionClosesPanel()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        RegisterServices(viewport, ReadySnapshot([
            Row("event-correction", AuditEventCategory.Access, "userId: target-user", eventType: "UserRoleChanged")]));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<bool> focus = module.Setup<bool>("focusElementById", _ => true);
        focus.SetResult(true);
        module.Setup<bool>("isFocusInsideAuditReceiptCorrection").SetResult(true);
        JSRuntimeInvocationHandler<bool> launcherFocus = module.Setup<bool>("focusCorrectionLauncher", _ => true);
        launcherFocus.SetResult(true);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-audit-receipt-open']").Click();
        FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditEvidenceReceipt>(),
            "tenants-correction-role", TenantRole.TenantReader.ToString());
        cut.Find("[data-testid='tenants-audit-receipt'] [data-testid='tenants-correction-start']").Click();
        cut.Find("[data-testid='tenants-correction-start-panel']");
        FluentSelectInterop.ChangeFluentSelect(cut.FindComponent<AuditEvidenceReceipt>(),
            "tenants-correction-role", TenantRole.TenantOwner.ToString());
        cut.FindAll("[data-testid='tenants-correction-start-panel']").ShouldBeEmpty();
        int previousFocusCount = focus.Invocations.Count;
        int previousLauncherFocusCount = launcherFocus.Invocations.Count;
        viewport.Observe(ViewportTier.Phone);
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBeGreaterThan(previousFocusCount));
        focus.Invocations.Last().Arguments[0].ShouldBe("tenants-audit-receipt-heading");
        SpinWait.SpinUntil(() => launcherFocus.Invocations.Count > previousLauncherFocusCount, TimeSpan.FromMilliseconds(500));
        launcherFocus.Invocations.Count.ShouldBe(previousLauncherFocusCount);
    }

    private StubTenantQueryGateway RegisterServices(params TenantAuditSnapshot[] snapshots)
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        return RegisterServices(viewport, snapshots);
    }

    private StubTenantQueryGateway RegisterServices(
        TenantHighImpactViewportObservation viewport,
        params TenantAuditSnapshot[] snapshots)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubTenantQueryGateway gateway = new(snapshots);
        Services.AddSingleton(viewport);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        return gateway;
    }

    private StubTenantQueryGateway RegisterGlobalAdminServices(
        bool authorized,
        GlobalAdministratorsSnapshot globalAdministrators,
        params TenantAuditSnapshot[] snapshots)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubTenantQueryGateway gateway = new(snapshots) { GlobalAdministrators = globalAdministrators };
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        Services.AddSingleton(viewport);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition(authorized: authorized));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();
        return gateway;
    }

    private void ConfigureRenderer(RendererInfo rendererInfo)
    {
        SetRendererInfo(rendererInfo);
        _rendererInfoConfigured = true;
    }

    private new IRenderedComponent<TComponent> Render<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>> parameterBuilder)
        where TComponent : IComponent
    {
        if (!_rendererInfoConfigured)
        {
            ConfigureRenderer(new RendererInfo("Server", isInteractive: true));
        }

        return base.Render(parameterBuilder);
    }

    private static GlobalAdministratorsSnapshot GlobalAdmins(params string[] userIds)
        => GlobalAdministratorsSnapshot.Ready(
            userIds.Select(userId => new GlobalAdministratorRow(
                userId,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)).ToArray(),
            nextCursor: null,
            hasMore: false,
            eTag: "\"ga-etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "tenant-sequence:1",
        };

    private static TenantAuditSnapshot GlobalAdminAuditSnapshot(string eventType, string targetUserId)
        => TenantAuditSnapshot.Ready(
            [
                new TenantAuditRow(
                    "event-global-admin",
                    eventType,
                    AuditEventCategory.Access,
                    "actor-user",
                    DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
                    "system",
                    targetUserId,
                    "system",
                    eventType,
                    $"userId: {targetUserId}",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    QueryResponseProvenance.ProjectionBacked,
                    new TenantAuditNarrative(UserId: targetUserId)),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current,
            new TenantAuditRequest("system")) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "tenant-sequence:2",
        };

    /// <summary>
    /// Builds a ready audit snapshot, with the snapshot lifecycle independent of the row lifecycle.
    /// </summary>
    /// <remarks>
    /// Production derives the two separately -- the snapshot lifecycle comes from response metadata, the row
    /// lifecycle from the row -- so deriving the snapshot's from <c>rows[0]</c> made the non-collapse case
    /// (metadata says the projection is not current while the rows still say Current) inexpressible, and no
    /// test in this file rendered a non-Current snapshot lifecycle.
    /// </remarks>
    private static TenantAuditSnapshot ReadySnapshot(
        IReadOnlyList<TenantAuditRow> rows,
        string? nextCursor = null,
        bool hasMore = false,
        string? requestCursor = null,
        ProjectionLifecycleState? lifecycle = null)
        => TenantAuditSnapshot.Ready(
            rows,
            nextCursor,
            hasMore,
            eTag: "\"etag\"",
            freshness: rows.Any(row => row.Freshness == ReadModelFreshnessState.Stale)
                ? ReadModelFreshnessState.Stale
                : ReadModelFreshnessState.Current,
            new TenantAuditRequest("tenant.alpha", Cursor: requestCursor)) with
        {
            Lifecycle = lifecycle
                ?? (rows.Count == 0 ? ProjectionLifecycleState.Current : rows[0].Lifecycle),
        };

    private static TenantDetailSnapshot DetailSnapshot(TenantRole role)
    {
        TenantDetail detail = new(
            "tenant.alpha",
            "Tenant Alpha",
            null,
            TenantStatus.Active,
            [new TenantMember("target-user", role)],
            new Dictionary<string, string>(StringComparer.Ordinal),
            DateTimeOffset.Parse("2026-06-01T09:00:00Z", CultureInfo.InvariantCulture));
        return TenantDetailSnapshot.Ready(detail, "\"detail-etag\"", ReadModelFreshnessState.Current);
    }

    private static TenantAuditSnapshot SnapshotFor(TenantAuditSurfaceKind kind)
    {
        TenantAuditRequest request = new("tenant.alpha", Category: kind is TenantAuditSurfaceKind.FilteredEmpty ? AuditEventCategory.Access : null);
        return kind switch
        {
            TenantAuditSurfaceKind.Loading => TenantAuditSnapshot.Loading("tenant.alpha"),
            TenantAuditSurfaceKind.Empty => TenantAuditSnapshot.Empty(true, ReadModelFreshnessState.Current, "\"etag\"", request),
            TenantAuditSurfaceKind.FilteredEmpty => TenantAuditSnapshot.Empty(true, ReadModelFreshnessState.Current, "\"etag\"", request),
            TenantAuditSurfaceKind.Stale => TenantAuditSnapshot.Stale([Row("event-stale", AuditEventCategory.Access, freshness: ReadModelFreshnessState.Stale)], null, false, "\"etag\"", request),
            TenantAuditSurfaceKind.Degraded => TenantAuditSnapshot.Degraded([Row("event-degraded", AuditEventCategory.Access)], TenantAuditReason.ProjectionDegraded, request),
            TenantAuditSurfaceKind.Unauthorized => TenantAuditSnapshot.Unauthorized(request),
            TenantAuditSurfaceKind.InvalidCursor => TenantAuditSnapshot.InvalidCursor(request),
            TenantAuditSurfaceKind.ListRefreshed => TenantAuditSnapshot.ListRefreshed([Row("event-refreshed", AuditEventCategory.Access)], null, false, "\"etag\"", ReadModelFreshnessState.Current, request),
            TenantAuditSurfaceKind.Unavailable => TenantAuditSnapshot.Unavailable(request),
            TenantAuditSurfaceKind.Error => TenantAuditSnapshot.Error(request),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static TenantAuditRow Row(
        string eventReference,
        AuditEventCategory category,
        string referenceContext = "userId: target-user",
        ReadModelFreshnessState freshness = ReadModelFreshnessState.Current,
        string? eventType = null,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current)
    {
        string outcome = eventType ?? (category is AuditEventCategory.Access ? "UserAddedToTenant" : "TenantConfigurationSet");
        TenantAuditNarrative narrative = new(
            UserId: referenceContext.Contains("userId: target-user", StringComparison.Ordinal) ? "target-user" : null,
            Role: referenceContext.Contains("role: TenantReader", StringComparison.Ordinal) ? TenantRole.TenantReader : null,
            OldRole: referenceContext.Contains("oldRole: TenantReader", StringComparison.Ordinal) ? TenantRole.TenantReader : null,
            PreviousRole: referenceContext.Contains("previousRole: TenantReader", StringComparison.Ordinal) ? TenantRole.TenantReader : null);

        return new(
            eventReference,
            outcome,
            category,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "tenant.alpha",
            "target-user",
            "tenant.alpha",
            outcome,
            referenceContext,
            freshness,
            lifecycle,
            QueryResponseProvenance.ProjectionBacked,
            narrative);
    }

    private static HashSet<string> ResourceKeys(string path)
        => XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class DelayedFocusJsRuntime : IJSRuntime, IJSObjectReference
    {
        public TaskCompletionSource ImportRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondImportRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstImportRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondImportRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstImportReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LauncherFocused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> FocusTargets { get; } = new();

        private int _importCount;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => identifier switch
            {
                "import" => new ValueTask<TValue>(ImportAsync<TValue>()),
                "focusElementById" => new ValueTask<TValue>((TValue)(object)RecordFocus(args)),
                _ => new ValueTask<TValue>(default(TValue)!),
            };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<TValue> ImportAsync<TValue>()
        {
            int importNumber = Interlocked.Increment(ref _importCount);
            if (importNumber == 1)
            {
                ImportRequested.TrySetResult();
                await FirstImportRelease.Task.ConfigureAwait(false);
                FirstImportReturned.TrySetResult();
            }
            else
            {
                SecondImportRequested.TrySetResult();
                await SecondImportRelease.Task.ConfigureAwait(false);
            }

            return (TValue)(object)this;
        }

        private bool RecordFocus(object?[]? args)
        {
            string target = (string)args!.Single()!;
            FocusTargets.Enqueue(target);
            if (target == "tenants-audit-receipt-launcher-0")
            {
                LauncherFocused.TrySetResult();
            }

            return true;
        }
    }

    private sealed class StubTenantQueryGateway(params TenantAuditSnapshot[] snapshots) : ITenantQueryGateway
    {
        /// <summary>
        /// Explicit because <c>ITenantQueryGateway.GetTenantUsersAsync</c> is no longer a default interface
        /// method. These stubs previously inherited a silent <c>Unavailable</c> fallback, so a member-read
        /// regression would have rendered as an outage here rather than failing the build.
        /// </summary>
        public Task<TenantUsersSnapshot> GetTenantUsersAsync(
            TenantUsersRequest request,
            TenantUsersSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(TenantUsersSnapshot.Unavailable(request.TenantId));
        }

        private readonly Queue<TenantAuditSnapshot> _snapshots = new(snapshots);
        private readonly Queue<Task<TenantAuditSnapshot>> _queuedResponses = [];
        private readonly Queue<Task<TenantDetailSnapshot>> _queuedDetailResponses = [];

        public GlobalAdministratorAuditProof? GlobalProof { get; set; }

        public Func<string, GlobalAdministratorAuditProof?>? GlobalProofProvider { get; set; }

        public Func<Task<GlobalAdministratorAuditProof?>>? AsyncGlobalProofProvider { get; set; }

        public Func<TenantAuditRequest, TenantAuditSnapshot>? ProofAuditProvider { get; set; }

        public Task<(GlobalAdministratorAuditProof? Proof, bool Complete)> FindGlobalAdministratorCorrectionProofAsync(
            string correlationId,
            long committedEventSequence,
            string targetUserId,
            string correctiveEventType,
            DateTimeOffset originalTimestamp,
            DateTimeOffset submittedAtUtc,
            CancellationToken cancellationToken = default)
            => GlobalAdministratorAuditProofLookup.FindAsync(
                async (request, _) =>
                {
                    GlobalAdministratorAuditProof? proof = AsyncGlobalProofProvider is null
                        ? GlobalProofProvider?.Invoke(correlationId) ?? GlobalProof
                        : await AsyncGlobalProofProvider().ConfigureAwait(false);
                    TenantAuditRow[] rows = proof is { } available && available.Row.Timestamp >= request.From
                        ? [available.Row]
                        : [];
                    return TenantAuditSnapshot.Ready(
                        rows, null, false, "\"proof-etag\"", ReadModelFreshnessState.Current, request) with
                    {
                        Lifecycle = ProjectionLifecycleState.Current,
                        ProjectionVersion = "tenant-sequence:2",
                    };
                },
                correlationId,
                committedEventSequence,
                targetUserId,
                correctiveEventType,
                originalTimestamp,
                submittedAtUtc,
                cancellationToken);

        public List<TenantAuditRequest> Requests { get; } = [];
        public List<TenantDetailRequest> DetailRequests { get; } = [];

        public void QueueResponse(Task<TenantAuditSnapshot> response)
            => _queuedResponses.Enqueue(response);

        public void QueueDetailResponse(Task<TenantDetailSnapshot> response)
            => _queuedDetailResponses.Enqueue(response);

        public bool CorrectionSupport { get; set; } = true;

        public bool SupportsTenantCorrectionStart => CorrectionSupport;

        public bool CorrectionAuthorized { get; set; } = true;

        public async Task<IReadOnlyList<TenantCorrectionProjection>> GetTenantCorrectionProjectionsAsync(
            string tenantId, IReadOnlyList<string> targetUserIds, CancellationToken cancellationToken = default)
        {
            if (targetUserIds.Count == 0) return [];
            TenantDetailSnapshot snapshot = await GetTenantAsync(new TenantDetailRequest(tenantId), null, cancellationToken);
            return targetUserIds.Select(targetUserId => !CorrectionAuthorized
                ? TenantCorrectionProjection.Unavailable(tenantId, targetUserId)
                : new TenantCorrectionProjection(tenantId, targetUserId,
                snapshot.Detail?.Status ?? TenantStatus.Unknown,
                snapshot.Detail?.Members.FirstOrDefault(member => member.UserId == targetUserId)?.Role,
                snapshot.Detail?.Members.Count == 0, CorrectionAuthorized, false,
                snapshot.Kind is TenantDetailSurfaceKind.Ready, snapshot.Freshness,
                snapshot.Kind is TenantDetailSurfaceKind.Ready ? ProjectionLifecycleState.Current : ProjectionLifecycleState.Unknown,
                QueryResponseProvenance.ProjectionBacked)
                { ProjectionVersion = snapshot.ProjectionVersion ?? "tenant-sequence:1",
                    OwnerCount = snapshot.Detail?.Members.Count(member => member.Role is TenantRole.TenantOwner) }).ToArray();
        }

        public async Task<TenantCorrectionProjection> GetTenantCorrectionProjectionAsync(
            string tenantId, string targetUserId, CancellationToken cancellationToken = default)
        {
            TenantDetailSnapshot snapshot = await GetTenantAsync(new TenantDetailRequest(tenantId), null, cancellationToken);
            if (!CorrectionAuthorized)
            {
                // The BFF withholds membership, lifecycle and role from an unauthorized principal.
                return TenantCorrectionProjection.Unavailable(tenantId, targetUserId);
            }

            return new(tenantId, targetUserId, snapshot.Detail?.Status ?? TenantStatus.Unknown,
                snapshot.Detail?.Members.FirstOrDefault(member => member.UserId == targetUserId)?.Role,
                snapshot.Detail?.Members.Count == 0, CorrectionAuthorized, false,
                snapshot.Kind is TenantDetailSurfaceKind.Ready, snapshot.Freshness,
                snapshot.Kind is TenantDetailSurfaceKind.Ready ? ProjectionLifecycleState.Current : ProjectionLifecycleState.Unknown,
                QueryResponseProvenance.ProjectionBacked)
                { ProjectionVersion = snapshot.ProjectionVersion ?? "tenant-sequence:1",
                    OwnerCount = snapshot.Detail?.Members.Count(member => member.Role is TenantRole.TenantOwner) };
        }

        public Task<TenantDetailSnapshot> GetTenantAsync(
            TenantDetailRequest request,
            TenantDetailSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(request);
            if (_queuedDetailResponses.Count > 0)
            {
                return _queuedDetailResponses.Dequeue();
            }

            TenantDetail detail = new(
                request.TenantId,
                "Tenant Alpha",
                null,
                TenantStatus.Active,
                [new TenantMember("target-user", TenantRole.TenantContributor)],
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.Parse("2026-06-01T09:00:00Z", CultureInfo.InvariantCulture));
            return Task.FromResult(TenantDetailSnapshot.Ready(detail, "\"detail-etag\"", ReadModelFreshnessState.Current));
        }

        public Task<TenantListSnapshot> ListTenantsAsync(
            TenantListRequest request,
            TenantListSnapshot? previous,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserTenantMembershipSnapshot> GetMyTenantsAsync(
            UserTenantMembershipRequest request,
            UserTenantMembershipSnapshot? previous,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserTenantMembershipSnapshot> GetUserTenantsAsync(
            UserTenantMembershipRequest request,
            UserTenantMembershipSnapshot? previous,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public GlobalAdministratorsSnapshot GlobalAdministrators { get; init; }
            = GlobalAdministratorsSnapshot.Empty(false, ReadModelFreshnessState.Current, "\"ga-etag\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };

        public Func<GlobalAdministratorsRequest, GlobalAdministratorsSnapshot>? GlobalAdminProvider { get; set; }

        public List<GlobalAdministratorsRequest> GlobalAdminRequests { get; } = [];

        public Exception? GlobalAdminFault { get; set; }

        public Task<GlobalAdministratorsSnapshot> GetGlobalAdministratorsAsync(
            GlobalAdministratorsRequest request,
            GlobalAdministratorsSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            GlobalAdminRequests.Add(request);
            return GlobalAdminFault is not null
                ? throw GlobalAdminFault
                : Task.FromResult((GlobalAdminProvider?.Invoke(request) ?? GlobalAdministrators) with
                {
                    RequestCursor = request.Cursor,
                    RequestPageSize = request.PageSize,
                });
        }

        public Task<TenantAuditSnapshot> GetTenantAuditAsync(
            TenantAuditRequest request,
            TenantAuditSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (ProofAuditProvider is not null && request.From is not null)
            {
                return Task.FromResult(ProofAuditProvider(request));
            }
            return _queuedResponses.Count > 0
                ? _queuedResponses.Dequeue()
                : Task.FromResult(_snapshots.Dequeue());
        }
    }

    private sealed class StubBffComposition(
        bool readConnected = true,
        bool commandConnected = true,
        bool authorized = false,
        string? permissionHref = "/support/audit-access",
        string? escalationHref = "/support/audit-incident") : ITenantsBffComposition
    {
        public Exception? GlobalAuthorizationFault { get; init; }
        public Task<TenantLifecycleAuthorizationReflectionState>? GlobalAuthorizationTask { get; set; }
        public int GlobalAuthorizationCalls { get; private set; }
        public bool Authorized { get; set; } = authorized;
        public bool IsReadSurfaceConnected => readConnected;

        public bool IsCommandSurfaceConnected => commandConnected;

        public string? AuditPermissionRecoveryHref => permissionHref;

        public string? AuditEscalationRecoveryHref => escalationHref;

        public bool IsGlobalAdministratorDispatchConnected => commandConnected;

        public bool IsGlobalAdministratorStatusConnected => commandConnected;

        public bool IsGlobalAdministratorRequeryConnected => readConnected;

        public bool IsGlobalAdministratorGrantPreviewReady => true;

        public bool IsGlobalAdministratorRemovePreviewReady => true;

        public ValueTask<GlobalAdministratorGrantPreview> ComposeGlobalAdministratorGrantPreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(GlobalAdministratorGrantPreview.Create(targetUserId, completeSnapshot, Authorized));

        public ValueTask<GlobalAdministratorRemovePreview> ComposeGlobalAdministratorRemovePreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(GlobalAdministratorRemovePreview.Create(targetUserId, "operator", completeSnapshot, Authorized));

        public TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
            => Authorized
                ? TenantLifecycleAuthorizationReflectionState.Authorized
                : TenantLifecycleAuthorizationReflectionState.Indeterminate;

        public ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
            CancellationToken cancellationToken = default)
        {
            GlobalAuthorizationCalls++;
            return GlobalAuthorizationTask is { } pending
                ? new ValueTask<TenantLifecycleAuthorizationReflectionState>(pending)
                : GlobalAuthorizationFault is null
                    ? ValueTask.FromResult(GlobalAdministratorsAuthorizationReflection)
                    : ValueTask.FromException<TenantLifecycleAuthorizationReflectionState>(GlobalAuthorizationFault);
        }
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsGlobalAdministratorDispatch => true;

        public bool SupportsTrackedGlobalAdministratorDispatch { get; set; } = true;

        public bool SupportsTrackedGlobalAdministratorRemoveDispatch { get; set; } = true;

        public bool SupportsCommandStatusLookup => true;

        public List<string> TrackedMessageIds { get; } = [];

        public List<TenantCommandTrackingHandle> StatusHandles { get; } = [];

        public TenantCommandStatusResult Status { get; set; }
            = new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true);

        public Task<TenantCommandSubmissionResult>? SubmissionTask { get; set; }

        public Task<TenantCommandSubmissionResult> SetGlobalAdministratorTrackedAsync(
            SetGlobalAdministrator request, string messageId, CancellationToken cancellationToken = default)
        {
            return TrackedSubmissionAsync(messageId);
        }

        public Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorTrackedAsync(
            RemoveGlobalAdministrator request, string messageId, CancellationToken cancellationToken = default)
        {
            return TrackedSubmissionAsync(messageId);
        }

        private Task<TenantCommandSubmissionResult> TrackedSubmissionAsync(string messageId)
        {
            TrackedMessageIds.Add(messageId);
            return SubmissionTask ?? Task.FromResult(TenantCommandSubmissionResult.Accepted(messageId, "tracking-safe"));
        }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            StatusHandles.Add(handle);
            return Task.FromResult(Status);
        }
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "test")));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);

        public void ChangePrincipal(Task<AuthenticationState> nextState)
        {
            if (nextState.IsCompletedSuccessfully)
            {
                _state = nextState.Result;
            }

            NotifyAuthenticationStateChanged(nextState);
        }
    }

    /// <summary>
    /// Resolution order: explicit override, then the real <c>TenantsResources.resx</c>, then throw.
    /// </summary>
    /// <remarks>
    /// Echoing an unknown key back as <c>name</c> made a missing resource indistinguishable from a present
    /// one: the component rendered the literal key as user-visible copy and any substring assertion over it
    /// passed whether or not the string existed. Falling through to the real resource lets a test assert
    /// shipped copy without hand-copying it here; a key defined in neither is a defect, not a silent echo.
    /// Same rule as the sibling stub in <c>TenantDetailSurfaceTests</c>.
    /// </remarks>
    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly ResourceManager RealResources = new(
            "Hexalith.Tenants.UI.Resources.TenantsResources",
            typeof(TenantsResources).Assembly);

        public LocalizedString this[string name] => new(name, Resolve(name));

        public LocalizedString this[string name, params object[] arguments]
            // No arguments means no substitution. Formatting unconditionally threw FormatException the
            // moment Resolve started falling through to a real .resx string containing `{0}` -- the stub
            // used to echo the placeholder-free key back, so the path could not be reached before.
            => new(name, arguments.Length == 0
                ? Resolve(name)
                : string.Format(CultureInfo.CurrentCulture, Resolve(name), arguments));

        // CurrentUICulture, not InvariantCulture. Pinning the invariant culture made this stub answer in
        // English no matter what culture a test rendered under, so a component that had hard-coded English
        // copy was indistinguishable from one that reads the localizer -- and no test could prove the French
        // resources are ever reached.
        private static string Resolve(string name)
            => Values.TryGetValue(name, out string? value)
                ? value
                : RealResources.GetString(name, CultureInfo.CurrentUICulture)
                    ?? throw new KeyNotFoundException(
                        $"Resource key '{name}' is defined neither in this stub nor in TenantsResources.resx. "
                        + "The stub must not echo an undefined key back as user-visible copy.");

        /// <summary>
        /// Enumerates everything <see cref="Resolve"/> can return, not just the overrides. Returning
        /// <c>Values</c> alone made enumeration and lookup disagree: a key resolvable through the real
        /// resource set was absent from the enumeration, so any caller reasoning about "the available
        /// strings" saw a set the indexer did not agree with. Overrides win, matching resolution order.
        /// </summary>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            Dictionary<string, string> all = new(StringComparer.Ordinal);
            // NOT disposed: GetResourceSet returns the ResourceManager's own cached set, so disposing it
            // corrupts every subsequent lookup for the whole process -- Resolve then throws its
            // KeyNotFoundException for keys that do exist, and every bUnit WaitForAssertion downstream burns
            // its full timeout instead of failing.
            ResourceSet? real = RealResources.GetResourceSet(
                CultureInfo.CurrentUICulture,
                createIfNotExists: true,
                tryParents: includeParentCultures);
            if (real is not null)
            {
                foreach (DictionaryEntry entry in real)
                {
                    if (entry.Key is string key && entry.Value is string text)
                    {
                        all[key] = text;
                    }
                }
            }

            foreach (KeyValuePair<string, string> over in Values)
            {
                all[over.Key] = over.Value;
            }

            return all.Select(entry => new LocalizedString(entry.Key, entry.Value));
        }

        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.Audit.Back"] = "Back to tenant details",
            ["Tenants.Audit.Category.Access"] = "Access",
            ["Tenants.Audit.Category.Administrative"] = "Administrative",
            ["Tenants.Audit.Column.Actor"] = "Actor",
            ["Tenants.Audit.Column.Category"] = "Category",
            ["Tenants.Audit.Column.Correction"] = "Correction",
            ["Tenants.Audit.Column.Freshness"] = "Freshness",
            ["Tenants.Audit.Column.Outcome"] = "Outcome",
            ["Tenants.Audit.Column.Reference"] = "Reference context",
            ["Tenants.Audit.Column.Receipt"] = "Receipt",
            ["Tenants.Audit.Column.Scope"] = "Tenant scope",
            ["Tenants.Audit.Column.Target"] = "Target",
            ["Tenants.Audit.Column.Timestamp"] = "Timestamp",
            ["Tenants.Audit.ControlsLabel"] = "Tenant audit filters and paging controls",
            ["Tenants.Audit.Copy.EventReference"] = "Copy audit event reference {0}",
            ["Tenants.Audit.Reference.Unavailable"] = "Reference unavailable",
            ["Tenants.Audit.Description"] = "Read-only tenant audit evidence from the server-side query gateway.",
            ["Tenants.Audit.Eyebrow"] = "Tenant audit trail",
            ["Tenants.Audit.Filter.Category"] = "Category",
            ["Tenants.Audit.Filter.Category.All"] = "All categories",
            ["Tenants.Audit.Filter.From"] = "From",
            ["Tenants.Audit.Filter.To"] = "To",
            ["Tenants.Audit.Freshness.Current"] = "Current",
            ["Tenants.Audit.Freshness.Stale"] = "Stale",
            ["Tenants.Audit.Freshness.Unknown"] = "Unknown",
            ["Tenants.Audit.GridTitle"] = "Audit entries",
            ["Tenants.Audit.Next"] = "Next",
            ["Tenants.Audit.PaginationLabel"] = "Tenant audit pages",
            ["Tenants.Audit.Previous"] = "Previous",
            ["Tenants.Audit.Refresh"] = "Refresh",
            ["Tenants.Audit.Reset"] = "Reset filters",
            ["Tenants.Audit.Receipt.ActionsLabel"] = "Audit receipt recovery actions",
            ["Tenants.Audit.Receipt.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Audit.Receipt.Action.Escalate"] = "Escalate with reference",
            ["Tenants.Audit.Receipt.Action.InspectAudit"] = "Inspect audit",
            ["Tenants.Audit.Receipt.Action.Refresh"] = "Refresh",
            ["Tenants.Audit.Receipt.Action.Retry"] = "Retry",
            ["Tenants.Audit.Receipt.Action.Wait"] = "Wait for audit evidence",
            ["Tenants.Audit.Receipt.Copy"] = "Copy full audit receipt summary",
            ["Tenants.Audit.Receipt.Field.Actor"] = "Actor",
            ["Tenants.Audit.Receipt.Field.CommandReference"] = "Command reference",
            ["Tenants.Audit.Receipt.Field.Outcome"] = "Outcome",
            ["Tenants.Audit.Receipt.Field.ProjectionMarker"] = "Projection marker",
            ["Tenants.Audit.Receipt.Field.Reference"] = "Audit reference",
            ["Tenants.Audit.Receipt.Field.Scope"] = "Tenant scope",
            ["Tenants.Audit.Receipt.Field.Target"] = "Target",
            ["Tenants.Audit.Receipt.Field.Timestamp"] = "Timestamp",
            ["Tenants.Audit.Receipt.Open"] = "View receipt",
            ["Tenants.Audit.Receipt.State.Degraded"] = "Audit evidence is degraded. Use the reference only with this limitation.",
            ["Tenants.Audit.Receipt.State.Delayed"] = "Audit evidence is delayed. Inspect audit or retry before citing proof.",
            ["Tenants.Audit.Receipt.State.InvalidReference"] = "The requested receipt reference is not loaded in the current tenant-scoped audit result.",
            ["Tenants.Audit.Receipt.State.MissingSupport"] = "Audit evidence support is missing. Escalate with the support-safe reference.",
            ["Tenants.Audit.Receipt.State.Partial"] = "Audit evidence is partial. The receipt cannot cite a complete proof.",
            ["Tenants.Audit.Receipt.State.Pending"] = "Audit evidence is pending. Wait or refresh before citing proof.",
            ["Tenants.Audit.Receipt.State.Ready"] = "Audit evidence is ready to cite.",
            ["Tenants.Audit.Receipt.State.Stale"] = "Audit evidence is stale. Refresh before treating it as current.",
            ["Tenants.Audit.Receipt.State.Unauthorized"] = "Audit evidence is not available for the current authorization scope. The requested event could not be verified.",
            ["Tenants.Audit.Receipt.State.Unavailable"] = "Audit evidence is unavailable. Continue read-only or retry later.",
            ["Tenants.Audit.Receipt.Title"] = "Audit evidence receipt",
            ["Tenants.Audit.Availability.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Audit.Availability.Action.InspectAudit"] = "Inspect audit",
            ["Tenants.Audit.Availability.Action.Refresh"] = "Retry status lookup",
            ["Tenants.Audit.Availability.ActionsLabel"] = "Audit availability recovery actions",
            ["Tenants.Audit.Availability.Reason.MissingSupport"] = "In-panel audit verification is not available for this command, so this panel cannot match an audit record to the attempt. The recorded outcome above is unchanged.",
            ["Tenants.Audit.Availability.Reason.Unavailable"] = "The audit status could not be read or verified after the command was sent. This does not mean the record does not exist, and no proof is claimed.",
            ["Tenants.Audit.Availability.State.Delayed"] = "Audit delayed",
            ["Tenants.Audit.Availability.State.MissingSupport"] = "Missing implementation support",
            ["Tenants.Audit.Availability.State.Pending"] = "Audit pending",
            ["Tenants.Audit.Availability.State.Unavailable"] = "Audit unavailable",
            ["Tenants.Audit.State.Degraded.Message"] = "Audit evidence is degraded. Last confirmed support-safe rows remain visible for this exact tenant and filter scope only.",
            ["Tenants.Audit.State.Degraded.Title"] = "Audit data degraded",
            ["Tenants.Audit.State.Empty.Message"] = "No audit entries are visible for this tenant scope.",
            ["Tenants.Audit.State.Empty.Title"] = "No audit entries",
            ["Tenants.Audit.State.Error.Message"] = "Audit data could not be loaded. No raw event payloads or internal gateway details are shown.",
            ["Tenants.Audit.State.Error.Title"] = "Audit data unavailable",
            ["Tenants.Audit.State.FilteredEmpty.Message"] = "No audit entries match the selected date range and category filters.",
            ["Tenants.Audit.State.FilteredEmpty.Title"] = "No audit entries match filters",
            ["Tenants.Audit.State.InvalidCursor.Message"] = "The audit cursor is no longer valid. Use refresh to request the first page again.",
            ["Tenants.Audit.State.InvalidCursor.Title"] = "Audit page cursor invalid",
            ["Tenants.Audit.State.ListRefreshed.Message"] = "The audit cursor changed, so the list was refreshed from page one for this tenant and filter scope.",
            ["Tenants.Audit.State.ListRefreshed.Title"] = "Audit list refreshed",
            ["Tenants.Audit.State.Loading.Message"] = "Audit entries are loading through the server-side query gateway.",
            ["Tenants.Audit.State.Loading.Title"] = "Loading audit entries",
            ["Tenants.Audit.State.Ready.Message"] = "Audit entries are loaded with support-safe row fields only.",
            ["Tenants.Audit.State.Ready.Title"] = "Audit entries loaded",
            ["Tenants.Audit.State.Stale.Message"] = "Audit freshness is stale. Refresh to check the projection again before treating the list as current.",
            ["Tenants.Audit.State.Stale.Title"] = "Audit data stale",
            ["Tenants.Audit.State.Unauthorized.Message"] = "You are not authorized to view tenant audit entries. No hidden audit data is shown.",
            ["Tenants.Audit.State.Unauthorized.Title"] = "Audit access unavailable",
            ["Tenants.Audit.State.Unavailable.Message"] = "The tenant audit read surface is unavailable. No audit payloads are shown.",
            ["Tenants.Audit.State.Unavailable.Title"] = "Audit read surface unavailable",
            ["Tenants.Audit.Title"] = "Audit trail for {0}",
            ["Tenants.Audit.UnknownTenant"] = "this tenant",
            ["Tenants.Correction.Action.RestoreAccess"] = "restore intended access",
            ["Tenants.Correction.Action.RestoreAccessAccessible"] = "restore intended access for audit evidence {0}",
            ["Tenants.Correction.Action.Start"] = "start correction",
            ["Tenants.Correction.Action.StartAccessible"] = "start correction for audit evidence {0}",
            ["Tenants.Correction.Unavailable.ExplicitRoleRequired"] = "Choose the intended role before starting correction.",
            ["Tenants.Correction.Unavailable.AuthorizationIndeterminate"] = "Current access could not be verified. Refresh or request permission before starting correction.",
            ["Tenants.Correction.Unavailable.CommandSupportUnavailable"] = "The tenant correction command path is not connected.",
            ["Tenants.Correction.Unavailable.GlobalAdministratorCommandSupportUnavailable"] = "Global administrator correction commands are not connected.",
            ["Tenants.Correction.Domain.GlobalAdministrators"] = "Global administrators",
            ["Tenants.Correction.Command.SetGlobalAdministrator"] = "Set global administrator",
            ["Tenants.Copy.Action"] = "Copy",
            ["Tenants.Copy.Feedback.Copied"] = "Copied.",
            ["Tenants.Audit.Availability.State.Available"] = "Audit available",
            ["Tenants.Audit.Availability.Reason.Pending"] = "The command's events are stored, but its audit record is not readable yet. It normally appears shortly, and no proof is claimed until it does.",
            ["Tenants.Audit.Availability.Reason.Delayed"] = "The audit record is taking longer than expected to become readable. No proof is claimed until it can be read.",
            ["Tenants.Audit.Availability.RetryLimit"] = "Repeated retries left this state unchanged, so retrying is no longer offered here.",
            ["Tenants.Audit.Receipt.Availability.Unavailable.Reason"] = "This audit read could not verify the requested evidence. This does not mean the record does not exist, and the recorded outcome is unchanged.",
            ["Tenants.Audit.Recovery.Action.Escalate"] = "Escalate without diagnostics",
        };
    }

    [Fact]
    public void Metadata_degraded_page_describing_the_prior_cursor_is_not_committed_as_the_requested_page()
    {
        // The reject side of the degraded-page cursor rule. The existing coverage builds its degraded
        // snapshot with a RequestCursor that MATCHES the request, so only the accept path ran and dropping
        // the equality check kept the suite green. A degraded snapshot retaining the PRIOR page's rows and
        // cursor must not advance _currentCursor to a page that never rendered.
        TenantAuditSnapshot degradedPriorPage = TenantAuditSnapshot.Degraded(
            [Row("event-1", AuditEventCategory.Access)],
            TenantAuditReason.ProjectionDegraded,
            new TenantAuditRequest("tenant.alpha", Cursor: null));
        StubTenantQueryGateway gateway = RegisterServices(
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true),
            degradedPriorPage,
            ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: "opaque-next", hasMore: true));
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(2));

        // Paging state must not have advanced, so Previous stays unavailable: page one is still current.
        // Find, not FindAll(...).All(...): the Previous button renders only inside the rows branch, so a
        // regression that stops rendering rows satisfies an All() over an empty match set unconditionally.
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeTrue());
    }

    [Fact]
    public void A_query_string_only_navigation_does_not_discard_the_retained_validator_or_blank_the_grid()
    {
        // The audit route carries six query-string parameters, none of which feed the request. The member
        // "open audit for this user" entry point therefore re-enters OnParametersSetAsync with an unchanged
        // TenantId and an identical request; re-reading unconditionally discarded the conditional-read
        // validator, reset the grid to Loading and cancelled any in-flight load.
        StubTenantQueryGateway gateway = RegisterServices(
            [.. Enumerable.Repeat(
                ReadySnapshot([Row("event-1", AuditEventCategory.Access)], nextCursor: null, hasMore: false),
                6)]);
        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");
        gateway.Requests.Count.ShouldBe(1);
        gateway.Requests[0].ETag.ShouldBeNull();

        // Query-string parameters must be supplied through navigation, exactly as the member audit entry
        // point does it: same route, same tenant, new query string.
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha/audit?targetUserId=user.alpha&returnFocus=tenants-member-row");
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.alpha"));

        gateway.Requests.Count.ShouldBe(1);

        // Context-only route changes reuse the already authorized result without a redundant read.
        cut.Find("[data-testid='tenants-audit-grid']").TextContent.ShouldContain("event-1");
    }

    [Fact]
    public async Task A_next_page_completing_after_the_route_moved_does_not_commit_its_cursor_onto_the_new_tenant()
    {
        // NextPageAsync's tenant-identity clause guards a LATE completion, so the alpha page read is held
        // pending across the route change. Without the clause, alpha's cursor and history entry are
        // committed onto tenant beta's surface and beta's Previous walks back through alpha's paging.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var pendingAlphaPage2 = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<TenantAuditRequest> requests = [];
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.Arg<TenantAuditRequest>()!;
                requests.Add(request);
                return request.Cursor == "alpha-next"
                    ? pendingAlphaPage2.Task
                    : Task.FromResult(ReadySnapshot(
                        [Row($"{request.TenantId}-event-1", AuditEventCategory.Access)],
                        nextCursor: "alpha-next",
                        hasMore: true));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        Task nextClick = cut.Find("[data-testid='tenants-audit-next']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => requests.Count.ShouldBe(2));

        // Route to beta while alpha's page-two read is still in flight, then let it complete.
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-grid']")
            .TextContent.ShouldContain("tenant.beta-event-1"));

        pendingAlphaPage2.SetResult(ReadySnapshot(
            [Row("alpha-event-2", AuditEventCategory.Access)],
            nextCursor: null,
            hasMore: false));
        await nextClick;

        // Beta must still be on its own first page: no inherited cursor, no inherited history. Find, not
        // FindAll(...).All(...), for the same reason as above: the throwing form is what makes "the pager is
        // rendered and disabled" distinguishable from "the pager is gone".
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
            .HasAttribute("disabled").ShouldBeTrue());
        cut.Find("[data-testid='tenants-audit-grid']").TextContent.ShouldNotContain("alpha-event-2");
    }

    /// <summary>
    /// A re-entrant route set, arriving while the old lease dispose is suspended, must not reuse the previous
    /// tenant's conditional validator or cursor.
    /// </summary>
    /// <remarks>
    /// <c>_loadedTenantId</c> was assigned before the awaited lease dispose and the marshalled clear, so a
    /// second <c>OnParametersSetAsync</c> arriving while that remote unsubscribe was suspended computed
    /// <c>tenantChanged == false</c>. The read then built its request from the *previous* tenant's
    /// <c>_snapshot.ETag</c> and <c>_currentCursor</c>, which the <c>reuseETag</c>/<c>retainConfirmed</c>
    /// change made reachable -- before it, both arguments were unconditionally false on this path.
    /// </remarks>
    [Fact]
    public async Task A_re_entrant_route_set_during_lease_disposal_never_reuses_the_previous_tenants_validator()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        List<TenantAuditRequest> requests = [];
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.Arg<TenantAuditRequest>()!;
                requests.Add(request);
                return Task.FromResult(TenantAuditSnapshot.Ready(
                    [Row($"{request.TenantId}-event", AuditEventCategory.Access)],
                    nextCursor: $"{request.TenantId}-page-2",
                    hasMore: true,
                    eTag: $"\"{request.TenantId}-etag\"",
                    freshness: ReadModelFreshnessState.Current,
                    request));
            });

        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var suspendedUnsubscribe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        subscription
            .UnsubscribeAsync(GetTenantAuditQuery.ProjectionType, "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(suspendedUnsubscribe.Task);
        Services.AddSingleton(gateway);
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        // Page alpha forward so it holds both a cursor and a page-two validator.
        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => requests.Count.ShouldBe(2));
        await subscription.Received(1).SubscribeAsync(
            GetTenantAuditQuery.ProjectionType,
            "tenant.alpha",
            Arg.Any<CancellationToken>());

        // Route to beta. The unsubscribe never completes, so the tenant-change block stays suspended...
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));

        // ...and a second parameter set for the same route re-enters while it is still suspended.
        cut.Render(parameters => parameters.Add(p => p.TenantId, "tenant.beta"));

        cut.WaitForAssertion(() => requests.Count(static request => request.TenantId == "tenant.beta")
            .ShouldBeGreaterThanOrEqualTo(1));
        foreach (TenantAuditRequest betaRequest in requests.Where(static request => request.TenantId == "tenant.beta"))
        {
            betaRequest.ETag.ShouldBeNull();
            betaRequest.Cursor.ShouldBeNull();
        }

        suspendedUnsubscribe.SetResult();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-grid']")
            .TextContent.ShouldContain("tenant.beta-event"));
        requests.Where(static request => request.TenantId == "tenant.beta")
            .ShouldAllBe(request => request.ETag == null && request.Cursor == null);
    }

    /// <summary>
    /// The snapshot lifecycle and the row lifecycle are independent and must not collapse into each other.
    /// </summary>
    /// <remarks>
    /// Production derives the surface lifecycle from response metadata and each row's from the row itself,
    /// so a projection that metadata reports as not current while the retained rows still carry Current is a
    /// real, renderable state. No test in this file could express it: the row factory hardcoded
    /// <c>Current</c> and the snapshot factory derived its lifecycle from <c>rows[0]</c>.
    /// </remarks>
    [Fact]
    public void Audit_surface_lifecycle_is_rendered_independently_of_the_row_lifecycle()
    {
        // Freshness is derived FROM lifecycle by TenantQueryGateway.ResolveFreshness (Current => Current,
        // Stale => Stale, everything else => Unknown), so the helper's default Current freshness paired with
        // a Rebuilding lifecycle is a snapshot the gateway cannot emit. Pinning badge independence over an
        // unproducible input proves nothing about any state a user can reach -- and a change that gated the
        // surface badge on freshness, which is what this guards, would still have passed.
        RegisterServices(ReadySnapshot(
            [Row("event-1", AuditEventCategory.Access, lifecycle: ProjectionLifecycleState.Current)],
            lifecycle: ProjectionLifecycleState.Rebuilding) with
        {
            Freshness = ReadModelFreshnessState.Unknown,
        });

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        cut.Find("[data-testid='tenants-audit-projection-lifecycle-status']")
            .TextContent.ShouldContain("Rebuilding", Case.Insensitive);
        cut.Find("[data-testid='tenants-audit-row-projection-lifecycle']")
            .TextContent.ShouldContain("Current", Case.Insensitive);
    }

    /// <summary>
    /// A Previous click that lands on page one because the history was trimmed must say so.
    /// </summary>
    /// <remarks>
    /// <c>CursorHistory.Trim</c> re-appends the first-page sentinel beneath the newest entries, which is what
    /// keeps page one reachable -- but it also means one Previous click walks the operator from the middle of
    /// the sequence straight to page one. Rendered as an ordinary one-page step back, the surface silently
    /// misstates where they are. This also pins adoption of the trim at this call site: deleting the
    /// <c>CursorHistory.Trim(...)</c> call previously survived the suite.
    /// </remarks>
    [Fact]
    public void A_previous_click_that_jumps_to_page_one_through_a_trimmed_history_is_announced()
    {
        // Bound taken from the production constant. Duplicating it as a literal meant a change to
        // CursorHistory.DefaultMaximum made this walk the wrong number of steps and fail with
        // "previous is not disabled" -- a diagnosis that names nothing.
        const int bound = CursorHistory.DefaultMaximum;
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.Arg<TenantAuditRequest>()!;
                int page = request.Cursor is null
                    ? 0
                    : int.Parse(request.Cursor["page-".Length..], CultureInfo.InvariantCulture);
                return Task.FromResult(ReadySnapshot(
                    [Row($"event-page-{page}", AuditEventCategory.Access)],
                    nextCursor: $"page-{page + 1}",
                    hasMore: true,
                    requestCursor: request.Cursor));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubBffComposition());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantAuditPage> cut = Render<TenantAuditPage>(parameters => parameters
            .Add(p => p.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-grid']");

        // One page past the bound, so the trim runs and drops the oldest non-sentinel entries.
        for (int page = 1; page <= bound + 1; page++)
        {
            cut.Find("[data-testid='tenants-audit-next']").Click();
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-grid']")
                .TextContent.ShouldContain($"event-page-{page}"));
        }

        cut.FindAll("[data-testid='tenants-audit-history-truncated']").ShouldBeEmpty(
            "Paging forward is not a jump; the notice belongs to the Previous click that lands on page one.");

        // Walk back. The retained history is 49 entries plus the re-appended sentinel, so the last of these
        // pops the sentinel and lands on page one from the middle of the sequence.
        for (int step = 1; step < bound; step++)
        {
            cut.Find("[data-testid='tenants-audit-previous']").Click();
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-audit-previous']")
                .HasAttribute("disabled").ShouldBeFalse());
            cut.FindAll("[data-testid='tenants-audit-history-truncated']").ShouldBeEmpty();
        }

        cut.Find("[data-testid='tenants-audit-previous']").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-audit-grid']").TextContent.ShouldContain("event-page-0");
            cut.Find("[data-testid='tenants-audit-previous']").HasAttribute("disabled").ShouldBeTrue();
        });

        IElement notice = cut.Find("[data-testid='tenants-audit-history-truncated']");
        notice.GetAttribute("role").ShouldBe("status");
        notice.GetAttribute("aria-live").ShouldBe("polite");
        notice.TextContent.ShouldContain("first page");

        // Paging forward again retires the notice: the operator is no longer on the jumped-to page.
        cut.Find("[data-testid='tenants-audit-next']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-history-truncated']").ShouldBeEmpty());
    }
}
