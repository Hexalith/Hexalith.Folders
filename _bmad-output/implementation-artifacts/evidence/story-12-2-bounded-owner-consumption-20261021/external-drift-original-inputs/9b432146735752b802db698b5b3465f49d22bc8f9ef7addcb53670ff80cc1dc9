using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;

using AngleSharp.Dom;

using Bunit;

using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Services.Auth;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantUsers;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Tenants.UI.State.UserTenants;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed partial class GlobalAdministratorsPageTests : FluentBunitContext
{
    public GlobalAdministratorsPageTests()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        Services.AddSingleton(viewport);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerlessTerminalGrantCompletionReleasesOnlyItsAdoptedLease(bool failed)
    {
        var deliveryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(failed
            ? TenantCommandSubmissionResult.Failed("delivery failed")
            : TenantCommandSubmissionResult.RejectedWithKey("Tenants.GlobalAdministrators.Grant.Submission.Rejected", "InsufficientPermissions"))
        {
            SubmissionGate = deliveryGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a")) { RepeatLastResponse = true });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        first.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(first);
        AcknowledgeGrantPreview(first);
        Task submit = first.Find("[data-testid='tenants-global-admin-grant-confirm']").ClickAsync(new MouseEventArgs());
        await commandGateway.SubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string messageId = commandGateway.GrantMessageIds.ShouldHaveSingleItem();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());
        deliveryGate.SetResult();
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        string key = TenantCommandAggregateLock.ForGlobalAdministrators();
        gate.IsLocked(key).ShouldBeTrue();
        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        replacement.WaitForAssertion(() =>
        {
            GlobalAdministratorGrantCommandSnapshot outcome = PrivateField<GlobalAdministratorGrantCommandSnapshot>(replacement.Instance, "_grantSnapshot");
            outcome.MessageId.ShouldBe(messageId);
            outcome.State.ShouldBe(failed ? TenantCommandLifecycleState.Failed : TenantCommandLifecycleState.Rejected);
            gate.IsLocked(key).ShouldBeFalse();
        });
        object nextOwner = new();
        gate.TryAcquireLease(key, nextOwner, out TenantAggregateCommandLease? next).ShouldBeTrue();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());
        gate.IsLocked(key).ShouldBeTrue();
        next!.TryAbandonBeforeDispatch(nextOwner).ShouldBeTrue();
        commandGateway.GrantMessageIds.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DisposedAdministratorsPageDoesNotReactToPrincipalChange()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var gateway = new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        };
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            principalSource: authentication));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-list']");
        GlobalAdministratorsPage instance = cut.Instance;
        long versionBeforeChange = PrivateField<long>(instance, "_authorizationTransitionVersion");

        await cut.InvokeAsync(() => authentication.Set(NonAdministratorPrincipal()));
        cut.WaitForAssertion(() =>
        {
            PrivateField<long>(instance, "_authorizationTransitionVersion").ShouldBe(versionBeforeChange + 1);
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
        });

        await DisposeComponentsAsync();
        PrivateField<bool>(instance, "_disposed").ShouldBeTrue();
        long versionAfterDispose = PrivateField<long>(instance, "_authorizationTransitionVersion");
        int readsAfterDispose = gateway.GlobalAdministratorCalls;
        authentication.Set(GlobalAdministratorPrincipal());

        PrivateField<long>(instance, "_authorizationTransitionVersion").ShouldBe(versionAfterDispose);
        gateway.GlobalAdministratorCalls.ShouldBe(readsAfterDispose);
    }

    [Fact]
    public void Authentication_revocation_collapses_privileged_state_without_another_query()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.before", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"before\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(
                TenantLifecycleAuthorizationReflectionState.Authorized,
                principalSource: authentication));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin.before");

        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
            cut.Markup.ShouldNotContain("admin.before");
            cut.FindAll("[data-testid='tenants-global-admins-list']").ShouldBeEmpty();
        });
        gateway.GlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public void Authentication_restore_requeries_after_claim_correct_authorization()
    {
        var authentication = new MutableAuthenticationStateProvider(NonAdministratorPrincipal());
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.after", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"after\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(
                TenantLifecycleAuthorizationReflectionState.Indeterminate,
                principalSource: authentication));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-unavailable']");
        gateway.GlobalAdministratorCalls.ShouldBe(0);

        authentication.Set(GlobalAdministratorPrincipal());
        cut.WaitForAssertion(
            () => gateway.GlobalAdministratorCalls.ShouldBe(1),
            TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin.after");
            cut.Find("[data-testid='tenants-global-admins-list']");
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Authentication_transition_from_a_later_page_announces_the_page_one_restart()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin.page-one", ReadModelFreshnessState.Current)],
                nextCursor: "protected-page-two",
                hasMore: true,
                eTag: "\"page-one\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin.page-two", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"page-two\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin.reauthorized", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"reauthorized\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            principalSource: authentication));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await cut.Find("[data-testid='tenants-global-admins-next']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-user-id']")
            .TextContent.ShouldBe("admin.page-two"));

        authentication.Set(GlobalAdministratorPrincipal());

        cut.WaitForAssertion(() =>
        {
            gateway.GlobalAdministratorCalls.ShouldBe(3);
            gateway.Requests[2].Cursor.ShouldBeNull();
            IElement notice = cut.Find("[data-testid='tenants-global-admins-authorization-page-reset']");
            notice.GetAttribute("role").ShouldBe("status");
            notice.GetAttribute("aria-live").ShouldBe("polite");
            notice.TextContent.ShouldBe("Authorization changed. The review restarted at the first page.");
        });
    }

    [Fact]
    public void Real_principal_resolver_authorizes_the_page_outside_an_inbound_circuit_activity()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns("operator.alpha");
        var principalResolver = new TenantConfigurationPrincipalResolver(
            new CircuitServicesAccessor(),
            userContext,
            authentication);
        var composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            principalResolver: principalResolver,
            readSurface: new TenantsReadSurfaceAvailability(IsConnected: true));
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.real-resolver", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"real-resolver\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        gateway.GlobalAdministratorCalls.ShouldBe(1);
        cut.Find("[data-testid='tenants-global-admins-user-id']")
            .TextContent.ShouldBe("admin.real-resolver");

        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
            cut.Markup.ShouldNotContain("admin.real-resolver");
        });
    }

    /// <summary>
    /// The authentication-transition path writes Authorized from inside a dispatcher callback. Without a version
    /// re-check INSIDE that callback, a sign-out landing while the resolve is in flight is overwritten by the
    /// pre-sign-out answer: the privileged surface returns for an operator who has already signed out, and the
    /// post-hoc check cannot undo a write that has already rendered. Deleting the in-callback guard fails here.
    /// </summary>
    [Fact]
    public async Task A_sign_out_landing_during_a_transition_resolve_is_not_overwritten_by_the_stale_answer()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.before-signout", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"before-signout\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-list']");
        gateway.GlobalAdministratorCalls.ShouldBe(1);

        // A transition starts and its resolve suspends while still holding the pre-sign-out Authorized answer.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = gate;
        authentication.Set(GlobalAdministratorPrincipal());
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The operator signs out. This bumps the transition version and collapses the surface.
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(
            () => cut.Find("[data-testid='tenants-global-admins-restricted-header']"),
            TimeSpan.FromSeconds(5));

        // Only now does the superseded resolve complete, answering Authorized.
        gate.SetResult();

        // Bounded settle: the assertion is that nothing further happens, which no retry-until-true wait can
        // express -- WaitForAssertion would pass on the current state before the stale continuation ran.
        await Task.Delay(250);

        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        cut.FindAll("[data-testid='tenants-global-admins-page-header']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admins-list']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("admin.before-signout");
        gateway.GlobalAdministratorCalls.ShouldBe(1);
    }

    /// <summary>
    /// The initial resolve must not be able to miss a transition. Subscribing to AuthenticationStateChanged only
    /// AFTER the resolve completed meant a sign-out landing during initialization fired against no handler at
    /// all and was lost, and the pre-sign-out answer was then written unconditionally -- leaving the full
    /// privileged surface rendered for a signed-out principal with no later transition to correct it. Moving the
    /// subscription back after the resolve, or dropping the version capture, fails here.
    /// </summary>
    /// <remarks>
    /// The read surface is deliberately disconnected. With it connected the initial apply falls through to
    /// <c>LoadAsync</c>, whose default <c>reauthorize: true</c> re-resolves and collapses the surface anyway --
    /// so the missed event is masked and the test passes even with the subscription moved back after the
    /// resolve. Disconnected, the apply takes the early return and nothing re-authorizes, which is what makes
    /// the subscription ordering itself observable.
    /// </remarks>
    [Fact]
    public async Task A_sign_out_during_initial_authorization_resolution_is_observed_and_wins()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            isReadSurfaceConnected: false);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = gate;
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.initial", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"initial\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The sign-out lands while initialization is still resolving. The page must already be subscribed.
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Set(NonAdministratorPrincipal());

        // Wait for the transition's own resolve before releasing the stale one, so the ordering under test is
        // deterministic rather than a race with the handler being dispatched. If the page is not subscribed --
        // the mutation this test exists to catch -- no second resolution ever happens and this times out.
        // Polled rather than WaitForAssertion: that helper only re-evaluates on a render, and this counter is
        // not render-driven, so it checked once and timed out under load.
        await WaitUntilAsync(() => composition.AsyncResolutionCount >= 2, TimeSpan.FromSeconds(5));

        gate.SetResult();
        await Task.Delay(250);

        // The restricted header, not the authorized area: the sign-out was seen and beat the stale answer.
        // `tenants-global-admins-unavailable` is NOT a discriminator here -- it renders in the authorized
        // branch too, for an Unavailable snapshot.
        // The restricted header, not the authorized page header: the sign-out was seen and beat the stale
        // answer. Neither `tenants-global-admins-area` nor `tenants-global-admins-unavailable` discriminates --
        // the restricted branch deliberately keeps the page area and publishes its own unavailable region.
        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        cut.FindAll("[data-testid='tenants-global-admins-page-header']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("admin.initial");
        gateway.GlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Successful_subscription_setups_do_not_exhaust_the_budget_across_authorization_transitions()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        GlobalAdministratorsSnapshot ready = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.current", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"current\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        // Five responses for five expected reads left no slack: one extra read -- a notification nudge, an
        // authorization re-read -- made Dequeue() throw InvalidOperationException from inside the gateway
        // call, which LoadAsync does not catch (only OperationCanceledException). The stub repeats its last
        // response, so an unexpected read fails an assertion instead of an unrelated queue underflow.
        var gateway = new StubTenantQueryGateway(ready) { RepeatLastResponse = true };
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(
                TenantLifecycleAuthorizationReflectionState.Authorized,
                principalSource: authentication));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());

        for (int transition = 1; transition <= 4; transition++)
        {
            authentication.Set(NonAdministratorPrincipal());
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-unavailable']"));

            // Polled, not asserted at an instant. CollapseAuthorizationAsync calls StateHasChanged BEFORE
            // disposing the lease, so the unavailable panel rendering does not imply the unsubscribe was
            // issued -- NSubstitute's Received() does not wait, so this raced and could fail on transition 1.
            cut.WaitForAssertion(() => subscription.Received(transition).UnsubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>()));

            authentication.Set(GlobalAdministratorPrincipal());
            cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(transition + 1));

            // Same reason: the gateway call count reaching n does not imply OnAfterRenderAsync's subscribe
            // has run.
            cut.WaitForAssertion(() => subscription.Received(transition + 1).SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>()));
        }

        await Task.CompletedTask;
    }

    [Fact]
    public void Authorized_operator_sees_global_administrators_from_fixed_scope()
    {
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("platform-admin.alpha", ReadModelFreshnessState.Current) with
            {
                Lifecycle = ProjectionLifecycleState.Current,
            }],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsClipboard.js");
        JSRuntimeInvocationHandler writeHandler = module.SetupVoid("writeText", "platform-admin.alpha").SetVoidResult();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        gateway.GlobalAdministratorCalls.ShouldBe(1);
        cut.Find("[data-testid='tenants-global-admins-area']");
        cut.Find("[data-testid='tenants-global-admins-scope']").TextContent.ShouldContain("global-administrators");
        cut.Find("[data-testid='tenants-global-admins-scope']").TextContent.ShouldContain("system");
        cut.Find("[data-testid='tenants-global-admins-list']");
        cut.Find("[data-testid='tenants-global-admins-row']");
        cut.Find("[data-testid='tenants-global-admins-mobile-readonly']");
        cut.FindAll(".global-admins__mutation-section").Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("platform-admin.alpha");
        cut.Find("[data-testid='tenants-global-admins-authority-scope']").TextContent.ShouldContain("Platform authority");
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle-status']")
            .GetAttribute("role").ShouldBe("status");
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle-status-badge']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("projection-lifecycle-badge--current");
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle-status-badge']")
            .TextContent.Trim().ShouldBe("Current");
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("projection-lifecycle-badge--current");

        // The localized label, not only the class: a class token is incidental markup, and asserting it
        // alone cannot detect a missing or wrong badge string.
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle']").TextContent.Trim().ShouldBe("Current");
        cut.Find("[data-testid='tenants-global-admins-action-reasons']").TextContent.ShouldContain("Grant is available");
        cut.Find("[data-testid='tenants-global-admins-evidence-scope']").TextContent
            .ShouldBe("Fixed platform global-administrator scope");
        cut.Find("[data-testid='tenants-global-admins-evidence-freshness']").TextContent
            .ShouldBe("Current lifecycle and version evidence");
        cut.Find("[data-testid='tenants-global-admins-evidence-count']").TextContent
            .ShouldBe("Administrator count from complete evidence: 1");
        cut.Find("[data-testid='tenants-global-admins-evidence-admission']").TextContent
            .ShouldBe("Available");
        cut.Find("[data-testid='tenants-global-admins-live-region']").GetAttribute("aria-live").ShouldBeNull();
        cut.Markup.ShouldNotContain("/api/tenants", Case.Insensitive);
        cut.Markup.ShouldNotContain("/api/users", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-authority-scope']").TextContent.ShouldContain("not tenant ownership");
        cut.Markup.ShouldContain("data-testid=\"tenants-global-admins-list\"");

        cut.Find("[data-surface-testid='tenants-global-admins-copy-user-id']").Click();
        cut.WaitForAssertion(() => writeHandler.Invocations.Count.ShouldBe(1));
        writeHandler.Invocations.Single().Arguments[0].ShouldBe("platform-admin.alpha");
    }

    [Fact]
    public async Task Tenant_owner_without_platform_authority_gets_fail_closed_without_querying_or_subscribing()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("hidden-admin", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Indeterminate));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        gateway.GlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admins-unavailable']").GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-global-admins-denied-message']").TextContent.ShouldContain("fails closed");

        // Anchor on the live region itself, not only on its parent. The denied-message element CONTAINS the
        // live region, so its TextContent includes the child either way -- deleting the live-region wrapper
        // from the restricted branch left the denial with no identified live region, test green.
        cut.Find("[data-testid='tenants-global-admins-live-region']").TextContent.ShouldContain("fails closed");
        cut.Markup.ShouldNotContain("hidden-admin");
        cut.Markup.ShouldNotContain("tenants-global-admins-list");
        cut.Markup.ShouldNotContain("tenants-global-admins-scope");
        cut.Markup.ShouldNotContain("tenants-global-admin-grant");
        cut.Markup.ShouldNotContain("tenants-global-admin-remove");
        cut.Markup.ShouldNotContain("global-administrators");
        cut.Markup.ShouldNotContain("system");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
        await subscription.DidNotReceive()
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Indeterminate_authorization_offers_a_safe_retry_before_querying()
    {
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.after-retry", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"after-retry\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        gateway.GlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admins-unavailable']");
        cut.Markup.ShouldNotContain("admin.after-retry");

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Authorized;
        await cut.Find("[data-testid='tenants-global-admins-authorization-retry']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            gateway.GlobalAdministratorCalls.ShouldBe(1);
            cut.Find("[data-testid='tenants-global-admins-user-id']")
                .TextContent.ShouldBe("admin.after-retry");
        });
    }

    [Fact]
    public void Server_denial_after_optimistic_reflection_discards_all_privileged_markup()
    {
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Unauthorized());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        gateway.GlobalAdministratorCalls.ShouldBe(1);
        cut.Find("[data-testid='tenants-global-admins-unavailable']");
        cut.Markup.ShouldNotContain("tenants-global-admins-list");
        cut.Markup.ShouldNotContain("tenants-global-admins-scope");
        cut.Markup.ShouldNotContain("tenants-global-admin-grant");
        cut.Markup.ShouldNotContain("tenants-global-admin-remove");
    }

    [Fact]
    public async Task Notification_reauthorization_denial_disposes_the_lease_and_clears_privileged_state()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("must-be-cleared", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1)
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>());

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
            cut.Markup.ShouldNotContain("must-be-cleared");
            cut.Markup.ShouldNotContain("tenants-global-admins-scope");
            cut.Markup.ShouldNotContain("tenants-global-admin-grant");
            cut.Markup.ShouldNotContain("tenants-global-admin-remove");
        });
        gateway.GlobalAdministratorCalls.ShouldBe(1);
        await subscription.Received(1)
            .UnsubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CorrelatedGrantNotificationPerformsOneStatusLookupWithoutRedispatch()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Received,
                HasVerifiedCommandIdentity: true),
            TenantCommandStatusResult.Pending("pending") with
            {
                SafeMessageKey = "Tenants.GlobalAdministrators.Grant.Status.Pending",
            });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);
        commandGateway.StatusHandles.Count.ShouldBe(1);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");

        cut.WaitForAssertion(() => commandGateway.StatusHandles.Count.ShouldBe(2));
        await cut.InvokeAsync(static () => Task.CompletedTask);
        commandGateway.StatusHandles.Count.ShouldBe(2);
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
        string messageId = commandGateway.GrantMessageIds.ShouldHaveSingleItem();
        commandGateway.StatusHandles.ShouldAllBe(handle =>
            handle.MessageId == messageId
            && handle.CorrelationId == "correlation-grant"
            && handle.AggregateId == GlobalAdministratorGrantPreview.FixedAggregateId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SupersededCorrelatedGrantNotificationCannotMutateOrQueryReplacementAttempt(
        bool replaceSnapshot,
        bool replaceLease)
    {
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-a", "correlation-a"),
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-a");
        PreviewAcknowledgeAndConfirmGrant(cut);
        commandGateway.StatusHandles.Count.ShouldBe(1);

        GlobalAdministratorGrantCommandSnapshot attemptA =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        TenantAggregateCommandLease leaseA =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");
        var notificationStatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.StatusGate = notificationStatusGate;
        MethodInfo refreshFromNotification = typeof(GlobalAdministratorsPage).GetMethod(
            "RefreshFromNotificationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        Task notification = (Task)refreshFromNotification.Invoke(cut.Instance, null)!;
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.StatusHandles.Count.ShouldBe(2);
        commandGateway.StatusHandles[1].MessageId.ShouldBe(attemptA.MessageId);

        GlobalAdministratorGrantCommandSnapshot attemptB = attemptA with
        {
            MessageId = NUlid.Ulid.NewUlid().ToString(),
            CorrelationId = "correlation-b",
        };
        var replacementGate = new TenantAggregateCommandAdmissionGate();
        object replacementOwner = new();
        replacementGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? leaseB).ShouldBeTrue();
        leaseB.ShouldNotBeNull().TryMarkDispatched(replacementOwner).ShouldBeTrue();
        if (replaceSnapshot)
        {
            typeof(GlobalAdministratorsPage).GetField(
                "_grantSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, attemptB);
        }

        if (replaceLease)
        {
            typeof(GlobalAdministratorsPage).GetField(
                "_grantAdmissionLease",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, leaseB);
        }

        notificationStatusGate.SetResult();
        await notification.WaitAsync(TimeSpan.FromSeconds(5));

        commandGateway.StatusHandles.Count.ShouldBe(2);
        commandGateway.StatusHandles.ShouldNotContain(handle => handle.MessageId == attemptB.MessageId);
        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .ShouldBe(replaceSnapshot ? attemptB : attemptA);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease")
            .ShouldBeSameAs(replaceLease ? leaseB : leaseA);
        leaseA.IsDispatchMarked.ShouldBeTrue();
    }

    [Fact]
    public async Task CorrelationlessGrantNotificationNeitherLooksUpStatusNorRedispatches()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");
        cut.Render();
        await cut.InvokeAsync(static () => Task.CompletedTask);

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
        commandGateway.StatusHandles.ShouldBeEmpty();
        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .IsSubmissionAmbiguous.ShouldBeTrue();
    }

    [Fact]
    public async Task Notification_refreshing_affordance_retains_rows_until_the_authoritative_read_completes()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var pending = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.confirmed", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"confirmed\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
        gateway.QueueResponse(pending.Task);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-notification-refreshing']")
                .GetAttribute("role").ShouldBe("status");
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin.confirmed");
        });

        pending.SetResult(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.refreshed", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"refreshed\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='tenants-global-admins-notification-refreshing']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin.refreshed");
        });
    }

    [Fact]
    public async Task Manual_refresh_retries_notification_setup_after_a_transient_empty_lease()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        int setupAttempts = 0;
        subscription
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>())
            // Keeps failing until the per-render budget is spent, so the budget itself is exercised and the
            // only attempt that can succeed is the one an explicit refresh unlocks.
            .Returns(_ => Interlocked.Increment(ref setupAttempts) <= 3
                ? Task.FromException(new HttpRequestException("transient setup failure"))
                : Task.CompletedTask);
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin.confirmed", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin.confirmed", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForAssertion(() => setupAttempts.ShouldBeGreaterThanOrEqualTo(1));

        // Exhaust the per-render retry budget. Without a bound, a failing subscribe was retried on EVERY
        // render, so "the Refresh click retried setup" was indistinguishable from "the click caused a
        // render": rewiring the button to a no-op that calls StateHasChanged kept the test green.
        for (int render = 0; render < 10; render++)
        {
            cut.Render();
        }

        int attemptsBeforeRefresh = Volatile.Read(ref setupAttempts);
        attemptsBeforeRefresh.ShouldBe(3, "The per-render retry must be bounded, and spend its whole budget.");

        // Only an explicit refresh resets the budget, so only it can produce a further attempt.
        cut.Find("[data-testid='tenants-global-admins-refresh']").Click();

        cut.WaitForAssertion(() => Volatile.Read(ref setupAttempts).ShouldBe(attemptsBeforeRefresh + 1));
        await subscription.Received(attemptsBeforeRefresh + 1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());

        // Exactly one further attempt, because the empty lease was transient and this retry SUCCEEDS -- not
        // because a refresh grants one attempt. Refresh restores the whole budget of 3; what stops it being
        // spent is the success. Re-rendering ten more times must therefore add nothing, which is what
        // separates "refresh reset the budget and the retry worked" from "every render retries forever" --
        // the regression this test exists for, and the half that was previously unasserted.
        for (int render = 0; render < 10; render++)
        {
            cut.Render();
        }

        Volatile.Read(ref setupAttempts).ShouldBe(attemptsBeforeRefresh + 1);
    }

    [Fact]
    public async Task Rows_free_retry_reopens_the_notification_setup_budget()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        int setupAttempts = 0;
        subscription
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref setupAttempts) <= 3
                ? Task.FromException(new HttpRequestException("transient setup failure"))
                : Task.CompletedTask);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Error())
        {
            RepeatLastResponse = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        for (int render = 0; render < 10; render++)
        {
            cut.Render();
        }

        Volatile.Read(ref setupAttempts).ShouldBe(3);
        cut.FindAll("[data-testid='tenants-global-admins-list']").ShouldBeEmpty();

        await cut.Find("[data-testid='tenants-global-admins-retry']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Volatile.Read(ref setupAttempts).ShouldBe(4));
        await subscription.Received(4).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Notification_setup_failure_logs_only_a_support_safe_reason_code()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        var logger = new CapturingUntypedLogger();
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current,
            eTag: null) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") })
        {
            RepeatLastResponse = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton<IProjectionChangeNotifierWithTenant>(new ThrowingProjectionNotifier());
        Services.AddSingleton(loggerFactory);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        const string expected = "Optional projection refresh setup failed. ReasonCode=notification-setup-failed";
        cut.WaitForAssertion(() => logger.Messages.ShouldContain(expected));
        int entry = logger.Messages.IndexOf(expected);
        logger.Levels[entry].ShouldBe(LogLevel.Warning);
        logger.Exceptions[entry].ShouldBeNull();
        string logged = logger.Messages[entry];
        logged.ShouldNotContain("system", Case.Insensitive);
        logged.ShouldNotContain("global-administrators", Case.Insensitive);
        logged.ShouldNotContain(nameof(InvalidOperationException));
    }

    /// <summary>
    /// Retry and Reset must refuse a click dispatched while a page read is already in flight.
    /// </summary>
    /// <remarks>
    /// Both render alongside the pager whenever a rows-bearing snapshot is also recoverable, and neither used
    /// to set or read the in-flight gate: a Next click followed by Reset cleared the cursor state
    /// synchronously, Next's load then returned null as superseded and its <c>finally</c> re-enabled the
    /// pager against the pre-reset snapshot, so the next Next pushed a null prior cursor and recorded page
    /// one for a view showing page four. Neutering the early return in both handlers survived the suite --
    /// the only retry test clicked once against a settled surface, and the rendered <c>disabled</c> attribute
    /// cannot observe a click dispatched before the re-render lands.
    /// </remarks>
    [Fact]
    public async Task Recovery_affordances_reject_a_click_dispatched_while_a_page_read_is_in_flight()
    {
        GlobalAdministratorsSnapshot recoverable = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.alpha", ReadModelFreshnessState.Stale)],
            nextCursor: "page-2",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Stale) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        var gateway = new StubTenantQueryGateway(recoverable) { RepeatLastResponse = true };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(1));

        // Hold the next page read open, so the surface really is mid-load.
        var pendingPage = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(pendingPage.Task);
        Task nextClick = cut.Find("[data-testid='tenants-global-admins-next']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(2));

        await cut.InvokeAsync(() => cut.Instance.RetryAsync());
        await cut.InvokeAsync(() => cut.Instance.ResetPagingAsync());

        gateway.GlobalAdministratorCalls.ShouldBe(
            2,
            "Neither recovery affordance may start a read while a page read is in flight.");

        pendingPage.SetResult(recoverable);
        await nextClick;

        // Once the load settles, the affordances work again. BOTH of them: re-exercising only RetryAsync
        // left a permanently dead ResetPagingAsync passing this test, because the assertion it was subject
        // to above is that it does NOT read -- an implementation that never reads satisfies that for the
        // wrong reason. Each is given its own positive control.
        await cut.InvokeAsync(() => cut.Instance.RetryAsync());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(3));

        await cut.InvokeAsync(() => cut.Instance.ResetPagingAsync());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(4));
    }

    [Fact]
    public void Page_load_mutual_exclusion_uses_an_atomic_test_and_set_gate()
    {
        string source = ReadGlobalAdministratorsPageSource();

        source.ShouldContain("private int _pageLoadInFlight;");
        Regex.IsMatch(
            source,
            @"Interlocked\.CompareExchange\s*\(\s*ref\s+_pageLoadInFlight\s*,\s*1\s*,\s*0\s*\)",
            RegexOptions.CultureInvariant).ShouldBeTrue();
        Regex.IsMatch(
            source,
            @"_pageLoadInFlight\s*=\s*true\s*;",
            RegexOptions.CultureInvariant).ShouldBeFalse();
    }

    [Fact]
    public void Initialization_marshals_authorization_and_terminal_snapshot_state_to_the_renderer()
    {
        string source = ReadGlobalAdministratorsPageSource();
        string initialization = ExtractMethodBody(source, "protected override async Task OnInitializedAsync()");

        initialization.ShouldContain("await InvokeRendererSafelyAsync(() =>");
        Regex.IsMatch(
            initialization,
            @"_authorizationReflection\s*=\s*await",
            RegexOptions.CultureInvariant).ShouldBeFalse();
    }

    [Fact]
    public void Retry_authorization_releases_the_page_load_gate_outside_the_renderer_dispatch()
    {
        string source = ReadGlobalAdministratorsPageSource();
        string retryAuthorization = ExtractMethodBody(source, "internal async Task RetryAuthorizationAsync()");

        Regex.IsMatch(
            retryAuthorization,
            @"finally\s*\{[^}]*EndPageLoad\s*\(\s*\)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant).ShouldBeTrue();
        Regex.IsMatch(
            retryAuthorization,
            @"finally\s*\{[^}]*await\s+InvokeAsync\s*\([^)]*EndPageLoad",
            RegexOptions.Singleline | RegexOptions.CultureInvariant).ShouldBeFalse();
    }

    [Fact]
    public async Task Recovery_affordances_announce_page_recovery_and_execute_reset_then_retry()
    {
        GlobalAdministratorsSnapshot recoverable = GlobalAdministratorsSnapshot.Error() with
        {
            PagingRecovered = true,
            Reason = GlobalAdministratorsReason.PageRecovered,
        };
        var gateway = new StubTenantQueryGateway(
            recoverable,
            GlobalAdministratorsSnapshot.Error(),
            GlobalAdministratorsSnapshot.Empty(
                isAuthorizationScoped: true,
                ReadModelFreshnessState.Current,
                eTag: "\"recovered\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-page-recovered']")
            .GetAttribute("aria-live").ShouldBe("polite");
        await cut.Find("[data-testid='tenants-global-admins-reset']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(2));
        gateway.Requests[1].Cursor.ShouldBeNull();
        gateway.Requests[1].ETag.ShouldBeNull();

        await cut.Find("[data-testid='tenants-global-admins-retry']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            gateway.GlobalAdministratorCalls.ShouldBe(3);
            cut.Find("[data-testid='tenants-global-admins-empty']");

            // Recovery affordances must SURVIVE an empty page. ShouldRenderRows requires Rows.Count > 0 and
            // owns the pager, Refresh and grid, so an Empty page renders no controls of its own; if Retry and
            // Reset also disappeared, an operator who paged into a page emptied by a concurrent removal would
            // be stranded on that cursor with only a browser reload to recover.
            cut.FindAll("[data-testid='tenants-global-admins-retry']").ShouldNotBeEmpty();
            cut.FindAll("[data-testid='tenants-global-admins-reset']").ShouldNotBeEmpty();
        });
        gateway.Requests[2].ETag.ShouldBeNull();
    }

    [Fact]
    public void Lifecycle_and_recovery_controls_are_outside_the_assertive_truth_region()
    {
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Error());
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        IElement assertiveState = cut.Find("[data-testid='tenants-global-admins-error']");
        assertiveState.QuerySelector("[data-testid='tenants-global-admins-projection-lifecycle-status']")
            .ShouldBeNull();
        assertiveState.QuerySelector("[data-testid='tenants-global-admins-retry']").ShouldBeNull();
        assertiveState.QuerySelector("[data-testid='tenants-global-admins-reset']").ShouldBeNull();
        cut.Find("[data-testid='tenants-global-admins-projection-lifecycle-status']");
        cut.Find("[data-testid='tenants-global-admins-retry']");
        cut.Find("[data-testid='tenants-global-admins-reset']");
    }

    [Fact]
    public async Task Newer_notification_refresh_rejects_a_late_cursor_result_and_preserves_cursor_history()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var latePage = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("first-admin", ReadModelFreshnessState.Current)],
                "protected-next",
                true,
                "\"first-etag\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("newest-admin", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"newest-etag\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1)
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>());
        gateway.QueueResponse(latePage.Task);

        Task nextNavigation = cut.Find("[data-testid='tenants-global-admins-next']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(2));

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");
        cut.WaitForAssertion(() =>
        {
            gateway.GlobalAdministratorCalls.ShouldBe(3);
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("newest-admin");
        });

        latePage.SetResult(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("late-admin", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"late-etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        await nextNavigation;

        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("newest-admin");
        cut.Markup.ShouldNotContain("late-admin");
        gateway.Requests[1].Cursor.ShouldBe("protected-next");
        gateway.Requests[2].Cursor.ShouldBeNull();
        cut.Find("[data-testid='tenants-global-admins-previous']").HasAttribute("disabled").ShouldBeTrue();

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        await subscription.Received(1)
            .UnsubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/tenants?search=alpha&sort=name", "/tenants?search=alpha&sort=name")]
    [InlineData("https://evil.example/tenants", "/tenants")]
    [InlineData("//evil.example/tenants", "/tenants")]
    [InlineData("/tenants?cursor=protected-secret", "/tenants")]
    public void Return_navigation_accepts_only_cursor_free_canonical_workspace_urls(string supplied, string expected)
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current,
            eTag: "\"empty\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("returnUrl", supplied));

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-return']").GetAttribute("href").ShouldBe(expected);
        cut.Markup.ShouldNotContain("protected-secret");
        cut.Markup.ShouldNotContain("evil.example");
    }

    [Theory]
    [InlineData("all-tenants")]
    [InlineData("my-tenants")]
    [InlineData("users")]
    public void Every_cursor_free_canonical_workspace_shape_is_accepted_as_return_context(string shape)
    {
        TenantWorkspaceState state = shape switch
        {
            "all-tenants" => TenantWorkspaceState.FromQuery(
                TenantWorkspaceState.TenantsTab,
                TenantWorkspaceState.AllScope,
                userId: null,
                search: "alpha beta",
                status: TenantStatus.Active.ToString(),
                sort: TenantListSortColumns.Name,
                sortDescending: bool.TrueString,
                cursor: null,
                selectedTenantId: "tenant.alpha",
                anchor: "tenant-row-tenant.alpha"),
            "my-tenants" => TenantWorkspaceState.FromQuery(
                TenantWorkspaceState.TenantsTab,
                TenantWorkspaceState.MyScope,
                userId: null,
                search: null,
                status: null,
                sort: null,
                sortDescending: null,
                cursor: null,
                selectedTenantId: "tenant.mine",
                anchor: "tenant-row-tenant.mine"),
            "users" => TenantWorkspaceState.FromQuery(
                TenantWorkspaceState.UsersTab,
                TenantWorkspaceState.AllScope,
                userId: "target/user",
                search: null,
                status: null,
                sort: UserTenantMembershipSortColumns.Role,
                sortDescending: null,
                cursor: null,
                selectedTenantId: null,
                anchor: null),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };
        string canonical = state.ToCanonicalUrl();
        canonical.ShouldNotBe("/tenants");
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current,
            eTag: "\"empty\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("returnUrl", canonical));

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-return']").GetAttribute("href").ShouldBe(canonical);
    }

    /// <summary>
    /// Client-local paging history is not evidence that other administrators exist.
    /// </summary>
    /// <remarks>
    /// On a later page the completeness gate is false, so the LastAdmin branch is skipped. The population
    /// check then accepted a non-empty cursor history — which only records that this circuit navigated
    /// forward at some point — and enabled Remove against what may be the platform's last global
    /// administrator. Only server-stated evidence (complete evidence with more than one row, or HasMore)
    /// may unlock removal.
    /// </remarks>
    [Fact]
    public void Single_row_page_without_complete_evidence_keeps_removal_unavailable()
    {
        GlobalAdministratorsSnapshot lastPage = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = false,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(lastPage));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.FindAll("[data-testid='tenants-global-admins-remove-reason']").ShouldNotBeEmpty(
            "removal must fail closed when no server evidence proves another administrator exists");
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty(
            "the remove launcher must not render while removal is unavailable");
    }

    [Fact]
    public void IncompleteCurrentPageWithMoreResultsAllowsGrantButBlocksRemoval()
    {
        GlobalAdministratorsSnapshot incomplete = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current),
            ],
            nextCursor: "opaque-next",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "paged-v1",
            IsCompleteEvidence = false,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(incomplete));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.FindAll("[data-testid='tenants-global-admin-grant-unavailable-reason']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admins-remove-reason']").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(GlobalAdministratorsSurfaceKind.Stale, ReadModelFreshnessState.Stale, "stale")]
    [InlineData(GlobalAdministratorsSurfaceKind.Degraded, ReadModelFreshnessState.Unknown, "degraded")]
    public void Stale_or_degraded_review_surface_keeps_rows_visible_and_actions_unavailable(
        GlobalAdministratorsSurfaceKind kind,
        ReadModelFreshnessState freshness,
        string expectedReason)
    {
        GlobalAdministratorsSnapshot snapshot = kind is GlobalAdministratorsSurfaceKind.Stale
            ? GlobalAdministratorsSnapshot.Stale([new GlobalAdministratorRow("admin-1", freshness)], null, false, "\"etag\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }
            : GlobalAdministratorsSnapshot.Degraded([new GlobalAdministratorRow("admin-1", freshness)], GlobalAdministratorsReason.ProjectionDegraded, "\"etag\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-list']");
        cut.Find("[data-testid='tenants-global-admins-row']").TextContent.ShouldContain("admin-1");
        cut.Find("[data-testid='tenants-global-admins-live-region']").TextContent.ShouldContain(expectedReason, Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-grant-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
    }

    [Fact]
    public void Unknown_review_surface_with_rows_keeps_list_visible_and_actions_unavailable()
    {
        GlobalAdministratorsSnapshot snapshot = GlobalAdministratorsSnapshot.Unknown(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Unknown)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-list']");
        cut.Find("[data-testid='tenants-global-admins-row']").TextContent.ShouldContain("admin-1");
        cut.Find("[data-testid='tenants-global-admins-grant-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
    }

    [Fact]
    public void Ready_has_more_without_cursor_offers_incomplete_paging_recovery()
    {
        GlobalAdministratorsSnapshot snapshot = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = false,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-next']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admins-incomplete-paging']").TextContent
            .ShouldContain("no continuation page", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-reset']");
        cut.Find("[data-testid='tenants-global-admins-retry']");
    }

    [Fact]
    public void Current_freshness_without_projection_lifecycle_uses_lifecycle_unavailable_copy()
    {
        GlobalAdministratorsSnapshot snapshot = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Unknown,
            IsCompleteEvidence = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-grant-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent.ShouldContain("fixed-scope", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(GlobalAdministratorsSurfaceKind.Empty, "tenants-global-admins-empty", "No global administrators")]
    [InlineData(GlobalAdministratorsSurfaceKind.Unknown, "tenants-global-admins-unknown", "truth unknown")]
    [InlineData(GlobalAdministratorsSurfaceKind.Error, "tenants-global-admins-error", "review failed")]
    [InlineData(GlobalAdministratorsSurfaceKind.Invalid, "tenants-global-admins-invalid", "Invalid global administrator page")]
    [InlineData(GlobalAdministratorsSurfaceKind.Unavailable, "tenants-global-admins-unavailable", "Global administrator data unavailable")]
    public void Empty_invalid_and_unavailable_states_do_not_render_false_success_or_hidden_rows(
        GlobalAdministratorsSurfaceKind kind,
        string expectedTestId,
        string expectedCopy)
    {
        GlobalAdministratorsSnapshot snapshot = kind switch
        {
            GlobalAdministratorsSurfaceKind.Empty => GlobalAdministratorsSnapshot.Empty(
                isAuthorizationScoped: true,
                ReadModelFreshnessState.Current,
                "\"empty\"") with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
            GlobalAdministratorsSurfaceKind.Unknown => GlobalAdministratorsSnapshot.Unknown([], null, false, null) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
            GlobalAdministratorsSurfaceKind.Error => GlobalAdministratorsSnapshot.Error(),
            GlobalAdministratorsSurfaceKind.Invalid => GlobalAdministratorsSnapshot.Invalid(),
            GlobalAdministratorsSurfaceKind.Unavailable => GlobalAdministratorsSnapshot.Unavailable(),
            _ => throw new InvalidOperationException($"Unsupported state {kind}."),
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find($"[data-testid='{expectedTestId}']").TextContent.ShouldContain(expectedCopy);
        cut.Markup.ShouldNotContain("tenants-global-admins-row");
        cut.Markup.ShouldNotContain("hidden-admin", Case.Insensitive);
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
    }

    [Fact]
    public void Refresh_reuses_etag_and_preserves_previous_snapshot_for_server_side_bff_query()
    {
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag-1\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag-1\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-refresh']").Click();

        gateway.GlobalAdministratorCalls.ShouldBe(2);
        gateway.Requests[0].ETag.ShouldBeNull();
        gateway.Requests[1].ETag.ShouldBe("\"etag-1\"");
        gateway.PreviousSnapshots[1].ShouldNotBeNull().Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-1");
    }

    [Fact]
    public void Next_page_uses_protected_cursor_without_offset_or_tenant_substitute_markers()
    {
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                nextCursor: "protected-next-cursor",
                hasMore: true,
                eTag: "\"etag-1\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag-2\"",
                freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-next']").Click();

        gateway.GlobalAdministratorCalls.ShouldBe(2);
        gateway.Requests[0].Cursor.ShouldBeNull();
        gateway.Requests[0].PageSize.ShouldBe(20);
        gateway.Requests[1].Cursor.ShouldBe("protected-next-cursor");
        gateway.Requests[1].ETag.ShouldBeNull();
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-2");
        cut.Markup.ShouldNotContain("offset", Case.Insensitive);
        cut.Markup.ShouldNotContain("/api/tenants", Case.Insensitive);
        cut.Markup.ShouldNotContain("/api/users", Case.Insensitive);
    }

    [Fact]
    public void Previous_page_returns_to_page_one_after_a_confirmed_next_page()
    {
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                "protected-next-cursor",
                true,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                Lifecycle = ProjectionLifecycleState.Current,
                RequestCursor = "protected-next-cursor",
            },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                "protected-next-cursor",
                true,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-2"));

        cut.Find("[data-testid='tenants-global-admins-previous']").Click();
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(3));
        gateway.Requests.Select(static request => request.Cursor).ShouldBe([null, "protected-next-cursor", null]);
    }

    [Fact]
    public void Failed_previous_keeps_history_so_the_operator_can_retry_page_one()
    {
        GlobalAdministratorsSnapshot pageTwo = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag-2\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            RequestCursor = "protected-next-cursor",
        };
        var gateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                "protected-next-cursor",
                true,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            pageTwo,
            GlobalAdministratorsSnapshot.Degraded(
                pageTwo.Rows,
                GlobalAdministratorsReason.GatewayUnavailable) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                RequestCursor = "protected-next-cursor",
            },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                "protected-next-cursor",
                true,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-2"));

        cut.Find("[data-testid='tenants-global-admins-previous']").Click();
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(3));
        cut.Find("[data-testid='tenants-global-admins-previous']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-global-admins-previous']").Click();

        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(4));
        gateway.Requests.Select(static request => request.Cursor)
            .ShouldBe([null, "protected-next-cursor", null, null]);
    }

    /// <summary>
    /// A Previous click that lands on page one through a trimmed history must be announced.
    /// </summary>
    /// <remarks>
    /// <c>CursorHistory.Trim</c> re-appends the first-page sentinel beneath the newest entries, so one later
    /// Previous click walks the operator from mid-sequence straight to page one. Deleting the whole notice
    /// block left the suite green: <c>tenants-global-admins-history-truncated</c> had no reference anywhere
    /// under tests/, which also made the <c>_pagingHistoryTruncated</c> plumbing on this page unobservable.
    /// The audit twin has carried this coverage since the notice was introduced.
    /// </remarks>
    [Fact]
    public void A_previous_click_that_jumps_to_page_one_through_a_trimmed_history_is_announced()
    {
        // Bound taken from the production constant. Duplicating it as a literal meant a change to
        // CursorHistory.DefaultMaximum made this walk the wrong number of steps and fail with
        // "previous is not disabled" -- a diagnosis that names nothing.
        const int bound = CursorHistory.DefaultMaximum;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetGlobalAdministratorsAsync(
            Arg.Any<GlobalAdministratorsRequest>(),
            Arg.Any<GlobalAdministratorsSnapshot?>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GlobalAdministratorsRequest request = call.Arg<GlobalAdministratorsRequest>()!;
                int page = request.Cursor is null
                    ? 0
                    : int.Parse(request.Cursor["page-".Length..], CultureInfo.InvariantCulture);
                return Task.FromResult(GlobalAdministratorsSnapshot.Ready(
                    [new GlobalAdministratorRow($"admin-page-{page}", ReadModelFreshnessState.Current)],
                    $"page-{page + 1}",
                    true,
                    $"\"etag-{page}\"",
                    ReadModelFreshnessState.Current) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    RequestCursor = request.Cursor,
                });
            });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-page-0"));

        // One page past the bound, so the trim runs and drops the oldest non-sentinel entries.
        for (int page = 1; page <= bound + 1; page++)
        {
            cut.Find("[data-testid='tenants-global-admins-next']").Click();
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-user-id']")
                .TextContent.ShouldBe($"admin-page-{page}"));
        }

        cut.FindAll("[data-testid='tenants-global-admins-history-truncated']").ShouldBeEmpty(
            "Paging forward is not a jump; the notice belongs to the Previous click that lands on page one.");

        // Walk back. The retained history is 49 entries plus the re-appended sentinel, so the last of these
        // pops the sentinel and lands on page one from the middle of the sequence.
        for (int step = 1; step < bound; step++)
        {
            cut.Find("[data-testid='tenants-global-admins-previous']").Click();
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admins-previous']")
                .HasAttribute("disabled").ShouldBeFalse());
            cut.FindAll("[data-testid='tenants-global-admins-history-truncated']").ShouldBeEmpty();
        }

        cut.Find("[data-testid='tenants-global-admins-previous']").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-page-0");
            cut.Find("[data-testid='tenants-global-admins-previous']").HasAttribute("disabled").ShouldBeTrue();
        });

        IElement notice = cut.Find("[data-testid='tenants-global-admins-history-truncated']");
        notice.GetAttribute("role").ShouldBe("status");
        notice.GetAttribute("aria-live").ShouldBe("polite");
        notice.TextContent.ShouldContain("first page");

        // An ordinary reload retires the one-shot navigation notice even though the operator remains on page
        // one. This pins the shared LoadAsync path used by manual, notification, and command-driven refreshes.
        cut.Find("[data-testid='tenants-global-admins-refresh']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-admins-history-truncated']").ShouldBeEmpty());

        // Paging forward also keeps it retired: the operator is no longer on the jumped-to page.
        cut.Find("[data-testid='tenants-global-admins-next']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-admins-history-truncated']").ShouldBeEmpty());
    }

    [Fact]
    public void Grant_flow_renders_fixed_scope_form_without_tenant_membership_inputs()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant']");
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']");
        cut.Find("[data-testid='tenants-global-admin-grant-scope']").TextContent.ShouldContain("system");
        cut.Find("[data-testid='tenants-global-admin-grant-scope']").TextContent.ShouldContain("global-administrators");
        cut.Markup.ShouldNotContain("TenantRole", Case.Insensitive);
        cut.Markup.ShouldNotContain("tenant-member", Case.Insensitive);
        cut.Markup.ShouldNotContain("member table", Case.Insensitive);
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("remove")]
    public void MissingActionSpecificPreviewReadinessBlocksOnlyTheAffectedEntryPointWithoutDispatch(
        string action)
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            isGrantPreviewReady: action != "grant",
            isRemovePreviewReady: action != "remove"));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-a", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-b", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        if (action == "grant")
        {
            IElement submit = cut.Find("[data-testid='tenants-global-admin-grant-submit']");
            submit.HasAttribute("disabled").ShouldBeTrue();
            submit.GetAttribute("aria-describedby").ShouldBe(
                "tenants-global-admin-grant-unavailable-reason tenants-global-admin-grant-recovery");
            cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
                .ShouldContain("safety flow is not ready");
            cut.Find("[data-testid='tenants-global-admin-grant-recovery']").TextContent
                .ShouldContain("dedicated grant preview");
            cut.FindAll("[data-testid='tenants-global-admin-remove']").Count.ShouldBe(2);

            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("candidate");
            cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();
        }
        else
        {
            cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled")
                .ShouldBeFalse();
            cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
            IReadOnlyList<IElement> actionSlots = cut.FindAll(
                "[data-testid='tenants-global-admins-action-reasons']");
            IReadOnlyList<IElement> reasons = cut.FindAll(
                "[data-testid='tenants-global-admins-remove-reason']");
            IReadOnlyList<IElement> recoveries = cut.FindAll(
                "[data-testid='tenants-global-admins-remove-recovery']");

            reasons.Count.ShouldBe(2);
            recoveries.Count.ShouldBe(2);
            reasons.Select(static element => element.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
            recoveries.Select(static element => element.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
            reasons.ShouldAllBe(static element =>
                !string.IsNullOrWhiteSpace(element.TextContent)
                && element.TextContent.Contains("consequence preview", StringComparison.Ordinal));
            recoveries.ShouldAllBe(static element =>
                !string.IsNullOrWhiteSpace(element.TextContent)
                && element.TextContent.Contains("dedicated removal preview", StringComparison.Ordinal));
            for (int index = 0; index < actionSlots.Count; index++)
            {
                actionSlots[index].GetAttribute("aria-describedby")
                    .ShouldBe($"{reasons[index].Id} {recoveries[index].Id}");
            }
        }

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingConcreteGatewayCapabilityBlocksBothActionsWithoutDispatch(
        bool supportsDispatch,
        bool supportsStatus)
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SupportsGlobalAdministratorDispatch = supportsDispatch,
            SupportsCommandStatusLookup = supportsStatus,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-a", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-b", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
            .ShouldContain("dispatch, status, or requery");
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admins-remove-reason']")
            .ShouldAllBe(static element => element.TextContent.Contains(
                "dispatch, status, or requery",
                StringComparison.Ordinal));

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("candidate");
        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void ViewportAndAdmissionEvidenceTransitionsReevaluateAvailabilityWithoutDispatch()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-a", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-b", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-global-admin-remove']").Count.ShouldBe(2);

        TenantHighImpactViewportObservation viewport =
            Services.GetRequiredService<TenantHighImpactViewportObservation>();
        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
                .ShouldContain("measures a safe");
            cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        });

        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-global-admin-remove']").Count.ShouldBe(2));

        TenantAggregateCommandAdmissionGate gate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var externalOwner = new object();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            externalOwner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-evidence-admission']").TextContent
                .ShouldContain("Active attempt");
            cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
                .ShouldContain("another global-administrator attempt");
            cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        });

        lease!.TryAbandonBeforeDispatch(externalOwner).ShouldBeTrue();
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-evidence-admission']").TextContent.ShouldBe("Available");
            cut.FindAll("[data-testid='tenants-global-admin-remove']").Count.ShouldBe(2);
        });
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void Last_global_administrator_remove_is_unavailable_without_confirmation_affordance()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("only-admin", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent.ShouldContain("last proven global administrator", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("override", Case.Insensitive);
        cut.Markup.ShouldNotContain("elevated friction", Case.Insensitive);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void Remove_preview_renders_fixed_scope_consequences_before_submission()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);

        cut.Find("[data-testid='tenants-global-admin-remove-preview']").TextContent.ShouldContain("target-admin");
        cut.Find("[data-testid='tenants-global-admin-remove-preview']").TextContent.ShouldContain("system");
        cut.Find("[data-testid='tenants-global-admin-remove-preview']").TextContent.ShouldContain("global-administrators");
        cut.Find("[data-testid='tenants-global-admin-remove-preview']").TextContent.ShouldContain("2");
        cut.Find("[data-testid='tenants-global-admin-remove-known-consequences']").TextContent.ShouldContain("platform authority", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-remove-known-unknowns']").TextContent.ShouldContain("token invalidation", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-remove-audit-expectation']").TextContent.ShouldContain("audit", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-remove-recovery-path']").TextContent.ShouldContain("grant", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").HasAttribute("disabled").ShouldBeFalse();
        cut.Markup.ShouldNotContain("tenant-member", Case.Insensitive);

        // Complete evidence: the count really is the platform total, so it carries the total label.
        cut.Find("[data-testid='tenants-global-admin-remove-count-label']")
            .TextContent.ShouldBe("Administrator counts");
        cut.Find("[data-testid='tenants-global-admin-remove-count']").TextContent.ShouldContain("2 administrators now");
    }

    [Fact]
    public void PageScopedRowsCannotOpenRemovePreviewOrClaimPlatformTotal()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-visible-admin", ReadModelFreshnessState.Current),
            ],
            nextCursor: "opaque-next-page",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = false,
        }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admins-remove-reason']").Count.ShouldBe(2);
    }

    /// <summary>
    /// The preview count and its label must come from the same evidence.
    /// </summary>
    /// <remarks>
    /// The count rendered <c>_removeSnapshot.PreviewRows</c>, captured at preview time, while its label was
    /// selected from the live <c>_snapshot.IsCompleteEvidence</c>. A notification refresh landing between
    /// preview and render therefore paired "Current administrator count" -- a claim about the whole platform
    /// -- with a page-one count, or the converse, on the destructive flow. Both are now read from the preview
    /// snapshot. The label and the id had zero test references before this.
    /// </remarks>
    [Fact]
    public void Remove_preview_count_and_label_survive_a_refresh_that_changes_evidence_completeness()
    {
        GlobalAdministratorsSnapshot completePage = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        };

        // The same rows, but the platform grew a second page while the dialog was open.
        GlobalAdministratorsSnapshot pagedRefresh = completePage with
        {
            NextCursor = "page-2",
            HasMore = true,
            IsCompleteEvidence = false,
        };

        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        StubTenantQueryGateway gateway = new(completePage, completePage, pagedRefresh);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);

        string previewCount = cut.Find("[data-testid='tenants-global-admin-remove-count']").TextContent;
        cut.Find("[data-testid='tenants-global-admin-remove-count-label']")
            .TextContent.ShouldBe("Administrator counts");

        // Refresh under the open dialog: the live snapshot is now page-scoped.
        cut.Find("[data-testid='tenants-global-admins-refresh']").Click();
        cut.WaitForAssertion(() => gateway.Requests.Count.ShouldBe(3));

        // The captured count is unchanged, so its label must be too -- the pair still describes one snapshot.
        cut.Find("[data-testid='tenants-global-admin-remove-count']").TextContent.ShouldBe(previewCount);
        cut.Find("[data-testid='tenants-global-admin-remove-count-label']")
            .TextContent.ShouldBe("Administrator counts");
    }

    /// <summary>
    /// The last-administrator hard stop must bind at SUBMIT, not only at preview time.
    /// </summary>
    /// <remarks>
    /// A projection notification replaces the snapshot underneath a Previewed intent without resetting the
    /// remove flow, so a preview taken while two administrators were visible could be submitted after the
    /// page had refreshed to a complete single-administrator page. IsRemoveSubmitDisabled consulted only the
    /// lifecycle state, and SubmitRemoveAsync re-checked only authorization -- the grant path already
    /// re-evaluated its full unavailable reason, so the asymmetry was the defect.
    /// </remarks>
    [Fact]
    public async Task Remove_submission_is_blocked_when_a_refresh_makes_the_target_the_last_administrator()
    {
        var commandGateway = new StubTenantCommandGateway();
        GlobalAdministratorsSnapshot eligible = GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true };
        var gateway = new StubTenantQueryGateway(
            eligible,
            eligible,
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current)],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag-2\"",
                freshness: ReadModelFreshnessState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").HasAttribute("disabled").ShouldBeFalse();

        // Refresh under the open preview: the page now holds a complete single-administrator page.
        await cut.Find("[data-testid='tenants-global-admins-refresh']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(3));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admin-remove-submit']").HasAttribute("disabled").ShouldBeTrue());
        await ClickRemoveSubmitEvenIfDisabledAsync(cut);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveMessageIds.ShouldBeEmpty();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admin-remove-safe-recovery']").TextContent
                .ShouldContain("Grant another global administrator", Case.Insensitive));
    }

    [Fact]
    public void Remove_preview_escape_cancels_without_submission_and_exposes_focus_sentinels()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-preview']").GetAttribute("role").ShouldBe("dialog");
        cut.Find("[data-testid='tenants-global-admin-remove-preview']").GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-end']").GetAttribute("tabindex").ShouldBe("0");

        cut.Find("[data-testid='tenants-global-admin-remove-preview']").KeyDown("Escape");

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent.ShouldContain("No global administrator remove command");
    }

    [Theory]
    [InlineData("target")]
    [InlineData("target-admin ")]
    [InlineData("target-ad")]
    [InlineData("Target-admin")]
    public async Task RemoveAcknowledgementRequiresTheCompleteOrdinalTarget(string entered)
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-acknowledge']").Change(entered);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeTrue();
        await ClickRemoveSubmitEvenIfDisabledAsync(cut);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveMessageIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangedCallerBeforeRemoveDispatchInvalidatesPreviewWithoutSendingCommand()
    {
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        composition.CallerSubject = "target-admin";

        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveMessageIds.ShouldBeEmpty();
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void TrackedLastAdministratorRaceRejectsWithoutChangingConfirmedRows()
    {
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(
                CommandStatus.Rejected,
                RejectionCode: "LastGlobalAdministrator",
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
                .State.ShouldBe(TenantCommandLifecycleState.Rejected);
            cut.Find("[data-testid='tenants-global-admin-remove-safe-message']")
                .TextContent.ShouldContain("last global administrator", Case.Insensitive);
        });
        cut.FindAll("[data-testid='tenants-global-admins-user-id']")
            .Select(static row => row.TextContent).ShouldBe(["target-admin", "other-admin"]);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("retry")]
    public async Task UnauthorizedRemovePreflightCollapsesPrivilegedSurface(string phase)
    {
        var queryGateway = new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin"))
        {
            RepeatLastResponse = true,
        };
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        if (phase == "retry")
        {
            await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
                .ClickAsync(new MouseEventArgs());
            commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        }

        queryGateway.QueueResponse(Task.FromResult(GlobalAdministratorsSnapshot.Unauthorized()));
        if (phase == "submit")
        {
            await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
                .ClickAsync(new MouseEventArgs());
            commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        }
        else
        {
            await cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
                .ClickAsync(new MouseEventArgs());
            commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        }

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").ShouldBeEmpty();
            PrivateField<TenantLifecycleAuthorizationReflectionState>(cut.Instance, "_authorizationReflection")
                .ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        });
    }

    [Fact]
    public void UnauthorizedRemoveRequeryCollapsesPrivilegedSurface()
    {
        GlobalAdministratorsSnapshot baseline = ComponentReady(
            "projection-v1",
            "target-admin",
            "other-admin");
        var queryGateway = new StubTenantQueryGateway(
            baseline,
            baseline,
            baseline,
            GlobalAdministratorsSnapshot.Unauthorized());
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-unavailable']");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").ShouldBeEmpty();
            PrivateField<TenantLifecycleAuthorizationReflectionState>(cut.Instance, "_authorizationReflection")
                .ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        });
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task UnsafeResizeKeepsRemoveCancelAndEscapeAvailableWithoutDispatch()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        GlobalAdministratorRemoveLauncher targetLauncher = cut
            .FindComponents<GlobalAdministratorRemoveLauncher>()
            .Single(component => component.Instance.UserId == "target-admin")
            .Instance;
        string launcherId = CapturedElementReferenceId(targetLauncher, "_button");
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        cut.Render();

        cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admin-remove-cancel']")
            .HasAttribute("disabled").ShouldBeFalse();
        await cut.Find("[data-testid='tenants-global-admin-remove-cancel']")
            .ClickAsync(new MouseEventArgs());
        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();
        FocusedElementIds()[^1].ShouldBe(launcherId);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void Remove_submission_confirms_only_after_projection_requery_excludes_target_user()
    {
        var queryGateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true, ProjectionVersion = "projection-v1" },
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true, ProjectionVersion = "projection-v1" },
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true, ProjectionVersion = "projection-v1" },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true, ProjectionVersion = "projection-v2" },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true, ProjectionVersion = "projection-v2" });
        var commandGateway = new StubTenantCommandGateway(statuses: [new TenantCommandStatusResult(
            CommandStatus.Completed,
            EventCount: 1,
            HasVerifiedCommandIdentity: true)])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("message-remove", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
            commandGateway.RemoveRequests.ShouldHaveSingleItem().UserId.ShouldBe("target-admin");
            TenantCommandTrackingHandle handle = commandGateway.StatusHandles.ShouldHaveSingleItem();
            handle.MessageId.ShouldBe(commandGateway.RemoveMessageIds.ShouldHaveSingleItem());
            handle.CorrelationId.ShouldBe("correlation-remove");
            handle.AggregateId.ShouldBe(GlobalAdministratorRemovePreview.FixedAggregateId);
            queryGateway.GlobalAdministratorCalls.ShouldBe(5);
            cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent.ShouldContain("Projection confirmed removal");
            cut.Find("[data-testid='tenants-global-admin-remove-live-region']").GetAttribute("aria-live").ShouldBe("polite");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
                .ShouldNotContain("target-admin");
        });
    }

    [Fact]
    public void RemoveReconciliationChecksEveryPageBeforeConfirmingTargetAbsence()
    {
        GlobalAdministratorsSnapshot page1 = PagedSnapshot(
            "projection-v1", null, "cursor-page-2", true, "target-admin");
        GlobalAdministratorsSnapshot page2 = PagedSnapshot(
            "projection-v1", "cursor-page-2", null, false, "other-admin");
        GlobalAdministratorsSnapshot changedPage1 = PagedSnapshot(
            "projection-v2", null, "cursor-page-2", true, "other-admin");
        GlobalAdministratorsSnapshot changedPage2 = PagedSnapshot(
            "projection-v2", "cursor-page-2", null, false, "target-admin");
        var queryGateway = new StubTenantQueryGateway(
            page1,
            page1,
            page2,
            page1,
            page2,
            page1,
            page2,
            changedPage1,
            changedPage2);
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            GlobalAdministratorRemoveCommandSnapshot state =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
            state.State.ShouldBe(
                TenantCommandLifecycleState.ProjectionPending,
                $"Key: {state.SafeMessageKey}; calls: {queryGateway.GlobalAdministratorCalls}; dispatches: {commandGateway.RemoveGlobalAdministratorCalls}; statuses: {commandGateway.StatusHandles.Count}; cursors: {string.Join(",", queryGateway.Requests.Select(static request => request.Cursor ?? "<first>"))}");
            cut.Find("[data-testid='tenants-global-admin-remove-safe-message']")
                .TextContent.ShouldContain("still shows the target", Case.Insensitive);
        });
        queryGateway.GlobalAdministratorCalls.ShouldBe(9);
        queryGateway.Requests[^1].Cursor.ShouldBe("cursor-page-2");
        cut.FindAll("[data-testid='tenants-global-admins-user-id']")
            .Select(static row => row.TextContent).ShouldContain("target-admin");
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public void RemoveReconciliationConfirmsCompleteAdvancedMultipageTargetAbsenceOnce()
    {
        GlobalAdministratorsSnapshot page1 = PagedSnapshot(
            "projection-v1", null, "cursor-page-2", true, "target-admin");
        GlobalAdministratorsSnapshot page2 = PagedSnapshot(
            "projection-v1", "cursor-page-2", null, false, "other-admin");
        GlobalAdministratorsSnapshot changedPage1 = PagedSnapshot(
            "projection-v2", null, "cursor-page-2", true, "other-admin");
        GlobalAdministratorsSnapshot changedPage2 = PagedSnapshot(
            "projection-v2", "cursor-page-2", null, false, "another-admin");
        var queryGateway = new StubTenantQueryGateway(
            page1, page1, page2, page1, page2, page1, page2,
            changedPage1, changedPage2, changedPage1);
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
                .State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            queryGateway.GlobalAdministratorCalls.ShouldBeGreaterThanOrEqualTo(9);
        });
        queryGateway.Requests[8].Cursor.ShouldBe("cursor-page-2");
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_grant_does_not_cancel_in_flight_remove_status_tracking()
    {
        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.SupportsGlobalAdministratorDispatch.Returns(true);
        commandGateway.SupportsTrackedGlobalAdministratorDispatch.Returns(true);
        commandGateway.SupportsTrackedGlobalAdministratorRemoveDispatch.Returns(true);
        commandGateway.SupportsCommandStatusLookup.Returns(true);
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken removeStatusToken = default;
        commandGateway
            .RemoveGlobalAdministratorTrackedAsync(
                Arg.Any<RemoveGlobalAdministrator>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(
                call.ArgAt<string>(1),
                "correlation-remove")));
        commandGateway
            .GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                removeStatusToken = call.ArgAt<CancellationToken>(1);
                return pendingStatus.Task;
            });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                null,
                false,
                "\"etag\"",
                ReadModelFreshnessState.Current) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                Lifecycle = ProjectionLifecycleState.Current,
                IsCompleteEvidence = true,
            }));
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("grant-candidate");
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        Task removeSubmit = cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => removeStatusToken.CanBeCanceled.ShouldBeTrue());

        cut.Find("[data-testid='tenants-global-admin-grant-cancel']").Click();

        removeStatusToken.IsCancellationRequested.ShouldBeFalse();
        pendingStatus.SetResult(TenantCommandStatusResult.Unknown("Status remains pending."));
        await removeSubmit;
        cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent
            .ShouldNotContain("No global administrator remove command");
    }

    [Theory]
    [InlineData("LastGlobalAdministrator", "last global administrator")]
    [InlineData("GlobalAdministratorNotFound", "exact administrator target")]
    public void Remove_rejection_keeps_last_confirmed_rows_without_success_or_member_copy(
        string rejectionCode,
        string expectedText)
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Rejected(expectedText, rejectionCode),
        });
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent.ShouldContain("rejected", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-remove-safe-message']").TextContent.ShouldContain(expectedText, Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-remove-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
                .ShouldContain("target-admin");
            // Visible text only — avoids the Fluent success-color token false positive (see VisibleText).
            cut.VisibleText().ShouldNotContain("success", Case.Insensitive);
            cut.Markup.ShouldNotContain("remove member", Case.Insensitive);
        });
    }

    [Fact]
    public void Blank_keyboard_form_submission_keeps_command_local_and_focuses_user_id_recovery()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admin-grant-submit']").GetAttribute("type").ShouldBe("submit");
        cut.Find("[data-testid='tenants-global-admin-grant-validation']").TextContent.ShouldContain("User id is required");
        cut.Find("[data-testid='tenants-global-admin-grant-validation']").GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']")
            .GetAttribute("aria-describedby")
            .ShouldNotBeNull()
            .ShouldContain("tenants-global-admin-grant-validation");
        cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void Unsupported_grant_user_ids_stay_local_and_explain_the_supported_boundary()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        foreach (string unsupportedUserId in new[] { "target\u0001user", new string('u', 257) })
        {
            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change(unsupportedUserId);
            cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

            cut.Find("[data-testid='tenants-global-admin-grant-validation']").TextContent.ShouldContain("256");
        }

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void Grant_user_id_input_does_not_expose_maxlength_truncation()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").GetAttribute("maxlength").ShouldBeNull();
    }

    [Fact]
    public void Grant_submission_confirms_only_after_projection_requery_contains_target_user()
    {
        GlobalAdministratorsSnapshot baseline = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag-1\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "projection-v1",
        };
        GlobalAdministratorsSnapshot confirmed = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("target-user", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag-2\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "projection-v2",
        };
        var queryGateway = new StubTenantQueryGateway(
            baseline,
            baseline,
            baseline,
            confirmed,
            confirmed);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true));
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.WaitForAssertion(() =>
        {
            commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
            commandGateway.Requests.ShouldHaveSingleItem().UserId.ShouldBe("target-user");
            TenantCommandTrackingHandle handle = commandGateway.StatusHandles.ShouldHaveSingleItem();
            handle.MessageId.ShouldBe(commandGateway.GrantMessageIds.ShouldHaveSingleItem());
            handle.CorrelationId.ShouldBe("correlation-grant");
            handle.AggregateId.ShouldBe("global-administrators");
            queryGateway.GlobalAdministratorCalls.ShouldBe(5);
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("Projection confirmed the target user", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("polite");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
                .ShouldContain("target-user");
        });
    }

    [Fact]
    public void GrantPreviewRendersTenBffFactsExactCountsAndModalIsolation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        const string target = "  CaseSensitive/Target.01  ";
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a", "admin-b"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change(target);
        OpenGrantPreview(cut);

        IElement preview = cut.Find("[data-testid='tenants-global-admin-grant-preview']");
        preview.GetAttribute("role").ShouldBe("dialog");
        preview.GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("[data-testid='tenants-global-admin-grant-preview-target']").TextContent.ShouldContain(target);
        cut.Find("[data-testid='tenants-global-admin-grant-preview-counts']").TextContent.ShouldContain("2");
        cut.Find("[data-testid='tenants-global-admin-grant-preview-counts']").TextContent.ShouldContain("3");
        cut.WaitForAssertion(() => FocusedElementIds()[^1].ShouldBe(
            CapturedChildElementReferenceId(cut.Instance, "_grantAcknowledgementElement")));
        string[] factSelectors =
        [
            "scope",
            "target",
            "counts",
            "authority-change",
            "freshness",
            "recovery",
            "audit",
            "context",
            "known-consequences",
            "known-unknowns",
        ];
        foreach (string selector in factSelectors)
        {
            _ = cut.Find($"[data-testid='tenants-global-admin-grant-preview-{selector}']");
        }
        cut.Find("[data-testid='tenants-global-admin-grant-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admins-return']").GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find("[data-testid='tenants-global-admins-area']").HasAttribute("inert").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admins-area']").GetAttribute("aria-hidden").ShouldBe("true");
        preview.Closest("[data-testid='tenants-global-admins-area']").ShouldBeNull();
        cut.Find("[data-testid='tenants-global-admins-list']").HasAttribute("inert").ShouldBeTrue();
        cut.FindAll("[data-testid='tenants-global-admin-remove']")
            .ShouldAllBe(static button => button.HasAttribute("disabled"));
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);

        cut.Find("[data-testid='tenants-global-admin-grant-focus-end']")
            .TriggerEvent("onfocus", new FocusEventArgs());
        FocusedElementIds()[^1].ShouldBe(CapturedChildElementReferenceId(
            cut.Instance,
            "_grantAcknowledgementElement"));
        cut.Find("[data-testid='tenants-global-admin-grant-focus-start']")
            .TriggerEvent("onfocus", new FocusEventArgs());
        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(
            cut.Instance,
            "_grantPreviewCancelElement"));

        preview.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(
            cut.Instance,
            "_grantLauncherElement"));
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change(target);
        OpenGrantPreview(cut);
        cut.Find("[data-testid='tenants-global-admin-grant-preview-cancel']").Click();
        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(
            cut.Instance,
            "_grantLauncherElement"));
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public async Task IdentityDisplayEscapesPresentationSurfacesWhileCopyAndDispatchKeepOriginalTargets()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        const string formatIdentity = "listed\u200Duser";
        const string literalEscapeIdentity = @"listed\{U+200D}user";
        const string grantTarget = "target\u200Duser";
        const string encodedFormatIdentity = @"listed\{U+200D}user";
        var queryGateway = new StubTenantQueryGateway(
            ComponentReady("projection-v1", formatIdentity, literalEscapeIdentity))
        {
            RepeatLastResponse = true,
        };
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Failed("remove failed for test"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop clipboard = JSInterop.SetupModule("./js/tenantsClipboard.js");
        JSRuntimeInvocationHandler copy = clipboard.SetupVoid("writeText", formatIdentity).SetVoidResult();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        IReadOnlyList<IElement> renderedIdentities = cut.FindAll("[data-testid='tenants-global-admins-user-id']");
        renderedIdentities
            .Select(static element => element.TextContent)
            .ShouldBe([encodedFormatIdentity, @"listed\\{U+200D}user"], ignoreOrder: true);
        IElement encodedRowIdentity = renderedIdentities.Single(element => element.TextContent == encodedFormatIdentity);
        encodedRowIdentity.GetAttribute("title").ShouldBe(encodedFormatIdentity);
        encodedRowIdentity.GetAttribute("aria-label")
            .ShouldBe($"Global administrator identifier {encodedFormatIdentity}");
        IElement copyButton = encodedRowIdentity.ParentElement!
            .QuerySelector("[data-surface-testid='tenants-global-admins-copy-user-id']")!;
        copyButton.GetAttribute("aria-label")
            .ShouldBe($"Copy global administrator identifier {encodedFormatIdentity}");
        copyButton.Click();
        cut.WaitForAssertion(() => copy.Invocations.Count.ShouldBe(1));
        copy.Invocations.Single().Arguments[0].ShouldBe(formatIdentity);

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change(grantTarget);
        OpenGrantPreview(cut);
        cut.Find("[data-testid='tenants-global-admin-grant-preview-target']")
            .TextContent.ShouldContain(@"target\{U+200D}user");
        cut.Find("[data-testid='tenants-global-admin-grant-acknowledge']").Change(true);
        cut.Find("[data-testid='tenants-global-admin-grant-confirm']").Click();

        cut.WaitForAssertion(() => commandGateway.Requests.ShouldHaveSingleItem());
        commandGateway.Requests[0].UserId.ShouldBe(grantTarget);

        OpenRemovePreview(cut, formatIdentity);
        cut.Find("[data-testid='tenants-global-admin-remove-target']")
            .TextContent.ShouldContain(encodedFormatIdentity);
        IElement acknowledgement = cut.Find("[data-testid='tenants-global-admin-remove-acknowledge']");
        string acknowledgementLabel = acknowledgement.ParentElement.ShouldNotBeNull().TextContent;
        acknowledgementLabel.ShouldContain(formatIdentity);
        acknowledgementLabel.ShouldNotContain(encodedFormatIdentity);
        acknowledgement.Change(formatIdentity);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-global-admin-remove-acknowledge']").Change(encodedFormatIdentity);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeTrue();
        AcknowledgeRemovePreview(cut, formatIdentity);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        commandGateway.RemoveRequests.ShouldHaveSingleItem().UserId.ShouldBe(formatIdentity);
    }

    [Fact]
    public void OpenGrantPreviewExplainsLivePrerequisiteLoss()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);

        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);

        cut.WaitForAssertion(() =>
        {
            IElement confirm = cut.Find("[data-testid='tenants-global-admin-grant-confirm']");
            confirm.HasAttribute("disabled").ShouldBeTrue();
            confirm.GetAttribute("aria-describedby").ShouldBe(
                "tenants-global-admin-grant-preview-unavailable-reason tenants-global-admin-grant-preview-unavailable-recovery");
            cut.Find("[data-testid='tenants-global-admin-grant-preview-unavailable-reason']")
                .TextContent.ShouldContain("safe tablet or desktop viewport", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-preview-unavailable-recovery']")
                .TextContent.ShouldContain("measured tablet or desktop viewport", Case.Insensitive);
        });
    }

    [Fact]
    public void IdentityRowsTokenizeControlsDefaultIgnorablesAndAccessibleWhitespace()
    {
        const string controlIdentity = "listed\u0001user";
        const string defaultIgnorableIdentity = "listed\u034F\uFE0Fuser";
        const string whitespaceIdentity = "  listed  user ";
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady(
                "projection-v1",
                controlIdentity,
                defaultIgnorableIdentity,
                whitespaceIdentity)));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        IReadOnlyList<IElement> identities = cut.FindAll("[data-testid='tenants-global-admins-user-id']");
        identities.Select(static element => element.TextContent).ShouldBe(
            [@"listed\{U+0001}user", @"listed\{U+034F}\{U+FE0F}user", whitespaceIdentity],
            ignoreOrder: true);
        IElement whitespaceRow = identities.Single(element => element.TextContent == whitespaceIdentity);
        whitespaceRow.GetAttribute("title").ShouldBe(whitespaceIdentity);
        whitespaceRow.GetAttribute("aria-label").ShouldBe(
            @"Global administrator identifier \{U+0020}\{U+0020}listed\{U+0020}\{U+0020}user\{U+0020}");
        whitespaceRow.ParentElement!
            .QuerySelector("[data-surface-testid='tenants-global-admins-copy-user-id']")!
            .GetAttribute("aria-label").ShouldBe(
                @"Copy global administrator identifier \{U+0020}\{U+0020}listed\{U+0020}\{U+0020}user\{U+0020}");
    }

    [Theory]
    [InlineData('\uD800')]
    [InlineData('\uDC00')]
    public async Task LoneSurrogateGrantTargetIsRejectedBeforePreviewCompositionOrDispatch(char surrogate)
    {
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized);
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        typeof(GlobalAdministratorsPage)
            .GetField("_grantUserId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, new string(surrogate, 1));

        Task submit = (Task)(typeof(GlobalAdministratorsPage)
            .GetMethod("SubmitGrantAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null)
            ?? throw new InvalidOperationException("SubmitGrantAsync did not return a task."));
        await submit;

        cut.Find("[data-testid='tenants-global-admin-grant-validation']").TextContent
            .ShouldContain("supported user id", Case.Insensitive);
        composition.GrantPreviewCompositionCount.ShouldBe(0);
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
    }

    [Fact]
    public void GrantLocalizationReadinessFailureShowsItsSpecificReasonAndRecovery()
    {
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            isGrantPreviewReady: false)
        {
            GlobalAdministratorGrantPreviewUnavailableReasonKey =
                "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization",
            GlobalAdministratorGrantPreviewRecoveryKey =
                "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization",
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
            .ShouldContain("Localized consequence facts are incomplete", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-recovery']").TextContent
            .ShouldContain("Restore the complete localized consequence resources", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-submit']")
            .HasAttribute("disabled").ShouldBeTrue();
    }

    [Theory]
    [InlineData("   ", "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization")]
    [InlineData("Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization", "   ")]
    public void GrantLocalizationOverrideUsesOnlyACompleteNonblankPair(string reasonKey, string recoveryKey)
    {
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            isGrantPreviewReady: false)
        {
            GlobalAdministratorGrantPreviewUnavailableReasonKey = reasonKey,
            GlobalAdministratorGrantPreviewRecoveryKey = recoveryKey,
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
            .ShouldContain("safety flow is not ready", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-recovery']").TextContent
            .ShouldContain("dedicated grant preview", Case.Insensitive);
    }

    [Fact]
    public void GrantPreviewCompositionExceptionRetainsRowsAndShowsRecoveryWithoutLease()
    {
        var commandGateway = new StubTenantCommandGateway();
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized)
        {
            GrantPreviewException = new InvalidOperationException("unsafe composition detail"),
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

        cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent
            .ShouldContain("Complete, current, versioned");
        cut.Find("[data-testid='tenants-global-admin-grant-safe-recovery']").TextContent
            .ShouldContain("Refresh the complete fixed-scope projection");
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-a");
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        cut.Markup.ShouldNotContain("unsafe composition detail");
    }

    [Fact]
    public void AuthorityLossAtConfirmationCollapsesRowsAndAbandonsPreviewLease()
    {
        var commandGateway = new StubTenantCommandGateway();
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;

        cut.Find("[data-testid='tenants-global-admin-grant-confirm']").Click();

        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        cut.Markup.ShouldNotContain("admin-a");
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentConfirmationDispatchesExactlyOnceAndKeepsMarkedLeaseOwned()
    {
        var submissionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-grant"))
        {
            SubmissionGate = submissionGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        IElement confirm = cut.Find("[data-testid='tenants-global-admin-grant-confirm']");

        Task first = confirm.ClickAsync(new MouseEventArgs());
        await commandGateway.SubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        GlobalAdministratorGrantCommandSnapshot activeSnapshot =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        TenantAggregateCommandLease activeLease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");
        Task disabledLaunch = (Task)(typeof(GlobalAdministratorsPage)
            .GetMethod("SubmitGrantAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null)
            ?? throw new InvalidOperationException("SubmitGrantAsync did not return a task."));
        await disabledLaunch;
        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .ShouldBeSameAs(activeSnapshot);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease")
            .ShouldBeSameAs(activeLease);
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
        Task duplicate = (Task)(typeof(GlobalAdministratorsPage)
            .GetMethod("ConfirmGrantAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null)
            ?? throw new InvalidOperationException("ConfirmGrantAsync did not return a task."));
        await duplicate;

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
        commandGateway.GrantMessageIds.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        submissionGate.SetResult();
        await first;
    }

    [Fact]
    public async Task SupersededConfirmationCannotDispatchLaterPreview()
    {
        GlobalAdministratorsSnapshot evidence = ComponentReady("projection-v1", "admin-a");
        var queryGateway = new StubTenantQueryGateway(evidence, evidence);
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("first-target");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);

        var revalidation = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        queryGateway.QueueResponse(revalidation.Task);
        Task oldConfirmation = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        await WaitUntilAsync(() => queryGateway.GlobalAdministratorCalls == 3, TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-global-admin-grant-preview-cancel']").Click();
        queryGateway.QueueResponse(Task.FromResult(evidence));
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("second-target");
        OpenGrantPreview(cut);
        revalidation.SetResult(evidence);
        await oldConfirmation;

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admin-grant-preview-target']").TextContent
            .ShouldContain("second-target");
        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .MessageId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TargetReplacementInvalidatesOpenUndispatchedPreviewAndReleasesItsExactLease()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-a");
        OpenGrantPreview(cut);
        TenantAggregateCommandLease previewLease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");

        // The field prevents new edits while a preview is open. Invoke its callback below to simulate
        // an input event already queued before it became disabled and exercise the stale-event guard.
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']")
            .HasAttribute("disabled").ShouldBeTrue();

        FluentTextInput targetInput = cut.FindComponents<FluentTextInput>()
            .Select(rendered => rendered.Instance)
            .Single(instance => instance.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? actual)
                && string.Equals(actual as string, "tenants-global-admin-grant-user-id", StringComparison.Ordinal));
        await cut.InvokeAsync(() => targetInput.ValueChanged.InvokeAsync("target-b"));

        GlobalAdministratorGrantCommandSnapshot invalidated =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        invalidated.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        invalidated.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Preview.Invalidated");
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']")
            .HasAttribute("disabled").ShouldBeFalse();
        PrivateField<TenantAggregateCommandLease?>(cut.Instance, "_grantAdmissionLease").ShouldBeNull();
        previewLease.TryAbandonBeforeDispatch(new object()).ShouldBeFalse();
        TenantAggregateCommandAdmissionGate gate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? replacement).ShouldBeTrue();
        replacement!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("tracked-capability")]
    [InlineData("preview-readiness")]
    [InlineData("viewport")]
    [InlineData("authorization")]
    [InlineData("acknowledgement")]
    public async Task FinalRendererPrerequisiteLossReleasesExactUndispatchedLeaseAndRequiresFreshReview(
        string prerequisite)
    {
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized);
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        TenantAggregateCommandLease previewLease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");
        var revalidationGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.GrantPreviewGate = revalidationGate;
        composition.GrantPreviewCompletedSignal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task confirmation = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        await composition.GrantPreviewEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        int trackedCapabilityReads = 0;
        switch (prerequisite)
        {
            case "tracked-capability":
                commandGateway.TrackedDispatchSupportProvider = () =>
                    Interlocked.Increment(ref trackedCapabilityReads) == 1;
                break;
            case "preview-readiness":
                composition.IsGlobalAdministratorGrantPreviewReady = false;
                break;
            case "viewport":
                Services.GetRequiredService<TenantHighImpactViewportObservation>()
                    .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
                break;
            case "authorization":
                typeof(GlobalAdministratorsPage)
                    .GetField("_authorizationReflection", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(cut.Instance, TenantLifecycleAuthorizationReflectionState.MissingPermission);
                break;
            default:
                typeof(GlobalAdministratorsPage)
                    .GetField("_grantAcknowledged", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(cut.Instance, false);
                break;
        }

        revalidationGate.SetResult();
        await confirmation.WaitAsync(TimeSpan.FromSeconds(5));

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        GlobalAdministratorGrantCommandSnapshot invalidated =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        invalidated.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        invalidated.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Preview.Invalidated");
        invalidated.SafeRecoveryKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh");
        PrivateField<bool>(cut.Instance, "_grantAcknowledged").ShouldBeFalse();
        PrivateField<TenantAggregateCommandLease?>(cut.Instance, "_grantAdmissionLease").ShouldBeNull();
        previewLease.IsDispatchMarked.ShouldBeFalse();

        commandGateway.TrackedDispatchSupportProvider = null;
        commandGateway.SupportsTrackedGlobalAdministratorDispatch = true;
        composition.IsGlobalAdministratorGrantPreviewReady = true;
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        typeof(GlobalAdministratorsPage)
            .GetField("_authorizationReflection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.Render();
        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admin-grant-confirm']").ShouldBeEmpty();

        TenantAggregateCommandAdmissionGate gate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? replacement).ShouldBeTrue();
        replacement!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
    }

    [Theory]
    [InlineData("version")]
    [InlineData("count")]
    [InlineData("completeness")]
    [InlineData("target")]
    public void ConfirmationProjectionChangesDispatchNothingAndRequireFreshReview(string change)
    {
        GlobalAdministratorsSnapshot baseline = ComponentReady("projection-v1", "admin-a");
        GlobalAdministratorsSnapshot changed = change switch
        {
            "version" => ComponentReady("projection-v2", "admin-a"),
            "count" => ComponentReady("projection-v1", "admin-a", "admin-b"),
            "target" => ComponentReady("projection-v1", "admin-a", "target-admin"),
            _ => ComponentReady("projection-v1", "admin-a") with
            {
                Freshness = ReadModelFreshnessState.Stale,
                IsCompleteEvidence = false,
            },
        };
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            baseline,
            baseline,
            changed)
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        TenantAggregateCommandLease previewLease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");

        cut.Find("[data-testid='tenants-global-admin-grant-confirm']").Click();

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        GlobalAdministratorGrantCommandSnapshot invalidated =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        invalidated.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        invalidated.SafeMessageKey.ShouldNotBeNullOrWhiteSpace();
        invalidated.SafeRecoveryKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh");
        PrivateField<TenantAggregateCommandLease?>(cut.Instance, "_grantAdmissionLease").ShouldBeNull();
        previewLease.IsDispatchMarked.ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
    }

    [Fact]
    public async Task RendererReplacementAutomaticallyRedispatchesSameAmbiguousGrantId()
    {
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        first.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(first);
        string messageId = commandGateway.GrantMessageIds.ShouldHaveSingleItem();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        var retryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.SubmissionGate = retryGate;
        IRenderedComponent<GlobalAdministratorsPage> retryingReplacement = Render<GlobalAdministratorsPage>();
        await commandGateway.SubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantAggregateCommandLease retryLease =
            PrivateField<TenantAggregateCommandLease>(retryingReplacement.Instance, "_grantAdmissionLease");
        retryLease.IsReconciliationDispatchInFlight.ShouldBeTrue();

        await retryingReplacement.InvokeAsync(
            async () => await retryingReplacement.Instance.DisposeAsync());
        IRenderedComponent<GlobalAdministratorsPage> finalReplacement = Render<GlobalAdministratorsPage>();

        // The durable dispatch token outlives the renderer that started this POST. Adoption by another
        // renderer must observe that token before it can begin a mutation, otherwise replacement races can
        // issue the same retained command concurrently.
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(2);
        commandGateway.GrantMessageIds.ShouldBe([messageId, messageId]);
        PrivateField<TenantAggregateCommandLease>(finalReplacement.Instance, "_grantAdmissionLease")
            .ShouldBeSameAs(retryLease);
        retryLease.IsReconciliationDispatchInFlight.ShouldBeTrue();

        retryGate.SetResult();
        await WaitUntilAsync(() => !retryLease.IsReconciliationDispatchInFlight, TimeSpan.FromSeconds(5));
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorGrantCommandSnapshot>(finalReplacement.Instance, "_grantSnapshot")
                .IsSubmissionAmbiguous,
            TimeSpan.FromSeconds(5));
        await finalReplacement.InvokeAsync(static () => Task.CompletedTask);

        commandGateway.GrantMessageIds.ShouldAllBe(id => id == messageId);
        GlobalAdministratorGrantCommandSnapshot adopted =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(finalReplacement.Instance, "_grantSnapshot");
        adopted.MessageId.ShouldBe(messageId);
        adopted.IsSubmissionAmbiguous.ShouldBeTrue();
        adopted.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous");
        adopted.BaselineProjectionVersion.ShouldBe("projection-v1");
        adopted.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        adopted.SafeRecoveryKey.ShouldBe("Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery");
        finalReplacement.Find("[data-testid='tenants-global-admin-grant-safe-recovery']").TextContent
            .ShouldContain("retained command identity", Case.Insensitive);
        IElement retry = finalReplacement.Find("[data-testid='tenants-global-admin-grant-refresh']");
        retry.TextContent.ShouldContain("Retry delivery", Case.Insensitive);
        finalReplacement.WaitForAssertion(() =>
            finalReplacement.Find("[data-testid='tenants-global-admin-grant-refresh']")
                .HasAttribute("disabled").ShouldBeFalse());
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(2);

        await retry.ClickAsync(new MouseEventArgs());
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(3);
        commandGateway.GrantMessageIds.ShouldBe([messageId, messageId, messageId]);
    }

    [Fact]
    public void AmbiguousGrantDeliveryRetryIsWithdrawnWhenItsDispatchPrerequisitesLapse()
    {
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.Find("[data-testid='tenants-global-admin-grant-refresh']").TextContent
            .ShouldContain("Retry delivery", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-refresh']")
            .HasAttribute("disabled").ShouldBeFalse();

        // A safe measured viewport is one of the live prerequisites the redispatch itself rechecks. Once it
        // lapses the action must stop advertising a retry it would then refuse, instead of accepting a click
        // that returns silently.
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        cut.Render();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-grant-refresh']")
            .HasAttribute("disabled").ShouldBeTrue());
        IElement withdrawnRetry = cut.Find("[data-testid='tenants-global-admin-grant-refresh']");
        withdrawnRetry.TextContent.ShouldContain("Delivery retry unavailable", Case.Insensitive);
        withdrawnRetry.GetAttribute("aria-describedby").ShouldBe(
            "tenants-global-admin-grant-delivery-retry-withdrawn-reason "
            + "tenants-global-admin-grant-delivery-retry-withdrawn-recovery "
            + "tenants-global-admin-grant-safe-message "
            + "tenants-global-admin-grant-safe-recovery");
        foreach (string describedId in withdrawnRetry.GetAttribute("aria-describedby")!.Split(' '))
        {
            cut.Find($"#{describedId}").TextContent.ShouldNotBeNullOrWhiteSpace();
        }
        cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent
            .ShouldContain("exact retained grant identity", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-delivery-retry-withdrawn-reason']")
            .TextContent.ShouldContain("temporarily withdrawn", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-delivery-retry-withdrawn-recovery']")
            .TextContent.ShouldContain("same retained command", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admin-grant-live-region']").TextContent
            .ShouldNotContain("temporarily withdrawn", Case.Insensitive);
        cut.Markup.Split("Delivery retry is temporarily withdrawn", StringSplitOptions.None)
            .Length.ShouldBe(2, "the visible withdrawal reason must not be duplicated in the live region");
        GlobalAdministratorGrantCommandSnapshot withdrawn =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        withdrawn.IsDeliveryRetryWithdrawn.ShouldBeTrue();
        withdrawn.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch");
        withdrawn.MessageId.ShouldNotBeNullOrWhiteSpace();
        string messageId = withdrawn.MessageId;
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);

        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        cut.Render();

        cut.WaitForAssertion(() =>
        {
            IElement rearmedRetry = cut.Find("[data-testid='tenants-global-admin-grant-refresh']");
            rearmedRetry.HasAttribute("disabled").ShouldBeFalse();
            rearmedRetry.TextContent.ShouldContain("Retry delivery", Case.Insensitive);
            cut.FindAll("[data-testid='tenants-global-admin-grant-delivery-retry-withdrawn-reason']")
                .ShouldBeEmpty();
        });
        GlobalAdministratorGrantCommandSnapshot rearmed =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        rearmed.IsDeliveryRetryWithdrawn.ShouldBeFalse();
        rearmed.MessageId.ShouldBe(messageId);
        rearmed.SafeMessageKey.ShouldBe(withdrawn.SafeMessageKey);

        cut.Find("[data-testid='tenants-global-admin-grant-refresh']").Click();
        cut.WaitForAssertion(() => commandGateway.SetGlobalAdministratorCalls.ShouldBe(2));
        commandGateway.GrantMessageIds.ShouldAllBe(id => id == messageId);
    }

    [Fact]
    public async Task AmbiguousGrantDeliveryRetryPrerequisiteRaceSurfacesRecovery()
    {
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"));
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);
        GlobalAdministratorGrantCommandSnapshot before =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        var resolutionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = resolutionGate;

        Task click = cut.Find("[data-testid='tenants-global-admin-grant-refresh']")
            .ClickAsync(new MouseEventArgs());
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        resolutionGate.SetResult();
        await click.WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorGrantCommandSnapshot after =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        after.ShouldNotBeSameAs(before);
        after.MessageId.ShouldBe(before.MessageId);
        after.IsSubmissionAmbiguous.ShouldBeTrue();
        after.IsDeliveryRetryWithdrawn.ShouldBeTrue();
        after.SafeMessageKey.ShouldBe(before.SafeMessageKey);
        after.SafeMessage.ShouldBe(before.SafeMessage);
        after.SafeRecoveryKey.ShouldBe("Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery");
        after.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Assertive);
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public void QualifiedGrantConfirmationRefreshesCurrentPageInsteadOfRenderingCompletePopulation()
    {
        GlobalAdministratorsSnapshot page1 = PagedSnapshot(
            "projection-v1",
            requestCursor: null,
            nextCursor: "cursor-page-2",
            hasMore: true,
            "admin-page-1");
        GlobalAdministratorsSnapshot page2 = PagedSnapshot(
            "projection-v1",
            requestCursor: "cursor-page-2",
            nextCursor: null,
            hasMore: false,
            "admin-page-2");
        GlobalAdministratorsSnapshot updatedPage1 = PagedSnapshot(
            "projection-v2",
            requestCursor: null,
            nextCursor: "cursor-page-2",
            hasMore: true,
            "admin-page-1",
            "target-admin");
        GlobalAdministratorsSnapshot updatedPage2 = PagedSnapshot(
            "projection-v2",
            requestCursor: "cursor-page-2",
            nextCursor: null,
            hasMore: false,
            "admin-page-2");
        var queryGateway = new StubTenantQueryGateway(
            page1,
            page1,
            page2,
            page2,
            page1,
            page2,
            page1,
            page2,
            updatedPage1,
            updatedPage2,
            updatedPage2);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.EventsStored,
                EventCount: 1,
                HasVerifiedCommandIdentity: true));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-next']").Click();
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("admin-page-2");
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent
            .ShouldContain("Projection confirmed");
        cut.FindAll("[data-testid='tenants-global-admins-user-id']")
            .Select(static row => row.TextContent).ShouldBe(["admin-page-2"]);
        cut.Find("[data-testid='tenants-global-admins-previous']").HasAttribute("disabled").ShouldBeFalse();
        queryGateway.Requests.Last().Cursor.ShouldBe("cursor-page-2");
        queryGateway.GlobalAdministratorCalls.ShouldBe(11);
    }

    [Theory]
    [InlineData(false, true, "fixed-scope projection is current")]
    [InlineData(true, false, "dispatch, status, or requery")]
    public void Command_or_read_surface_unavailable_blocks_grant_without_command_submission(
        bool isReadSurfaceConnected,
        bool isCommandSurfaceConnected,
        string expectedReason)
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized,
            isReadSurfaceConnected,
            isCommandSurfaceConnected));
        var queryGateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true });
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        if (!isReadSurfaceConnected)
        {
            queryGateway.GlobalAdministratorCalls.ShouldBe(0);
            cut.Markup.ShouldNotContain("admin-1");
        }

        cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent.ShouldContain(expectedReason);
        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("No global administrator grant command");
        cut.Find("[data-testid='tenants-global-admin-grant-audit-state']").TextContent.ShouldContain("before command submission");
        cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("polite");
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation-", Case.Insensitive);
    }

    // A projection-backed response carrying only the legacy X-Hexalith-Is-Stale: false compatibility signal
    // resolves to Current freshness with no projection lifecycle evidence. That is honest for display, but it
    // is not projection-confirmed evidence and must not unlock platform-authority mutations. HasMore is true
    // so the removal population gate is satisfied on its own: without the lifecycle requirement, Remove would
    // render as an enabled launcher.
    [Fact]
    public void Current_freshness_without_projection_lifecycle_evidence_blocks_grant_and_remove()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current),
            ],
            nextCursor: "opaque-next",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Unknown }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admin-grant-unavailable-reason']").TextContent
            .ShouldContain("Grant is unavailable until the fixed-scope projection is current and versioned.");

        cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-global-admins-remove-reason']")
            .Select(static element => element.TextContent)
            .ShouldAllBe(static text => text.Contains("Remove is unavailable until the visible fixed-scope projection is current and versioned.", StringComparison.Ordinal));
    }

    [Fact]
    public void Cancel_grant_clears_literal_user_id_and_does_not_submit_command()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        cut.Find("[data-testid='tenants-global-admin-grant-cancel']").Click();

        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("No global administrator grant command");
        cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("polite");
        cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
            .ShouldNotContain("target-user");
    }

    [Fact]
    public void CompletedGrantWithoutTargetEvidenceRemainsPendingAndNotOptimistic()
    {
        GlobalAdministratorsSnapshot baseline = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag-1\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        };
        GlobalAdministratorsSnapshot advancedWithoutTarget = baseline with
        {
            ETag = "\"etag-2\"",
            ProjectionVersion = "component-test-v2",
        };
        var queryGateway = new StubTenantQueryGateway(
            baseline,
            baseline,
            baseline,
            advancedWithoutTarget);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("Projection pending");
            cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent.ShouldContain("did not confirm");
            cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("polite");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
                .ShouldNotContain("target-user");
        });
    }

    [Theory]
    [InlineData(CommandStatus.PublishFailed, "degraded", "Audit evidence is delayed;", "could not be published")]
    [InlineData(CommandStatus.TimedOut, "could not be verified", "Audit evidence is delayed;", "timed out")]
    public void Terminal_status_without_projection_confirmation_stays_distinct_and_assertive(
        CommandStatus status,
        string expectedStateText,
        string expectedAuditText,
        string expectedSafeMessage)
    {
        var queryGateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(
                status,
                "Status remained support-safe.",
                HasVerifiedCommandIdentity: true)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.WaitForAssertion(() =>
        {
            queryGateway.GlobalAdministratorCalls.ShouldBe(3);
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain(expectedStateText, Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-audit-state']").TextContent.ShouldContain(expectedAuditText);
            cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent.ShouldContain(
                expectedSafeMessage,
                Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-lifecycle']").GetAttribute("role").ShouldBe("alert");
            cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").Select(static element => element.TextContent)
                .ShouldNotContain("target-user");
        });
    }

    [Fact]
    public void ExistingGlobalAdministratorIsRejectedBeforeDispatch()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("existing-admin", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        });
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Rejected(
                "This user is already a global administrator. Refresh the platform authority projection before trying another action.",
                "GlobalAdministratorAlreadyExists"));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("existing-admin");
        cut.Find("[data-testid='tenants-global-admin-grant-form']").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("could not be verified", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent.ShouldContain("already present");
            cut.Find("[data-testid='tenants-global-admin-grant-safe-recovery']").TextContent.ShouldContain("confirmed rows unchanged");
            cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
            commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
            cut.Markup.ShouldNotContain("AlreadyApplied");
            // Visible text only — avoids the Fluent success-color token false positive (see VisibleText).
            cut.VisibleText().ShouldNotContain("success", Case.Insensitive);
        });
    }

    [Fact]
    public void Insufficient_permissions_rejection_uses_safe_platform_governance_copy()
    {
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway(
            TenantCommandSubmissionResult.RejectedWithKey(
                "Tenants.GlobalAdministrators.Grant.Submission.Rejected",
                "InsufficientPermissions")));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("rejected", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-safe-message']").TextContent.ShouldContain("platform governance");
            cut.Find("[data-testid='tenants-global-admin-grant-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
            cut.Markup.ShouldNotContain("tenant command succeeded", Case.Insensitive);
            cut.Markup.ShouldNotContain("target-user is now", Case.Insensitive);
            cut.Markup.ShouldNotContain("correlation-", Case.Insensitive);
        });
    }

    [Fact]
    public void Processing_grant_keeps_one_at_a_time_lock_and_remove_placeholder_unavailable()
    {
        var queryGateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Processing,
                HasVerifiedCommandIdentity: true)));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        PreviewAcknowledgeAndConfirmGrant(cut);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("accepted", Case.Insensitive);
            cut.Find("[data-testid='tenants-global-admin-grant-submit']").HasAttribute("disabled").ShouldBeTrue();
            cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent.ShouldContain("attempt is active");
        });
    }

    [Fact]
    public void Grant_resources_and_styles_cover_accessible_forced_colors_support_safe_states()
    {
        string projectRoot = ProjectRoot();
        string resourceRoot = Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Resources");
        string styles = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "GlobalAdministratorsPage.razor.css"));
        HashSet<string> englishKeys = ResourceKeys(Path.Combine(resourceRoot, "TenantsResources.resx"), "Tenants.GlobalAdministrators.Grant.");
        HashSet<string> frenchKeys = ResourceKeys(Path.Combine(resourceRoot, "TenantsResources.fr.resx"), "Tenants.GlobalAdministrators.Grant.");
        HashSet<string> englishRemoveKeys = ResourceKeys(Path.Combine(resourceRoot, "TenantsResources.resx"), "Tenants.GlobalAdministrators.Remove.");
        HashSet<string> frenchRemoveKeys = ResourceKeys(Path.Combine(resourceRoot, "TenantsResources.fr.resx"), "Tenants.GlobalAdministrators.Remove.");

        englishKeys.ShouldBe(frenchKeys);
        englishKeys.ShouldContain("Tenants.GlobalAdministrators.Grant.State.Confirmed");
        englishKeys.ShouldContain("Tenants.GlobalAdministrators.Grant.State.Rejected");
        englishKeys.ShouldContain("Tenants.GlobalAdministrators.Grant.State.UnableToVerify");
        englishKeys.ShouldContain("Tenants.GlobalAdministrators.Grant.Audit.AuditDelayed");
        englishKeys.ShouldContain("Tenants.GlobalAdministrators.Grant.Unavailable.CommandSurface");
        englishRemoveKeys.ShouldBe(frenchRemoveKeys);
        englishRemoveKeys.ShouldContain("Tenants.GlobalAdministrators.Remove.State.Confirmed");
        englishRemoveKeys.ShouldContain("Tenants.GlobalAdministrators.Remove.State.Rejected");
        englishRemoveKeys.ShouldContain("Tenants.GlobalAdministrators.Remove.Unavailable.LastAdmin");
        englishRemoveKeys.ShouldContain("Tenants.GlobalAdministrators.Remove.Preview.KnownUnknowns.Value");
        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain(".global-admins__grant-lifecycle:focus-visible");
        styles.ShouldContain(".global-admins__grant-state-symbol");
        styles.ShouldContain(".global-admins__remove-lifecycle:focus-visible");
        styles.ShouldContain(".global-admins__remove-state-symbol");
        styles.ShouldContain("@media (max-width: 42rem)");
        styles.ShouldContain("overflow-wrap: anywhere");

        // The per-row Remove launcher is a FluentButton, so it carries no scope attribute of its own, but it
        // renders inside a plain scoped ancestor -- ::deep is both necessary and sufficient there.
        styles.ShouldContain("::deep .global-admins__mutation-initiation");

        // The read-only notice is different: ::deep alone was NOT enough, because ::deep still compiles to
        // "[b-xxx] <selector>" and the FluentMessageBar had no ancestor rendered by this component to carry
        // the scope attribute. It is now hidden via its own plain-HTML host element, which does. Asserting
        // the host selector (and the absence of the ::deep form) keeps the inert construct from returning.
        styles.ShouldContain(".global-admins__mobile-readonly-host");
        styles.ShouldNotContain("::deep .global-admins__mobile-readonly ");
        styles.ShouldNotContain("::deep .global-admins__mobile-readonly{");
        styles.ShouldNotContain("::deep .global-admins__mobile-readonly {");

        // Both halves of the breakpoint, separately. A bare `ShouldContain(".global-admins__mobile-readonly-host")`
        // is satisfied by either the base `display: none` rule or the media-query `display: block` one, so
        // deleting the base rule -- which makes the read-only notice permanently visible on desktop, the
        // exact regression this replaced -- left the assertion green. Likewise, hoisting the
        // mutation-initiation rule out of the media query hides Grant and every Remove launcher at all
        // widths, also undetected.
        // Guarded: an unguarded IndexOf slice throws ArgumentOutOfRangeException when the media query is
        // renamed, which reports as an error rather than as the assertion failure it actually is.
        int breakpointIndex = styles.IndexOf("@media (max-width: 42rem)", StringComparison.Ordinal);
        breakpointIndex.ShouldBeGreaterThan(-1);
        string mobileBreakpoint = styles[breakpointIndex..];
        string beforeBreakpoint = styles[..breakpointIndex];

        beforeBreakpoint.Contains(".global-admins__mobile-readonly-host", StringComparison.Ordinal)
            .ShouldBeTrue("the notice must be hidden by default, or it is permanently visible on desktop");
        mobileBreakpoint.Contains(".global-admins__mobile-readonly-host", StringComparison.Ordinal)
            .ShouldBeTrue("the notice must be revealed at the mobile breakpoint");
        beforeBreakpoint.Contains(".global-admins__mobile-mutation-reason", StringComparison.Ordinal)
            .ShouldBeTrue("per-row mobile reasons must be hidden by default");
        mobileBreakpoint.Contains(".global-admins__mobile-mutation-reason", StringComparison.Ordinal)
            .ShouldBeTrue("per-row mobile reasons must be revealed at the mobile breakpoint");
        beforeBreakpoint.Contains("::deep .global-admins__mutation-initiation", StringComparison.Ordinal)
            .ShouldBeFalse("hoisting the launcher rule out of the media query hides mutation affordances at every width");
        mobileBreakpoint.Contains("::deep .global-admins__mutation-initiation", StringComparison.Ordinal)
            .ShouldBeTrue();
    }

    [Fact]
    public void GrantPreviewStylesPresentAsElevatedViewportBoundedModal()
    {
        string styles = File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Pages",
            "GlobalAdministratorsPage.razor.css"));
        int ruleStart = styles.IndexOf(".global-admins__grant-preview {", StringComparison.Ordinal);
        ruleStart.ShouldBeGreaterThan(-1);
        int ruleEnd = styles.IndexOf('}', ruleStart);
        ruleEnd.ShouldBeGreaterThan(ruleStart);
        string modalRule = styles[ruleStart..ruleEnd];

        modalRule.ShouldContain("background: var(--colorNeutralBackground1)");
        modalRule.ShouldContain("box-sizing: border-box");
        modalRule.ShouldContain("box-shadow: var(--shadow16)");
        modalRule.ShouldContain("inset-block-start: 50%");
        modalRule.ShouldContain("inset-inline-start: 50%");
        modalRule.ShouldContain("max-block-size: 90vh");
        modalRule.ShouldContain("overflow-y: auto");
        modalRule.ShouldContain("position: fixed");
        modalRule.ShouldContain("transform: translate(-50%, -50%)");
        int removeRuleStart = styles.IndexOf(".global-admins__remove-preview {", StringComparison.Ordinal);
        removeRuleStart.ShouldBeGreaterThan(-1);
        int removeRuleEnd = styles.IndexOf('}', removeRuleStart);
        removeRuleEnd.ShouldBeGreaterThan(removeRuleStart);
        string removeModalRule = styles[removeRuleStart..removeRuleEnd];
        removeModalRule.ShouldContain("background: var(--colorNeutralBackground1)");
        removeModalRule.ShouldContain("box-sizing: border-box");
        removeModalRule.ShouldContain("box-shadow: var(--shadow16)");
        removeModalRule.ShouldContain("inset-block-start: 50%");
        removeModalRule.ShouldContain("inset-inline-start: 50%");
        removeModalRule.ShouldContain("max-block-size: 90vh");
        removeModalRule.ShouldContain("overflow-y: auto");
        removeModalRule.ShouldContain("position: fixed");
        removeModalRule.ShouldContain("transform: translate(-50%, -50%)");
        removeModalRule.ShouldContain("z-index: 1000");
        styles.ShouldContain("border: 2px solid CanvasText");
        int sentinelRuleStart = styles.IndexOf(
            ".global-admins__grant-focus-sentinel,",
            StringComparison.Ordinal);
        sentinelRuleStart.ShouldBeGreaterThan(-1);
        int sentinelRuleEnd = styles.IndexOf('}', sentinelRuleStart);
        sentinelRuleEnd.ShouldBeGreaterThan(sentinelRuleStart);
        string sentinelRule = styles[sentinelRuleStart..sentinelRuleEnd];
        sentinelRule.ShouldContain("clip: rect(0 0 0 0)");
        sentinelRule.ShouldContain("clip-path: inset(50%)");
        sentinelRule.ShouldContain("overflow: hidden");
        sentinelRule.ShouldContain("position: absolute");
        sentinelRule.ShouldContain(".global-admins__remove-focus-sentinel");
        styles.ShouldContain(".global-admins__remove-preview-grid {");
        styles.ShouldNotContain(".global-admins__remove-preview-grid div");
        int identityRuleStart = styles.IndexOf(".global-admins__identity-value {", StringComparison.Ordinal);
        identityRuleStart.ShouldBeGreaterThan(-1);
        int identityRuleEnd = styles.IndexOf('}', identityRuleStart);
        identityRuleEnd.ShouldBeGreaterThan(identityRuleStart);
        string identityRule = styles[identityRuleStart..identityRuleEnd];
        identityRule.ShouldContain("overflow-wrap: anywhere");
        identityRule.ShouldContain("white-space: pre-wrap");
        identityRule.ShouldNotContain("text-overflow: ellipsis");
        identityRule.ShouldNotContain("overflow: hidden");
    }

    [Fact]
    public void RemovePreviewUsesFluentControlsAndIsolatesTheBackground()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);

        IElement dialog = cut.Find("[data-testid='tenants-global-admin-remove-preview']");
        dialog.ClassList.ShouldContain("global-admins__remove-preview");
        dialog.GetAttribute("role").ShouldBe("dialog");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("[data-testid='tenants-global-admins-area']").HasAttribute("inert").ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admins-area']").GetAttribute("aria-hidden").ShouldBe("true");
        cut.FindComponents<FluentButton>()
            .ShouldContain(component => component.Markup.Contains(
                "data-testid=\"tenants-global-admin-remove-cancel\"",
                StringComparison.Ordinal));
        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-end']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']")
            .ClassList.ShouldContain("global-admins__remove-focus-sentinel");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-end']")
            .ClassList.ShouldContain("global-admins__remove-focus-sentinel");
        string source = ReadGlobalAdministratorsPageSource();
        int removeModalStart = source.IndexOf(
            "@if (_removeSnapshot.State is TenantCommandLifecycleState.Previewed",
            StringComparison.Ordinal);
        int grantModalStart = source.IndexOf(
            "@if (_grantSnapshot.State is TenantCommandLifecycleState.Previewed",
            removeModalStart,
            StringComparison.Ordinal);
        string removeModalSource = source[removeModalStart..grantModalStart];
        removeModalSource.ShouldContain("<FluentButton");
        removeModalSource.ShouldNotContain("<fluent-button");

        dialog.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-global-admins-area']").HasAttribute("inert").ShouldBeFalse();
        cut.Find("[data-testid='tenants-global-admins-area']").HasAttribute("aria-hidden").ShouldBeFalse();
    }

    [Fact]
    public void RemoveFocusSentinelsRouteToExactInteractiveDestinations()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        var focusCancel = module.Setup<bool>(
            "focusElementById",
            "tenants-global-admin-remove-cancel-button");
        focusCancel.SetResult(true);
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        IReadOnlyList<string> focusedBeforeStart = FocusedElementIds();

        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']")
            .TriggerEvent("onfocus", new FocusEventArgs());
        focusCancel.Invocations.ShouldHaveSingleItem().Arguments.ShouldBe(
            ["tenants-global-admin-remove-cancel-button"]);
        FocusedElementIds().ShouldBe(focusedBeforeStart);

        int acknowledgementFocusCount = FocusedElementIds().Count;
        cut.Find("[data-testid='tenants-global-admin-remove-focus-end']")
            .TriggerEvent("onfocus", new FocusEventArgs());
        FocusedElementIds().Count.ShouldBe(acknowledgementFocusCount + 1);
        FocusedElementIds()[^1].ShouldBe(CapturedChildElementReferenceId(
            cut.Instance,
            "_removeAcknowledgementElement"));
    }

    [Fact]
    public void RealChromiumFocusValidatorPinsActiveElementMovementAndFallbacks()
    {
        string validatorRoot = Path.Combine(
            ProjectRoot(),
            "tests",
            "Hexalith.Tenants.UI.Tests",
            "Browser");
        string harness = File.ReadAllText(Path.Combine(
            validatorRoot,
            "tenants-focus-browser-validation.html"));
        string runner = File.ReadAllText(Path.Combine(
            validatorRoot,
            "validate-tenants-focus-browser.sh"));

        harness.ShouldContain("await import(modulePath)");
        harness.ShouldContain("await import(\"./fluent-ui.js\")");
        harness.ShouldContain("fluentUi.beforeStart(document, \"web\")");
        harness.ShouldContain("customElements.whenDefined(\"fluent-button\")");
        harness.ShouldContain("start-to-exact-cancel");
        harness.ShouldContain("document.activeElement");
        harness.ShouldContain("existing-unfocusable-cancel-to-acknowledgement");
        harness.ShouldContain("missing-cancel-to-acknowledgement");
        harness.ShouldContain("missing-preview-to-lifecycle");
        harness.ShouldContain("narrow-correction-retry-hidden");
        harness.ShouldContain("getComputedStyle(retry).display");
        harness.ShouldContain("class=\"global-admins__remove-preview\"");
        harness.ShouldContain("pageCss.match(/\\.global-admins__remove-preview\\[");
        harness.ShouldContain("getComputedStyle(preview)");
        harness.ShouldContain("previewStyle.position !== \"fixed\"");
        harness.ShouldContain("preview.contains(previewCenter)");
        harness.ShouldContain("remove-dialog-computed-fixed-visible-bounded-foreground");
        // The shared audit availability control: 390px stacking, desktop row, action-free live region, and a
        // programmatically focusable state line, measured against the compiled scoped and Fluent bundle CSS.
        harness.ShouldContain("availability-actions-narrow-stacked-full-width");
        harness.ShouldContain("availability-actions-desktop-row");
        harness.ShouldContain("availability-live-region");
        harness.ShouldContain("availability-state-line-programmatic-focus");
        harness.ShouldContain("class=\"fluent-stack-horizontal tenants-audit-availability__actions\"");
        runner.ShouldContain("AuditAvailabilityState.razor.rz.scp.css");
        runner.ShouldContain("Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css");
        runner.ShouldContain("availability-unstacked.css");
        runner.ShouldContain("Validator accepted availability recoveries that no longer stack at 390px.");
        runner.ShouldContain("src/Hexalith.Tenants.UI/wwwroot/js/tenantsFocus.js");
        runner.ShouldContain("GlobalAdministratorCorrectionPanel.razor.rz.scp.css");
        runner.ShouldContain("GlobalAdministratorsPage.razor.rz.scp.css");
        runner.ShouldContain("global-admins-inflow.css");
        runner.ShouldContain("global-admins-hidden.css");
        runner.ShouldContain("remove-dialog-style");
        // The narrow run takes the default 390px width; the desktop run passes its width explicitly.
        runner.ShouldContain("local window_width=\"${5:-390}\"");
        runner.ShouldContain("--window-size=${window_width},800");
        runner.ShouldContain("microsoft.fluentui.aspnetcore.components");
        runner.ShouldContain("tenantsFocus-return-true.js");
        runner.ShouldContain("data-validation-status=\"passed\"");
        runner.ShouldContain("data-validation-status=\"failed\"");
        runner.ShouldContain("server_ready=false");
        runner.ShouldContain("server_ready=true");
        runner.ShouldContain("if [[ \"$server_ready\" != true ]]");
    }

    [Fact]
    public async Task RealChromiumFocusValidatorStopsBeforeChromiumWhenServerStartupFails()
    {
        static bool HasCommand(string name)
            => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator)
                .Any(directory => File.Exists(Path.Combine(directory, name)));

        string assets = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "obj", "project.assets.json");
        string? configuredBrowser = Environment.GetEnvironmentVariable("CHROMIUM_BIN");
        string browserCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "ms-playwright");
        bool hasBrowser = configuredBrowser is not null && File.Exists(configuredBrowser)
            || new[] { "google-chrome-stable", "google-chrome", "chromium", "chromium-browser" }
                .Any(HasCommand)
            || Directory.Exists(browserCache)
                && Directory.EnumerateFiles(browserCache, "*", SearchOption.AllDirectories)
                    .Any(path => Path.GetFileName(path) is "chrome" or "headless_shell");
        Assert.SkipUnless(
            File.Exists(assets) && HasCommand("bash") && HasCommand("python3") && hasBrowser,
            "The restored UI assets, bash, python3, and Chromium are required for this browser subprocess check.");

        string runner = Path.Combine(
            ProjectRoot(),
            "tests",
            "Hexalith.Tenants.UI.Tests",
            "Browser",
            "validate-tenants-focus-browser.sh");
        string browserMarker = Path.Combine(
            Path.GetTempPath(),
            $"tenants-focus-browser-invoked-{Guid.NewGuid():N}");
        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = ProjectRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(runner);
        string currentConfiguration = typeof(GlobalAdministratorsPageTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?
            .Configuration
            ?? (Directory.Exists(Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "obj", "Release")) ? "Release" : "Debug");
        startInfo.Environment["TENANTS_BROWSER_BUILD_CONFIGURATION"] =
            Environment.GetEnvironmentVariable("TENANTS_BROWSER_BUILD_CONFIGURATION")
            ?? currentConfiguration;
        startInfo.Environment["TENANTS_FOCUS_TEST_SERVER_START_FAILURE"] = "true";
        startInfo.Environment["TENANTS_FOCUS_BROWSER_INVOCATION_MARKER"] = browserMarker;

        try
        {
            using Process process = Process.Start(startInfo).ShouldNotBeNull();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }

            string combinedOutput = await output + await error;
            process.ExitCode.ShouldNotBe(0);
            combinedOutput.ShouldContain("Focus validation server did not become ready");
            combinedOutput.ShouldContain("forced focus-validator server startup failure");
            File.Exists(browserMarker).ShouldBeFalse();
        }
        finally
        {
            File.Delete(browserMarker);
        }
    }

    [Fact]
    public async Task RealChromiumFocusValidatorStopsWithinItsBoundWhenChromiumHangs()
    {
        static bool HasCommand(string name)
            => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator)
                .Any(directory => File.Exists(Path.Combine(directory, name)));

        string projectRoot = ProjectRoot();
        string assets = Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "obj", "project.assets.json");
        Assert.SkipUnless(
            OperatingSystem.IsLinux() && File.Exists(assets)
                && HasCommand("bash") && HasCommand("python3") && HasCommand("timeout"),
            "The restored UI assets, Linux, bash, python3, and timeout are required for this browser subprocess check.");

        string validationDirectory = Directory.CreateTempSubdirectory("tenants-focus-hanging-browser-").FullName;
        string browser = Path.Combine(validationDirectory, "chromium-stub");
        string browserMarker = Path.Combine(validationDirectory, "browser-invoked");
        await File.WriteAllTextAsync(browser, "#!/usr/bin/env bash\nwhile :; do sleep 60; done\n");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(browser, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Path.Combine(projectRoot, "tests", "Hexalith.Tenants.UI.Tests", "Browser",
            "validate-tenants-focus-browser.sh"));
        startInfo.Environment["CHROMIUM_BIN"] = browser;
        startInfo.Environment["TENANTS_BROWSER_BUILD_CONFIGURATION"] = typeof(GlobalAdministratorsPageTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>().ShouldNotBeNull().Configuration;
        startInfo.Environment["TENANTS_FOCUS_BROWSER_TIMEOUT_SECONDS"] = "1";
        startInfo.Environment["TENANTS_FOCUS_TEST_SERVER_START_FAILURE"] = "false";
        startInfo.Environment["TENANTS_FOCUS_BROWSER_INVOCATION_MARKER"] = browserMarker;

        try
        {
            using Process process = Process.Start(startInfo).ShouldNotBeNull();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            Stopwatch elapsed = Stopwatch.StartNew();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }

            string combinedOutput = await output + await error;
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
            process.ExitCode.ShouldNotBe(0);
            combinedOutput.ShouldContain("Chromium scenario profile-shipped failed (exit 124; bound 1s).");
            File.Exists(browserMarker).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(validationDirectory, recursive: true);
        }
    }

    [Fact]
    public void RemoveStartSentinelFallsBackToAcknowledgementWhenRenderedCancelCannotBeFocused()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        var focusCancel = module.Setup<bool>(
            "focusElementById",
            "tenants-global-admin-remove-cancel-button");
        focusCancel.SetResult(false);
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);

        cut.Find("[data-testid='tenants-global-admin-remove-cancel']")
            .LocalName.ShouldBe("fluent-button");
        cut.Find("[data-testid='tenants-global-admin-remove-cancel']")
            .GetAttribute("id").ShouldBe("tenants-global-admin-remove-cancel-button");
        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']")
            .TriggerEvent("onfocus", new FocusEventArgs());

        focusCancel.Invocations.ShouldHaveSingleItem();
        FocusedElementIds()[^1].ShouldBe(CapturedChildElementReferenceId(
            cut.Instance,
            "_removeAcknowledgementElement"));
    }

    [Theory]
    [MemberData(nameof(RemoveFocusExceptions))]
    public void RemoveStartSentinelContainsRealFocusInteropFailure(Exception exception)
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        module.Setup<bool>("focusElementById", _ => true).SetException(exception);
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);

        cut.Find("[data-testid='tenants-global-admin-remove-focus-start']")
            .TriggerEvent("onfocus", new FocusEventArgs());
        FocusedElementIds()[^1].ShouldBe(CapturedChildElementReferenceId(
            cut.Instance,
            "_removeAcknowledgementElement"));
    }

    [Fact]
    public async Task RemoveStartSentinelFallsBackToLifecycleWhenPreviewHasDisappeared()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        GlobalAdministratorRemovePreview stalePreview = PrivateField<GlobalAdministratorRemoveCommandSnapshot>(
                cut.Instance,
                "_removeSnapshot")
            .PreviewEvidence.ShouldNotBeNull();
        cut.Find("[data-testid='tenants-global-admin-remove-cancel']").Click();
        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();

        Task focus = (Task)typeof(GlobalAdministratorsPage)
            .GetMethod("FocusElementByIdSafelyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, ["tenants-global-admin-remove-cancel-button", stalePreview])!;
        await focus;

        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(cut.Instance, "_removeLifecycleElement"));
    }

    [Fact]
    public async Task StaleCancelFocusContinuationFallsBackInsideTheNewerPreview()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        var focusCancel = module.Setup<bool>(
            "focusElementById",
            "tenants-global-admin-remove-cancel-button");
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        GlobalAdministratorRemovePreview stalePreview = PrivateField<GlobalAdministratorRemoveCommandSnapshot>(
                cut.Instance,
                "_removeSnapshot")
            .PreviewEvidence.ShouldNotBeNull();
        MethodInfo focusMethod = typeof(GlobalAdministratorsPage).GetMethod(
            "FocusElementByIdSafelyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        Task staleFocus = (Task)focusMethod.Invoke(
            cut.Instance,
            ["tenants-global-admin-remove-cancel-button", stalePreview])!;
        await WaitUntilAsync(() => focusCancel.Invocations.Count == 1, TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-global-admin-remove-cancel']").Click();
        OpenRemovePreview(cut, "other-admin");
        GlobalAdministratorRemovePreview currentPreview = PrivateField<GlobalAdministratorRemoveCommandSnapshot>(
                cut.Instance,
                "_removeSnapshot")
            .PreviewEvidence.ShouldNotBeNull();
        currentPreview.ShouldNotBeSameAs(stalePreview);
        string acknowledgementId = CapturedChildElementReferenceId(
            cut.Instance,
            "_removeAcknowledgementElement");
        string lifecycleId = CapturedElementReferenceId(cut.Instance, "_removeLifecycleElement");

        focusCancel.SetResult(false);
        await staleFocus.WaitAsync(TimeSpan.FromSeconds(5));

        FocusedElementIds()[^1].ShouldBe(acknowledgementId);
        FocusedElementIds()[^1].ShouldNotBe(lifecycleId);
    }

    [Fact]
    public async Task CancelLauncherFocusCompletionReturnsInsideANewerRemovalPreview()
    {
        GlobalAdministratorsSnapshot complete = ComponentReady(
            "projection-v1",
            "target-admin",
            "other-admin");
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(complete));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        GlobalAdministratorRemoveLauncher targetLauncher = cut
            .FindComponents<GlobalAdministratorRemoveLauncher>()
            .Single(component => component.Instance.UserId == "target-admin")
            .Instance;
        OpenRemovePreview(cut);
        string targetLauncherId = CapturedElementReferenceId(targetLauncher, "_button");
        JSRuntimeInvocationHandler delayedLauncherFocus = JSInterop.SetupVoid(
            "Blazor._internal.domWrapper.focus",
            invocation => invocation.Arguments.Count > 0
                && invocation.Arguments[0] is ElementReference element
                && element.Id == targetLauncherId);

        Task staleCancelFocus = cut.Find("[data-testid='tenants-global-admin-remove-cancel']")
            .ClickAsync(new MouseEventArgs());
        await WaitUntilAsync(
            () => delayedLauncherFocus.Invocations.Count == 1,
            TimeSpan.FromSeconds(5));

        OpenRemovePreview(cut, "other-admin");
        cut.Find("[data-testid='tenants-global-admin-remove-target']")
            .TextContent.ShouldContain("other-admin");

        int focusCountBeforeCompletion = FocusedElementIds().Count;
        delayedLauncherFocus.SetVoidResult();
        await staleCancelFocus.WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorRemoveCommandSnapshot currentSnapshot =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        currentSnapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        currentSnapshot.PreviewEvidence.ShouldNotBeNull().TargetUserId.ShouldBe("other-admin");

        await WaitUntilAsync(
            () => FocusedElementIds().Count > focusCountBeforeCompletion,
            TimeSpan.FromSeconds(5));
        FocusedElementIds()[^1].ShouldBe(CapturedChildElementReferenceId(
            cut.Instance,
            "_removeAcknowledgementElement"));
    }

    [Fact]
    public async Task RemoveSubmissionRechecksExactAcknowledgementAtTheFinalRendererBoundary()
    {
        GlobalAdministratorsSnapshot complete = ComponentReady(
            "projection-v1",
            "target-admin",
            "other-admin");
        var queryGateway = new StubTenantQueryGateway(complete, complete);
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        var preflight = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        queryGateway.QueueResponse(preflight.Task);

        Task submit = cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-global-admin-remove-acknowledge']")
            .Change("TARGET-admin");
        preflight.SetResult(complete);
        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveMessageIds.ShouldBeEmpty();
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveEndSentinelFallsBackToLifecycleWhenPreviewHasDisappeared()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        GlobalAdministratorRemovePreview stalePreview = PrivateField<GlobalAdministratorRemoveCommandSnapshot>(
                cut.Instance,
                "_removeSnapshot")
            .PreviewEvidence.ShouldNotBeNull();
        cut.Find("[data-testid='tenants-global-admin-remove-cancel']").Click();
        cut.FindAll("[data-testid='tenants-global-admin-remove-preview']").ShouldBeEmpty();

        Task focus = (Task)typeof(GlobalAdministratorsPage)
            .GetMethod("FocusRemoveAcknowledgementAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, [new FocusEventArgs(), stalePreview])!;
        await focus;

        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(cut.Instance, "_removeLifecycleElement"));
    }

    public static TheoryData<Exception> RemoveFocusExceptions
        => new()
        {
            new JSDisconnectedException("Circuit disconnected."),
            new ObjectDisposedException("focus-module"),
            new JSException("Focus target detached."),
            new TaskCanceledException("Focus interop canceled during circuit teardown."),
            new InvalidOperationException("Renderer is no longer interactive."),
        };

    public static TheoryData<Exception> RemoveFocusModuleDisposalExceptions
        => new()
        {
            new JSDisconnectedException("Circuit disconnected during focus module disposal."),
            new ObjectDisposedException("focus-module"),
            new JSException("Focus module detached during disposal."),
            new TaskCanceledException("Focus module disposal canceled during circuit teardown."),
        };

    [Theory]
    [MemberData(nameof(RemoveFocusModuleDisposalExceptions))]
    public async Task RemoveFocusModuleDisposalContainsCircuitTeardown(Exception exception)
    {
        IJSObjectReference module = Substitute.For<IJSObjectReference>();
        module.DisposeAsync().Returns(ValueTask.FromException(exception));
        MethodInfo dispose = typeof(GlobalAdministratorsPage).GetMethod(
            "DisposeFocusModuleSafelyAsync",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();

        await ((Task)dispose.Invoke(null, [module])!).WaitAsync(TimeSpan.FromSeconds(5));

        module.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IAsyncDisposable.DisposeAsync))
            .ShouldBe(1);
    }

    [Fact]
    public void AmbiguousRemoveRetryHasDestructiveLabelAndIsWithdrawnWhenViewportBecomesUnsafe()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();

        IElement retry = cut.Find("[data-testid='tenants-global-admin-remove-refresh']");
        retry.TextContent.Trim().ShouldBe("Retry removal delivery");
        retry.GetAttribute("data-recovery-kind").ShouldBe("delivery-retry");
        retry.HasAttribute("disabled").ShouldBeFalse();
        retry.GetAttribute("aria-describedby").ShouldBe(
            "tenants-global-admin-remove-safe-message tenants-global-admin-remove-safe-recovery");
        foreach (string describedId in retry.GetAttribute("aria-describedby")!.Split(' '))
        {
            cut.Find($"#{describedId}").TextContent.ShouldNotBeNullOrWhiteSpace();
        }

        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='tenants-global-admin-remove-refresh']").ShouldBeEmpty());
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AmbiguousRemoveRetryIsDisabledWhileItsSameIdentityDispatchIsInFlight()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").Click();
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();

        var retryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.RemoveSubmissionGate = retryGate;
        Task retry = cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeTrue());
        commandGateway.RemoveMessageIds.ShouldAllBe(id => id == messageId);

        retryGate.SetResult();
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(2);
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeFalse());
    }

    [Fact]
    public async Task ChangedCallerBeforeAmbiguousRemoveRetryRetainsIdentityWithoutRedispatch()
    {
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();
        composition.CallerSubject = "target-admin";

        await cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());

        commandGateway.RemoveMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        GlobalAdministratorRemoveCommandSnapshot retained =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        retained.IsSubmissionAmbiguous.ShouldBeTrue();
        retained.MessageId.ShouldBe(messageId);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task AmbiguousRemoveRetryExplainsWhenItsLeaseDispatchCannotBegin()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        GlobalAdministratorRemoveCommandSnapshot expected =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        TenantAggregateCommandLease lease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease");
        object owner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        MethodInfo createReconciliation = typeof(GlobalAdministratorsPage).GetMethod(
            "CreateReconciliation",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(GlobalAdministratorRemoveCommandSnapshot)],
            modifiers: null).ShouldNotBeNull();
        GlobalAdministratorReconciliationState dispatchBasis =
            ((GlobalAdministratorReconciliationState?)createReconciliation.Invoke(null, [expected]))
            .ShouldNotBeNull();
        lease.TryBeginReconciliationDispatch(owner, dispatchBasis, out long competingToken).ShouldBeTrue();

        MethodInfo retryMethod = typeof(GlobalAdministratorsPage).GetMethod(
            "RetryAmbiguousRemoveDeliveryAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        await ((Task)retryMethod.Invoke(cut.Instance, [expected])!).WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorRemoveCommandSnapshot blocked =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        blocked.SafeMessageKey.ShouldBe(
            "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.AggregateBusy");
        blocked.SafeRecoveryKey.ShouldBe(
            "Tenants.GlobalAdministrators.Availability.Recovery.AggregateBusy");
        blocked.MessageId.ShouldBe(expected.MessageId);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        cut.Find("[data-testid='tenants-global-admin-remove-safe-message']")
            .TextContent.ShouldContain("another global-administrator attempt", Case.Insensitive);

        lease.TryAbortReconciliationDispatch(competingToken).ShouldBeTrue();
        MethodInfo completeSingleFlight = typeof(GlobalAdministratorsPage).GetMethod(
            "CompleteRemoveSingleFlightAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        await ((Task)completeSingleFlight.Invoke(cut.Instance, null)!).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelledRemoveRetryPopulationReadRetainsAttemptAndLease()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        var queryGateway = new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin"));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        GlobalAdministratorRemoveCommandSnapshot retained =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        TenantAggregateCommandLease lease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease");
        var heldRead = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        queryGateway.QueueResponse(heldRead.Task);

        Task retry = cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        PrivateField<CancellationTokenSource>(cut.Instance, "_removeMutationCancellation").Cancel();
        await retry.WaitAsync(TimeSpan.FromSeconds(5));

        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .ShouldBeSameAs(retained);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease")
            .ShouldBeSameAs(lease);
        lease.IsDispatchMarked.ShouldBeTrue();
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        queryGateway.QueuedResponseCancellationObserved.ShouldBe(1);
    }

    [Fact]
    public void ChangedRetryEvidenceNeverSurfacesTheAvailableReasonOrRecovery()
    {
        GlobalAdministratorsSnapshot current = ComponentReady(
            "projection-v2",
            "target-admin",
            "other-admin");
        GlobalAdministratorRemovePreview rebuilt = GlobalAdministratorRemovePreview.Create(
            "target-admin",
            "operator-admin",
            current,
            isAuthorized: true);
        var available = new GlobalAdministratorActionAvailability(
            GlobalAdministratorActionKind.Remove,
            IsAvailable: true,
            GlobalAdministratorActionUnavailableReason.None,
            "Tenants.GlobalAdministrators.Availability.Remove.Available",
            "Tenants.GlobalAdministrators.Availability.Recovery.None");
        MethodInfo evidenceMethod = typeof(GlobalAdministratorsPage).GetMethod(
            "RemovePreflightFailureEvidence",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();

        var evidence = ((string ReasonKey, string RecoveryKey))evidenceMethod.Invoke(
            null,
            [rebuilt, available])!;

        evidence.ReasonKey.ShouldBe("Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Evidence");
        evidence.RecoveryKey.ShouldBe("Tenants.GlobalAdministrators.Remove.Preview.Recovery.Refresh");
        evidence.ReasonKey.ShouldNotBe(available.ReasonKey);
        evidence.RecoveryKey.ShouldNotBe(available.RecoveryKey);
    }

    [Fact]
    public async Task CorrelatedRemoveStatusRecoveryStaysVisibleOnMobileAndNeverRedispatches()
    {
        TenantCommandStatusResult pending = new(
            CommandStatus.Received,
            EventCount: 0,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [pending, pending])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();

        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        IElement refresh = cut.WaitForElement("[data-testid='tenants-global-admin-remove-refresh']");
        refresh.TextContent.Trim().ShouldBe("Refresh status");
        refresh.GetAttribute("data-recovery-kind").ShouldBe("status");
        refresh.ClassList.ShouldNotContain("global-admins__mutation-initiation");
        refresh.HasAttribute("disabled").ShouldBeFalse();

        await refresh.ClickAsync(new MouseEventArgs());

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        commandGateway.RemoveMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        commandGateway.StatusHandles.Count.ShouldBe(2);
        foreach (TenantCommandTrackingHandle handle in commandGateway.StatusHandles)
        {
            handle.MessageId.ShouldBe(messageId);
            handle.CorrelationId.ShouldBe("correlation-remove");
            handle.AggregateId.ShouldBe(GlobalAdministratorRemovePreview.FixedAggregateId);
        }
        cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task CorrelatedRemoveNotificationQueuedDuringBusyStatusDrainsWithoutRedispatch()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        TenantCommandStatusResult pending = new(
            CommandStatus.Received,
            EventCount: 0,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [pending, pending, pending])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());

        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.StatusGate = statusGate;
        Task manualRefresh = cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeTrue();

        for (int index = 0; index < 12; index++)
        {
            notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system");
        }
        statusGate.SetResult();
        await manualRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => commandGateway.StatusHandles.Count == 3, TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(static () => Task.CompletedTask);
        commandGateway.StatusHandles.Count.ShouldBe(3);

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        foreach (TenantCommandTrackingHandle handle in commandGateway.StatusHandles)
        {
            handle.MessageId.ShouldBe(commandGateway.RemoveMessageIds.ShouldHaveSingleItem());
            handle.CorrelationId.ShouldBe("correlation-remove");
            handle.AggregateId.ShouldBe(GlobalAdministratorRemovePreview.FixedAggregateId);
        }
        cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task NotificationOnlyRemoveStatusRefreshRendersDisabledThenEnabledWithoutRedispatch()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        TenantCommandStatusResult pending = new(
            CommandStatus.Received,
            EventCount: 0,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [pending, pending])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => commandGateway.StatusHandles.Count.ShouldBe(1));

        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.StatusGate = statusGate;
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeTrue());
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);

        statusGate.SetResult();
        await WaitUntilAsync(() => commandGateway.StatusHandles.Count == 2, TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeFalse());
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task UnrelatedAggregateAdmissionChangesDoNotIssueRemovalStatusTraffic()
    {
        TenantCommandStatusResult pending = new(
            CommandStatus.Received,
            EventCount: 0,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [pending])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => commandGateway.StatusHandles.Count.ShouldBe(1));
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object unrelatedOwner = new();

        admissionGate.TryAcquire(
            TenantCommandAggregateLock.ForTenant("tenant-unrelated"),
            unrelatedOwner).ShouldBeTrue();
        await cut.InvokeAsync(static () => Task.CompletedTask);
        admissionGate.Release(
            TenantCommandAggregateLock.ForTenant("tenant-unrelated"),
            unrelatedOwner);
        await cut.InvokeAsync(static () => Task.CompletedTask);

        commandGateway.StatusHandles.Count.ShouldBe(1);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Fact]
    public async Task UnrelatedAggregateAdmissionChangesDoNotRebuildStillPresentRemoval()
    {
        TenantCommandStatusResult eventsStored = new(
            CommandStatus.EventsStored,
            EventCount: 1,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [eventsStored])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-global-admin-remove-safe-message']")
            .TextContent.ShouldContain("still shows the target"));
        int statusLookups = commandGateway.StatusHandles.Count;
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object unrelatedOwner = new();

        admissionGate.TryAcquire(
            TenantCommandAggregateLock.ForTenant("tenant-unrelated"),
            unrelatedOwner).ShouldBeTrue();
        await cut.InvokeAsync(static () => Task.CompletedTask);
        admissionGate.Release(
            TenantCommandAggregateLock.ForTenant("tenant-unrelated"),
            unrelatedOwner);
        await cut.InvokeAsync(static () => Task.CompletedTask);

        commandGateway.StatusHandles.Count.ShouldBe(statusLookups);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        cut.Find("[data-testid='tenants-global-admin-remove-safe-message']")
            .TextContent.ShouldContain("still shows the target");
    }

    [Fact]
    public async Task AttemptAEnqueuePausedBeforePendingSlotCannotOverwriteAttemptB()
    {
        TenantCommandStatusResult pending = new(
            CommandStatus.Received,
            EventCount: 0,
            HasVerifiedCommandIdentity: true);
        var commandGateway = new StubTenantCommandGateway(statuses: [pending])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-a"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        GlobalAdministratorRemoveCommandSnapshot attemptA =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        commandGateway.StatusHandles.Count.ShouldBe(1);

        FieldInfo singleFlight = typeof(GlobalAdministratorsPage).GetField(
            "_removeSubmissionInFlight",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        singleFlight.SetValue(cut.Instance, 1);
        GlobalAdministratorRemoveCommandSnapshot attemptB = attemptA with
        {
            MessageId = NUlid.Ulid.NewUlid().ToString(),
            CorrelationId = "correlation-b",
        };
        FieldInfo snapshotField = typeof(GlobalAdministratorsPage).GetField(
            "_removeSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        FieldInfo pendingField = typeof(GlobalAdministratorsPage).GetField(
            "_pendingRemoveStatusNudge",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        MethodInfo enqueue = typeof(GlobalAdministratorsPage).GetMethod(
            "QueueRemoveStatusNudgeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        TenantAggregateCommandLease lease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease");
        Lock pendingLock = PrivateField<Lock>(cut.Instance, "_removeStatusNudgeLock");
        var workerStarted = new ManualResetEventSlim();
        Task? enqueueTask = null;
        Exception? workerFailure = null;
        var worker = new Thread(() =>
        {
            workerStarted.Set();
            try
            {
                enqueueTask = (Task)enqueue.Invoke(cut.Instance, [attemptA])!;
            }
            catch (Exception exception)
            {
                workerFailure = exception;
            }
        });

        using (pendingLock.EnterScope())
        {
            worker.Start();
            workerStarted.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            SpinWait.SpinUntil(
                () => (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5)).ShouldBeTrue(
                    "attempt A must be paused on the exact pending-slot lock before attempt B replaces it");
            snapshotField.SetValue(cut.Instance, attemptB);
            pendingField.SetValue(cut.Instance, (attemptB, lease));
        }

        worker.Join(TimeSpan.FromSeconds(5)).ShouldBeTrue();
        workerFailure.ShouldBeNull();
        await enqueueTask.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(5));

        commandGateway.StatusHandles.Count.ShouldBe(1);
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .ShouldBe(attemptB);
        var retained = PrivateField<(
            GlobalAdministratorRemoveCommandSnapshot Attempt,
            TenantAggregateCommandLease Lease)?>(cut.Instance, "_pendingRemoveStatusNudge");
        retained.ShouldNotBeNull().Attempt.ShouldBe(attemptB);
        retained.Value.Lease.ShouldBeSameAs(lease);
    }

    [Fact]
    public async Task CorrelationlessRemoveReplacementAdoptsWithoutAutomaticRedispatch()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(first);
        AcknowledgeRemovePreview(first);
        await first.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                .MessageId == messageId,
            TimeSpan.FromSeconds(5));

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        GlobalAdministratorRemoveCommandSnapshot adopted =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot");
        adopted.MessageId.ShouldBe(messageId);
        adopted.IsSubmissionAmbiguous.ShouldBeTrue();
        adopted.PreviewEvidence?.ProjectionVersion.ShouldBe("projection-v1");
        replacement.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .TextContent.Trim().ShouldBe("Retry removal delivery");
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task CorrelationlessRemoveNotificationDoesNotAutomaticallyRetryDelivery()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
                .IsSubmissionAmbiguous.ShouldBeTrue());
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        commandGateway.StatusHandles.ShouldBeEmpty();

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system");
        await cut.InvokeAsync(static () => Task.CompletedTask);

        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
        commandGateway.StatusHandles.ShouldBeEmpty();
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .IsSubmissionAmbiguous.ShouldBeTrue();
        cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .GetAttribute("data-recovery-kind").ShouldBe("delivery-retry");
    }

    [Fact]
    public async Task FailedRemoveDeliveryReleasesLeaseAndKeepsConfirmedRows()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Failed("The command failed."),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
                .State.ShouldBe(TenantCommandLifecycleState.Failed));
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .IsSubmissionAmbiguous.ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldContain("target-admin");
        cut.Find("[data-testid='tenants-global-admin-remove-safe-recovery']").TextContent
            .ShouldContain("Refresh current evidence", Case.Insensitive);
    }

    [Fact]
    public async Task UnexpectedFailureAfterRemoveDispatchIsArmedPublishesAmbiguityAndDrainsPendingWork()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        GlobalAdministratorRemoveCommandSnapshot expected =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        TenantAggregateCommandLease lease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease");
        object owner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        MethodInfo createReconciliation = typeof(GlobalAdministratorsPage).GetMethod(
            "CreateReconciliation",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(GlobalAdministratorRemoveCommandSnapshot)],
            modifiers: null).ShouldNotBeNull();
        GlobalAdministratorReconciliationState dispatchBasis =
            ((GlobalAdministratorReconciliationState?)createReconciliation.Invoke(null, [expected]))
            .ShouldNotBeNull();
        lease.TryBeginReconciliationDispatch(owner, dispatchBasis, out long completionToken).ShouldBeTrue();

        typeof(GlobalAdministratorsPage).GetField(
                "_removeSubmissionInFlight",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, 1);
        typeof(GlobalAdministratorsPage).GetField(
                "_isRemoveSubmitting",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, true);
        typeof(GlobalAdministratorsPage).GetField(
                "_pendingRemoveStatusNudge",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, (expected, lease));
        long generation = PrivateField<long>(cut.Instance, "_removeMutationGeneration");
        MethodInfo publishAmbiguity = typeof(GlobalAdministratorsPage).GetMethod(
            "CompleteArmedRemoveDeliveryAsAmbiguousAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();

        await ((Task)publishAmbiguity.Invoke(
            cut.Instance,
            [expected, lease, completionToken, generation])!).WaitAsync(TimeSpan.FromSeconds(5));
        MethodInfo completeSingleFlight = typeof(GlobalAdministratorsPage).GetMethod(
            "CompleteRemoveSingleFlightAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        await ((Task)completeSingleFlight.Invoke(cut.Instance, null)!).WaitAsync(TimeSpan.FromSeconds(5));

        lease.IsReconciliationDispatchInFlight.ShouldBeFalse();
        PrivateField<int>(cut.Instance, "_removeSubmissionInFlight").ShouldBe(0);
        PrivateField<bool>(cut.Instance, "_isRemoveSubmitting").ShouldBeFalse();
        PrivateField<(
            GlobalAdministratorRemoveCommandSnapshot Attempt,
            TenantAggregateCommandLease Lease)?>(cut.Instance, "_pendingRemoveStatusNudge").ShouldBeNull();
        GlobalAdministratorRemoveCommandSnapshot retained =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        retained.MessageId.ShouldBe(expected.MessageId);
        retained.IsSubmissionAmbiguous.ShouldBeTrue();
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("ambiguous")]
    [InlineData("unsupported")]
    [InlineData("rejected")]
    public async Task InitialRemoveDeliveryCompletionSurvivesDisposalAndReplacement(string outcome)
    {
        var deliveryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmissionGate = deliveryGate,
            StatusGate = outcome == "accepted" ? statusGate : null,
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(first);
        AcknowledgeRemovePreview(first);
        Task submit = first.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();
        TenantAggregateCommandLease initialLease =
            PrivateField<TenantAggregateCommandLease>(first.Instance, "_removeAdmissionLease");
        initialLease.IsReconciliationDispatchInFlight.ShouldBeTrue();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                .MessageId == messageId,
            TimeSpan.FromSeconds(5));
        replacement.FindAll("[data-testid='tenants-global-admin-remove-refresh']").ShouldBeEmpty();
        commandGateway.RemoveSubmission = outcome switch
        {
            "accepted" => TenantCommandSubmissionResult.Accepted("ignored", "correlation-safe"),
            "ambiguous" => TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
            "unsupported" => new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.AlreadyApplied,
                "ignored",
                "unsupported-correlation"),
            _ => TenantCommandSubmissionResult.Rejected("rejected", "LastGlobalAdministrator"),
        };
        deliveryGate.SetResult();

        if (outcome == "accepted")
        {
            await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            replacement.WaitForAssertion(() =>
            {
                GlobalAdministratorRemoveCommandSnapshot snapshot =
                    PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot");
                snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
                snapshot.CorrelationId.ShouldBe("correlation-safe");
            });
            statusGate.SetResult();
        }
        else if (outcome == "ambiguous")
        {
            replacement.WaitForAssertion(() =>
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                    .State.ShouldBe(TenantCommandLifecycleState.RequestSent));
        }
        else if (outcome == "unsupported")
        {
            replacement.WaitForAssertion(() =>
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                    .State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        }
        else
        {
            replacement.WaitForAssertion(() =>
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                    .State.ShouldBe(TenantCommandLifecycleState.Rejected));
        }

        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.RemoveMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        initialLease.IsReconciliationDispatchInFlight.ShouldBeFalse();
        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators())
            .ShouldBe(outcome != "rejected");
    }

    [Theory]
    [InlineData("failed", TenantCommandLifecycleState.Failed)]
    [InlineData("rejected", TenantCommandLifecycleState.Rejected)]
    public async Task OwnerlessTerminalRemoveDeliveryIsRenderedAndReleasedAfterReplacementAdopts(
        string outcome,
        TenantCommandLifecycleState expectedState)
    {
        var deliveryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { RemoveSubmissionGate = deliveryGate };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(first);
        AcknowledgeRemovePreview(first);
        Task submit = first.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        commandGateway.RemoveSubmission = outcome == "failed"
            ? TenantCommandSubmissionResult.Failed("delivery failed")
            : TenantCommandSubmissionResult.Rejected("rejected", "LastGlobalAdministrator");
        deliveryGate.SetResult();
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        replacement.WaitForAssertion(() =>
        {
            GlobalAdministratorRemoveCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot");
            snapshot.State.ShouldBe(expectedState);
            snapshot.MessageId.ShouldBe(messageId);
            replacement.Find("[data-testid='tenants-global-admin-remove-state']")
                .TextContent.ShouldContain(outcome == "failed" ? "failed" : "rejected", Case.Insensitive);
            admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        });
        commandGateway.RemoveMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);

        var nextOwner = new object();
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            nextOwner,
            out TenantAggregateCommandLease? nextLease).ShouldBeTrue();
        nextLease!.TryAbandonBeforeDispatch(nextOwner).ShouldBeTrue();
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("ambiguous")]
    [InlineData("unsupported")]
    [InlineData("rejected")]
    public async Task InitialGrantDeliveryCompletionSurvivesDisposalAndReplacement(string outcome)
    {
        var deliveryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantCommandSubmissionResult submission = outcome switch
        {
            "accepted" => TenantCommandSubmissionResult.Accepted("ignored", "correlation-safe"),
            "ambiguous" => TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"),
            "unsupported" => new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.AlreadyApplied,
                "ignored",
                "unsupported-correlation"),
            _ => TenantCommandSubmissionResult.RejectedWithKey(
                "Tenants.GlobalAdministrators.Grant.Submission.Rejected",
                "InsufficientPermissions"),
        };
        var commandGateway = new StubTenantCommandGateway(submission)
        {
            SubmissionGate = deliveryGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        first.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(first);
        AcknowledgeGrantPreview(first);
        Task submit = first.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.SubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string messageId = commandGateway.GrantMessageIds.ShouldHaveSingleItem();
        TenantAggregateCommandLease initialLease =
            PrivateField<TenantAggregateCommandLease>(first.Instance, "_grantAdmissionLease");
        initialLease.IsReconciliationDispatchInFlight.ShouldBeTrue();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorGrantCommandSnapshot>(replacement.Instance, "_grantSnapshot")
                .MessageId == messageId,
            TimeSpan.FromSeconds(5));
        replacement.Find("[data-testid='tenants-global-admin-grant-refresh']")
            .HasAttribute("disabled")
            .ShouldBeTrue("the in-flight lease token must suppress overlapping recovery delivery");

        deliveryGate.SetResult();
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        PrivateField<bool>(first.Instance, "_isGrantSubmitting").ShouldBeFalse();
        replacement.WaitForAssertion(() => initialLease.IsReconciliationDispatchInFlight.ShouldBeFalse());
        commandGateway.GrantMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);

        GlobalAdministratorGrantCommandSnapshot applied =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(replacement.Instance, "_grantSnapshot");
        if (outcome == "ambiguous")
        {
            applied.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
            applied.IsSubmissionAmbiguous.ShouldBeTrue();
        }
        else if (outcome == "unsupported")
        {
            applied.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            applied.IsSubmissionAmbiguous.ShouldBeTrue();
        }
        else if (outcome == "rejected")
        {
            applied.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        }
        else
        {
            applied.CorrelationId.ShouldBe("correlation-safe");
        }

        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators())
            .ShouldBe(outcome != "rejected");
    }

    [Fact]
    public async Task RepeatedUnsupportedAmbiguityAcrossReplacementKeepsSameIdRetryableWithoutStuckToken()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.AlreadyApplied,
                "ignored",
                "unsupported-correlation"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> first = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(first);
        AcknowledgeRemovePreview(first);
        await first.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();
        first.WaitForAssertion(() => first.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .HasAttribute("disabled").ShouldBeFalse());

        var retryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
            "ignored",
            "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous");
        commandGateway.RemoveSubmissionGate = retryGate;
        Task retry = first.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantAggregateCommandLease retryLease =
            PrivateField<TenantAggregateCommandLease>(first.Instance, "_removeAdmissionLease");
        retryLease.IsReconciliationDispatchInFlight.ShouldBeTrue();
        await first.InvokeAsync(async () => await first.Instance.DisposeAsync());

        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                .MessageId == messageId,
            TimeSpan.FromSeconds(5));
        retryGate.SetResult();
        await retry.WaitAsync(TimeSpan.FromSeconds(5));

        replacement.WaitForAssertion(() =>
        {
            GlobalAdministratorRemoveCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot");
            snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            snapshot.IsSubmissionAmbiguous.ShouldBeTrue();
            replacement.Find("[data-testid='tenants-global-admin-remove-refresh']")
                .HasAttribute("disabled").ShouldBeFalse();
        });
        commandGateway.RemoveMessageIds.ShouldBe([messageId, messageId]);
        retryLease.IsReconciliationDispatchInFlight.ShouldBeFalse();
    }

    [Theory]
    [InlineData(
        TenantCommandLifecycleState.Degraded,
        "Tenants.GlobalAdministrators.Remove.Recovery.PublishFailed",
        TenantCommandAuditState.AuditDelayed)]
    [InlineData(
        TenantCommandLifecycleState.UnableToVerify,
        "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Refresh",
        TenantCommandAuditState.AuditUnavailable)]
    [InlineData(
        TenantCommandLifecycleState.UnableToVerify,
        "Tenants.GlobalAdministrators.Remove.Recovery.TimedOut",
        TenantCommandAuditState.AuditDelayed)]
    public void RetainedRemoveReconstructionPreservesAuditTruthAndAssertiveUrgency(
        TenantCommandLifecycleState lifecycleState,
        string recoveryKey,
        TenantCommandAuditState expectedAuditState)
    {
        GlobalAdministratorsSnapshot complete = ComponentReady(
            "projection-v1",
            "target-admin",
            "other-admin");
        GlobalAdministratorRemovePreview preview = GlobalAdministratorRemovePreview.Create(
            "target-admin",
            "operator-admin",
            complete,
            isAuthorized: true);
        var reconciliation = new GlobalAdministratorReconciliationState(
            GlobalAdministratorActionKind.Remove,
            "target-admin",
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "correlation-safe",
            lifecycleState,
            SafeMessageKey: "Tenants.GlobalAdministrators.Remove.Status.Unknown",
            SafeRecoveryKey: recoveryKey,
            RemovePreview: preview);
        MethodInfo createSnapshot = typeof(GlobalAdministratorsPage).GetMethod(
            "CreateRemoveSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();

        var snapshot = (GlobalAdministratorRemoveCommandSnapshot)createSnapshot.Invoke(
            null,
            [reconciliation])!;

        snapshot.AuditState.ShouldBe(expectedAuditState);
        snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Assertive);
        snapshot.SafeRecoveryKey.ShouldBe(recoveryKey);
    }

    [Theory]
    [InlineData(TenantCommandLifecycleState.Degraded, TenantCommandAuditState.AuditDelayed)]
    [InlineData(TenantCommandLifecycleState.UnableToVerify, TenantCommandAuditState.AuditUnavailable)]
    public void RetainedGrantReconstructionPreservesAdoptedAuditFocusAndAssertiveUrgency(
        TenantCommandLifecycleState lifecycleState,
        TenantCommandAuditState expectedAuditState)
    {
        GlobalAdministratorsSnapshot complete = ComponentReady("projection-v1", "other-admin");
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            "target-admin",
            complete,
            isAuthorized: true);
        var reconciliation = new GlobalAdministratorReconciliationState(
            GlobalAdministratorActionKind.Grant,
            "target-admin",
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "correlation-safe",
            lifecycleState,
            preview,
            SafeMessageKey: "Tenants.GlobalAdministrators.Grant.Status.Unknown",
            SafeRecoveryKey: "Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh");
        MethodInfo createSnapshot = typeof(GlobalAdministratorsPage).GetMethod(
            "CreateGrantSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();

        var snapshot = (GlobalAdministratorGrantCommandSnapshot)createSnapshot.Invoke(
            null,
            [reconciliation])!;

        snapshot.AuditState.ShouldBe(expectedAuditState);
        snapshot.FocusTarget.ShouldBe(TenantCommandFocusTarget.Refresh);
        snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Assertive);
        snapshot.SafeMessageKey.ShouldBe(reconciliation.SafeMessageKey);
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant)]
    [InlineData(GlobalAdministratorActionKind.Remove)]
    public void Adopted_panel_lease_with_proof_inputs_does_not_rebuild_unchanged_page_snapshot(
        GlobalAdministratorActionKind action)
    {
        GlobalAdministratorsSnapshot complete = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        var reconciliation = new GlobalAdministratorReconciliationState(
            action,
            "target-admin",
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "correlation-safe",
            TenantCommandLifecycleState.Accepted,
            GrantPreview: action is GlobalAdministratorActionKind.Grant
                ? GlobalAdministratorGrantPreview.Create("target-admin", complete, isAuthorized: true) : null,
            RemovePreview: action is GlobalAdministratorActionKind.Remove
                ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", complete, isAuthorized: true) : null)
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 6, 1, 10, 1, 0, TimeSpan.Zero),
            CommittedEventSequence = 2,
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        };
        var page = new GlobalAdministratorsPage();
        object owner = typeof(GlobalAdministratorsPage)
            .GetField("_fixedAggregateOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page)!;
        var gate = new TenantAggregateCommandAdmissionGate();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        lease.TryAdvanceReconciliation(owner, reconciliation).ShouldBeTrue();

        string prefix = action is GlobalAdministratorActionKind.Grant ? "Grant" : "Remove";
        object snapshot = typeof(GlobalAdministratorsPage)
            .GetMethod($"Create{prefix}Snapshot", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [reconciliation])!;
        if (action is GlobalAdministratorActionKind.Grant)
        {
            snapshot = ((GlobalAdministratorGrantCommandSnapshot)snapshot) with { IsDeliveryRetryWithdrawn = true };
        }
        else
        {
            snapshot = ((GlobalAdministratorRemoveCommandSnapshot)snapshot) with
            {
                LastConfirmedProjection = new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
            };
        }

        string field = action is GlobalAdministratorActionKind.Grant ? "_grantSnapshot" : "_removeSnapshot";
        typeof(GlobalAdministratorsPage).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, snapshot);
        typeof(GlobalAdministratorsPage).GetField(
            action is GlobalAdministratorActionKind.Grant ? "_grantAdmissionLease" : "_removeAdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, lease);

        bool rebuilt = (bool)typeof(GlobalAdministratorsPage)
            .GetMethod($"SynchronizeOwned{prefix}Reconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, null)!;
        rebuilt.ShouldBeFalse();
        typeof(GlobalAdministratorsPage).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page).ShouldBeSameAs(snapshot);
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant)]
    [InlineData(GlobalAdministratorActionKind.Remove)]
    public void Sibling_completion_records_the_correction_source_for_audit_reverification(
        GlobalAdministratorActionKind action)
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("tenant-sequence:1", "target-admin", "other-admin")) { RepeatLastResponse = true });
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        object owner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        GlobalAdministratorsSnapshot before = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        DateTimeOffset sourceTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var reconciliation = new GlobalAdministratorReconciliationState(
            action, "target-admin", "message-safe", "correlation-safe", TenantCommandLifecycleState.ProjectionPending,
            GrantPreview: action is GlobalAdministratorActionKind.Grant
                ? GlobalAdministratorGrantPreview.Create("target-admin", before, isAuthorized: true) : null,
            HasCommandEventEvidence: true,
            RemovePreview: action is GlobalAdministratorActionKind.Remove
                ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", before, isAuthorized: true) : null)
        {
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = sourceTime,
            SubmittedAtUtc = sourceTime.AddMinutes(1),
            CommittedEventSequence = 2,
        };
        lease.TryAdvanceReconciliation(owner, reconciliation).ShouldBeTrue();

        typeof(GlobalAdministratorsPage)
            .GetMethod("RememberCorrectionCompletion", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, [lease, TenantCommandLifecycleState.Confirmed, true,
                action is GlobalAdministratorActionKind.Grant
                    ? ComponentReady("tenant-sequence:2", "target-admin", "other-admin")
                    : ComponentReady("tenant-sequence:2", "other-admin")]);

        GlobalAdministratorReconciliationState retained = registry.PendingCompletions().ShouldHaveSingleItem();
        retained.ActionKind.ShouldBe(action);
        retained.SourceAuditReference.ShouldBe("event-original");
        retained.SourceAuditTimestampUtc.ShouldBe(sourceTime);
        retained.CommittedEventSequence.ShouldBe(2);
        retained.ConfirmedProjectionEvidence.ShouldNotBeNull().ProjectionVersion.ShouldBe("tenant-sequence:2");
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant, 1)]
    [InlineData(GlobalAdministratorActionKind.Grant, 2)]
    [InlineData(GlobalAdministratorActionKind.Remove, 1)]
    [InlineData(GlobalAdministratorActionKind.Remove, 2)]
    public async Task Correction_origin_waits_for_committed_sequence_before_sibling_confirms(
        GlobalAdministratorActionKind action,
        long committedSequence)
    {
        GlobalAdministratorsSnapshot before = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        GlobalAdministratorsSnapshot after = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:2", "target-admin", "other-admin")
            : ComponentReady("tenant-sequence:2", "other-admin");
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(after) { RepeatLastResponse = true });
        var commands = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(CommandStatus.EventsStored, EventCount: 1,
                HasVerifiedCommandIdentity: true),
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1,
                HasVerifiedCommandIdentity: true) { CommittedEventSequence = committedSequence },
        ]);
        Services.AddSingleton<ITenantCommandGateway>(commands);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        var owner = new object();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        DateTimeOffset sourceTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        lease.TryRetainReconciliation(owner, new GlobalAdministratorReconciliationState(
            action, "target-admin", "message-safe", "correlation-safe", TenantCommandLifecycleState.Accepted,
            GrantPreview: action is GlobalAdministratorActionKind.Grant
                ? GlobalAdministratorGrantPreview.Create("target-admin", before, isAuthorized: true) : null,
            RemovePreview: action is GlobalAdministratorActionKind.Remove
                ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", before, isAuthorized: true) : null)
        {
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = sourceTime,
            SubmittedAtUtc = sourceTime.AddMinutes(1),
        }).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();
        string prefix = action is GlobalAdministratorActionKind.Grant ? "grant" : "remove";
        page.WaitForAssertion(() => commands.StatusHandles.Count.ShouldBe(1));
        page.Find($"[data-testid='tenants-global-admin-{prefix}-state']")
            .TextContent.ShouldContain("pending", Case.Insensitive);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        registry.PendingCompletions().ShouldBeEmpty();

        await page.Find($"[data-testid='tenants-global-admin-{prefix}-refresh']")
            .ClickAsync(new MouseEventArgs());

        if (committedSequence == 1)
        {
            page.WaitForAssertion(() =>
            {
                object snapshot = action is GlobalAdministratorActionKind.Grant
                    ? PrivateField<GlobalAdministratorGrantCommandSnapshot>(page.Instance, "_grantSnapshot")
                    : PrivateField<GlobalAdministratorRemoveCommandSnapshot>(page.Instance, "_removeSnapshot");
                if (snapshot is GlobalAdministratorGrantCommandSnapshot grant)
                {
                    grant.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
                    grant.SafeMessageKey.ShouldBe("Tenants.Correction.Unavailable.CommandProofUnavailable");
                }
                else
                {
                    GlobalAdministratorRemoveCommandSnapshot remove = (GlobalAdministratorRemoveCommandSnapshot)snapshot;
                    remove.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
                    remove.SafeMessageKey.ShouldBe("Tenants.Correction.Unavailable.CommandProofUnavailable");
                }
            });
            if (action is GlobalAdministratorActionKind.Grant)
            {
                page.Find("[data-testid='tenants-global-admin-grant-refresh']")
                    .HasAttribute("disabled").ShouldBeTrue();
            }
            registry.PendingCompletions().ShouldBeEmpty();
        }
        else
        {
            page.WaitForAssertion(() => page.Find($"[data-testid='tenants-global-admin-{prefix}-state']")
                .TextContent.ShouldContain("Confirmed"));
            registry.PendingCompletions().ShouldHaveSingleItem().CommittedEventSequence.ShouldBe(2);
        }
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant)]
    [InlineData(GlobalAdministratorActionKind.Remove)]
    public async Task Superseded_baseline_failure_keeps_lease_until_current_requery_releases_it(
        GlobalAdministratorActionKind action)
    {
        GlobalAdministratorsSnapshot before = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        GlobalAdministratorsSnapshot after = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:2", "target-admin", "other-admin")
            : ComponentReady("tenant-sequence:2", "other-admin");
        var heldProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(before);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();

        object owner = PrivateField<object>(page.Instance, "_fixedAggregateOwner");
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        GlobalAdministratorGrantPreview? grantPreview = action is GlobalAdministratorActionKind.Grant
            ? GlobalAdministratorGrantPreview.Create("target-admin", before, isAuthorized: true) : null;
        GlobalAdministratorRemovePreview? removePreview = action is GlobalAdministratorActionKind.Remove
            ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", before, isAuthorized: true) : null;
        lease.TryAdvanceReconciliation(owner, new GlobalAdministratorReconciliationState(
            action, "target-admin", "message-safe", "correlation-safe", TenantCommandLifecycleState.ProjectionPending,
            GrantPreview: grantPreview, HasCommandEventEvidence: true, RemovePreview: removePreview)
        {
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            CommittedEventSequence = 1,
        }).ShouldBeTrue();

        bool grant = action is GlobalAdministratorActionKind.Grant;
        string prefix = grant ? "Grant" : "Remove";
        object pending = grant
            ? new GlobalAdministratorGrantCommandSnapshot(
                TenantCommandLifecycleState.ProjectionPending, new SetGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: grantPreview, HasCommandEventEvidence: true)
            : new GlobalAdministratorRemoveCommandSnapshot(
                TenantCommandLifecycleState.ProjectionPending, new RemoveGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: removePreview, HasCommandEventEvidence: true);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}AdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, lease);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}Snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, pending);

        queryGateway.QueueResponse(heldProjection.Task);
        FieldInfo generationField = typeof(GlobalAdministratorsPage).GetField(
            grant ? "_grantMutationGeneration" : "_removeMutationGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        long generation = (long)generationField.GetValue(page.Instance)!;
        MethodInfo requery = typeof(GlobalAdministratorsPage).GetMethod(
            grant ? "RequeryGrantProjectionAsync" : "RequeryRemoveProjectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: grant ? [typeof(long), typeof(CancellationToken)] : [typeof(long)],
            modifiers: null).ShouldNotBeNull();
        Task firstRead = (Task)requery.Invoke(page.Instance, grant
            ? [generation, CancellationToken.None] : [generation])!;
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        generationField.SetValue(page.Instance, generation + 1);
        heldProjection.SetResult(after);
        await firstRead.WaitAsync(TimeSpan.FromSeconds(5));

        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        object current = grant
            ? PrivateField<GlobalAdministratorGrantCommandSnapshot>(page.Instance, "_grantSnapshot")
            : PrivateField<GlobalAdministratorRemoveCommandSnapshot>(page.Instance, "_removeSnapshot");
        current.ShouldBeSameAs(pending);

        Task secondRead = (Task)requery.Invoke(page.Instance, grant
            ? [generation + 1, CancellationToken.None] : [generation + 1])!;
        await secondRead.WaitAsync(TimeSpan.FromSeconds(5));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        registry.PendingCompletions().ShouldBeEmpty();
        if (grant)
        {
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(page.Instance, "_grantSnapshot")
                .State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            page.Find("[data-testid='tenants-global-admin-grant-refresh']")
                .HasAttribute("disabled").ShouldBeTrue();
        }
        else
        {
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(page.Instance, "_removeSnapshot")
                .State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            page.FindAll("[data-testid='tenants-global-admin-remove-refresh']")
                .ShouldAllBe(button => button.HasAttribute("disabled"));
        }
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant)]
    [InlineData(GlobalAdministratorActionKind.Remove)]
    public async Task Superseded_projection_read_retains_the_confirming_projection_for_correction_proof(
        GlobalAdministratorActionKind action)
    {
        GlobalAdministratorsSnapshot before = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        GlobalAdministratorsSnapshot after = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:2", "target-admin", "other-admin")
            : ComponentReady("tenant-sequence:2", "other-admin");
        var heldProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(before);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();

        object owner = PrivateField<object>(page.Instance, "_fixedAggregateOwner");
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        GlobalAdministratorGrantPreview? grantPreview = action is GlobalAdministratorActionKind.Grant
            ? GlobalAdministratorGrantPreview.Create("target-admin", before, isAuthorized: true) : null;
        GlobalAdministratorRemovePreview? removePreview = action is GlobalAdministratorActionKind.Remove
            ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", before, isAuthorized: true) : null;
        DateTimeOffset sourceTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        lease.TryAdvanceReconciliation(owner, new GlobalAdministratorReconciliationState(
            action, "target-admin", "message-safe", "correlation-safe", TenantCommandLifecycleState.ProjectionPending,
            GrantPreview: grantPreview, HasCommandEventEvidence: true, RemovePreview: removePreview)
        {
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = sourceTime,
            SubmittedAtUtc = sourceTime.AddMinutes(1),
            CommittedEventSequence = 2,
        }).ShouldBeTrue();

        bool grant = action is GlobalAdministratorActionKind.Grant;
        string prefix = grant ? "Grant" : "Remove";
        object pending = grant
            ? new GlobalAdministratorGrantCommandSnapshot(
                TenantCommandLifecycleState.ProjectionPending, new SetGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: grantPreview, HasCommandEventEvidence: true)
            : new GlobalAdministratorRemoveCommandSnapshot(
                TenantCommandLifecycleState.ProjectionPending, new RemoveGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: removePreview, HasCommandEventEvidence: true);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}AdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, lease);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}Snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, pending);

        queryGateway.QueueResponse(heldProjection.Task);
        FieldInfo generationField = typeof(GlobalAdministratorsPage).GetField(
            grant ? "_grantMutationGeneration" : "_removeMutationGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        long generation = (long)generationField.GetValue(page.Instance)!;
        MethodInfo requery = typeof(GlobalAdministratorsPage).GetMethod(
            grant ? "RequeryGrantProjectionAsync" : "RequeryRemoveProjectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: grant ? [typeof(long), typeof(CancellationToken)] : [typeof(long)],
            modifiers: null).ShouldNotBeNull();
        Task read = (Task)requery.Invoke(page.Instance, grant
            ? [generation, CancellationToken.None] : [generation])!;
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        generationField.SetValue(page.Instance, generation + 1);
        heldProjection.SetResult(after);
        await read.WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorReconciliationState completion = registry.PendingCompletions().ShouldHaveSingleItem();
        completion.ConfirmedProjectionEvidence.ShouldNotBeNull().ProjectionVersion.ShouldBe("tenant-sequence:2");
        completion.ConfirmedProjectionEvidence.Rows.Select(row => row.UserId)
            .ShouldBe(after.Rows.Select(row => row.UserId), ignoreOrder: true);
        completion.SourceAuditReference.ShouldBe("event-original");
        completion.CommittedEventSequence.ShouldBe(2);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        object retainedSnapshot = grant
            ? PrivateField<GlobalAdministratorGrantCommandSnapshot>(page.Instance, "_grantSnapshot")
            : PrivateField<GlobalAdministratorRemoveCommandSnapshot>(page.Instance, "_removeSnapshot");
        retainedSnapshot.ShouldBeSameAs(pending);
    }

    [Theory]
    [InlineData(GlobalAdministratorActionKind.Grant)]
    [InlineData(GlobalAdministratorActionKind.Remove)]
    public void Superseded_confirmed_projection_completion_records_correction_source_and_releases_lease(
        GlobalAdministratorActionKind action)
    {
        GlobalAdministratorsSnapshot before = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:1", "other-admin")
            : ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        GlobalAdministratorsSnapshot after = action is GlobalAdministratorActionKind.Grant
            ? ComponentReady("tenant-sequence:2", "target-admin", "other-admin")
            : ComponentReady("tenant-sequence:2", "other-admin");
        DateTimeOffset sourceTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(before));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();
        object owner = PrivateField<object>(page.Instance, "_fixedAggregateOwner");
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        GlobalAdministratorGrantPreview? grantPreview = action is GlobalAdministratorActionKind.Grant
            ? GlobalAdministratorGrantPreview.Create("target-admin", before, isAuthorized: true) : null;
        GlobalAdministratorRemovePreview? removePreview = action is GlobalAdministratorActionKind.Remove
            ? GlobalAdministratorRemovePreview.Create("target-admin", "operator-admin", before, isAuthorized: true) : null;
        lease.TryAdvanceReconciliation(owner, new GlobalAdministratorReconciliationState(
            action, "target-admin", "message-safe", "correlation-safe", TenantCommandLifecycleState.ProjectionPending,
            GrantPreview: grantPreview, HasCommandEventEvidence: true, RemovePreview: removePreview)
        {
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = sourceTime,
            SubmittedAtUtc = sourceTime.AddMinutes(1),
            CommittedEventSequence = 2,
        }).ShouldBeTrue();

        string prefix = action is GlobalAdministratorActionKind.Grant ? "Grant" : "Remove";
        object completed = action is GlobalAdministratorActionKind.Grant
            ? new GlobalAdministratorGrantCommandSnapshot(
                TenantCommandLifecycleState.Confirmed, new SetGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: grantPreview, HasCommandEventEvidence: true)
            : new GlobalAdministratorRemoveCommandSnapshot(
                TenantCommandLifecycleState.Confirmed, new RemoveGlobalAdministrator("target-admin"),
                MessageId: "message-safe", CorrelationId: "correlation-safe",
                PreviewEvidence: removePreview, HasCommandEventEvidence: true);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}AdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, lease);
        typeof(GlobalAdministratorsPage).GetField($"_{prefix.ToLowerInvariant()}Snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page.Instance, completed);

        typeof(GlobalAdministratorsPage).GetMethod($"RetainOrRelease{prefix}Completion",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page.Instance, [completed, lease, after]);

        GlobalAdministratorReconciliationState pending = registry.PendingCompletions().ShouldHaveSingleItem();
        pending.SourceAuditReference.ShouldBe("event-original");
        pending.SourceAuditTimestampUtc.ShouldBe(sourceTime);
        pending.CommittedEventSequence.ShouldBe(2);
        pending.ConfirmedProjectionEvidence.ShouldBe(after);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void Rendered_revoke_handoff_retains_completion_for_a_verified_audit_pair()
    {
        GlobalAdministratorsSnapshot before = ComponentReady("tenant-sequence:1", "target-admin", "other-admin");
        GlobalAdministratorsSnapshot after = ComponentReady("tenant-sequence:2", "other-admin");
        DateTimeOffset sourceTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(after));
        var commands = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1,
                HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        ]);
        Services.AddSingleton<ITenantCommandGateway>(commands);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        var owner = new object();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.TryAcquireLease(TenantCommandAggregateLock.ForGlobalAdministrators(), owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        lease.TryRetainReconciliation(owner, new GlobalAdministratorReconciliationState(
            GlobalAdministratorActionKind.Remove, "target-admin", "message-safe", "correlation-safe",
            TenantCommandLifecycleState.Accepted,
            RemovePreview: GlobalAdministratorRemovePreview.Create(
                "target-admin", "operator-admin", before, isAuthorized: true))
        {
            HasCommandEventEvidence = true,
            SourceAuditReference = "event-original",
            SourceAuditTimestampUtc = sourceTime,
            SubmittedAtUtc = sourceTime.AddMinutes(1),
            CommittedEventSequence = 2,
        }).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();
        page.WaitForAssertion(() => page.Find("[data-testid='tenants-global-admin-remove-state']")
            .TextContent.ShouldContain("Confirmed"));
        GlobalAdministratorReconciliationState completion = registry.PendingCompletions().ShouldHaveSingleItem();
        completion.ActionKind.ShouldBe(GlobalAdministratorActionKind.Remove);
        completion.SourceAuditReference.ShouldBe("event-original");
        completion.SourceAuditTimestampUtc.ShouldBe(sourceTime);
        completion.CommittedEventSequence.ShouldBe(2);
        completion.ConfirmedProjectionEvidence.ShouldNotBeNull().ProjectionVersion.ShouldBe("tenant-sequence:2");
        commands.StatusHandles.ShouldHaveSingleItem().MessageId.ShouldBe("message-safe");

        TenantAuditRow original = new(
            "event-original", "GlobalAdministratorSet", AuditEventCategory.Access,
            "operator-admin", sourceTime, "system", "target-admin", "system", "GlobalAdministratorSet",
            "userId: target-admin", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked, new TenantAuditNarrative(UserId: "target-admin"));
        TenantAuditRow corrective = original with
        {
            EventReference = "event-corrective",
            EventType = "GlobalAdministratorRemoved",
            Outcome = "GlobalAdministratorRemoved",
            Timestamp = sourceTime.AddMinutes(2),
            CommandCorrelationId = "correlation-safe",
            AggregateSequence = 2,
        };
        GlobalAdministratorCorrectionSnapshot? verified = GlobalAdministratorCorrectionSnapshot.RebuildVerifiedCompletion(
            completion, original, after, new GlobalAdministratorAuditProof(corrective));
        verified.ShouldNotBeNull().AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        verified.ProofLink.ShouldNotBeNull().OriginalAuditReference.ShouldBe("event-original");
        verified.ProofLink.CorrectiveAuditReference.ShouldBe("event-corrective");

        GlobalAdministratorCorrectionSnapshot.RebuildVerifiedCompletion(
            completion, original, after with { ProjectionVersion = "tenant-sequence:1" },
            new GlobalAdministratorAuditProof(corrective)).ShouldBeNull();
        GlobalAdministratorCorrectionSnapshot.RebuildVerifiedCompletion(
            completion, original, after with { IsCompleteEvidence = false },
            new GlobalAdministratorAuditProof(corrective)).ShouldBeNull();
        GlobalAdministratorCorrectionSnapshot? retainedAfterReversal =
            GlobalAdministratorCorrectionSnapshot.RebuildVerifiedCompletion(
                completion, original, before with { ProjectionVersion = "tenant-sequence:3" },
                new GlobalAdministratorAuditProof(corrective));
        retainedAfterReversal.ShouldNotBeNull().ProofLink.ShouldNotBeNull()
            .CorrectiveAuditReference.ShouldBe("event-corrective");
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("rejected")]
    public async Task SupersededAmbiguousRemoveRetryRetainsOrReleasesItsExactLease(string outcome)
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveSubmission = TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        string messageId = commandGateway.RemoveMessageIds.ShouldHaveSingleItem();

        commandGateway.RemoveSubmission = outcome == "accepted"
            ? TenantCommandSubmissionResult.Accepted("ignored", "correlation-retry")
            : TenantCommandSubmissionResult.Rejected("rejected", "LastGlobalAdministrator");
        var retryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commandGateway.RemoveSubmissionGate = retryGate;
        commandGateway.StatusGate = outcome == "accepted" ? statusGate : null;
        Task retry = cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        IRenderedComponent<GlobalAdministratorsPage> replacement = Render<GlobalAdministratorsPage>();
        await WaitUntilAsync(
            () => PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                .MessageId == messageId,
            TimeSpan.FromSeconds(5));
        retryGate.SetResult();

        commandGateway.RemoveMessageIds.ShouldAllBe(id => id == messageId);
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        if (outcome == "accepted")
        {
            await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            replacement.WaitForAssertion(() =>
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                    .CorrelationId.ShouldBe("correlation-retry"));
            admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
            GlobalAdministratorRemoveCommandSnapshot retained =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot");
            retained.MessageId.ShouldBe(messageId);
            retained.CorrelationId.ShouldBe("correlation-retry");
            statusGate.SetResult();
        }
        else
        {
            replacement.WaitForAssertion(() =>
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(replacement.Instance, "_removeSnapshot")
                    .State.ShouldBe(TenantCommandLifecycleState.Rejected));
            var replacementOwner = new object();
            admissionGate.TryAcquireLease(
                TenantCommandAggregateLock.ForGlobalAdministrators(),
                replacementOwner,
                out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
            replacementLease!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
        }

        await retry.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GrantFocusHelperSwallowsDetachedElementJsException()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true)
            .SetException(new JSException("Focus target detached."));
        MethodInfo helper = typeof(GlobalAdministratorsPage).GetMethod(
            "FocusSafelyAsync",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();
        ElementReference launcher = PrivateField<ElementReference>(cut.Instance, "_grantLauncherElement");

        Task invocation = (Task)helper.Invoke(null, [launcher])!;
        await invocation;
    }

    [Fact]
    public async Task GrantFocusHelperSwallowsRendererInvalidOperationException()
    {
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a")));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true)
            .SetException(new InvalidOperationException("Renderer is no longer interactive."));
        MethodInfo helper = typeof(GlobalAdministratorsPage).GetMethod(
            "FocusSafelyAsync",
            BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();
        ElementReference launcher = PrivateField<ElementReference>(cut.Instance, "_grantLauncherElement");

        Task invocation = (Task)helper.Invoke(null, [launcher])!;
        await invocation;
    }

    /// <summary>
    /// Pins the classes the mobile read-only breakpoint depends on to the rendered DOM.
    /// </summary>
    /// <remarks>
    /// The stylesheet assertions above cannot show that any element actually carries these classes. The
    /// previous regex-only check passed while the notice was permanently visible on desktop and while the
    /// per-row Remove launcher sat outside every selector the breakpoint could match, so dropping a class
    /// from the markup changed nothing it observed.
    /// </remarks>
    [Fact]
    public void Mobile_read_only_notice_and_mutation_launchers_are_present_in_the_rendered_dom()
    {
        GlobalAdministratorsSnapshot snapshot = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("admin-2", ReadModelFreshnessState.Current),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(snapshot));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.FindAll("[data-testid='tenants-global-admins-mobile-readonly']")
            .ShouldNotBeEmpty("the mobile read-only notice must exist for the breakpoint rule to reveal it");

        // The host wrapper is the element the breakpoint actually hides and reveals. It must be plain HTML
        // rendered by this component, because that is the only thing CSS isolation will scope; asserting the
        // class on the FluentMessageBar alone is what let the permanently-visible-on-desktop defect survive.
        cut.FindAll("div.global-admins__mobile-readonly-host")
            .ShouldNotBeEmpty("the notice must sit inside a plain scoped host for the breakpoint rule to match");

        // Every per-row Remove launcher must be hideable, not just the grant/remove panel controls.
        cut.FindAll("[data-testid='tenants-global-admin-remove']").Count.ShouldBe(2);
        cut.FindAll(".global-admins__mutation-initiation").Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Routes_stay_reachable_while_tenants_nav_collapses_to_one_module_entry()
    {
        string projectRoot = ProjectRoot();
        string page = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "GlobalAdministratorsPage.razor"));
        string myTenantsPage = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "MyTenantsPage.razor"));
        string userLookupPage = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "UserMembershipLookupPage.razor"));
        string workspace = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "TenantsWorkspace.razor"));
        string registration = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Composition", "TenantsFrontComposerRegistration.cs"));
        string detail = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Pages", "TenantDetailPage.razor"));
        string routes = File.ReadAllText(
            Path.Combine(projectRoot, "src", "Hexalith.Tenants.UI", "Components", "Routes.razor"));

        page.ShouldContain("@page \"/global-administrators\"");

        // Endpoint authorization is attached conditionally by Program.cs when OIDC is configured, not as
        // static component metadata. The component must therefore remain routable on the Keycloak-disabled
        // topology, where its rendered state is the fail-closed authority.
        page.ShouldNotContain("@attribute [Authorize");
        routes.ShouldNotContain("<AuthorizeRouteView");
        routes.ShouldContain("<RouteView");
        myTenantsPage.ShouldContain("@page \"/tenants/my\"");
        userLookupPage.ShouldContain("@page \"/tenants/users\"");

        // Correct Course 2026-06-27: the shell rail exposes one Tenants module entry. My Tenants,
        // User lookup, and Global Administrators remain implemented routes, but they are no longer
        // registered as Tenants left-menu entries.
        registration.ShouldContain("\"/tenants\"");
        registration.ShouldNotContain("\"/tenants/my\"");
        registration.ShouldNotContain("\"/tenants/users\"");
        registration.ShouldNotContain("\"/global-administrators\"");
        workspace.ShouldNotContain("href=\"/users\"");
        workspace.ShouldNotContain("href=\"/tenants/my\"");
        workspace.ShouldNotContain("href=\"/tenants/users\"");
        detail.ShouldContain("TenantAuditNavigationSafety.SafeListReturnUrl(ReturnUrl, out bool strippedCursor)");
    }

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string ReadGlobalAdministratorsPageSource()
        => File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Pages",
            "GlobalAdministratorsPage.razor")).ReplaceLineEndings("\n");

    private static string ExtractMethodBody(string source, string methodSignature)
    {
        int start = source.IndexOf(methodSignature, StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, $"Method '{methodSignature}' was not found in GlobalAdministratorsPage.razor.");
        int braceStart = source.IndexOf('{', start);
        braceStart.ShouldBeGreaterThan(start);
        int depth = 0;
        for (int index = braceStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not extract the body for '{methodSignature}'.");
    }

    private static ClaimsPrincipal GlobalAdministratorPrincipal()
        => new(new ClaimsIdentity(
        [
            new Claim("sub", "operator.alpha"),
            new Claim("role", "global-administrator"),
            new Claim("eventstore:tenant", "system"),
        ],
        "test"));

    private static ClaimsPrincipal NonAdministratorPrincipal()
        => new(new ClaimsIdentity([new Claim("sub", "operator.alpha")], "test"));

    private static HashSet<string> ResourceKeys(string path, string prefix)
        => System.Xml.Linq.XDocument
            .Load(path)
            .Descendants("data")
            .Select(static element => element.Attribute("name")?.Value)
            .Where(name => name is not null && name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// A page reporting more results with no cursor to reach them must render Next disabled rather than
    /// clickable-and-silent. Every other Next assertion in this file clicks a snapshot that carries a cursor,
    /// so dropping the blank-cursor clause from the binding stayed green.
    /// </summary>
    [Fact]
    public void Next_is_disabled_when_the_service_reports_more_results_without_a_cursor()
    {
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.only-page", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: true,
            eTag: "\"no-cursor\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.Find("[data-testid='tenants-global-admins-next']").HasAttribute("disabled").ShouldBeTrue();
        gateway.GlobalAdministratorCalls.ShouldBe(1);
    }

    /// <summary>
    /// Grant requery must not confirm from a snapshot object that is no longer the one on screen. The pagers
    /// already guard with <c>ReferenceEquals</c>; without it on the requery path a notification refresh landing
    /// mid-requery would report "confirmed" from rows the operator never saw.
    /// </summary>
    [Fact]
    public async Task Grant_requery_does_not_confirm_from_a_superseded_snapshot()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        GlobalAdministratorsSnapshot notificationSnapshot = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("notification-admin", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"notification\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        GlobalAdministratorsSnapshot confirmingSnapshot = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("target-user", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"confirmed\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        var queryGateway = new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1));
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        await subscription.Received(1).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());

        queryGateway.QueueResponse(Task.Run(async () =>
        {
            await Task.Delay(100);
            return confirmingSnapshot;
        }));
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        Task grantSubmit = cut.Find("[data-testid='tenants-global-admin-grant-form']").SubmitAsync();
        queryGateway.QueueResponse(Task.FromResult(notificationSnapshot));
        await cut.InvokeAsync(() =>
        {
            notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system");
            return Task.CompletedTask;
        });
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("notification-admin"));

        await grantSubmit;

        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent
            .ShouldNotContain("Projection confirmed the target user", Case.Insensitive);
        cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldBe("notification-admin");
    }

    /// <summary>
    /// The remove requery path carries the same supersession guard as grant. Without
    /// <c>ReferenceEquals(_snapshot, snapshot)</c> a mid-requery notification could confirm removal while the
    /// operator still sees the target on screen.
    /// </summary>
    [Fact]
    public async Task Remove_requery_does_not_confirm_from_a_superseded_snapshot()
    {
        GlobalAdministratorsSnapshot baseline = GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"baseline\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "projection-v1",
            IsCompleteEvidence = true,
        };
        GlobalAdministratorsSnapshot confirmingSnapshot = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"confirmed\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "projection-v2",
            IsCompleteEvidence = true,
        };
        var heldRequery = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(baseline) { RepeatLastResponse = true };
        var commandGateway = new StubTenantCommandGateway(statuses: [new TenantCommandStatusResult(
            CommandStatus.Completed,
            EventCount: 1,
            HasVerifiedCommandIdentity: true)])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("message-remove", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        queryGateway.QueueResponse(Task.FromResult(baseline));
        queryGateway.QueueResponse(heldRequery.Task);
        Task removeSubmit = cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
        await WaitUntilAsync(() => queryGateway.GlobalAdministratorCalls >= 4, TimeSpan.FromSeconds(5));

        GlobalAdministratorRemoveCommandSnapshot attemptA =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        GlobalAdministratorRemoveCommandSnapshot sameGenerationAttemptB = attemptA with
        {
            MessageId = NUlid.Ulid.NewUlid().ToString(),
            CorrelationId = "correlation-b",
            State = TenantCommandLifecycleState.Accepted,
            HasCommandEventEvidence = false,
        };
        typeof(GlobalAdministratorsPage).GetField(
            "_removeSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, sameGenerationAttemptB);
        heldRequery.SetResult(confirmingSnapshot);

        await removeSubmit;

        cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent
            .ShouldNotContain("Projection confirmed removal", Case.Insensitive);
        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .ShouldBe(sameGenerationAttemptB);
        cut.FindAll("[data-testid='tenants-global-admins-user-id']")
            .Select(static element => element.TextContent)
            .ShouldContain("target-admin");
    }

    /// <summary>
    /// Behavioral requery races can be invalidated by load-generation before <c>ReferenceEquals</c> is
    /// reached, so deleting that clause alone can stay green. Pin both requery sites structurally.
    /// </summary>
    [Fact]
    public void GrantRequeryPreservesPagedDisplayWhileRemoveKeepsSnapshotIdentityGuard()
    {
        string source = ReadGlobalAdministratorsPageSource();
        string grantRequery = ExtractMethodBody(
            source,
            "private async Task RequeryGrantProjectionAsync(\n        long generation,");
        string removeRequery = ExtractMethodBody(source, "private async Task RequeryRemoveProjectionAsync(long generation)");

        grantRequery.ShouldNotContain("_snapshot = snapshot");
        grantRequery.ShouldContain("_completeSnapshot = snapshot");
        grantRequery.ShouldContain("LoadAsync(reuseETag: false, retainConfirmed: true)");
        grantRequery.ShouldContain("ReferenceEquals(_grantSnapshot, projectionBasis)");
        grantRequery.ShouldContain("ReferenceEquals(_grantAdmissionLease, projectionLease)");
        Regex.IsMatch(
            removeRequery,
            @"ReferenceEquals\s*\(\s*_removeSnapshot\s*,\s*projectionBasis\s*\)",
            RegexOptions.CultureInvariant).ShouldBeTrue();
    }

    [Fact]
    public void Status_and_projection_writes_recheck_generation_and_snapshot_identity_inside_renderer_callbacks()
    {
        string source = ReadGlobalAdministratorsPageSource();
        AssertGuardsInsideRendererCallback(
            ExtractMethodBody(source, "private async Task RefreshGrantStatusCoreAsync(\n        long generation,"),
            "SetGrantSnapshot(statusSnapshot)",
            "CanApplyGrantMutation(generation)",
            "ReferenceEquals(_grantSnapshot, statusBasis)",
            "ReferenceEquals(_grantAdmissionLease, statusLease)");
        AssertGuardsInsideRendererCallback(
            ExtractMethodBody(
                source,
                "private async Task RequeryGrantProjectionAsync(\n        long generation,"),
            "SetGrantSnapshot(projectionSnapshot, snapshot)",
            "CanApplyGrantMutation(generation)",
            "ReferenceEquals(_grantSnapshot, projectionBasis)",
            "ReferenceEquals(_grantAdmissionLease, projectionLease)");
        AssertGuardsInsideRendererCallback(
            ExtractMethodBody(source, "private async Task RefreshRemoveStatusCoreAsync("),
            "SetRemoveSnapshot(statusSnapshot)",
            "CanApplyRemoveMutation(generation)",
            "ReferenceEquals(_removeSnapshot, statusBasis)",
            "ReferenceEquals(_removeAdmissionLease, statusLease)");
        AssertGuardsInsideRendererCallback(
            ExtractMethodBody(source, "private async Task RequeryRemoveProjectionAsync(long generation)"),
            "SetRemoveSnapshot(projectionSnapshot, snapshot)",
            "CanApplyRemoveMutation(generation)",
            "ReferenceEquals(_removeSnapshot, projectionBasis)",
            "ReferenceEquals(_removeAdmissionLease, projectionLease)");
    }

    [Fact]
    public void AdmissionWakeupPinsTheCapturedGrantAttempt()
    {
        string source = ReadGlobalAdministratorsPageSource();
        string admissionWakeup = ExtractMethodBody(
            source,
            "private async Task ApplyFixedAggregateAdmissionChangeAsync()");
        string capturedRefresh = ExtractMethodBody(
            source,
            "private async Task RefreshGrantStatusAsync(\n        GlobalAdministratorGrantCommandSnapshot activationSnapshot,");

        admissionWakeup.ShouldContain("resumeGrantLease = _grantAdmissionLease");
        admissionWakeup.ShouldContain("RefreshGrantStatusAsync(resumeGrant, resumeGrantLease)");
        capturedRefresh.ShouldContain("ReferenceEquals(_grantSnapshot, activationSnapshot)");
        capturedRefresh.ShouldContain("ReferenceEquals(_grantAdmissionLease, activationLease)");
    }

    [Fact]
    public async Task AdmissionWakeupCapturedForAttemptACannotQueryAttemptB()
    {
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-a"),
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-a");
        PreviewAcknowledgeAndConfirmGrant(cut);
        commandGateway.StatusHandles.Count.ShouldBe(1);
        GlobalAdministratorGrantCommandSnapshot attemptA =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        TenantAggregateCommandLease leaseA =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease");
        GlobalAdministratorGrantCommandSnapshot attemptB = attemptA with
        {
            MessageId = NUlid.Ulid.NewUlid().ToString(),
            CorrelationId = "correlation-b",
        };
        object pageOwner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        var replacementGate = new TenantAggregateCommandAdmissionGate();
        replacementGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            pageOwner,
            out TenantAggregateCommandLease? leaseB).ShouldBeTrue();
        leaseB.ShouldNotBeNull().TryMarkDispatched(pageOwner).ShouldBeTrue();

        leaseA.Reconciliation = leaseA.Reconciliation.ShouldNotBeNull() with
        {
            LifecycleState = TenantCommandLifecycleState.ProjectionPending,
            HasCommandEventEvidence = true,
        };
        var resolutionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = resolutionGate;
        MethodInfo applyAdmissionChange = typeof(GlobalAdministratorsPage).GetMethod(
            "ApplyFixedAggregateAdmissionChangeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        Task wakeup = (Task)applyAdmissionChange.Invoke(cut.Instance, null)!;
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() =>
        {
            typeof(GlobalAdministratorsPage).GetField(
                "_grantSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, attemptB);
            typeof(GlobalAdministratorsPage).GetField(
                "_grantAdmissionLease",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, leaseB);
        });
        resolutionGate.SetResult();
        await wakeup.WaitAsync(TimeSpan.FromSeconds(5));

        commandGateway.StatusHandles.Count.ShouldBe(1);
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(1);
        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .ShouldBeSameAs(attemptB);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease")
            .ShouldBeSameAs(leaseB);
        replacementGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        leaseA.IsDispatchMarked.ShouldBeTrue();
    }

    [Fact]
    public async Task HeldRemoveStatusCannotMutateOrReleaseReplacementLease()
    {
        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
            new TenantCommandStatusResult(
                CommandStatus.Rejected,
                RejectionCode: "InsufficientPermissions",
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ComponentReady("projection-v1", "target-admin", "other-admin")));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        commandGateway.StatusHandles.Count.ShouldBe(1);
        GlobalAdministratorRemoveCommandSnapshot attempt =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        commandGateway.StatusGate = statusGate;
        Task refresh = cut.Find("[data-testid='tenants-global-admin-remove-refresh']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        object pageOwner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        var replacementGate = new TenantAggregateCommandAdmissionGate();
        replacementGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            pageOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease.ShouldNotBeNull().TryMarkDispatched(pageOwner).ShouldBeTrue();
        await cut.InvokeAsync(() => typeof(GlobalAdministratorsPage).GetField(
            "_removeAdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, replacementLease));

        statusGate.SetResult();
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .ShouldBeSameAs(attempt);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease")
            .ShouldBeSameAs(replacementLease);
        replacementLease.IsDispatchMarked.ShouldBeTrue();
        replacementGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task HeldGrantProjectionCannotMutateOrReleaseReplacementLease()
    {
        var heldProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        };
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);
        GlobalAdministratorGrantCommandSnapshot attempt =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        attempt.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        queryGateway.QueueResponse(heldProjection.Task);
        long generation = PrivateField<long>(cut.Instance, "_grantMutationGeneration");
        MethodInfo requery = typeof(GlobalAdministratorsPage).GetMethod(
            "RequeryGrantProjectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(long), typeof(CancellationToken)],
            modifiers: null).ShouldNotBeNull();
        Task projection = (Task)requery.Invoke(cut.Instance, [generation, CancellationToken.None])!;
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        object pageOwner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        var replacementGate = new TenantAggregateCommandAdmissionGate();
        replacementGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            pageOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease.ShouldNotBeNull().TryMarkDispatched(pageOwner).ShouldBeTrue();
        await cut.InvokeAsync(() => typeof(GlobalAdministratorsPage).GetField(
            "_grantAdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, replacementLease));

        heldProjection.SetResult(ComponentReady("projection-v2", "admin-a", "target-admin"));
        await projection.WaitAsync(TimeSpan.FromSeconds(5));

        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .ShouldBeSameAs(attempt);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease")
            .ShouldBeSameAs(replacementLease);
        replacementLease.IsDispatchMarked.ShouldBeTrue();
        replacementGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task HeldGrantProjectionCannotOverwriteReplacementSnapshotWithSameLease()
    {
        var heldProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a"));
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        PreviewAcknowledgeAndConfirmGrant(cut);
        GlobalAdministratorGrantCommandSnapshot attempt =
            PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
        attempt.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        queryGateway.QueueResponse(heldProjection.Task);
        long generation = PrivateField<long>(cut.Instance, "_grantMutationGeneration");
        MethodInfo requery = typeof(GlobalAdministratorsPage).GetMethod(
            "RequeryGrantProjectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(long), typeof(CancellationToken)],
            modifiers: null).ShouldNotBeNull();
        Task projection = (Task)requery.Invoke(cut.Instance, [generation, CancellationToken.None])!;
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorGrantCommandSnapshot replacement = attempt with
        {
            SafeMessageKey = "Tenants.GlobalAdministrators.Grant.Status.Pending",
        };
        await cut.InvokeAsync(() => typeof(GlobalAdministratorsPage).GetField(
            "_grantSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, replacement));

        heldProjection.SetResult(ComponentReady("projection-v2", "admin-a", "target-admin"));
        await projection.WaitAsync(TimeSpan.FromSeconds(5));

        PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
            .ShouldBeSameAs(replacement);
        replacement.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
    }

    [Fact]
    public async Task HeldRemoveProjectionCannotMutateOrReleaseReplacementLease()
    {
        GlobalAdministratorsSnapshot baseline = ComponentReady(
            "projection-v1",
            "target-admin",
            "other-admin");
        GlobalAdministratorsSnapshot confirmation = ComponentReady("projection-v2", "other-admin");
        var heldProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queryGateway = new StubTenantQueryGateway(baseline);
        var commandGateway = new StubTenantCommandGateway(statuses:
        [
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        ])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        await cut.Find("[data-testid='tenants-global-admin-remove-submit']")
            .ClickAsync(new MouseEventArgs());
        GlobalAdministratorRemoveCommandSnapshot attempt =
            PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
        attempt.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        queryGateway.QueueResponse(heldProjection.Task);
        long generation = PrivateField<long>(cut.Instance, "_removeMutationGeneration");
        MethodInfo requery = typeof(GlobalAdministratorsPage).GetMethod(
            "RequeryRemoveProjectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(long)],
            modifiers: null).ShouldNotBeNull();
        Task projection = (Task)requery.Invoke(cut.Instance, [generation])!;
        await queryGateway.QueuedResponseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        object pageOwner = PrivateField<object>(cut.Instance, "_fixedAggregateOwner");
        var replacementGate = new TenantAggregateCommandAdmissionGate();
        replacementGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            pageOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease.ShouldNotBeNull().TryMarkDispatched(pageOwner).ShouldBeTrue();
        await cut.InvokeAsync(() => typeof(GlobalAdministratorsPage).GetField(
            "_removeAdmissionLease",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, replacementLease));

        heldProjection.SetResult(confirmation);
        await projection.WaitAsync(TimeSpan.FromSeconds(5));

        PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
            .ShouldBeSameAs(attempt);
        PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease")
            .ShouldBeSameAs(replacementLease);
        replacementLease.IsDispatchMarked.ShouldBeTrue();
        replacementGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task UnauthorizedGrantProjectionWalkCollapsesPrivilegedSurface()
    {
        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-grant"),
            new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true))
        {
            StatusGate = statusGate,
        };
        var queryGateway = new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a"));
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-admin");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        Task submit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queryGateway.QueueResponse(Task.FromResult(GlobalAdministratorsSnapshot.Unauthorized()));
        statusGate.SetResult();

        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-global-admins-unavailable']");
        cut.Markup.ShouldNotContain("admin-a");
        cut.FindAll("[data-testid='tenants-global-admin-grant-form']").ShouldBeEmpty();
        PrivateField<TenantLifecycleAuthorizationReflectionState>(cut.Instance, "_authorizationReflection")
            .ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
    }

    [Fact]
    public void Submission_lease_arming_and_component_field_writes_are_renderer_guarded()
    {
        string source = ReadGlobalAdministratorsPageSource();
        string grantSubmit = ExtractMethodBody(source, "private async Task SubmitGrantAsync()");
        string grantDispatch = ExtractMethodBody(source, "private async Task DispatchGrantAsync(");
        string grantCompletion = ExtractMethodBody(source, "private async Task PublishGrantDeliveryCompletionAsync(");
        string removePreview = ExtractMethodBody(source, "private async Task PreviewRemoveAsync(");
        string removeSubmit = ExtractMethodBody(source, "private async Task SubmitRemoveAsync()");

        grantSubmit.IndexOf("if (IsGrantSubmitDisabled)", StringComparison.Ordinal).ShouldBeGreaterThan(-1);
        grantSubmit.IndexOf("if (IsGrantSubmitDisabled)", StringComparison.Ordinal)
            .ShouldBeLessThan(grantSubmit.IndexOf("_grantValidationMessage = null", StringComparison.Ordinal));
        AssertGuardsInsideRendererCallback(
            grantSubmit,
            "_grantAdmissionLease = acquiredLease",
            "CanApplyGrantPreviewOperation(generation, previewGeneration, targetUserId)",
            "_grantAdmissionLease is not null");
        AssertGuardsInsideRendererCallback(
            grantDispatch,
            "SetGrantSnapshot(requestSent)",
            "CanApplyGrantMutation(generation)",
            "MatchesGrantPreviewAttempt(",
            "expectedLease.TryBeginInitialReconciliationDispatch(");
        grantDispatch.ShouldContain("expectedLease.TryCompleteReconciliationDispatch(completionToken, durableCompletion)");
        grantDispatch.ShouldContain("await PublishGrantDeliveryCompletionAsync(");
        AssertGuardsInsideRendererCallback(
            grantCompletion,
            "SetGrantSnapshot(completionSnapshot)",
            "CanApplyGrantMutation(generation)",
            "ReferenceEquals(_grantSnapshot, requestSentSnapshot)",
            "ReferenceEquals(_grantAdmissionLease, submissionLease)");
        AssertGuardsInsideRendererCallback(
            removePreview,
            "_removeAdmissionLease = lease",
            "CanApplyRemoveMutation(generation)",
            "_aggregateAdmissionGate.TryAcquireLease(");
        AssertGuardsInsideRendererCallback(
            removeSubmit,
            "SetRemoveSnapshot(requestSent)",
            "CanApplyRemoveMutation(generation)",
            "submissionLease.TryBeginInitialReconciliationDispatch(");
        removeSubmit.ShouldContain("_ = submissionLease.TryAbandonBeforeDispatch(_fixedAggregateOwner);");
        grantSubmit.ShouldNotContain("out _grantAdmissionLease");
        removePreview.ShouldNotContain("out _removeAdmissionLease");
    }

    /// <summary>
    /// <c>ReauthorizeAsync</c> captures a transition version before resolving. A sign-out landing while grant
    /// submission is re-authorizing must not be overwritten by the pre-sign-out answer and must not dispatch
    /// the platform command.
    /// </summary>
    [Theory]
    [InlineData("authorization", "cancel")]
    [InlineData("authorization", "replace")]
    [InlineData("complete-walk", "cancel")]
    [InlineData("complete-walk", "replace")]
    [InlineData("composition", "cancel")]
    [InlineData("composition", "replace")]
    public async Task CancelOrTargetReplacementDuringInitialGrantPreviewStagesCancelsAndLeavesNoStaleAttempt(
        string stage,
        string action)
    {
        var composition = new StubTenantsBffComposition(
            TenantLifecycleAuthorizationReflectionState.Authorized);
        var queryGateway = new StubTenantQueryGateway(ComponentReady("projection-v1", "admin-a"))
        {
            RepeatLastResponse = true,
        };
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-list']");
        var suspended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task entered;
        if (stage == "authorization")
        {
            composition.ResolutionGate = suspended;
            entered = composition.ResolutionEntered.Task;
        }
        else if (stage == "complete-walk")
        {
            queryGateway.QueueResponse(suspended.Task.ContinueWith(
                _ => ComponentReady("projection-v1", "admin-a"),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default));
            entered = queryGateway.QueuedResponseEntered.Task;
        }
        else
        {
            composition.GrantPreviewGate = suspended;
            entered = composition.GrantPreviewEntered.Task;
        }

        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-a");
        Task submit = cut.Find("[data-testid='tenants-global-admin-grant-form']").SubmitAsync();
        await entered.WaitAsync(TimeSpan.FromSeconds(5));

        if (action == "cancel")
        {
            await cut.Find("[data-testid='tenants-global-admin-grant-cancel']")
                .ClickAsync(new MouseEventArgs());
        }
        else
        {
            // Exercise a queued callback; the current DOM input correctly refuses edits while disabled.
            FluentTextInput targetInput = cut.FindComponents<FluentTextInput>()
                .Select(rendered => rendered.Instance)
                .Single(instance => instance.AdditionalAttributes is { } attributes
                    && attributes.TryGetValue("data-testid", out object? actual)
                    && string.Equals(actual as string, "tenants-global-admin-grant-user-id", StringComparison.Ordinal));
            await cut.InvokeAsync(() => targetInput.ValueChanged.InvokeAsync("target-b"));
        }

        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        int cancellationCount = stage switch
        {
            "authorization" => composition.ResolutionCancellationObserved,
            "complete-walk" => queryGateway.QueuedResponseCancellationObserved,
            _ => composition.GrantPreviewCancellationObserved,
        };
        cancellationCount.ShouldBe(1);
        cut.FindAll("[data-testid='tenants-global-admin-grant-preview']").ShouldBeEmpty();
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
        PrivateField<TenantAggregateCommandLease?>(cut.Instance, "_grantAdmissionLease").ShouldBeNull();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        if (action == "replace")
        {
            PrivateField<string?>(cut.Instance, "_grantUserId").ShouldBe("target-b");
        }
    }

    [Fact]
    public async Task A_sign_out_during_grant_reauthorization_fails_closed_without_dispatching_the_command()
    {
        var authentication = new MutableAuthenticationStateProvider(GlobalAdministratorPrincipal());
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"));
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-list']");

        composition.ResolutionGate = gate;
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        Task grantSubmit = cut.Find("[data-testid='tenants-global-admin-grant-form']").SubmitAsync();
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Set(NonAdministratorPrincipal());
        cut.WaitForAssertion(
            () => cut.Find("[data-testid='tenants-global-admins-restricted-header']"),
            TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => composition.AsyncResolutionCount >= 2, TimeSpan.FromSeconds(5));

        gate.SetResult();
        await grantSubmit;

        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        commandGateway.SetGlobalAdministratorCalls.ShouldBe(0);
    }

    /// <summary>
    /// The fail-closed half of Indeterminate Retry: clicking retry while authorization stays indeterminate must
    /// not query privileged data or restore the authorized surface.
    /// </summary>
    [Fact]
    public async Task Indeterminate_authorization_retry_stays_fail_closed_when_reauthorization_remains_indeterminate()
    {
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.after-retry", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"after-retry\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        gateway.GlobalAdministratorCalls.ShouldBe(0);

        await cut.Find("[data-testid='tenants-global-admins-authorization-retry']")
            .ClickAsync(new MouseEventArgs());

        await Task.Delay(250);
        gateway.GlobalAdministratorCalls.ShouldBe(0);
        cut.Find("[data-testid='tenants-global-admins-restricted-header']");
        cut.FindAll("[data-testid='tenants-global-admins-page-header']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("admin.after-retry");
    }

    /// <summary>
    /// Hoisting <c>ReauthorizeAsync</c> consumed the automatic render at submit suspension; without the
    /// marshalled follow-up render the RequestSent lifecycle string never reached the DOM while the command was
    /// still in flight.
    /// </summary>
    [Fact]
    public async Task Grant_submission_announces_request_sent_while_the_command_is_in_flight()
    {
        var submissionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1))
        {
            SubmissionGate = submissionGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("target-user");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        Task grantSubmit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        await commandGateway.SubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent
            .ShouldContain("Grant command request was sent.");
        submissionGate.SetResult();
        await grantSubmit;
    }

    /// <summary>
    /// Remove submit uses the same marshalled RequestSent render as grant.
    /// </summary>
    [Fact]
    public async Task Remove_submission_announces_request_sent_while_the_command_is_in_flight()
    {
        var submissionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(statuses: [new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1)])
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("message-remove", "correlation-remove"),
            RemoveSubmissionGate = submissionGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        }));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        Task removeSubmit = cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-global-admin-remove-state']").TextContent
            .ShouldContain("Remove command request was sent.");
        submissionGate.SetResult();
        await removeSubmit;
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("remove")]
    public async Task Superseded_accepted_completion_is_immediately_adopted_and_resumed(string mutation)
    {
        var completionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"))
        {
            SubmissionGate = mutation == "grant" ? completionGate : null,
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("message-remove", "correlation-remove"),
            RemoveSubmissionGate = mutation == "remove" ? completionGate : null,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        Task submit;
        Task entered;
        string invalidator;
        if (mutation == "grant")
        {
            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("new-admin");
            OpenGrantPreview(cut);
            AcknowledgeGrantPreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
                .ClickAsync(new MouseEventArgs());
            entered = commandGateway.SubmissionEntered.Task;
            invalidator = "InvalidateGrantMutation";
        }
        else
        {
            OpenRemovePreview(cut);
            AcknowledgeRemovePreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
            entered = commandGateway.RemoveSubmissionEntered.Task;
            invalidator = "InvalidateRemoveMutation";
        }

        await entered.WaitAsync(TimeSpan.FromSeconds(5));
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        completionGate.SetResult();
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        typeof(GlobalAdministratorsPage).GetMethod(invalidator, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null);
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        await WaitUntilAsync(
            () => mutation == "grant"
                ? PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot")
                    .State is TenantCommandLifecycleState.UnableToVerify
                : PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot")
                    .State is TenantCommandLifecycleState.UnableToVerify,
            TimeSpan.FromSeconds(5));

        if (mutation == "grant")
        {
            GlobalAdministratorGrantCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
            snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            snapshot.MessageId.ShouldBe(commandGateway.GrantMessageIds.ShouldHaveSingleItem());
        }
        else
        {
            GlobalAdministratorRemoveCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
            snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            snapshot.MessageId.ShouldBe(commandGateway.RemoveMessageIds.ShouldHaveSingleItem());
        }

        var replacementOwner = new object();
        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        admissionGate.TryAdoptRetainedLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? retainedLease,
            out GlobalAdministratorReconciliationState? reconciliation).ShouldBeFalse();
        retainedLease.ShouldBeNull();
        reconciliation.ShouldBeNull();
        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("grant", "rejected")]
    [InlineData("grant", "failed")]
    [InlineData("remove", "rejected")]
    [InlineData("remove", "failed")]
    public async Task Superseded_terminal_submission_completion_releases_aggregate_lease(
        string mutation,
        string outcome)
    {
        var completionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantCommandSubmissionResult terminalResult = outcome == "rejected"
            ? TenantCommandSubmissionResult.Rejected("The command was rejected.", "RejectedForTest")
            : TenantCommandSubmissionResult.Failed("The command failed.");
        var commandGateway = new StubTenantCommandGateway(terminalResult)
        {
            SubmissionGate = mutation == "grant" ? completionGate : null,
            RemoveSubmission = terminalResult,
            RemoveSubmissionGate = mutation == "remove" ? completionGate : null,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        Task submit;
        Task entered;
        string invalidator;
        if (mutation == "grant")
        {
            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("new-admin");
            OpenGrantPreview(cut);
            AcknowledgeGrantPreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
                .ClickAsync(new MouseEventArgs());
            entered = commandGateway.SubmissionEntered.Task;
            invalidator = "InvalidateGrantMutation";
        }
        else
        {
            OpenRemovePreview(cut);
            AcknowledgeRemovePreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
            entered = commandGateway.RemoveSubmissionEntered.Task;
            invalidator = "InvalidateRemoveMutation";
        }

        await entered.WaitAsync(TimeSpan.FromSeconds(5));
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        completionGate.SetResult();
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        typeof(GlobalAdministratorsPage).GetMethod(invalidator, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null);
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("remove")]
    public async Task OrdinarySubmissionGatewayExceptionPreservesAmbiguousAttemptIdentity(string mutation)
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SubmissionException = mutation == "grant" ? new HttpRequestException("transport detail") : null,
            RemoveSubmissionException = mutation == "remove" ? new HttpRequestException("transport detail") : null,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        if (mutation == "grant")
        {
            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("new-admin");
            OpenGrantPreview(cut);
            AcknowledgeGrantPreview(cut);
            await cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
                .ClickAsync(new MouseEventArgs());
            GlobalAdministratorGrantCommandSnapshot grant =
                PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
            grant.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
            grant.IsSubmissionAmbiguous.ShouldBeTrue();
        }
        else
        {
            OpenRemovePreview(cut);
            AcknowledgeRemovePreview(cut);
            await cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
            GlobalAdministratorRemoveCommandSnapshot remove =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
            remove.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
            remove.IsSubmissionAmbiguous.ShouldBeTrue();
            remove.MessageId.ShouldBe(commandGateway.RemoveMessageIds.ShouldHaveSingleItem());
        }

        cut.Markup.ShouldNotContain("transport detail", Case.Insensitive);
        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        bool acquired = admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out _);
        acquired.ShouldBeFalse();
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("remove")]
    public async Task Superseded_status_completion_is_immediately_adopted_and_resumed(string mutation)
    {
        var statusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway(
            TenantCommandSubmissionResult.Accepted("message-grant", "correlation-grant"),
            new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1,
                HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 })
        {
            RemoveSubmission = TenantCommandSubmissionResult.Accepted("message-remove", "correlation-remove"),
            StatusGate = statusGate,
        };
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [
                new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
            ],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        Task submit;
        string invalidator;
        if (mutation == "grant")
        {
            cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("new-admin");
            OpenGrantPreview(cut);
            AcknowledgeGrantPreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
                .ClickAsync(new MouseEventArgs());
            invalidator = "InvalidateGrantMutation";
        }
        else
        {
            OpenRemovePreview(cut);
            AcknowledgeRemovePreview(cut);
            submit = cut.Find("[data-testid='tenants-global-admin-remove-submit']").ClickAsync(new MouseEventArgs());
            invalidator = "InvalidateRemoveMutation";
        }

        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantAggregateCommandLease trackedLease = mutation == "grant"
            ? PrivateField<TenantAggregateCommandLease>(cut.Instance, "_grantAdmissionLease")
            : PrivateField<TenantAggregateCommandLease>(cut.Instance, "_removeAdmissionLease");
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        statusGate.SetResult();
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        typeof(GlobalAdministratorsPage).GetMethod(invalidator, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null);
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        if (mutation == "grant")
        {
            GlobalAdministratorGrantCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorGrantCommandSnapshot>(cut.Instance, "_grantSnapshot");
            snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            snapshot.MessageId.ShouldBe(commandGateway.GrantMessageIds.ShouldHaveSingleItem());
        }
        else
        {
            GlobalAdministratorRemoveCommandSnapshot snapshot =
                PrivateField<GlobalAdministratorRemoveCommandSnapshot>(cut.Instance, "_removeSnapshot");
            snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            snapshot.MessageId.ShouldBe(commandGateway.RemoveMessageIds.ShouldHaveSingleItem());
        }

        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        trackedLease.Reconciliation.ShouldNotBeNull().CommittedEventSequence.ShouldBe(2);
    }

    /// <summary>
    /// Reset paging must reopen the bounded notification-subscription budget after it was exhausted on the
    /// current circuit, not only Retry and Refresh.
    /// </summary>
    [Fact]
    public async Task Reset_paging_reopens_the_notification_setup_budget()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        int setupAttempts = 0;
        subscription
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref setupAttempts) <= 3
                ? Task.FromException(new HttpRequestException("transient setup failure"))
                : Task.CompletedTask);
        GlobalAdministratorsSnapshot recoverable = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.alpha", ReadModelFreshnessState.Stale)],
            nextCursor: "page-2",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Stale) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        var gateway = new StubTenantQueryGateway(recoverable) { RepeatLastResponse = true };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        for (int render = 0; render < 10; render++)
        {
            cut.Render();
        }

        Volatile.Read(ref setupAttempts).ShouldBe(3);

        await cut.Find("[data-testid='tenants-global-admins-reset']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Volatile.Read(ref setupAttempts).ShouldBe(4));
        await subscription.Received(4).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Authorization retry must reopen the bounded notification-subscription budget before a recovered authorized
    /// boundary can subscribe again.
    /// </summary>
    [Fact]
    public async Task Authorization_retry_reopens_the_notification_setup_budget()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        int setupAttempts = 0;
        subscription
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref setupAttempts) <= 3
                ? Task.FromException(new HttpRequestException("transient setup failure"))
                : Task.CompletedTask);
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        GlobalAdministratorsSnapshot ready = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.current", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"current\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        var gateway = new StubTenantQueryGateway(ready) { RepeatLastResponse = true };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        for (int render = 0; render < 10; render++)
        {
            cut.Render();
        }

        Volatile.Read(ref setupAttempts).ShouldBe(3);

        FieldInfo authorizationReflectionField = typeof(GlobalAdministratorsPage)
            .GetField("_authorizationReflection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo readRefreshAttemptsField = typeof(GlobalAdministratorsPage)
            .GetField("_readRefreshAttempts", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await cut.InvokeAsync(() =>
        {
            authorizationReflectionField.SetValue(
                cut.Instance,
                TenantLifecycleAuthorizationReflectionState.Indeterminate);
            readRefreshAttemptsField.SetValue(cut.Instance, 3);
        });

        await cut.InvokeAsync(() => cut.Instance.RetryAuthorizationAsync());

        cut.WaitForAssertion(() => Volatile.Read(ref setupAttempts).ShouldBe(4));
        await subscription.Received(4).SubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Refresh must take the same atomic page-load gate as Next. Without it, Refresh during an in-flight Next
    /// cancels the pager read and produces no navigation or feedback.
    /// </summary>
    [Fact]
    public async Task Refresh_does_not_start_a_read_while_next_page_load_is_in_flight()
    {
        GlobalAdministratorsSnapshot recoverable = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin.alpha", ReadModelFreshnessState.Current)],
            nextCursor: "page-2",
            hasMore: true,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current };
        var gateway = new StubTenantQueryGateway(recoverable) { RepeatLastResponse = true };
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(1));

        var pendingPage = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.QueueResponse(pendingPage.Task);
        Task nextClick = cut.Find("[data-testid='tenants-global-admins-next']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(2));

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        gateway.GlobalAdministratorCalls.ShouldBe(
            2,
            "Refresh must not start another read while Next is in flight.");

        pendingPage.SetResult(recoverable);
        await nextClick;
    }

    /// <summary>
    /// A dispose racing a suspended subscribe continuation must not leave the lease attached after assignment.
    /// </summary>
    [Fact]
    public async Task A_dispose_racing_subscribe_assignment_does_not_retain_the_refresh_lease()
    {
        IProjectionSubscription subscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var subscribeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        subscription
            .SubscribeAsync(
                GetGlobalAdministratorsQuery.ProjectionType,
                "system",
                Arg.Any<CancellationToken>())
            .Returns(_ => subscribeGate.Task);
        var gateway = new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            null,
            false,
            "\"etag\"",
            ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(subscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForAssertion(() => subscription.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync))
            .ShouldBeGreaterThanOrEqualTo(1));

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        subscribeGate.SetResult();
        await Task.Delay(250);

        await subscription.Received(1).UnsubscribeAsync(
            GetGlobalAdministratorsQuery.ProjectionType,
            "system",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Polls a non-render-driven condition. <c>WaitForAssertion</c> re-evaluates only when the component
    /// renders, so it cannot observe counters on a test double.
    /// </summary>
    private static void OpenGrantPreview(IRenderedComponent<GlobalAdministratorsPage> cut)
    {
        cut.Find("[data-testid='tenants-global-admin-grant-submit']").Click();
        cut.WaitForElement("[data-testid='tenants-global-admin-grant-preview']");
    }

    private static void OpenRemovePreview(
        IRenderedComponent<GlobalAdministratorsPage> cut,
        string targetUserId = "target-admin")
    {
        IElement launcher = cut.Find(
            $"[data-testid='tenants-global-admin-remove'][data-user-id='{targetUserId}']");
        launcher.Click();
        cut.WaitForElement("[data-testid='tenants-global-admin-remove-preview']");
    }

    private static void AcknowledgeRemovePreview(
        IRenderedComponent<GlobalAdministratorsPage> cut,
        string targetUserId = "target-admin")
    {
        cut.Find("[data-testid='tenants-global-admin-remove-acknowledge']").Change(targetUserId);
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admin-remove-submit']")
                .HasAttribute("disabled").ShouldBeFalse());
    }

    private static async Task ClickRemoveSubmitEvenIfDisabledAsync(
        IRenderedComponent<GlobalAdministratorsPage> cut)
    {
        IElement submit = cut.Find("[data-testid='tenants-global-admin-remove-submit']");
        await submit.ClickAsync(new MouseEventArgs());
        if (cut.FindAll("[data-testid='tenants-global-admin-remove-submit']").Count == 0)
        {
            return;
        }

        EventCallback<MouseEventArgs> onClick = cut.FindComponents<FluentButton>()
            .Select(rendered => rendered.Instance)
            .Single(instance => instance.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? actual)
                && string.Equals(actual as string, "tenants-global-admin-remove-submit", StringComparison.Ordinal))
            .OnClick;
        await cut.InvokeAsync(() => onClick.InvokeAsync(new MouseEventArgs()));
    }

    private static void AcknowledgeGrantPreview(IRenderedComponent<GlobalAdministratorsPage> cut)
    {
        cut.Find("[data-testid='tenants-global-admin-grant-acknowledge']").Change(true);
        cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .HasAttribute("disabled").ShouldBeFalse();
    }

    private static void PreviewAcknowledgeAndConfirmGrant(
        IRenderedComponent<GlobalAdministratorsPage> cut)
    {
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        cut.Find("[data-testid='tenants-global-admin-grant-confirm']").Click();
    }

    private static GlobalAdministratorsSnapshot ComponentReady(
        string projectionVersion,
        params string[] userIds)
        => GlobalAdministratorsSnapshot.Ready(
            userIds.Select(static userId => new GlobalAdministratorRow(
                userId,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)).ToArray(),
            nextCursor: null,
            hasMore: false,
            eTag: $"\"{projectionVersion}\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = projectionVersion,
            IsCompleteEvidence = true,
        };

    private static GlobalAdministratorsSnapshot PagedSnapshot(
        string projectionVersion,
        string? requestCursor,
        string? nextCursor,
        bool hasMore,
        params string[] userIds)
        => GlobalAdministratorsSnapshot.Ready(
            userIds.Select(static userId => new GlobalAdministratorRow(
                userId,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)).ToArray(),
            nextCursor,
            hasMore,
            eTag: $"\"{projectionVersion}\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = projectionVersion,
            RequestCursor = requestCursor,
            RequestPageSize = 50,
            IsCompleteEvidence = false,
        };

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The awaited condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class StubTenantsBffComposition(
        TenantLifecycleAuthorizationReflectionState reflection,
        bool isReadSurfaceConnected = true,
        bool isCommandSurfaceConnected = true,
        AuthenticationStateProvider? principalSource = null,
        bool isGrantPreviewReady = true,
        bool isRemovePreviewReady = true) : ITenantsBffComposition
    {
        public bool IsReadSurfaceConnected => isReadSurfaceConnected;

        public bool IsCommandSurfaceConnected => isCommandSurfaceConnected;

        public bool IsGlobalAdministratorDispatchConnected => isCommandSurfaceConnected;

        public bool IsGlobalAdministratorStatusConnected => isCommandSurfaceConnected;

        public bool IsGlobalAdministratorRequeryConnected => isReadSurfaceConnected;

        public bool IsGlobalAdministratorGrantPreviewReady { get; set; } = isGrantPreviewReady;

        public string? GlobalAdministratorGrantPreviewUnavailableReasonKey { get; set; }

        public string? GlobalAdministratorGrantPreviewRecoveryKey { get; set; }

        public bool IsGlobalAdministratorRemovePreviewReady => isRemovePreviewReady;

        public TenantLifecycleAuthorizationReflectionState Reflection { get; set; } = reflection;

        public string CallerSubject { get; set; } = "operator";

        /// <summary>
        /// When set, the stub resolves from the live principal instead of a fixed value, modelling the real
        /// BFF seam. The page no longer evaluates the authentication event's principal itself -- it delegates
        /// every path, transitions included -- so a transition test must drive authorization through this seam.
        /// A fixed-value stub cannot express "the principal changed" and would pass whatever the page did.
        /// </summary>
        private readonly AuthenticationStateProvider? _principalSource = principalSource;

        /// <summary>Set to make the async resolver throw, exercising the page's fail-closed catch.</summary>
        public bool ThrowFromAsyncResolution { get; set; }

        /// <summary>Set to make grant-preview composition fail before any lease is acquired.</summary>
        public Exception? GrantPreviewException { get; set; }

        public int GrantPreviewCompositionCount { get; private set; }

        /// <summary>
        /// Arms a one-shot suspension of the next fixed-value resolution, so a test can land an authentication
        /// transition while an earlier resolve is still in flight. Without this every stub resolution completes
        /// synchronously, the in-flight window is zero-width, and the page's transition-version guards are
        /// unobservable -- which is why reverting them kept the whole suite green.
        /// </summary>
        public TaskCompletionSource? ResolutionGate { get; set; }

        /// <summary>Completes when a gated resolution has entered and suspended.</summary>
        public TaskCompletionSource ResolutionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ResolutionCancellationObserved { get; private set; }

        public bool IgnoreResolutionCancellation { get; set; }

        public CancellationToken LastResolutionCancellation { get; private set; }

        public TaskCompletionSource ResolutionCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource? GrantPreviewGate { get; set; }

        public TaskCompletionSource GrantPreviewEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int GrantPreviewCancellationObserved { get; private set; }

        public TaskCompletionSource? GrantPreviewCompletedSignal { get; set; }

        /// <summary>Counts async resolutions, proving the page does not fall back to the sync property.</summary>
        public int AsyncResolutionCount { get; private set; }

        /// <summary>
        /// Deliberately NOT the value the page should read. Overriding the async resolver below was the
        /// missing piece: without it every test here hit the default interface implementation, which
        /// returns this property, so swapping the page's call back to claims-only authorization kept the
        /// whole suite green and the fail-closed catch was unreachable because the default cannot throw.
        /// </summary>
        public TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
            => TenantLifecycleAuthorizationReflectionState.Indeterminate;

        public async ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
            CancellationToken cancellationToken = default)
        {
            AsyncResolutionCount++;
            if (ThrowFromAsyncResolution)
            {
                throw new InvalidOperationException("principal resolution failed");
            }

            if (_principalSource is null)
            {
                // Captured at entry, before any suspension: this models a resolve whose answer was computed
                // from the evidence that was current when it started. A gated resolve therefore completes with
                // the PRE-transition answer, which is exactly the race the page's version guard must reject.
                TenantLifecycleAuthorizationReflectionState answer = Reflection;
                TaskCompletionSource? gate = ResolutionGate;
                if (gate is not null)
                {
                    // One-shot: only the resolve that is armed suspends, so a later transition can overtake it.
                    ResolutionGate = null;
                    _ = ResolutionEntered.TrySetResult();
                    LastResolutionCancellation = cancellationToken;
                    try
                    {
                        if (IgnoreResolutionCancellation)
                        {
                            await gate.Task.ConfigureAwait(false);
                        }
                        else
                        {
                            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        ResolutionCancellationObserved++;
                        throw;
                    }
                    finally
                    {
                        _ = ResolutionCompleted.TrySetResult();
                    }
                }

                return answer;
            }

            AuthenticationState state = await _principalSource
                .GetAuthenticationStateAsync()
                .ConfigureAwait(false);
            return TenantsGlobalAdministratorClaims.Evaluate(state.User);
        }

        public async ValueTask<GlobalAdministratorGrantPreview> ComposeGlobalAdministratorGrantPreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
        {
            GrantPreviewCompositionCount++;
            if (GrantPreviewException is not null)
            {
                throw GrantPreviewException;
            }

            TaskCompletionSource? previewGate = GrantPreviewGate;
            if (previewGate is not null)
            {
                GrantPreviewGate = null;
                _ = GrantPreviewEntered.TrySetResult();
                try
                {
                    await previewGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    GrantPreviewCancellationObserved++;
                    throw;
                }
            }

            TenantLifecycleAuthorizationReflectionState current = await ResolveGlobalAdministratorsAuthorizationAsync(
                cancellationToken);
            GlobalAdministratorGrantPreview result = GlobalAdministratorGrantPreview.Create(
                targetUserId,
                completeSnapshot,
                current is TenantLifecycleAuthorizationReflectionState.Authorized);
            _ = GrantPreviewCompletedSignal?.TrySetResult();
            return result;
        }

        public async ValueTask<GlobalAdministratorRemovePreview> ComposeGlobalAdministratorRemovePreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
        {
            TenantLifecycleAuthorizationReflectionState current = await ResolveGlobalAdministratorsAuthorizationAsync(
                cancellationToken);
            return GlobalAdministratorRemovePreview.Create(
                targetUserId,
                CallerSubject,
                completeSnapshot,
                current is TenantLifecycleAuthorizationReflectionState.Authorized);
        }
    }

    private sealed class StubTenantQueryGateway(params GlobalAdministratorsSnapshot[] snapshots) : ITenantQueryGateway
    {
        /// <summary>
        /// Explicit because <c>ITenantQueryGateway.GetTenantUsersAsync</c> is no longer a default interface
        /// method. This stub returns <c>Unavailable</c> deliberately: these tests do not exercise the member
        /// read, and an unavailable member surface is the correct fail-closed shape for them. Note this is
        /// the same value the removed default interface method returned, so a member-read regression is NOT
        /// caught here -- it is caught by the member-specific suites in
        /// <c>TenantDetailSurfaceTests</c>. (An earlier version of this remark claimed the opposite.)
        /// </summary>
        public Task<TenantUsersSnapshot> GetTenantUsersAsync(
            TenantUsersRequest request,
            TenantUsersSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(TenantUsersSnapshot.Unavailable(request.TenantId));
        }

        private readonly Queue<GlobalAdministratorsSnapshot> _snapshots = new(snapshots);
        private readonly Queue<Task<GlobalAdministratorsSnapshot>> _queuedResponses = [];
        private GlobalAdministratorsSnapshot? _lastSnapshot;

        /// <summary>
        /// When set, the final queued snapshot is returned again for any further read instead of the queue
        /// underflowing. A fixed-length queue sized to the exact number of expected reads turns one extra
        /// read into an <see cref="InvalidOperationException"/> thrown from inside the gateway call, which
        /// the page does not catch -- so the test fails with a queue error rather than naming the behaviour.
        /// </summary>
        public bool RepeatLastResponse { get; init; } = true;

        public int GlobalAdministratorCalls { get; private set; }

        public List<GlobalAdministratorsRequest> Requests { get; } = [];

        public List<GlobalAdministratorsSnapshot?> PreviousSnapshots { get; } = [];

        public int QueuedResponseCancellationObserved { get; private set; }

        public TaskCompletionSource QueuedResponseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void QueueResponse(Task<GlobalAdministratorsSnapshot> response)
            => _queuedResponses.Enqueue(response);

        public Task<TenantDetailSnapshot> GetTenantAsync(
            TenantDetailRequest request,
            TenantDetailSnapshot? previous,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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

        public async Task<GlobalAdministratorsSnapshot> GetGlobalAdministratorsAsync(
            GlobalAdministratorsRequest request,
            GlobalAdministratorsSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            GlobalAdministratorCalls++;
            Requests.Add(request);
            PreviousSnapshots.Add(previous);
            if (_queuedResponses.Count > 0)
            {
                _ = QueuedResponseEntered.TrySetResult();
                try
                {
                    return QualifyLegacyFixture(await _queuedResponses.Dequeue().WaitAsync(cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    QueuedResponseCancellationObserved++;
                    throw;
                }
            }

            if (_snapshots.Count > 0)
            {
                _lastSnapshot = _snapshots.Dequeue();
            }
            else if (!RepeatLastResponse)
            {
                _ = _snapshots.Dequeue();
            }

            return QualifyLegacyFixture(_lastSnapshot!);

            GlobalAdministratorsSnapshot QualifyLegacyFixture(GlobalAdministratorsSnapshot snapshot)
            {
                bool isQualifiedShape = snapshot.Kind is GlobalAdministratorsSurfaceKind.Ready
                    or GlobalAdministratorsSurfaceKind.Empty
                    && snapshot.Freshness is ReadModelFreshnessState.Current
                    && snapshot.Lifecycle is ProjectionLifecycleState.Current;
                IReadOnlyList<GlobalAdministratorRow> rows = isQualifiedShape
                    ? snapshot.Rows.Select(static row => row with
                    {
                        Freshness = ReadModelFreshnessState.Current,
                        Lifecycle = ProjectionLifecycleState.Current,
                    }).ToArray()
                    : snapshot.Rows;

                // Historical component fixtures pre-date explicit projection provenance. The gateway double
                // models the qualified BFF response those fixtures intended; tests that exercise malformed or
                // incomplete evidence use the loader/evaluator suites directly or a paged shape here.
                return snapshot with
                {
                    Rows = rows,
                    ProjectionVersion = isQualifiedShape && !snapshot.HasMore
                        ? snapshot.ProjectionVersion ?? "component-test-v1"
                        : snapshot.ProjectionVersion,
                    IsCompleteEvidence = snapshot.IsCompleteEvidence
                        || (isQualifiedShape && !snapshot.HasMore && snapshot.RequestCursor is null),
                    RequestCursor = snapshot.RequestCursor ?? request.Cursor,
                    RequestPageSize = request.PageSize,
                };
            }
        }

        public Task<TenantAuditSnapshot> GetTenantAuditAsync(
            TenantAuditRequest request,
            TenantAuditSnapshot? previous,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantAuditSnapshot.Unavailable(request));
    }

    private static T PrivateField<T>(GlobalAdministratorsPage instance, string name)
    {
        FieldInfo field = typeof(GlobalAdministratorsPage)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field {name} was not found.");
        return (T)field.GetValue(instance)!;
    }

    private IReadOnlyList<string> FocusedElementIds()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
                && invocation.Arguments.Count > 0
                && invocation.Arguments[0] is ElementReference)
            .Select(invocation => ((ElementReference)invocation.Arguments[0]!).Id)];

    private static string CapturedElementReferenceId(object component, string fieldName)
    {
        object value = component.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(component)
            ?? throw new InvalidOperationException($"'{fieldName}' was not captured by the component.");
        return ((ElementReference)value).Id;
    }

    private static string CapturedChildElementReferenceId(object component, string fieldName)
    {
        object child = component.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(component)
            ?? throw new InvalidOperationException($"'{fieldName}' was not captured by the component.");
        object element = child.GetType().GetProperty("Element")?.GetValue(child)
            ?? throw new InvalidOperationException($"'{fieldName}' does not expose a captured Element reference.");
        return ((ElementReference)element).Id;
    }

    private static void AssertGuardsInsideRendererCallback(
        string methodBody,
        string mutation,
        params string[] guards)
    {
        int mutationIndex = methodBody.IndexOf(mutation, StringComparison.Ordinal);
        mutationIndex.ShouldBeGreaterThan(-1, $"Mutation '{mutation}' was not found.");
        int callbackIndex = Math.Max(
            methodBody.LastIndexOf("await InvokeAsync(() =>", mutationIndex, StringComparison.Ordinal),
            methodBody.LastIndexOf("await InvokeRendererSafelyAsync(() =>", mutationIndex, StringComparison.Ordinal));
        callbackIndex.ShouldBeGreaterThan(-1, $"Mutation '{mutation}' is not inside an InvokeAsync callback.");
        int priorCallbackEnd = methodBody.LastIndexOf("}).ConfigureAwait(false);", mutationIndex, StringComparison.Ordinal);
        priorCallbackEnd.ShouldBeLessThan(callbackIndex, $"Mutation '{mutation}' follows a closed renderer callback.");
        int callbackEnd = methodBody.IndexOf("}).ConfigureAwait(false);", mutationIndex, StringComparison.Ordinal);
        if (callbackEnd < 0)
        {
            callbackEnd = methodBody.IndexOf("}).ConfigureAwait(false))", mutationIndex, StringComparison.Ordinal);
        }

        callbackEnd.ShouldBeGreaterThan(mutationIndex, $"Mutation '{mutation}' callback end was not found.");
        foreach (string guard in guards)
        {
            int guardIndex = methodBody.IndexOf(guard, callbackIndex, StringComparison.Ordinal);
            guardIndex.ShouldBeInRange(
                callbackIndex,
                mutationIndex,
                $"Guard '{guard}' must execute inside the renderer callback before '{mutation}'.");
        }
    }

    private static async Task<(Task RendererBlock, TaskCompletionSource ReleaseRenderer)> BlockRendererAsync(
        IRenderedComponent<GlobalAdministratorsPage> cut)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task block = Task.Run(() => cut.InvokeAsync(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return (block, release);
    }

    private sealed class StubTenantCommandGateway(
        TenantCommandSubmissionResult? submission = null,
        params TenantCommandStatusResult[] statuses) : ITenantCommandGateway
    {
        public bool SupportsGlobalAdministratorDispatch { get; init; } = true;

        private bool _supportsTrackedGlobalAdministratorDispatch = true;

        public Func<bool>? TrackedDispatchSupportProvider { get; set; }

        public bool SupportsTrackedGlobalAdministratorDispatch
        {
            get => TrackedDispatchSupportProvider?.Invoke() ?? _supportsTrackedGlobalAdministratorDispatch;
            set => _supportsTrackedGlobalAdministratorDispatch = value;
        }

        public bool SupportsTrackedGlobalAdministratorRemoveDispatch { get; set; } = true;

        public bool SupportsCommandStatusLookup { get; init; } = true;

        private readonly Queue<TenantCommandStatusResult> _statuses = new(statuses);

        public TenantCommandSubmissionResult? RemoveSubmission { get; set; }

        public Exception? SubmissionException { get; init; }

        public Exception? RemoveSubmissionException { get; init; }

        /// <summary>Arms a one-shot suspension of the next grant submission so RequestSent can be observed.</summary>
        public TaskCompletionSource? SubmissionGate { get; set; }

        /// <summary>Arms a one-shot suspension of the next remove submission so RequestSent can be observed.</summary>
        public TaskCompletionSource? RemoveSubmissionGate { get; set; }

        /// <summary>Completes when a gated grant submission has entered and suspended.</summary>
        public TaskCompletionSource SubmissionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when a gated remove submission has entered and suspended.</summary>
        public TaskCompletionSource RemoveSubmissionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Arms a one-shot suspension of the next status lookup.</summary>
        public TaskCompletionSource? StatusGate { get; set; }

        /// <summary>Completes when a gated status lookup has entered and suspended.</summary>
        public TaskCompletionSource StatusEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SetGlobalAdministratorCalls { get; private set; }

        public int RemoveGlobalAdministratorCalls { get; private set; }

        public List<SetGlobalAdministrator> Requests { get; } = [];

        public List<string> GrantMessageIds { get; } = [];

        public List<TenantCommandTrackingHandle> StatusHandles { get; } = [];

        public List<RemoveGlobalAdministrator> RemoveRequests { get; } = [];

        public List<string> RemoveMessageIds { get; } = [];

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(
            CreateTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(
            AddUserToTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(
            ChangeUserRole request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(
            RemoveUserFromTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(
            UpdateTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(
            SetTenantConfiguration request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(
            RemoveTenantConfiguration request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<TenantCommandSubmissionResult> SetGlobalAdministratorAsync(
            SetGlobalAdministrator request,
            CancellationToken cancellationToken = default)
        {
            SetGlobalAdministratorCalls++;
            Requests.Add(request);
            TaskCompletionSource? gate = SubmissionGate;
            if (gate is not null)
            {
                SubmissionGate = null;
                _ = SubmissionEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }

            if (SubmissionException is not null)
            {
                throw SubmissionException;
            }

            return submission ?? TenantCommandSubmissionResult.Failed("No command response configured.");
        }

        public async Task<TenantCommandSubmissionResult> SetGlobalAdministratorTrackedAsync(
            SetGlobalAdministrator request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            SetGlobalAdministratorCalls++;
            Requests.Add(request);
            GrantMessageIds.Add(messageId);
            TaskCompletionSource? gate = SubmissionGate;
            if (gate is not null)
            {
                SubmissionGate = null;
                _ = SubmissionEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }

            if (SubmissionException is not null)
            {
                throw SubmissionException;
            }

            TenantCommandSubmissionResult result = submission
                ?? TenantCommandSubmissionResult.Failed("No command response configured.");
            return result with { MessageId = messageId };
        }

        public async Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorAsync(
            RemoveGlobalAdministrator request,
            CancellationToken cancellationToken = default)
        {
            RemoveGlobalAdministratorCalls++;
            RemoveRequests.Add(request);
            TaskCompletionSource? gate = RemoveSubmissionGate;
            if (gate is not null)
            {
                RemoveSubmissionGate = null;
                _ = RemoveSubmissionEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }

            if (RemoveSubmissionException is not null)
            {
                throw RemoveSubmissionException;
            }

            return RemoveSubmission ?? TenantCommandSubmissionResult.Failed("No remove command response configured.");
        }

        public async Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorTrackedAsync(
            RemoveGlobalAdministrator request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            RemoveMessageIds.Add(messageId);
            TenantCommandSubmissionResult result = await RemoveGlobalAdministratorAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            return result with { MessageId = messageId };
        }

        public Task<TenantCommandSubmissionResult> EnableTenantTrackedAsync(
            TenantLifecycleCommandRequest request,
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> DisableTenantTrackedAsync(
            TenantLifecycleCommandRequest request,
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<TenantCommandStatusResult> GetStatusAsync(
            TenantCommandTrackingHandle handle,
            CancellationToken cancellationToken = default)
        {
            StatusHandles.Add(handle);
            TaskCompletionSource? gate = StatusGate;
            if (gate is not null)
            {
                StatusGate = null;
                _ = StatusEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }

            return _statuses.Count == 0
                ? TenantCommandStatusResult.Unknown("No command status configured.")
                : _statuses.Dequeue();
        }
    }

    private sealed class CapturingUntypedLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public List<LogLevel> Levels { get; } = [];

        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    private sealed class ThrowingProjectionNotifier : IProjectionChangeNotifierWithTenant
    {
        public event Action<string>? ProjectionChanged
        {
            add { }
            remove { }
        }

        public event Action<string, string>? ProjectionChangedForTenant
        {
            add => throw new InvalidOperationException("unsafe setup detail");
            remove { }
        }

        public void NotifyChanged(string projectionType) { }

        public void NotifyChanged(string projectionType, string tenantId) { }
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

    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            // Without these, ProjectionLifecycleBadge rendered the literal resource key back through this
            // echoing stub, so the badge's label was never really asserted and only its CSS class was --
            // which project rules forbid relying on alone.
            ["Tenants.ProjectionLifecycle.Label"] = "Projection lifecycle",
            ["Tenants.ProjectionLifecycle.Current"] = "Current",
            ["Tenants.ProjectionLifecycle.Stale"] = "Stale",
            ["Tenants.ProjectionLifecycle.Unknown"] = "Unknown",
            ["Tenants.ProjectionLifecycle.Rebuilding"] = "Rebuilding",
            ["Tenants.ProjectionLifecycle.Degraded"] = "Degraded",
            ["Tenants.ProjectionLifecycle.Unavailable"] = "Unavailable",
            ["Tenants.ProjectionLifecycle.LocalOnly"] = "Local only",
            ["Tenants.Copy.Action"] = "Copy",
            ["Tenants.Copy.Feedback.Copied"] = "Copied.",
            ["Tenants.GlobalAdministrators.Aggregate.Domain.Label"] = "Domain",
            ["Tenants.GlobalAdministrators.Aggregate.Domain.Value"] = "global-administrators",
            ["Tenants.GlobalAdministrators.Aggregate.Id.Label"] = "Aggregate id",
            ["Tenants.GlobalAdministrators.Aggregate.Id.Value"] = "global-administrators",
            ["Tenants.GlobalAdministrators.Aggregate.Tenant.Label"] = "Tenant scope",
            ["Tenants.GlobalAdministrators.Aggregate.Tenant.Value"] = "system",
            ["Tenants.GlobalAdministrators.Column.Actions"] = "Grant/remove availability",
            ["Tenants.GlobalAdministrators.Column.Freshness"] = "Freshness",
            ["Tenants.GlobalAdministrators.Column.Identity"] = "Administrator identity",
            ["Tenants.GlobalAdministrators.Column.Scope"] = "Authority scope",
            ["Tenants.GlobalAdministrators.Copy.UserId"] = "Copy global administrator identifier {0}",
            ["Tenants.GlobalAdministrators.Description"] = "Review platform-level administrators from the fixed global-administrators authority scope.",
            ["Tenants.GlobalAdministrators.Eyebrow"] = "Platform governance",
            ["Tenants.GlobalAdministrators.Freshness.Current"] = "Current",
            ["Tenants.GlobalAdministrators.Freshness.Stale"] = "Stale",
            ["Tenants.GlobalAdministrators.Freshness.Unknown"] = "Unknown",
            ["Tenants.GlobalAdministrators.Grant.Audit.AuditDelayed"] = "Audit evidence is delayed; refresh status or inspect audit before citing proof.",
            ["Tenants.GlobalAdministrators.Grant.Audit.AuditPending"] = "Audit evidence is pending; do not cite audit proof until it is visible.",
            ["Tenants.GlobalAdministrators.Grant.Audit.AuditUnavailable"] = "Audit evidence is unavailable from this flow.",
            ["Tenants.GlobalAdministrators.Grant.Audit.MissingSupport"] = "Audit support for this flow is not available.",
            ["Tenants.GlobalAdministrators.Grant.Audit.NotStarted"] = "No audit evidence is available before command submission.",
            ["Tenants.GlobalAdministrators.Grant.Available"] = "Grant is available from the confirmed platform authority projection.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Available"] = "Grant is available for the fixed platform scope.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Available"] = "Remove is available for this visible administrator.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingPermission"] = "Grant is unavailable because current platform authority is not proven.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingPermission"] = "Remove is unavailable because current platform authority is not proven.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.StaleData"] = "Grant is unavailable until the fixed-scope projection is current and versioned.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.StaleData"] = "Remove is unavailable until the visible fixed-scope projection is current and versioned.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingLifecycleSupport"] = "Grant is unavailable because dispatch, status, or requery support is missing.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingLifecycleSupport"] = "Remove is unavailable because dispatch, status, or requery support is missing.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.UnsafeViewport"] = "Grant is read-only until the browser measures a safe tablet or desktop viewport.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.UnsafeViewport"] = "Remove is read-only until the browser measures a safe tablet or desktop viewport.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.AggregateBusy"] = "Grant is unavailable while another global-administrator attempt is active.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.AggregateBusy"] = "Remove is unavailable while another global-administrator attempt is active.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingConsequencePreview"] = "Grant is unavailable because its safety flow is not ready.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingConsequencePreview"] = "Remove is unavailable until its complete consequence preview is ready.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.IncompletePopulation"] = "Remove is unavailable because the complete fixed-scope population is not proven.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.TargetMissing"] = "Remove is unavailable because the target is not present in qualified visible evidence.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.LastAdministrator"] = "Remove is unavailable because this is the last proven global administrator.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.None"] = "No recovery is required.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.MissingPermission"] = "Refresh authorization or ask a platform administrator to verify your authority.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.StaleData"] = "Refresh the fixed-scope projection and review its current version.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.MissingLifecycleSupport"] = "Restore dispatch, status, and requery support, then retry.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.UnsafeViewport"] = "Use a measured tablet or desktop viewport to continue.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.AggregateBusy"] = "Reconcile the active attempt to terminal evidence before starting another.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.MissingConsequencePreview"] = "Restore the dedicated removal preview before continuing.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Recovery.MissingConsequencePreview"] = "Restore the dedicated grant preview before continuing.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Recovery.MissingConsequencePreview"] = "Restore the dedicated removal preview before continuing.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.IncompletePopulation"] = "Refresh and complete the bounded fixed-scope population read.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.TargetMissing"] = "Refresh the visible rows and select a current administrator.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.LastAdministrator"] = "Grant another administrator before removing this authority.",
            ["Tenants.GlobalAdministrators.Evidence.Scope.Fixed"] = "Fixed platform global-administrator scope",
            ["Tenants.GlobalAdministrators.Evidence.Freshness.Qualified"] = "Current lifecycle and version evidence",
            ["Tenants.GlobalAdministrators.Evidence.Count.Qualified"] = "Administrator count from complete evidence: {0}",
            ["Tenants.GlobalAdministrators.Evidence.Admission.Available"] = "Available",
            ["Tenants.GlobalAdministrators.Evidence.Admission.Busy"] = "Active attempt requires reconciliation",
            ["Tenants.GlobalAdministrators.Grant.Cancel"] = "Cancel",
            ["Tenants.GlobalAdministrators.Grant.Preview.Launch"] = "Review grant consequences",
            ["Tenants.GlobalAdministrators.Grant.Preview.Title"] = "Grant consequence preview",
            ["Tenants.GlobalAdministrators.Grant.Preview.Scope"] = "Platform authority scope",
            ["Tenants.GlobalAdministrators.Grant.Preview.Scope.Value"] = "tenant system, domain global-administrators, aggregate global-administrators",
            ["Tenants.GlobalAdministrators.Grant.Preview.Target"] = "Target user id",
            ["Tenants.GlobalAdministrators.Grant.Preview.Counts"] = "Administrator count change",
            ["Tenants.GlobalAdministrators.Grant.Preview.Counts.Value"] = "Current complete count: {0}; resulting count after confirmation: {1}",
            ["Tenants.GlobalAdministrators.Grant.Preview.AuthorityChange"] = "Authority change",
            ["Tenants.GlobalAdministrators.Grant.Preview.AuthorityChange.Value"] = "Qualified confirmation records that this exact tracked grant produced an event and appeared in a newer complete fixed-scope projection; it does not prove downstream enforcement timing.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Freshness"] = "Evidence freshness",
            ["Tenants.GlobalAdministrators.Grant.Preview.Freshness.Value"] = "The preview uses a complete, current, versioned fixed-scope projection captured before dispatch.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery"] = "Recovery path",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Value"] = "Refresh authority and projection truth, then retry the same tracked attempt when delivery is ambiguous.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Audit"] = "Audit expectation",
            ["Tenants.GlobalAdministrators.Grant.Preview.Audit.Value"] = "Audit evidence is expected after qualified confirmation, but this flow does not fabricate or promise an audit receipt.",
            ["Tenants.GlobalAdministrators.Grant.Preview.CallerTargetContext"] = "Caller and target context",
            ["Tenants.GlobalAdministrators.Grant.Preview.CallerTargetContext.Value"] = "A currently authorized platform operator is granting the literal target identifier; tenant membership is not read or changed.",
            ["Tenants.GlobalAdministrators.Grant.Preview.KnownConsequences"] = "Known consequences",
            ["Tenants.GlobalAdministrators.Grant.Preview.KnownConsequences.Value"] = "The fixed-scope projection is expected to include the target after the tracked grant; session and downstream enforcement remain separately unproven.",
            ["Tenants.GlobalAdministrators.Grant.Preview.KnownUnknowns"] = "Known unknowns",
            ["Tenants.GlobalAdministrators.Grant.Preview.KnownUnknowns.Value"] = "Session refresh, token issuance, downstream enforcement timing, and audit availability are not proven by command acceptance.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Acknowledge"] = "I reviewed the fixed scope, authority change, evidence limits, and recovery path.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Confirm"] = "Confirm tracked grant",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Authorization"] = "Current platform authority is not proven, so the grant preview is unavailable.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Target"] = "The literal target identifier is unsupported.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Evidence"] = "Complete, current, versioned fixed-scope evidence is required for the grant preview.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.TargetExists"] = "The exact target is already present in the complete global-administrator projection, so no grant was dispatched.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.Localization"] = "Localized consequence facts are incomplete, so the grant preview cannot be reviewed safely.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Authorization"] = "Refresh authorization or ask a platform administrator to verify your authority.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Target"] = "Enter a supported literal user id without changing its casing or whitespace.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh"] = "Refresh the complete fixed-scope projection and rebuild the preview.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.TargetExists"] = "Keep the confirmed rows unchanged and choose a target absent from the complete projection.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Localization"] = "Restore the complete localized consequence resources, then rebuild and review the preview.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Invalidated"] = "The grant preview changed before dispatch. Refresh and review a new preview.",
            ["Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"] = "Grant delivery is ambiguous. Refresh or retry with the same retained command identity.",
            ["Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"] = "Command status did not match the exact retained grant identity.",
            ["Tenants.GlobalAdministrators.Grant.Submission.Rejected"] = "The global administrator grant was rejected. Refresh platform governance authority before trying again.",
            ["Tenants.GlobalAdministrators.Grant.Submission.Invalid"] = "The global administrator grant request was not accepted. Review the literal user id and try again.",
            ["Tenants.GlobalAdministrators.Grant.Submission.Failed"] = "The global administrator grant submission failed before dispatch could be verified. Keep the current evidence and review the request before trying again.",
            ["Tenants.GlobalAdministrators.Grant.Submission.Unavailable"] = "The global administrator command gateway is unavailable. Keep the current evidence and retry later.",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry"] = "Retry delivery with the same tracked command",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery"] = "Retry only with the retained command identity; do not create a new grant attempt.",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Label"] = "Delivery retry unavailable",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Reason"] = "Delivery retry is temporarily withdrawn because its current safety prerequisites are not satisfied.",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry.Withdrawn.Recovery"] = "Restore the live safety prerequisites, then retry this same retained command; do not create a new grant attempt.",
            ["Tenants.GlobalAdministrators.Grant.UnableToVerify.StatusTimeout"] = "Grant status timed out before the tracked result could be verified.",
            ["Tenants.GlobalAdministrators.Grant.UnableToVerify.UnsupportedSubmission"] = "The grant submission returned an unsupported lifecycle state.",
            ["Tenants.GlobalAdministrators.Grant.Status.Pending"] = "Tracked grant status is pending; keep the same command identity and refresh.",
            ["Tenants.GlobalAdministrators.Grant.Status.Unknown"] = "Tracked grant status is unavailable; keep the same command identity and retry verification.",
            ["Tenants.GlobalAdministrators.Grant.Status.Rejected"] = "The tracked grant was rejected. Refresh platform authority before trying another action.",
            ["Tenants.GlobalAdministrators.Grant.Status.PublishFailed"] = "The grant event could not be published. Keep the same tracked command and retry verification.",
            ["Tenants.GlobalAdministrators.Grant.Status.TimedOut"] = "The tracked grant timed out before its result could be verified.",
            ["Tenants.GlobalAdministrators.Grant.Description"] = "Grant platform authority in tenant system, domain global-administrators, aggregate global-administrators. Completion requires projection confirmation.",
            ["Tenants.GlobalAdministrators.Grant.Lifecycle.Title"] = "Grant lifecycle",
            ["Tenants.GlobalAdministrators.Grant.Refresh"] = "Refresh status",
            ["Tenants.GlobalAdministrators.Grant.State.Accepted"] = "Command accepted; projection confirmation is still required.",
            ["Tenants.GlobalAdministrators.Grant.State.AlreadyApplied"] = "Already-applied is not used for global administrator grants.",
            ["Tenants.GlobalAdministrators.Grant.State.Confirmed"] = "Projection confirmed the target user in the fixed global-administrators scope.",
            ["Tenants.GlobalAdministrators.Grant.State.Degraded"] = "Grant verification is degraded; publication or audit evidence could not be verified.",
            ["Tenants.GlobalAdministrators.Grant.State.DuplicatePrevented"] = "A concurrent grant command was prevented.",
            ["Tenants.GlobalAdministrators.Grant.State.Failed"] = "Grant command failed before it could be verified.",
            ["Tenants.GlobalAdministrators.Grant.State.Idle"] = "No global administrator grant command has been submitted.",
            ["Tenants.GlobalAdministrators.Grant.State.Previewed"] = "Grant intent is previewed but not submitted.",
            ["Tenants.GlobalAdministrators.Grant.State.ProjectionPending"] = "Projection pending; the target user is not confirmed as a global administrator yet.",
            ["Tenants.GlobalAdministrators.Grant.State.Rejected"] = "Grant command was rejected.",
            ["Tenants.GlobalAdministrators.Grant.State.RequestSent"] = "Grant command request was sent.",
            ["Tenants.GlobalAdministrators.Grant.State.UnableToVerify"] = "Grant command could not be verified from command status and projection evidence.",
            ["Tenants.GlobalAdministrators.Grant.Submit"] = "Grant global administrator",
            ["Tenants.GlobalAdministrators.Grant.Title"] = "Grant global administrator",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.Authorization"] = "Platform authority is not confirmed, so grant fails closed without revealing administrator data.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.CommandSurface"] = "The command surface is unavailable for platform governance changes.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.Freshness"] = "Refresh projection freshness before granting platform authority.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.ProjectionLifecycle"] = "Current projection lifecycle evidence is required before granting platform authority.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.InFlight"] = "Another platform authority command is in flight.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.ReadSurface"] = "The global administrator read projection must be available before grant can be submitted.",
            ["Tenants.GlobalAdministrators.Grant.Unavailable.RemoveDeferred"] = "Remove global administrator is handled by a separate guarded flow.",
            ["Tenants.GlobalAdministrators.Grant.UserId.Help"] = "Enter the literal caller-supplied user id (256 characters or fewer, no control characters). It is not parsed as a tenant member, GUID, or ULID.",
            ["Tenants.GlobalAdministrators.Grant.Confirm.EvidenceRequired"] = "Current projection evidence is required before confirming the global administrator grant.",
            ["Tenants.GlobalAdministrators.Grant.Confirm.DidNotConfirm"] = "Projection re-query did not confirm the target global administrator.",
            ["Tenants.GlobalAdministrators.Grant.Confirm.PageScoped"] = "The projection re-query covers only the first page of global administrators, which does not include this user, so the grant cannot be confirmed from this page. Confirm the outcome from the tenant audit trail.",
            ["Tenants.GlobalAdministrators.Grant.UserId.Label"] = "User id",
            ["Tenants.GlobalAdministrators.Grant.Validation.UserIdInvalid"] = "Enter a supported user id of 256 characters or fewer without control characters.",
            ["Tenants.GlobalAdministrators.Grant.Validation.UserIdRequired"] = "User id is required before granting global administrator authority.",
            ["Tenants.GlobalAdministrators.Remove.Audit.AuditDelayed"] = "Audit evidence is delayed; refresh status or inspect audit before citing proof.",
            ["Tenants.GlobalAdministrators.Remove.Audit.AuditPending"] = "Audit evidence is pending; do not cite audit proof until it is visible.",
            ["Tenants.GlobalAdministrators.Remove.Audit.AuditUnavailable"] = "Audit evidence is unavailable from this flow.",
            ["Tenants.GlobalAdministrators.Remove.Audit.MissingSupport"] = "Audit support for this flow is not available.",
            ["Tenants.GlobalAdministrators.Remove.Audit.NotStarted"] = "No audit evidence is available before command submission.",
            ["Tenants.GlobalAdministrators.Remove.Cancel"] = "Cancel",
            ["Tenants.GlobalAdministrators.Remove.Description"] = "Remove platform authority only when the fixed global-administrators projection proves it will not remove the last global administrator.",
            ["Tenants.GlobalAdministrators.Remove.Launch"] = "Remove global administrator",
            ["Tenants.GlobalAdministrators.Remove.Lifecycle.Title"] = "Remove lifecycle",
            ["Tenants.GlobalAdministrators.Remove.Preview.AccessRevoked"] = "Access being revoked",
            ["Tenants.GlobalAdministrators.Remove.Preview.AccessRevoked.Value"] = "Platform global-administrator authority is revoked from the target user only after projection confirmation.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Audit"] = "Audit expectation",
            ["Tenants.GlobalAdministrators.Remove.Preview.Audit.Value"] = "Audit evidence is expected after command acceptance and projection-confirmed removal; this panel does not fabricate proof.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Count"] = "Current administrator count",
            ["Tenants.GlobalAdministrators.Remove.Preview.Count.VisiblePage"] = "Administrators visible on this page",
            ["Tenants.GlobalAdministrators.Remove.Preview.Freshness"] = "Projection freshness",
            ["Tenants.GlobalAdministrators.Remove.Preview.KnownConsequences"] = "Known consequences",
            ["Tenants.GlobalAdministrators.Remove.Preview.KnownConsequences.Value"] = "The target loses platform authority in the system global-administrators scope; tenant membership is not changed.",
            ["Tenants.GlobalAdministrators.Remove.Preview.KnownUnknowns"] = "Known unknowns",
            ["Tenants.GlobalAdministrators.Remove.Preview.KnownUnknowns.Value"] = "Session revocation, token invalidation, downstream enforcement timing, and audit proof timing are not proven by command status alone.",
            ["Tenants.GlobalAdministrators.Remove.Preview.LastAdminImpact"] = "Last administrator impact",
            ["Tenants.GlobalAdministrators.Remove.Preview.LastAdminImpact.Value"] = "The target is not the last visible global administrator in the current projection.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Recovery"] = "Recovery path",
            ["Tenants.GlobalAdministrators.Remove.Preview.Recovery.Value"] = "Refresh projection truth, inspect audit evidence, or grant global administrator authority again through the fixed platform-governance flow.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Recovery.LastAdministrator"] = "Grant another global administrator before removing this authority.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Recovery.TargetMissing"] = "Refresh and select an administrator currently present.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Unavailable.LastAdministrator"] = "The last global administrator cannot be removed.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Unavailable.TargetMissing"] = "The exact target is not present in the complete projection.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Scope"] = "Platform authority scope",
            ["Tenants.GlobalAdministrators.Remove.Preview.Scope.Value"] = "tenant system, domain global-administrators, aggregate global-administrators",
            ["Tenants.GlobalAdministrators.Remove.Preview.Target"] = "Target user id",
            ["Tenants.GlobalAdministrators.Remove.Preview.Target.Value"] = "Exact target: “{0}”",
            ["Tenants.GlobalAdministrators.Remove.Preview.Counts"] = "Administrator counts",
            ["Tenants.GlobalAdministrators.Remove.Preview.Counts.Value"] = "{0} administrators now; {1} after confirmed removal.",
            ["Tenants.GlobalAdministrators.Remove.Preview.AuthorityChange"] = "Authority change",
            ["Tenants.GlobalAdministrators.Remove.Preview.AuthorityChange.Value"] = "The target loses platform-wide global-administrator authority after confirmation.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Freshness.Value"] = "Current complete fixed-scope evidence with a qualified projection version.",
            ["Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext"] = "Caller and target",
            ["Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext.Self.Value"] = "This removes your own global-administrator authority.",
            ["Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext.Other.Value"] = "This removes another administrator’s global authority.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Acknowledge"] = "Type the exact target “{0}” to acknowledge this removal.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Confirm"] = "Confirm removal",
            ["Tenants.GlobalAdministrators.Remove.Preview.Title"] = "Remove consequence preview",
            ["Tenants.GlobalAdministrators.Remove.Status.Rejected.LastAdministrator"] = "The server rejected removal of the last global administrator.",
            ["Tenants.GlobalAdministrators.Remove.Status.Rejected.NotFound"] = "The server could not find the exact administrator target.",
            ["Tenants.GlobalAdministrators.Remove.Refresh"] = "Refresh status",
            ["Tenants.GlobalAdministrators.Remove.DeliveryRetry"] = "Retry removal delivery",
            ["Tenants.GlobalAdministrators.Remove.DeliveryRetry.Recovery"] = "Refresh evidence, then retry this same tracked removal attempt.",
            ["Tenants.GlobalAdministrators.Remove.Recovery.Failed"] = "Refresh current evidence before starting a new attempt.",
            ["Tenants.GlobalAdministrators.Remove.Status.Failed"] = "The removal command failed.",
            ["Tenants.GlobalAdministrators.Remove.State.Accepted"] = "Command accepted; projection confirmation is still required.",
            ["Tenants.GlobalAdministrators.Remove.State.AlreadyApplied"] = "Already-applied is not used for global administrator removal.",
            ["Tenants.GlobalAdministrators.Remove.State.Confirmed"] = "Projection confirmed removal from the fixed global-administrators scope.",
            ["Tenants.GlobalAdministrators.Remove.State.Degraded"] = "Remove verification is degraded; publication or audit evidence could not be verified.",
            ["Tenants.GlobalAdministrators.Remove.State.DuplicatePrevented"] = "A concurrent remove command was prevented.",
            ["Tenants.GlobalAdministrators.Remove.State.Failed"] = "Remove command failed before it could be verified.",
            ["Tenants.GlobalAdministrators.Remove.State.Idle"] = "No global administrator remove command has been submitted.",
            ["Tenants.GlobalAdministrators.Remove.State.Previewed"] = "Remove preview is ready for deliberate confirmation.",
            ["Tenants.GlobalAdministrators.Remove.State.ProjectionPending"] = "Projection pending; the target user is still visible until a re-query proves absence.",
            ["Tenants.GlobalAdministrators.Remove.State.Rejected"] = "Remove command was rejected.",
            ["Tenants.GlobalAdministrators.Remove.State.RequestSent"] = "Remove command request was sent.",
            ["Tenants.GlobalAdministrators.Remove.State.UnableToVerify"] = "Remove command could not be verified from command status and projection evidence.",
            ["Tenants.GlobalAdministrators.Remove.Submit"] = "Confirm removal",
            ["Tenants.GlobalAdministrators.Remove.Title"] = "Remove global administrator",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.Authorization"] = "Platform authority is not confirmed, so remove fails closed without revealing administrator data.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.CommandSurface"] = "The command surface is unavailable for platform governance changes.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.Freshness"] = "Refresh projection freshness before removing platform authority.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.ProjectionLifecycle"] = "Current projection lifecycle evidence is required before removing platform authority.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.InFlight"] = "Another platform authority command is in flight.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.Incomplete"] = "Current complete projection evidence is required before removing platform authority.",
            ["Tenants.GlobalAdministrators.Remove.Confirm.PageScoped"] = "The projection re-query covers only the first page of global administrators, which cannot prove this administrator was removed platform-wide. Confirm the outcome from the tenant audit trail.",
            ["Tenants.GlobalAdministrators.Remove.Confirm.EvidenceRequired"] = "Current complete projection evidence is required before confirming global administrator removal.",
            ["Tenants.GlobalAdministrators.Remove.Confirm.StillPresent"] = "Projection re-query still shows the target global administrator. Do not treat removal as complete.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.LastAdmin"] = "The last global administrator cannot be removed.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.ReadSurface"] = "The global administrator read projection must be available before removal can be submitted.",
            ["Tenants.GlobalAdministrators.Remove.Unavailable.TargetMissing"] = "The target administrator is not visible in the current projection.",
            ["Tenants.GlobalAdministrators.Identity.Accessible"] = "Global administrator identifier {0}",
            ["Tenants.GlobalAdministrators.List.Title"] = "Current global administrators",
            ["Tenants.GlobalAdministrators.Next"] = "Next",
            ["Tenants.GlobalAdministrators.PaginationLabel"] = "Global administrator pages",
            ["Tenants.GlobalAdministrators.Previous"] = "Previous",
            ["Tenants.GlobalAdministrators.Refresh"] = "Refresh",
            ["Tenants.GlobalAdministrators.Return"] = "Return to Tenants",
            ["Tenants.GlobalAdministrators.Recovery.Retry"] = "Retry review",
            ["Tenants.GlobalAdministrators.Recovery.Reset"] = "Reset to first page",
            ["Tenants.GlobalAdministrators.Recovery.IncompletePaging"] = "More administrators are indicated, but no continuation page is available. Reset to the first page and retry the review.",
            ["Tenants.GlobalAdministrators.Recovery.PageRecovered"] = "The protected page could not be restored. The review restarted at the first page.",
            ["Tenants.GlobalAdministrators.Recovery.HistoryTruncated"] = "Paging history reached its limit, so this step went back to the first page instead of the previous one.",
            ["Tenants.GlobalAdministrators.Recovery.AuthorizationChanged"] = "Authorization changed. The review restarted at the first page.",
            ["Tenants.GlobalAdministrators.Mobile.ReadOnly.Title"] = "Read-only on narrow screens",
            ["Tenants.GlobalAdministrators.Mobile.ReadOnly.Message"] = "Review, paging, copy, and recovery remain available. Grant and remove controls require a wider viewport.",
            ["Tenants.GlobalAdministrators.RestrictedTitle"] = "Platform area unavailable",
            ["Tenants.GlobalAdministrators.Row.Scope"] = "Platform authority, not tenant ownership",
            ["Tenants.GlobalAdministrators.Scope.Message"] = "This surface uses the singleton platform authority aggregate and never substitutes tenant membership data.",
            ["Tenants.GlobalAdministrators.Scope.Title"] = "Fixed aggregate scope",
            ["Tenants.GlobalAdministrators.State.Degraded.Message"] = "Projection data is degraded. Last confirmed administrators remain visible and actions are unavailable.",
            ["Tenants.GlobalAdministrators.State.Degraded.Title"] = "Global administrator data degraded",
            ["Tenants.GlobalAdministrators.State.Empty.Message"] = "No global administrators were returned for the authorized fixed scope.",
            ["Tenants.GlobalAdministrators.State.Empty.Title"] = "No global administrators returned",
            ["Tenants.GlobalAdministrators.State.Invalid.Message"] = "The requested page cursor is invalid. No administrator data is revealed.",
            ["Tenants.GlobalAdministrators.State.Invalid.Title"] = "Invalid global administrator page",
            ["Tenants.GlobalAdministrators.State.Loading.Message"] = "Loading global administrators from the fixed platform authority scope.",
            ["Tenants.GlobalAdministrators.State.Loading.Title"] = "Loading global administrators",
            ["Tenants.GlobalAdministrators.State.Ready.Message"] = "Global administrators are loaded from the fixed platform authority projection.",
            ["Tenants.GlobalAdministrators.State.Ready.Title"] = "Global administrators loaded",
            ["Tenants.GlobalAdministrators.State.Unknown.Message"] = "Projection freshness is unknown. Previously confirmed rows may be reviewed, but no current or complete claim is made.",
            ["Tenants.GlobalAdministrators.State.Unknown.Title"] = "Global administrator truth unknown",
            ["Tenants.GlobalAdministrators.State.Error.Message"] = "The review could not be refreshed. Retry or reset to the first page; no current projection claim is made.",
            ["Tenants.GlobalAdministrators.State.Error.Title"] = "Global administrator review failed",
            ["Tenants.GlobalAdministrators.State.Stale.Message"] = "Projection freshness is stale. Last confirmed administrators remain visible and actions are unavailable.",
            ["Tenants.GlobalAdministrators.State.Stale.Title"] = "Global administrator data stale",
            ["Tenants.GlobalAdministrators.State.Unauthorized.Message"] = "Platform authority was not confirmed. The area fails closed and does not reveal administrator data.",
            ["Tenants.GlobalAdministrators.State.Unauthorized.Title"] = "Platform area unavailable",
            ["Tenants.GlobalAdministrators.State.Unavailable.Message"] = "The global administrator read surface is unavailable. No hidden administrator data is shown.",
            ["Tenants.GlobalAdministrators.State.Unavailable.Title"] = "Global administrator data unavailable",
            ["Tenants.GlobalAdministrators.Title"] = "Global Administrators",
        };

        public LocalizedString this[string name]
            => new(name, Values.TryGetValue(name, out string? value) ? value : name);

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                string value = Values.TryGetValue(name, out string? template) ? template : name;
                return new(name, string.Format(CultureInfo.CurrentCulture, value, arguments));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Values.Select(static v => new LocalizedString(v.Key, v.Value));
    }

    [Fact]
    public async Task Cancelling_a_previewed_remove_does_not_cancel_in_flight_grant_status_tracking()
    {
        // The converse of Cancelling_grant_does_not_cancel_in_flight_remove_status_tracking. Only the
        // grant->remove direction was covered, so restoring the shared-invalidation defect in the
        // remove->grant direction kept the whole suite green -- while in production it discarded an accepted
        // grant's tracking handle and left its snapshot stuck at RequestSent.
        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.SupportsGlobalAdministratorDispatch.Returns(true);
        commandGateway.SupportsTrackedGlobalAdministratorDispatch.Returns(true);
        commandGateway.SupportsTrackedGlobalAdministratorRemoveDispatch.Returns(true);
        commandGateway.SupportsCommandStatusLookup.Returns(true);
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken grantStatusToken = default;
        commandGateway
            .SetGlobalAdministratorTrackedAsync(
                Arg.Any<SetGlobalAdministrator>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(
                call.ArgAt<string>(1),
                "correlation-grant")));
        commandGateway
            .GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                grantStatusToken = call.ArgAt<CancellationToken>(1);
                return pendingStatus.Task;
            });
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                ],
                null,
                false,
                "\"etag\"",
                ReadModelFreshnessState.Current) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                Lifecycle = ProjectionLifecycleState.Current,
                IsCompleteEvidence = true,
            })
        {
            RepeatLastResponse = true,
        });
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        // Submit a grant, then exercise the independent Remove cancellation path directly. The aggregate
        // admission gate intentionally prevents a second preview from opening while the grant is tracked;
        // invoking the cancellation seam is the precise regression check for cross-operation invalidation.
        cut.Find("[data-testid='tenants-global-admin-grant-user-id']").Change("grant-candidate");
        OpenGrantPreview(cut);
        AcknowledgeGrantPreview(cut);
        Task grantSubmit = cut.Find("[data-testid='tenants-global-admin-grant-confirm']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => grantStatusToken.CanBeCanceled.ShouldBeTrue());

        MethodInfo cancelRemove = typeof(GlobalAdministratorsPage).GetMethod(
            "CancelRemoveAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        await (Task)cancelRemove.Invoke(cut.Instance, null)!;

        grantStatusToken.IsCancellationRequested.ShouldBeFalse();
        pendingStatus.SetResult(TenantCommandStatusResult.Unknown("Status remains pending."));
        await grantSubmit;
        cut.Find("[data-testid='tenants-global-admin-grant-state']").TextContent
            .ShouldNotContain("No global administrator grant command");
    }

    [Fact]
    public void Client_local_paging_history_is_not_population_evidence_for_the_last_administrator_stop()
    {
        // The removed fail-open. On page 2+ the completeness gate is false, so the LastAdmin branch is
        // skipped; treating a non-empty cursor history as evidence that other administrators exist then
        // re-enabled Remove against a platform that may have exactly one administrator left. Only
        // server-stated population counts may satisfy the gate.
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            // Page one: HasMore, so paging forward is offered.
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-page-1", ReadModelFreshnessState.Current)],
                "cursor-page-2",
                true,
                "\"etag-1\"",
                ReadModelFreshnessState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current },
            // Page two: the last page (HasMore false) and a cursor was used, so IsCompleteEvidence is false.
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-page-2", ReadModelFreshnessState.Current)],
                null,
                false,
                "\"etag-2\"",
                ReadModelFreshnessState.Current) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                Lifecycle = ProjectionLifecycleState.Current,
                RequestCursor = "cursor-page-2",
            }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();
        cut.WaitForElement("[data-testid='tenants-global-admins-next']").Click();

        // On page two the history is non-empty, HasMore is false and evidence is incomplete: Remove must
        // stay unavailable rather than trusting client-local paging bookkeeping.
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldContain("admin-page-2");

            // The launcher is replaced by its unavailability reason rather than rendered disabled.
            cut.FindAll("[data-testid='tenants-global-admin-remove']").ShouldBeEmpty();
            cut.Find("[data-testid='tenants-global-admins-remove-reason']").TextContent
                .ShouldContain("Remove is unavailable because the complete fixed-scope population is not proven.");
        });
    }

    [Fact]
    public async Task Remove_submission_is_blocked_when_a_refresh_removes_the_previewed_target_from_the_page()
    {
        // The TargetMissing re-check. Preview is not a durable authorization: a refresh replaces _snapshot
        // underneath an open preview without resetting the intent, so the submit path must re-derive the
        // gates against the CURRENT snapshot. Only the LastAdmin branch was covered, so replacing this
        // condition with `false` kept the suite green.
        var commandGateway = new StubTenantCommandGateway();
        GlobalAdministratorsSnapshot eligible = GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("third-admin", ReadModelFreshnessState.Current),
                ],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag\"",
                freshness: ReadModelFreshnessState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true };
        var gateway = new StubTenantQueryGateway(
            eligible,
            eligible,
            // The previewed target is gone, but two administrators remain -- so the LastAdmin branch cannot
            // be what blocks the submit. Only the TargetMissing re-check can.
            GlobalAdministratorsSnapshot.Ready(
                [
                    new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                    new GlobalAdministratorRow("third-admin", ReadModelFreshnessState.Current),
                ],
                nextCursor: null,
                hasMore: false,
                eTag: "\"etag-2\"",
                freshness: ReadModelFreshnessState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<ITenantQueryGateway>(gateway);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        OpenRemovePreview(cut);
        AcknowledgeRemovePreview(cut);
        cut.Find("[data-testid='tenants-global-admin-remove-target']").TextContent.ShouldContain("target-admin");
        cut.Find("[data-testid='tenants-global-admin-remove-submit']").HasAttribute("disabled").ShouldBeFalse();

        await cut.Find("[data-testid='tenants-global-admins-refresh']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => gateway.GlobalAdministratorCalls.ShouldBe(3));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admin-remove-submit']").HasAttribute("disabled").ShouldBeTrue());
        await ClickRemoveSubmitEvenIfDisabledAsync(cut);
        commandGateway.RemoveGlobalAdministratorCalls.ShouldBe(0);
        commandGateway.RemoveMessageIds.ShouldBeEmpty();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admin-remove-safe-recovery']").TextContent
                .ShouldContain("currently present", Case.Insensitive));
    }

    [Fact]
    public void The_page_authorizes_through_the_async_resolver_not_the_synchronous_claims_property()
    {
        // The stub's synchronous property returns Indeterminate while its async resolver returns Authorized.
        // A page that reverted to claims-only authorization would render the denied surface.
        StubTenantsBffComposition composition = new(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-global-admins-user-id']").TextContent.ShouldContain("admin-1"));
        composition.AsyncResolutionCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void A_throwing_principal_resolution_fails_closed_to_the_denied_surface()
    {
        // The page's `catch { return Indeterminate; }` was unreachable in test, because the default
        // interface implementation it was hitting cannot throw.
        StubTenantsBffComposition composition = new(TenantLifecycleAuthorizationReflectionState.Authorized)
        {
            ThrowFromAsyncResolution = true,
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), Lifecycle = ProjectionLifecycleState.Current, IsCompleteEvidence = true }));
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());

        IRenderedComponent<GlobalAdministratorsPage> cut = Render<GlobalAdministratorsPage>();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='tenants-global-admins-user-id']").ShouldBeEmpty());
        composition.AsyncResolutionCount.ShouldBeGreaterThan(0);
    }
}
