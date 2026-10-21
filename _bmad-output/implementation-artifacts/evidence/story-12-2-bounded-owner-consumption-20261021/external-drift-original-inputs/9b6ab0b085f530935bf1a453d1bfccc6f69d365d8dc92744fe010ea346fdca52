using Hexalith.Tenants.Contracts.Identity;
using static Hexalith.Tenants.UI.Tests.TenantCommandProofFixtures;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using Bunit;
using Fluxor;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Tenants.Audit;
using Hexalith.Tenants.UI.Components.Tenants.Members;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class RemoveTenantMemberFlowTests : FluentBunitContext
{
    public RemoveTenantMemberFlowTests()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        Services.AddSingleton(viewport);
    }

    [Fact]
    public void Ready_receipt_with_wrong_tenant_return_disables_inspect_with_reason()
    {
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.Returns(true);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemovalWithAuditReturn(
            "/tenants/tenant.beta", composition);

        cut.Find("[data-testid='tenants-remove-member-inspect-unavailable']")
            .TextContent.ShouldContain("invalid", Case.Insensitive);
        cut.FindComponent<AuditEvidenceReceipt>().Instance.OnInspectAudit.HasDelegate.ShouldBeFalse();
    }

    [Fact]
    public async Task Ready_receipt_inspect_navigates_to_the_scoped_audit_route_with_the_detail_return()
    {
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.Returns(true);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemovalWithAuditReturn(
            "/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha", composition);

        EventCallback inspect = cut.FindComponent<AuditEvidenceReceipt>().Instance.OnInspectAudit;
        inspect.HasDelegate.ShouldBeTrue();
        await cut.InvokeAsync(() => inspect.InvokeAsync());

        string navigated = Services.GetRequiredService<NavigationManager>().Uri;
        Uri.UnescapeDataString(navigated).ShouldBe(
            "http://localhost/tenants/tenant.alpha/audit?targetUserId=reader-user&source=command-result"
            + "&returnUrl=/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha&returnFocus=tenants-remove-member-lifecycle");
        navigated.ShouldNotContain("supportSafeCommandReference");
        navigated.ShouldNotContain("message-1");
    }

    [Fact]
    public void Ready_receipt_for_a_denied_caller_offers_no_audit_handoff_or_invalid_context_reason()
    {
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.Returns(true);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemovalWithAuditReturn(
            "/tenants/tenant.alpha", composition, auditReadAvailable: false, auditReadDenied: true);

        cut.FindComponents<AuditEvidenceReceipt>().ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-inspect-unavailable']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-command-audit-entrypoint']").ShouldBeEmpty();
    }

    [Fact]
    public void Ready_receipt_with_unconfirmed_audit_access_explains_access_instead_of_invalid_context()
    {
        const string unconfirmed = "Audit access could not be confirmed for the current tenant scope. Refresh to try again.";
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.Returns(true);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemovalWithAuditReturn(
            "/tenants/tenant.alpha", composition, auditReadAvailable: false, auditReadUnavailableReason: unconfirmed);

        cut.FindComponents<AuditEvidenceReceipt>().ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-inspect-unavailable']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-command-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
        cut.Find(".tenants-audit-entrypoint__reason").TextContent.ShouldBe(unconfirmed);
        cut.Markup.ShouldNotContain("The audit context is invalid");
    }

    [Fact]
    public async Task Ready_receipt_rechecks_live_audit_connection_before_inspect()
    {
        bool connected = true;
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.Returns(_ => connected);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemovalWithAuditReturn(
            "/tenants/tenant.alpha", composition);

        EventCallback inspect = cut.FindComponent<AuditEvidenceReceipt>().Instance.OnInspectAudit;
        inspect.HasDelegate.ShouldBeTrue();
        connected = false;
        await cut.InvokeAsync(() => inspect.InvokeAsync());

        new Uri(Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath.ShouldBe("/");
    }

    [Fact]
    public void Remove_flow_renders_complete_preview_with_stable_selectors_and_no_audit_receipt_claim()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-flow']").GetAttribute("role").ShouldBe("dialog");
        cut.Find("[data-testid='tenants-remove-member-flow']").GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("[data-testid='tenants-remove-member-focus-start']");
        cut.Find("[data-testid='tenants-remove-member-focus-end']");
        cut.Find("[data-testid='tenants-remove-member-preview']");
        cut.FindAll("[data-testid='tenants-remove-member-preview-item']").Count.ShouldBe(10);
        cut.Find("[data-testid='tenants-remove-member-target-user-id']").TextContent.ShouldContain("reader-user");
        cut.Find("[data-testid='tenants-remove-member-current-role']").TextContent.ShouldContain("Tenant reader");
        cut.Find("[data-testid='tenants-remove-member-owner-context']").TextContent.ShouldContain("2 visible owners");
        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent.ShouldContain("unproven");
        cut.Find("[data-testid='tenants-remove-member-consequences-versus-unknowns']").TextContent
            .ShouldContain("Known consequence");
        cut.FindAll("[data-testid='tenants-remove-member-global-admin-risk']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldBeNull();
        cut.Markup.ShouldNotContain("audit available", Case.Insensitive);
        cut.Markup.ShouldNotContain("receipt", Case.Insensitive);
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Last_owner_warning_is_visible_but_does_not_block_destructive_confirmation()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-999"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        TenantDetail detail = Detail("tenant.alpha", [new TenantMember("owner-user", TenantRole.TenantOwner)]);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.Member, new TenantMember("owner-user", TenantRole.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-owner-risk']").TextContent.ShouldContain("Last-owner");
        cut.FindAll("[data-testid='tenants-remove-member-global-admin-risk']").ShouldBeEmpty();
        cut.Markup.ShouldContain("Elevated risk: type the target user id exactly to confirm removal");
        cut.Find("#tenants-remove-member-confirmation-help").TextContent
            .ShouldContain("Elevated risk: type owner-user exactly");
        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldBeNull();

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("Owner-User");
        cut.Find("form").Submit();
        cut.Find("[data-testid='tenants-remove-member-validation']").TextContent
            .ShouldContain("Elevated risk requires typing owner-user exactly");
        gateway.RemoveMemberCallCount.ShouldBe(0);

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("owner-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
    }

    [Fact]
    public void Global_admin_friction_is_visible_when_reflected_without_dispatching_global_admin_command()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.TargetGlobalAdministratorFriction, true)
            .Add(p => p.TargetPlatformStandingResolved, true));

        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("Also a global administrator");
        cut.Find("[data-testid='tenants-remove-member-global-admin-risk']").TextContent
            .ShouldContain("will not remove global-administrator authority");
        cut.Markup.ShouldContain("Elevated risk", Case.Insensitive);
        cut.Markup.ShouldNotContain("RemoveGlobalAdministrator", Case.Insensitive);
    }

    [Fact]
    public void Resolved_non_global_admin_platform_standing_does_not_raise_elevated_ga_friction()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.TargetPlatformStandingResolved, true));

        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("Not reflected as a global administrator");
        cut.FindAll("[data-testid='tenants-remove-member-global-admin-risk']").ShouldBeEmpty();
    }

    [Fact]
    public void Incomplete_preview_blocks_confirm_and_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(p => p.Freshness, ReadModelFreshnessState.Stale));

        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        string unavailable = cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent;
        unavailable.ShouldContain("Refresh current tenant detail", Case.Insensitive);
        // Same reason must not also render as the preview-blocked banner.
        cut.FindAll("[data-testid='tenants-remove-member-preview-blocked']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-preview-item']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(0);
    }

    [Fact]
    public void Css_contains_focus_trap_and_narrow_layout_hooks()
    {
        string styles = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Members",
            "RemoveTenantMemberFlow.razor.css"));

        styles.ShouldContain("tenants-remove-member-flow__focus-sentinel");
        styles.ShouldContain("tenants-remove-member-flow__narrow");
        int narrowMedia = styles.IndexOf("@media (max-width: 767px)", StringComparison.Ordinal);
        narrowMedia.ShouldBeGreaterThanOrEqualTo(0, "Expected a max-width: 767px media query for narrow fail-closed layout.");
        int nextMedia = styles.IndexOf("@media", narrowMedia + 1, StringComparison.Ordinal);
        string narrowBody = nextMedia < 0 ? styles[narrowMedia..] : styles[narrowMedia..nextMedia];
        Regex.IsMatch(
                narrowBody,
                @"\.tenants-remove-member-flow__form\s*\{\s*display:\s*none\s*;",
                RegexOptions.CultureInvariant)
            .ShouldBeTrue("Narrow media query must hide .tenants-remove-member-flow__form with display: none.");
    }

    [Fact]
    public void Rendered_form_carries_the_narrow_layout_hide_class()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("form").ClassList.ShouldContain("tenants-remove-member-flow__form");
    }

    [Fact]
    public void Initial_focus_targets_visible_cancel_instead_of_the_narrow_hidden_confirmation_form()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        string cancelReferenceId = ((ElementReference)(cut.Instance.GetType()
            .GetField("_cancelElement", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(cut.Instance)
            ?? throw new InvalidOperationException("Remove flow cancel focus reference is unavailable."))).Id;
        string focusedReferenceId = JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Last()
            .Id;

        focusedReferenceId.ShouldBe(cancelReferenceId);
    }

    [Fact]
    public void Confirmation_submits_literal_user_id_and_confirms_only_after_absent_projection_evidence()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-999"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        RegisterServices(gateway);
        TenantDetail originalDetail = Detail("tenant.alpha");
        int projectionCalls = 0;
        string liveProjectionVersion = "tenant-sequence:1";

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, originalDetail)
            .Add(p => p.Member, new TenantMember("User/CaseSensitive.01", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                if (++projectionCalls == 1)
                {
                    return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                        request.TenantId,
                        [
                            new TenantMember("owner-user", TenantRole.TenantOwner),
                            new TenantMember(request.UserId, TenantRole.TenantReader),
                        ]), liveProjectionVersion));
                }

                liveProjectionVersion = "tenant-sequence:2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                    request.TenantId,
                    [new TenantMember("owner-user", TenantRole.TenantOwner)]), liveProjectionVersion));
            }));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("User/CaseSensitive.01");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        gateway.LastRemoveMemberRequest.ShouldNotBeNull().UserId.ShouldBe("User/CaseSensitive.01");
        cut.Find("[data-testid='tenants-remove-member-target-user-id']").TextContent.ShouldContain("User/CaseSensitive.01");
        cut.Find("[data-testid='tenants-remove-member-state']").TextContent.ShouldContain("Projection pending");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);

        cut.Find("[data-testid='tenants-remove-member-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        projectionCalls.ShouldBe(2);
        cut.Find("[data-testid='tenants-remove-member-state']").TextContent.ShouldContain("Projection confirmed");
        cut.Markup.ShouldNotContain("correlation-999", Case.Insensitive);
    }

    [Fact]
    public void User_not_in_tenant_rejection_requires_absent_projection_before_already_applied()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-999"),
            Status = new TenantCommandStatusResult(CommandStatus.Rejected, "Target user not in tenant.", "UserNotInTenant", HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                request.TenantId,
                [new TenantMember("owner-user", TenantRole.TenantOwner)]), "tenant-sequence:2"))));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.AlreadyApplied));
        cut.Find("[data-testid='tenants-remove-member-state']").TextContent.ShouldContain("Already applied");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Submission_time_user_not_in_tenant_rejection_reconciles_to_already_applied_after_absent_projection()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Rejected("The target user is not a visible member of this tenant.", "UserNotInTenant"),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                request.TenantId,
                [new TenantMember("owner-user", TenantRole.TenantOwner)]), "tenant-sequence:2"))));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        // A submission-time rejection carries no tracking handle, but the refresh recovery action
        // must still be reachable so projection evidence can reconcile it to already-applied (AC4).
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Rejected));
        cut.Find("[data-testid='tenants-remove-member-refresh']").GetAttribute("disabled").ShouldBeNull();
        // A rejection stored nothing: no audit state is implied and the shared control stays hidden.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-audit-availability']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-remove-member-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.AlreadyApplied));
        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Find("[data-testid='tenants-remove-member-state']").TextContent.ShouldContain("Already applied");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Confirmation_text_must_match_target_before_gateway_submission()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("Reader-User");
        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-validation']").TextContent.ShouldContain("reader-user");
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
    }

    [Fact]
    public void Already_absent_target_before_submit_records_already_applied_without_gateway_submission()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha", [new TenantMember("owner-user", TenantRole.TenantOwner)]))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.AlreadyApplied);
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldContain("already absent");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Duplicate_submit_while_remove_command_is_in_flight_is_blocked_before_gateway_submission()
    {
        TaskCompletionSource<TenantCommandSubmissionResult> pendingSubmission = new();
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => pendingSubmission.Task,
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));

        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Find("[data-testid='tenants-remove-member-state']").TextContent.ShouldContain("request sent", Case.Insensitive);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("polite");

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Failed("Command submission cancelled by the test."));
    }

    [Fact]
    public void In_flight_retry_with_tracking_reuses_status_lookup_and_does_not_dispatch_again()
    {
        int closeCount = 0;
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-999"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.CommandActivityLease, active =>
            {
                activity.Add(active);
                return Task.FromResult(true);
            })
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        int statusCallsAfterSubmit = gateway.StatusCallCount;

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        gateway.StatusCallCount.ShouldBeGreaterThan(statusCallsAfterSubmit);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Find("[data-testid='tenants-remove-member-cancel']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-remove-member-flow']").KeyDown("Escape");
        closeCount.ShouldBe(0);
        activity.ShouldBe([true]);
    }

    [Fact]
    public void Ambiguous_failure_reuses_message_id_only_for_the_exact_same_removal()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Submission outcome is ambiguous.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        DateTimeOffset firstAttemptStartedAtUtc = cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldNotBeNull();
        cut.Find("form").Submit();
        gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(firstAttemptStartedAtUtc);

        cut.Render(parameters => parameters
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("second-owner", TenantRole.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("second-owner");
        cut.Find("form").Submit();

        NUlid.Ulid.TryParse(gateway.LastRemoveMemberMessageId, out _).ShouldBeTrue();
        gateway.RemoveMemberCallCount.ShouldBe(3);
    }

    /// <summary>Undispatched refusals preserve the preview until admission permits exactly one delivery.</summary>
    /// <param name="throwOnRefusal">Whether the activity callback throws instead of declining.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstLeaseRefusalsAllowResubmissionWithoutReopening(bool throwOnRefusal)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => delivery.Task,
        };
        ITenantQueryGateway queries = CreateCapableQueryGateway();
        RegisterServices(gateway, queries);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (!active || ++acquisitions > 2)
            {
                return Task.FromResult(true);
            }
            return throwOnRefusal ? Task.FromException<bool>(new InvalidOperationException("Unavailable callback")) : Task.FromResult(false);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        TenantRemoveMemberCommandSnapshot preview = cut.Instance.Snapshot;

        for (int i = 0; i < 2; i++)
        {
            await cut.Find("form").SubmitAsync();
            TenantRemoveMemberCommandSnapshot refused = cut.Instance.Snapshot;
            refused.State.ShouldBe(TenantCommandLifecycleState.Previewed);
            refused.Intent.ShouldBe(preview.Intent);
            refused.LastConfirmedMemberProjection.ShouldBeSameAs(preview.LastConfirmedMemberProjection);
            refused.CurrentConfirmedRole.ShouldBe(preview.CurrentConfirmedRole);
            refused.OwnerCount.ShouldBe(preview.OwnerCount);
            refused.TargetGlobalAdministratorFriction.ShouldBe(preview.TargetGlobalAdministratorFriction);
            refused.IsPreviewComplete.ShouldBeTrue();
            refused.MessageId.ShouldBeNull();
            refused.CorrelationId.ShouldBeNull();
            refused.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
            cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe("A tenant command is already in progress.");
            cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
            cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBe("reader-user");
            cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeFalse();
            cut.FindAll("[data-testid='tenants-command-audit-entrypoint']").ShouldBeEmpty();
            gateway.RemoveMemberCallCount.ShouldBe(0);
            gateway.StatusCallCount.ShouldBe(0);
            queries.ReceivedCalls().ShouldBeEmpty();
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
            gate.IsLocked(key).ShouldBeFalse();
        }

        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.alpha", "reader-user"));
        string messageId = gateway.LastRemoveMemberMessageId.ShouldNotBeNull();
        NUlid.Ulid.TryParse(messageId, out _).ShouldBeTrue();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(key).ShouldBeTrue();
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        delivery.SetResult(TenantCommandSubmissionResult.Failed("Delivery refused by test."));
        await submission;
        acquisitions.ShouldBe(3);
    }

    /// <summary>A real competing aggregate lease survives refusal and its release permits resubmission.</summary>
    [Fact]
    public async Task ForeignAggregateLeaseRefusalAllowsResubmissionAfterItsOwnerReleases()
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => delivery.Task,
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
            {
                CommittedEventSequence = 2,
            },
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        object owner = new();
        gate.TryAcquireLease(key, owner, out TenantAggregateCommandLease? foreign).ShouldBeTrue();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(key).ShouldBeTrue();
        foreign.ShouldNotBeNull().TryAbandonBeforeDispatch(owner).ShouldBeTrue();
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        string messageId = gateway.LastRemoveMemberMessageId.ShouldNotBeNull();
        delivery.SetResult(TenantCommandSubmissionResult.Accepted(messageId, "correlation-1"));
        await submission;
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        gate.IsLocked(key).ShouldBeTrue();
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        TenantDetail after = Detail("tenant.alpha", [new("owner-user", TenantRole.TenantOwner), new("second-owner", TenantRole.TenantOwner)]);
        await cut.InvokeAsync(() => cut.Instance.ApplyProjectionEvidence(Proof(after, "tenant-sequence:2")));
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            gate.IsLocked(key).ShouldBeFalse();
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        });
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.alpha", "reader-user"));
    }

    /// <summary>Releasing admission cannot bypass current authorization or freshness.</summary>
    /// <param name="loseAuthorization">Whether authorization changes instead of freshness.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResubmissionAfterLeaseRefusalStillChecksCurrentSafetyEvidence(bool loseAuthorization)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active => Task.FromResult(!active || ++acquisitions > 1));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Render(p => p
            .Add(x => x.IsAuthorized, !loseAuthorization)
            .Add(x => x.Freshness, loseAuthorization ? ReadModelFreshnessState.Current : ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        acquisitions.ShouldBe(1);
        cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeTrue();
    }

    /// <summary>Refusal feedback in the live region resolves the shipped whole-message resources.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public async Task FirstLeaseRefusalAnnouncesLocalizedBusyExplanation(string cultureName)
    {
        RegisterServices(new StubTenantCommandGateway());
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(false));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        string explanation = localizer["Tenants.RemoveMember.Unavailable.InFlight"].Value;
        explanation.ShouldNotBe("Tenants.RemoveMember.Unavailable.InFlight");
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
    }

    /// <summary>Fresh authority must still admit the next submission when rendered parameters remain authorized.</summary>
    /// <param name="authority">The newly resolved authority.</param>
    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate)]
    public async Task ResubmissionAfterLeaseRefusalRequeriesCurrentAuthority(TenantLifecycleAuthorizationReflectionState authority)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active => Task.FromResult(!active || ++acquisitions > 1));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
        {
            authorityReads++;
            return Task.FromResult(authority);
        }));
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(1);
        acquisitions.ShouldBe(1);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    /// <summary>Changed safety evidence during a held reacquisition prevents dispatch and releases undispatched ownership.</summary>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReacquisitionRechecksSafetyAfterAwaitAndCleansUp(bool changeConfirmation)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
            !active ? Task.FromResult(true) : ++acquisitions == 1 ? Task.FromResult(false) : acquired.Task);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(2));
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        if (changeConfirmation)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        }
        acquired.SetResult(true);
        await submission;
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    /// <summary>Repeated pre-dispatch freshness refusals recover without reopening or losing the confirmed preview.</summary>
    /// <param name="freshness">The unavailable freshness to recover from.</param>
    [Theory]
    [InlineData(ReadModelFreshnessState.Stale)]
    [InlineData(ReadModelFreshnessState.Unknown)]
    [InlineData(ReadModelFreshnessState.Aging)]
    public async Task UndispatchedFreshnessRefusalAllowsExplicitResubmissionAfterRecovery(ReadModelFreshnessState freshness)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => delivery.Task,
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
            {
                CommittedEventSequence = 2,
            },
        };
        ITenantQueryGateway queries = CreateCapableQueryGateway();
        RegisterServices(gateway, queries);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                acquisitions++;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        TenantRemoveMemberCommandSnapshot preview = cut.Instance.Snapshot;
        cut.Render(p => p.Add(x => x.Freshness, freshness));

        for (int i = 0; i < 2; i++)
        {
            await cut.Find("form").SubmitAsync();
            gateway.RemoveMemberCallCount.ShouldBe(0);
            gateway.StatusCallCount.ShouldBe(0);
            queries.ReceivedCalls().ShouldBeEmpty();
            acquisitions.ShouldBe(0);
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
            cut.Instance.Snapshot.MessageId.ShouldBeNull();
            cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
            cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeTrue();
        }

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-remove-member-safe-message']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.ShouldNotContain("Refresh current tenant detail");
        cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBe("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        cut.Instance.Snapshot.Intent.ShouldBe(preview.Intent);
        cut.Instance.Snapshot.LastConfirmedMemberProjection.ShouldBeSameAs(preview.LastConfirmedMemberProjection);
        cut.Instance.Snapshot.CurrentConfirmedRole.ShouldBe(preview.CurrentConfirmedRole);
        cut.Instance.Snapshot.OwnerCount.ShouldBe(preview.OwnerCount);
        cut.Instance.Snapshot.TargetGlobalAdministratorFriction.ShouldBe(preview.TargetGlobalAdministratorFriction);
        string messageId = gateway.LastRemoveMemberMessageId.ShouldNotBeNull();
        NUlid.Ulid.TryParse(messageId, out _).ShouldBeTrue();
        acquisitions.ShouldBe(1);
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        delivery.SetResult(TenantCommandSubmissionResult.Accepted(messageId, "correlation-1"));
        await submission;
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        TenantDetail after = Detail("tenant.alpha", [new("owner-user", TenantRole.TenantOwner), new("second-owner", TenantRole.TenantOwner)]);
        await cut.InvokeAsync(() => cut.Instance.ApplyProjectionEvidence(Proof(after, "tenant-sequence:2")));
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        });
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>Recovery from a held admission safety refusal rechecks all evidence on a new explicit submit.</summary>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndispatchedHeldAdmissionRefusalAllowsResubmissionAfterSafetyRecovers(bool changeConfirmation)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
            !active || ++acquisitions > 1 ? Task.FromResult(true) : acquired.Task);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        if (changeConfirmation)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        }
        acquired.SetResult(true);
        await submission;
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.alpha", "reader-user"));
        acquisitions.ShouldBe(2);
    }

    /// <summary>Held admission reports the current safety blocker in the shipped resource culture.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    /// <param name="blocker">The safety evidence changed while admission is held.</param>
    /// <param name="isRetry">Whether the refused submission retries a failed delivery.</param>
    [Theory]
    [InlineData("en-US", "stale", false)]
    [InlineData("fr-FR", "stale", false)]
    [InlineData("en-US", "unknown", false)]
    [InlineData("fr-FR", "unknown", false)]
    [InlineData("en-US", "confirmation", false)]
    [InlineData("fr-FR", "confirmation", false)]
    [InlineData("en-US", "elevated-confirmation", false)]
    [InlineData("fr-FR", "elevated-confirmation", false)]
    [InlineData("en-US", "stale", true)]
    [InlineData("fr-FR", "stale", true)]
    [InlineData("en-US", "unknown", true)]
    [InlineData("fr-FR", "unknown", true)]
    [InlineData("en-US", "confirmation", true)]
    [InlineData("fr-FR", "confirmation", true)]
    [InlineData("en-US", "elevated-confirmation", true)]
    [InlineData("fr-FR", "elevated-confirmation", true)]
    public async Task HeldRemovalAdmissionExplainsCurrentSafetyRefusal(string cultureName, string blocker, bool isRetry)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        int heldAcquisition = isRetry ? 2 : 1;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (!active)
            {
                releases++;
                return Task.FromResult(true);
            }
            return ++acquisitions == heldAcquisition ? acquired.Task : Task.FromResult(true);
        });
        bool confirmationChanged = blocker is "confirmation" or "elevated-confirmation";
        cut.Render(p => p.Add(x => x.TargetGlobalAdministratorFriction, blocker is "elevated-confirmation"));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            await cut.Find("form").SubmitAsync();
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        }
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(heldAcquisition));
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        if (confirmationChanged)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, blocker is "stale" ? ReadModelFreshnessState.Stale : ReadModelFreshnessState.Unknown));
        }
        acquired.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));

        string resourceKey = confirmationChanged
            ? blocker is "elevated-confirmation"
                ? "Tenants.RemoveMember.Validation.ElevatedConfirmationRequired"
                : "Tenants.RemoveMember.Validation.ConfirmationRequired"
            : "Tenants.RemoveMember.Unavailable.Freshness";
        string explanation = confirmationChanged ? localizer[resourceKey, "reader-user"].Value : localizer[resourceKey].Value;
        explanation.ShouldNotBe(resourceKey);
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Instance.Snapshot.SafeMessage.ShouldBe(explanation);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBeNull();
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBe(original.CorrelationId);
        cut.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        cut.Instance.Snapshot.State.ShouldBe(original.State);
        cut.Instance.Snapshot.AuditState.ShouldBe(isRetry ? TenantCommandAuditState.AuditUnavailable : TenantCommandAuditState.NotStarted);
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        gateway.StatusCallCount.ShouldBe(0);
        releases.ShouldBe(heldAcquisition);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(heldAcquisition + 1);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 2 : 1);
        if (isRetry)
        {
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        }
    }

    /// <summary>Stable-generation fresh authority failures retain their distinct localized explanations.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    /// <param name="failure">The fresh authority outcome after activity admission.</param>
    /// <param name="isRetry">Whether the refused submission retries a failed delivery.</param>
    [Theory]
    [InlineData("en-US", "denied", false)]
    [InlineData("fr-FR", "denied", false)]
    [InlineData("en-US", "indeterminate", false)]
    [InlineData("fr-FR", "indeterminate", false)]
    [InlineData("en-US", "fault", false)]
    [InlineData("fr-FR", "fault", false)]
    [InlineData("en-US", "denied", true)]
    [InlineData("fr-FR", "denied", true)]
    [InlineData("en-US", "indeterminate", true)]
    [InlineData("fr-FR", "indeterminate", true)]
    [InlineData("en-US", "fault", true)]
    [InlineData("fr-FR", "fault", true)]
    public async Task HeldRemovalAdmissionExplainsActualAuthorityFailure(string cultureName, string failure, bool isRetry)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int heldAcquisition = isRetry ? 2 : 1;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
            active && ++acquisitions == heldAcquisition ? acquired.Task : Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            await cut.Find("form").SubmitAsync();
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        }
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
        {
            if (++authorityReads == 1)
            {
                return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            }
            return failure switch
            {
                "denied" => Task.FromResult(TenantLifecycleAuthorizationReflectionState.MissingPermission),
                "indeterminate" => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Indeterminate),
                _ => Task.FromException<TenantLifecycleAuthorizationReflectionState>(new InvalidOperationException("private authority failure")),
            };
        }));
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(heldAcquisition));
        acquired.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        string resourceKey = failure is "denied" ? "Tenants.RemoveMember.Unavailable.Authorization" : "Tenants.Mutation.Unavailable.AuthorityIndeterminate";
        string explanation = localizer[resourceKey].Value;
        explanation.ShouldNotBe(resourceKey);
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Markup.ShouldNotContain("private authority failure");
        authorityReads.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        cut.Instance.Snapshot.AuditState.ShouldBe(isRetry ? TenantCommandAuditState.AuditUnavailable : TenantCommandAuditState.NotStarted);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(heldAcquisition + 1);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 2 : 1);
        if (isRetry)
        {
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        }
    }

    /// <summary>Restored safety evidence cannot revive invalidated admission or bypass a fresh explicit submission.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    /// <param name="isRetry">Whether the refused submission retries a failed delivery.</param>
    [Theory]
    [InlineData("en-US", false, false)]
    [InlineData("fr-FR", false, false)]
    [InlineData("en-US", true, false)]
    [InlineData("fr-FR", true, false)]
    [InlineData("en-US", false, true)]
    [InlineData("fr-FR", false, true)]
    [InlineData("en-US", true, true)]
    [InlineData("fr-FR", true, true)]
    public async Task RestoredSafetyStillRefusesInvalidatedRemovalAdmission(string cultureName, bool changeConfirmation, bool isRetry)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int authorityReads = 0;
        int heldAcquisition = isRetry ? 2 : 1;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
            active && ++acquisitions == heldAcquisition ? acquired.Task : Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            await cut.Find("form").SubmitAsync();
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        }
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
        {
            authorityReads++;
            return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        }));
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(heldAcquisition));
        if (changeConfirmation)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        }
        acquired.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        authorityReads.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.State.ShouldBe(original.State);
        string explanation = localizer["Tenants.RemoveMember.Admission.Changed"].Value;
        explanation.ShouldNotBe("Tenants.RemoveMember.Admission.Changed");
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(4);
        acquisitions.ShouldBe(heldAcquisition + 1);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 2 : 1);
        if (isRetry)
        {
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        }
    }

    /// <summary>Safety invalidation cancels a held authority read and ignores its late result.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    /// <param name="isRetry">Whether the refused submission retries a failed delivery.</param>
    /// <param name="holdCleanup">Whether activity release completes asynchronously after cancellation.</param>
    [Theory]
    [InlineData("en-US", false, false, false)]
    [InlineData("en-US", false, false, true)]
    [InlineData("fr-FR", false, false, false)]
    [InlineData("fr-FR", false, false, true)]
    [InlineData("en-US", true, false, false)]
    [InlineData("en-US", true, false, true)]
    [InlineData("fr-FR", true, false, false)]
    [InlineData("fr-FR", true, false, true)]
    [InlineData("en-US", false, true, false)]
    [InlineData("en-US", false, true, true)]
    [InlineData("fr-FR", false, true, false)]
    [InlineData("fr-FR", false, true, true)]
    [InlineData("en-US", true, true, false)]
    [InlineData("en-US", true, true, true)]
    [InlineData("fr-FR", true, true, false)]
    [InlineData("fr-FR", true, true, true)]
    public async Task PendingRemovalAuthorityReadReportsChangedSafety(string cultureName, bool changeConfirmation, bool isRetry, bool holdCleanup)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool observeRelease = false;
        bool? releaseHadDispatcherAccess = null;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (!active && observeRelease)
            {
                releaseHadDispatcherAccess = Renderer.Dispatcher.CheckAccess();
                releaseEntered.TrySetResult();
                return holdCleanup ? released.Task : Task.FromResult(true);
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            await cut.Find("form").SubmitAsync();
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        }
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken authorityToken = default;
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, token =>
        {
            if (++authorityReads == 2)
            {
                authorityToken = token;
                return authority.Task;
            }
            return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        }));
        observeRelease = true;
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => authorityReads.ShouldBe(2));
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        if (changeConfirmation)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        }
        await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseHadDispatcherAccess.ShouldBe(true);
        if (holdCleanup)
        {
            submission.IsCompleted.ShouldBeFalse();
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
            await cut.Find("form").SubmitAsync();
            gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
            authorityReads.ShouldBe(2);
            await Task.Run(() => released.SetResult(true));
        }
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        authorityToken.IsCancellationRequested.ShouldBeTrue();
        string explanation = changeConfirmation
            ? localizer["Tenants.RemoveMember.Validation.ConfirmationRequired", "reader-user"].Value
            : localizer["Tenants.RemoveMember.Unavailable.Freshness"].Value;
        cut.Instance.Snapshot.SafeMessage.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        TenantRemoveMemberCommandSnapshot refused = cut.Instance.Snapshot;
        authority.SetResult(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.Instance.Snapshot.ShouldBe(refused);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(4);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 2 : 1);
        if (isRetry)
        {
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        }
    }

    /// <summary>An authorized initial read queued before a safety transition cannot admit the obsolete generation.</summary>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    /// <param name="isRetry">Whether submission retries a failed delivery.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task QueuedInitialRemovalAuthorityCannotAdmitChangedGeneration(bool changeConfirmation, bool isRetry)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active) acquisitions++;
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            await cut.Find("form").SubmitAsync();
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        }
        acquisitions = 0;
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => ++authorityReads == 1
            ? authority.Task : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => authorityReads.ShouldBe(1));
        using var dispatcherEntered = new ManualResetEventSlim();
        using var releaseDispatcher = new ManualResetEventSlim();
        Task blocker = Task.Run(() => Renderer.Dispatcher.InvokeAsync(() =>
        {
            dispatcherEntered.Set();
            releaseDispatcher.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            if (changeConfirmation)
            {
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
            }
            else
            {
                cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
                cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
            }
        }));
        try
        {
            dispatcherEntered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            // The initial-read wrapper clears this flag only after its authorization task has returned.
            // Holding the renderer lets that successful result queue before the safety transition.
            FieldInfo resolving = typeof(RemoveTenantMemberFlow).GetField("_authorityResolutionInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await WaitUntilAsync(() => !(bool)resolving.GetValue(cut.Instance)!, TimeSpan.FromSeconds(5));
            submission.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseDispatcher.Set();
            await blocker.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        authorityReads.ShouldBe(1);
        acquisitions.ShouldBe(0);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.ShouldBe(original);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(3);
        acquisitions.ShouldBe(1);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 2 : 1);
        if (isRetry)
        {
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        }
    }

    /// <summary>Ambiguous delivery retries reject obsolete authority while retaining their original ownership and identity.</summary>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    /// <param name="queueSuccessfulAuthority">Whether authority succeeds before the safety transition.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AmbiguousRemovalRetryRejectsChangedAdmissionGeneration(bool changeConfirmation, bool queueSuccessfulAuthority)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                IsAmbiguousFailure = true,
            },
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        using var releaseGateway = new ManualResetEventSlim();
        int observeGatewayResolution = 0;
        int offRendererGatewayResolutions = 0;
        Services.RemoveAll<ITenantCommandGateway>();
        Services.AddTransient<ITenantCommandGateway>(_ =>
        {
            if (Volatile.Read(ref observeGatewayResolution) == 1 && !Renderer.Dispatcher.CheckAccess())
            {
                Interlocked.Increment(ref offRendererGatewayResolutions);
                // An obsolete off-renderer continuation must consume the restored safety values,
                // rather than racing dispatch ahead of the controlled parameter/input transition.
                releaseGateway.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            }
            return gateway;
        });
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active) acquisitions++;
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        original.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        original.CorrelationId.ShouldBeNull();
        original.AttemptStartedAtUtc.ShouldNotBeNull();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        acquisitions.ShouldBe(1);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => ++authorityReads == 2
            ? authority.Task : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        Task retry = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => authorityReads.ShouldBe(2));
        Volatile.Write(ref observeGatewayResolution, 1);
        using var dispatcherEntered = new ManualResetEventSlim();
        using var releaseDispatcher = new ManualResetEventSlim();
        Task blocker = Task.Run(() => Renderer.Dispatcher.InvokeAsync(() =>
        {
            dispatcherEntered.Set();
            releaseDispatcher.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            if (changeConfirmation)
            {
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
            }
            else
            {
                cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
                cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
            }
        }));
        try
        {
            dispatcherEntered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            if (queueSuccessfulAuthority)
            {
                authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
                FieldInfo resolving = typeof(RemoveTenantMemberFlow).GetField("_authorityResolutionInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
                await WaitUntilAsync(() => !(bool)resolving.GetValue(cut.Instance)!, TimeSpan.FromSeconds(5));
                retry.IsCompleted.ShouldBeFalse();
            }
        }
        finally
        {
            releaseDispatcher.Set();
            try
            {
                await blocker.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                releaseGateway.Set();
                authority.TrySetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            }
        }
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Write(ref observeGatewayResolution, 0);
        gateway.RemoveMemberCallCount.ShouldBe(1);
        offRendererGatewayResolutions.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        acquisitions.ShouldBe(1);
        cut.Instance.Snapshot.ShouldBe(original);
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(4);
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberRequest.ShouldBe(original.Intent);
        gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        gateway.StatusCallCount.ShouldBe(0);
        acquisitions.ShouldBe(1);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    /// <summary>A real ambiguous retry refuses unavailable current authority and can later be accepted under its original identity.</summary>
    /// <param name="refusal">The current authority failure to exercise.</param>
    [Theory]
    [InlineData("denied")]
    [InlineData("indeterminate")]
    [InlineData("faulted")]
    [InlineData("cancelled")]
    public async Task AmbiguousRemovalRetryReauthorizesBeforeSameIdentityAcceptance(string refusal)
    {
        int deliveries = 0;
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => Task.FromResult(++deliveries == 1
                ? TenantCommandSubmissionResult.Ambiguous(messageId, "Tenants.RemoveMember.Unavailable.CommandSurface")
                : TenantCommandSubmissionResult.Accepted(messageId, "retry-correlation")),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        original.CorrelationId.ShouldBeNull();
        int authorityReads = 0;
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => ++authorityReads == 1
            ? Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)
            : refusal switch
            {
                "denied" => Task.FromResult(TenantLifecycleAuthorizationReflectionState.MissingPermission),
                "indeterminate" => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Indeterminate),
                "faulted" => Task.FromException<TenantLifecycleAuthorizationReflectionState>(new InvalidOperationException("Private authority failure")),
                _ => Task.FromCanceled<TenantLifecycleAuthorizationReflectionState>(new CancellationToken(canceled: true)),
            }));
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        cut.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(localizer[refusal is "denied"
            ? "Tenants.RemoveMember.Unavailable.Authorization" : "Tenants.Mutation.Unavailable.AuthorityIndeterminate"].Value);
        cut.Markup.ShouldNotContain("Private authority failure");
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
        {
            authorityReads++;
            return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        }));
        await cut.Find("form").SubmitAsync();
        authorityReads.ShouldBe(4);
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberRequest.ShouldBe(original.Intent);
        gateway.LastRemoveMemberMessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        cut.Instance.Snapshot.CorrelationId.ShouldBe("retry-correlation");
        gateway.StatusCallCount.ShouldBe(1);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    /// <summary>Suspended ambiguous recovery can proceed through status and projection without inventing audit availability.</summary>
    [Fact]
    public async Task SuspendedAmbiguousRemovalRetryConfirmsOnlyAfterMatchingProjection()
    {
        int deliveries = 0;
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        bool completed = false;
        bool removed = false;
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => Task.FromResult(++deliveries == 1
                ? TenantCommandSubmissionResult.Ambiguous(messageId, "Tenants.RemoveMember.Unavailable.CommandSurface")
                : TenantCommandSubmissionResult.Accepted(messageId, "retry-correlation")),
            StatusAsync = (_, _) => Task.FromResult(completed
                ? new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }
                : new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int authorityReads = 0;
        cut.Render(p => p
            .Add(x => x.AuthorizationReflectionProvider, _ => ++authorityReads == 2
                ? authority.Task : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.ProjectionEvidenceProvider, request => Task.FromResult(removed
                ? Proof(Detail(request.TenantId, [new TenantMember("owner-user", TenantRole.TenantOwner)]), "tenant-sequence:2")
                : Proof(Detail(request.TenantId), "tenant-sequence:1"))));
        Task retry = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => authorityReads.ShouldBe(2));
        gateway.RemoveMemberCallCount.ShouldBe(1);
        authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberRequest.ShouldBe(original.Intent);
        gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        completed = true;
        await cut.Find("[data-testid='tenants-remove-member-refresh']").ClickAsync(new MouseEventArgs());
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        removed = true;
        await cut.Find("[data-testid='tenants-remove-member-refresh']").ClickAsync(new MouseEventArgs());
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.AuditState.ShouldNotBe(TenantCommandAuditState.AuditAvailable);
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.StatusCallCount.ShouldBe(3);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>Late ambiguous retry authority cannot alter replacement authority, targets, or renderers.</summary>
    /// <param name="replacement">The authority or renderer context to replace.</param>
    /// <param name="queueSuccessfulAuthority">Whether authority succeeds before replacement.</param>
    [Theory]
    [InlineData("provider", false)]
    [InlineData("provider", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("tenant", false)]
    [InlineData("tenant", true)]
    [InlineData("member", false)]
    [InlineData("member", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    public async Task AmbiguousRemovalRetryPreservesReplacementContext(string replacement, bool queueSuccessfulAuthority)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Ambiguous("01ARZ3NDEKTSV4RRFFQ69G5FAV", "Tenants.RemoveMember.Unavailable.CommandSurface"),
        };
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int authorityReads = 0;
        Func<CancellationToken, Task<TenantLifecycleAuthorizationReflectionState>> provider = _ => ++authorityReads == 2
            ? authority.Task : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, provider));
        Task retry = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => authorityReads.ShouldBe(2));
        IRenderedComponent<RemoveTenantMemberFlow> current = cut;
        TenantRemoveMemberCommandSnapshot? replacementSnapshot = null;
        using var dispatcherEntered = new ManualResetEventSlim();
        using var releaseDispatcher = new ManualResetEventSlim();
        Task blocker = Task.Run(() => Renderer.Dispatcher.InvokeAsync(() =>
        {
            dispatcherEntered.Set();
            releaseDispatcher.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            switch (replacement)
            {
                case "provider":
                    cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
                    cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, provider));
                    break;
                case "version":
                    cut.Render(p => p.Add(x => x.AuthorizationVersion, 1));
                    cut.Render(p => p.Add(x => x.AuthorizationVersion, 0));
                    break;
                case "tenant":
                    cut.Render(p => p.Add(x => x.Detail, Detail("tenant.beta")));
                    break;
                case "member":
                    cut.Render(p => p.Add(x => x.Member, new TenantMember("second-owner", TenantRole.TenantOwner)));
                    break;
                default:
                    // Dispose the component before remounting; bUnit wrapper cleanup can be queued
                    // behind the same held renderer continuation this fixture is controlling.
                    ((IDisposable)cut.Instance).Dispose();
                    current = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
                    break;
            }
            replacementSnapshot = current.Instance.Snapshot;
        }));
        try
        {
            dispatcherEntered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            if (queueSuccessfulAuthority)
            {
                authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
                FieldInfo resolving = typeof(RemoveTenantMemberFlow).GetField("_authorityResolutionInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
                await WaitUntilAsync(() => !(bool)resolving.GetValue(cut.Instance)!, TimeSpan.FromSeconds(5));
                retry.IsCompleted.ShouldBeFalse();
            }
        }
        finally
        {
            releaseDispatcher.Set();
            try
            {
                await blocker.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                authority.TrySetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            }
        }
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(0);
        current.Instance.Snapshot.ShouldBe(replacementSnapshot.ShouldNotBeNull());
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        if (replacement is "tenant")
        {
            attempts.HasPendingOwnership("tenant.beta").ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeFalse();
        }
        if (replacement is "provider" or "version" or "dispose")
        {
            current.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
            await current.Find("form").SubmitAsync();
            gateway.RemoveMemberCallCount.ShouldBe(2);
            gateway.LastRemoveMemberRequest.ShouldBe(original.Intent);
            gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
            current.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        }
    }

    /// <summary>Feedback reflects safety changes that occur while undispatched cleanup is held.</summary>
    /// <param name="cultureName">The resource culture to render.</param>
    /// <param name="blocker">The blocker that supersedes the original stale-read refusal.</param>
    [Theory]
    [InlineData("en-US", "denied")]
    [InlineData("fr-FR", "denied")]
    [InlineData("en-US", "confirmation")]
    [InlineData("fr-FR", "confirmation")]
    [InlineData("en-US", "restored")]
    [InlineData("fr-FR", "restored")]
    public async Task HeldRemovalCleanupPublishesCurrentRefusal(string cultureName, string blocker)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                return acquired.Task;
            }
            releaseEntered.TrySetResult();
            return released.Task;
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue());
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (blocker is "denied")
        {
            cut.Render(p => p.Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.MissingPermission));
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
            if (blocker is "confirmation")
            {
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
            }
        }
        released.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        string explanation = blocker switch
        {
            "denied" => localizer["Tenants.RemoveMember.Unavailable.Authorization"].Value,
            "confirmation" => localizer["Tenants.RemoveMember.Validation.ConfirmationRequired", "reader-user"].Value,
            _ => localizer["Tenants.RemoveMember.Admission.Changed"].Value,
        };
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>A faulted release preserves an undispatched refusal and requires fresh activity admission.</summary>
    /// <param name="changeConfirmation">Whether confirmation changes instead of freshness.</param>
    /// <param name="useActivityCallback">Whether activity uses the event callback rather than the lease delegate.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UndispatchedReleaseFaultAllowsSafeResubmission(bool changeConfirmation, bool useActivityCallback)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => delivery.Task,
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
            {
                CommittedEventSequence = 2,
            },
        };
        ITenantQueryGateway queries = CreateCapableQueryGateway();
        RegisterServices(gateway, queries);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        Task<bool> ActivityAsync(bool active)
            => active ? ++acquisitions == 1 ? acquired.Task : Task.FromResult(true)
                : ++releases == 1 ? Task.FromException<bool>(new InvalidOperationException("Release failed")) : Task.FromResult(true);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(ActivityAsync);
        if (useActivityCallback)
        {
            cut.Render(p => p
                .Add(x => x.CommandActivityLease, (Func<bool, Task<bool>>?)null)
                .Add(x => x.OnCommandActivityChanged, async active => { _ = await ActivityAsync(active); }));
        }
        TenantRemoveMemberCommandSnapshot preview = cut.Instance.Snapshot;
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        if (changeConfirmation)
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else
        {
            cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        }
        acquired.SetResult(true);
        await submission;
        releases.ShouldBe(1);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        queries.ReceivedCalls().ShouldBeEmpty();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.Intent.ShouldBe(preview.Intent);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Markup.ShouldNotContain("Release failed");

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        gateway.RemoveMemberCallCount.ShouldBe(0);
        Task resubmission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        acquisitions.ShouldBe(2);
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.alpha", "reader-user"));
        string messageId = gateway.LastRemoveMemberMessageId.ShouldNotBeNull();
        delivery.SetResult(TenantCommandSubmissionResult.Accepted(messageId, "correlation-1"));
        await resubmission;
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        releases.ShouldBe(1);
        TenantDetail after = Detail("tenant.alpha", [new("owner-user", TenantRole.TenantOwner), new("second-owner", TenantRole.TenantOwner)]);
        await cut.InvokeAsync(() => cut.Instance.ApplyProjectionEvidence(Proof(after, "tenant-sequence:2")));
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
            releases.ShouldBe(2);
        });
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>A failed release cannot let recovery skip a refused or faulted activity reacquisition.</summary>
    /// <param name="throwOnReacquisition">Whether the second activity acquisition faults instead of refusing.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseFaultRecoveryRequiresSuccessfulReacquisition(bool throwOnReacquisition)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (!active)
            {
                return ++releases == 1 ? Task.FromException<bool>(new InvalidOperationException("Release failed")) : Task.FromResult(true);
            }
            return ++acquisitions switch
            {
                1 => acquired.Task,
                2 => throwOnReacquisition ? Task.FromException<bool>(new InvalidOperationException("Admission failed")) : Task.FromResult(false),
                _ => Task.FromResult(true),
            };
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await submission;
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(3);
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>Restored safety cannot start another submission while the previous release is still pending.</summary>
    /// <param name="faultRelease">Whether the pending release completes with a fault.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSafetyReleasePreventsReentrantSubmissionUntilItSettles(bool faultRelease)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                return ++acquisitions == 1 ? acquired.Task : Task.FromResult(true);
            }
            if (++releases == 1)
            {
                releaseStarted.SetResult(true);
                return released.Task;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        acquisitions.ShouldBe(1);
        if (faultRelease)
        {
            released.SetException(new InvalidOperationException("Release failed"));
        }
        else
        {
            released.SetResult(true);
        }
        await submission;
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>A late old release fault cannot overwrite a newly admitted tenant command.</summary>
    /// <param name="faultRelease">Whether the old release completes with a fault.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateSafetyReleasePreservesReplacementTenantOwnership(bool faultRelease)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new() { RemoveMemberAsync = _ => delivery.Task };
        RegisterServices(gateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                return ++acquisitions == 1 ? acquired.Task : Task.FromResult(true);
            }
            if (++releases == 1)
            {
                releaseStarted.SetResult(true);
                return released.Task;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task oldSubmission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(p => p
            .Add(x => x.Detail, Detail("tenant.beta"))
            .Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task replacementSubmission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.beta", "reader-user"));
        TenantRemoveMemberCommandSnapshot replacement = cut.Instance.Snapshot;
        if (faultRelease)
        {
            released.SetException(new InvalidOperationException("Old release failed"));
        }
        else
        {
            released.SetResult(true);
        }
        await oldSubmission;
        cut.Instance.Snapshot.ShouldBe(replacement);
        acquisitions.ShouldBe(2);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        attempts.HasPendingOwnership("tenant.beta").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeTrue();
        delivery.SetResult(TenantCommandSubmissionResult.Failed("Test delivery finished."));
        await replacementSubmission;
    }

    /// <summary>Disposal during release cannot let its late fault alter a new renderer's admission.</summary>
    [Fact]
    public async Task DisposedSafetyReleaseFaultPreservesReplacementRendererOwnership()
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new() { RemoveMemberAsync = _ => delivery.Task };
        RegisterServices(gateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<RemoveTenantMemberFlow> old = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                return acquired.Task;
            }
            releaseStarted.SetResult(true);
            return released.Task;
        });
        old.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task oldSubmission = old.Find("form").SubmitAsync();
        old.WaitForAssertion(() => attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue());
        old.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ((IDisposable)old.Instance).Dispose();
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task newSubmission = replacement.Find("form").SubmitAsync();
        replacement.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
        TenantRemoveMemberCommandSnapshot snapshot = replacement.Instance.Snapshot;
        released.SetException(new InvalidOperationException("Disposed release failed"));
        await oldSubmission;
        replacement.Instance.Snapshot.ShouldBe(snapshot);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        delivery.SetResult(TenantCommandSubmissionResult.Failed("Test delivery finished."));
        await newSubmission;
    }

    /// <summary>Fault recovery checks current safety both before and after reacquiring activity.</summary>
    /// <param name="blocker">The safety requirement that remains unavailable after cleanup faults.</param>
    [Theory]
    [InlineData("authority-denied")]
    [InlineData("authority-unknown")]
    [InlineData("audit")]
    [InlineData("viewport")]
    public async Task ReleaseFaultRecoveryStillRequiresEveryCurrentSafetyGate(string blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reacquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active => !active
            ? ++releases == 1 ? Task.FromException<bool>(new InvalidOperationException("Release failed")) : Task.FromResult(true)
            : ++acquisitions switch { 1 => acquired.Task, 2 => reacquired.Task, _ => Task.FromResult(true) });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await submission;
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        int authorityReads = 0;
        if (blocker.StartsWith("authority-", StringComparison.Ordinal))
        {
            cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
            {
                authorityReads++;
                return Task.FromResult(blocker == "authority-denied"
                    ? TenantLifecycleAuthorizationReflectionState.MissingPermission
                    : TenantLifecycleAuthorizationReflectionState.Indeterminate);
            }));
        }
        else if (blocker == "audit")
        {
            cut.Render(p => p.Add(x => x.AuditProofCapabilityAvailable, false));
        }
        else
        {
            Services.GetRequiredService<TenantHighImpactViewportObservation>()
                .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        }
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(1);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        if (blocker.StartsWith("authority-", StringComparison.Ordinal))
        {
            authorityReads.ShouldBe(1);
        }
        cut.Render(p => p
            .Add(x => x.AuditProofCapabilityAvailable, true)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        Task retry = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(2));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        reacquired.SetResult(true);
        await retry;
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(3);
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>A cleanup fault during a failed-delivery retry retains the possibly delivered message identity.</summary>
    [Fact]
    public async Task RetrySafetyReleaseFaultPreservesOriginalDeliveryIdentity()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active => active
            ? ++acquisitions == 2 ? acquired.Task : Task.FromResult(true)
            : ++releases == 2 ? Task.FromException<bool>(new InvalidOperationException("Release failed")) : Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        Task retry = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(2));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await retry;
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        acquisitions.ShouldBe(3);
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
    }

    /// <summary>Freshness restoration cannot bypass confirmation, fresh authority, audit support or measured viewport.</summary>
    /// <param name="blocker">The safety gate that remains unavailable after freshness recovers.</param>
    [Theory]
    [InlineData("confirmation")]
    [InlineData("authority-denied")]
    [InlineData("authority-unknown")]
    [InlineData("audit")]
    [InlineData("viewport")]
    public async Task UndispatchedFreshnessRecoveryStillRequiresEveryCurrentSafetyGate(string blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                acquisitions++;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        int authorityReads = 0;
        if (blocker == "confirmation")
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed-user");
        }
        else if (blocker.StartsWith("authority-", StringComparison.Ordinal))
        {
            cut.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ =>
            {
                authorityReads++;
                return Task.FromResult(blocker == "authority-denied"
                    ? TenantLifecycleAuthorizationReflectionState.MissingPermission
                    : TenantLifecycleAuthorizationReflectionState.Indeterminate);
            }));
        }
        else if (blocker == "audit")
        {
            cut.Render(p => p.Add(x => x.AuditProofCapabilityAvailable, false));
        }
        else
        {
            Services.GetRequiredService<TenantHighImpactViewportObservation>()
                .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        }

        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        acquisitions.ShouldBe(0);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        if (blocker.StartsWith("authority-", StringComparison.Ordinal))
        {
            authorityReads.ShouldBe(1);
        }

        cut.Render(p => p
            .Add(x => x.AuditProofCapabilityAvailable, true)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        Services.GetRequiredService<TenantHighImpactViewportObservation>()
            .Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
    }

    /// <summary>A refused previously delivered failed attempt retains its exact identity after freshness recovers.</summary>
    [Fact]
    public async Task FailedDeliveryFreshnessRefusalPreservesSameIdentityRecovery()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
    }

    /// <summary>Unknown delivered-attempt state without tracking cannot be reset as a new preview.</summary>
    /// <param name="state">The uncertain lifecycle whose tracking is missing.</param>
    /// <param name="messageId">The remaining message identifier, if any.</param>
    /// <param name="correlationId">The remaining correlation identifier, if any.</param>
    [Theory]
    [InlineData(TenantCommandLifecycleState.UnableToVerify, null, null)]
    [InlineData(TenantCommandLifecycleState.UnableToVerify, "message-only", null)]
    [InlineData(TenantCommandLifecycleState.UnableToVerify, null, "correlation-only")]
    [InlineData(TenantCommandLifecycleState.Degraded, null, null)]
    [InlineData(TenantCommandLifecycleState.Degraded, "message-only", null)]
    [InlineData(TenantCommandLifecycleState.Degraded, null, "correlation-only")]
    public async Task UncertainStateWithoutTrackingRemainsFailClosed(
        TenantCommandLifecycleState state,
        string? messageId,
        string? correlationId)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        typeof(RemoveTenantMemberFlow).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, cut.Instance.Snapshot with { State = state, MessageId = messageId, CorrelationId = correlationId });

        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(state);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Members.Submit.TrackingLost");
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBe(correlationId);
    }

    /// <summary>Pre-dispatch refusal feedback announces the existing whole localized freshness explanation.</summary>
    /// <param name="cultureName">The shipped resource culture.</param>
    /// <param name="initiallyUnavailable">Whether no complete preview existed before the refusal.</param>
    [Theory]
    [InlineData("en-US", false)]
    [InlineData("fr-FR", false)]
    [InlineData("en-US", true)]
    [InlineData("fr-FR", true)]
    public async Task UndispatchedFreshnessRefusalAnnouncesLocalizedExplanation(string cultureName, bool initiallyUnavailable)
    {
        RegisterServices(new StubTenantCommandGateway());
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(cultureName)));
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(
            _ => Task.FromResult(true), initiallyUnavailable ? ReadModelFreshnessState.Stale : ReadModelFreshnessState.Current);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        string explanation = localizer["Tenants.RemoveMember.Unavailable.Freshness"].Value;
        explanation.ShouldNotBe("Tenants.RemoveMember.Unavailable.Freshness");
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim().ShouldBe(explanation);
        cut.Find("[data-testid='tenants-remove-member-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe(explanation);
        cut.Instance.Snapshot.State.ShouldBe(initiallyUnavailable ? TenantCommandLifecycleState.Idle : TenantCommandLifecycleState.Previewed);
        cut.FindAll("[data-testid='tenants-remove-member-preview-item']").ShouldBeEmpty();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.FindAll("[data-testid='tenants-remove-member-safe-message']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-remove-member-live-region']").TextContent.Trim()
            .ShouldBe(localizer["Tenants.RemoveMember.State.Previewed"].Value);
    }

    /// <summary>Changing a pre-dispatch identity discards old confirmation and rebuilds the matching preview.</summary>
    /// <param name="changeTenant">Whether the tenant changes instead of the target member.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndispatchedRecoveryRequiresConfirmationForTheCurrentIdentity(bool changeTenant)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        await cut.Find("form").SubmitAsync();
        string tenantId = changeTenant ? "tenant.beta" : "tenant.alpha";
        string userId = changeTenant ? "reader-user" : "second-reader";
        TenantDetail replacement = Detail(tenantId,
            [new("owner-user", TenantRole.TenantOwner), new(userId, TenantRole.TenantReader)]);
        cut.Render(p => p
            .Add(x => x.Detail, replacement)
            .Add(x => x.Member, new TenantMember(userId, TenantRole.TenantReader)));
        cut.Instance.Snapshot.Intent.ShouldBeNull();
        cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        RemoveUserFromTenant request = new(tenantId, userId);
        cut.Instance.Snapshot.Intent.ShouldBe(request);
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change(userId);
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.LastRemoveMemberRequest.ShouldBe(request);
        cut.Instance.Snapshot.Intent.ShouldBe(request);
    }

    /// <summary>Initially unavailable identity replacement clears confirmation even though no preview intent exists.</summary>
    /// <param name="changeTenant">Whether the tenant changes instead of the target member.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitiallyUnavailableIdentityReplacementRequiresNewTypedConfirmation(bool changeTenant)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(_ => Task.FromResult(true), ReadModelFreshnessState.Stale);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Instance.Snapshot.Intent.ShouldBeNull();
        string tenantId = changeTenant ? "tenant.beta" : "tenant.alpha";
        string userId = changeTenant ? "reader-user" : "second-reader";
        cut.Render(p => p
            .Add(x => x.Detail, Detail(tenantId, [new("owner-user", TenantRole.TenantOwner), new(userId, TenantRole.TenantReader)]))
            .Add(x => x.Member, new TenantMember(userId, TenantRole.TenantReader)));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change(userId);
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant(tenantId, userId));
        cut.Instance.Snapshot.Intent.ShouldBe(gateway.LastRemoveMemberRequest);
    }

    /// <summary>Replacing the tenant during held admission cannot reuse the old tenant's confirmation.</summary>
    [Fact]
    public async Task HeldAdmissionTenantReplacementRequiresNewTypedConfirmation()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
            !active || ++acquisitions > 1 ? Task.FromResult(true) : acquired.Task);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task oldSubmission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        cut.Render(p => p.Add(x => x.Detail, Detail("tenant.beta")));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Instance.Snapshot.Intent.ShouldBe(new RemoveUserFromTenant("tenant.beta", "reader-user"));
        acquired.SetResult(true);
        await oldSubmission;
        gateway.RemoveMemberCallCount.ShouldBe(0);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.LastRemoveMemberRequest.ShouldBe(new RemoveUserFromTenant("tenant.beta", "reader-user"));
        cut.Instance.Snapshot.Intent.ShouldBe(gateway.LastRemoveMemberRequest);
    }

    /// <summary>A gateway lost immediately before delivery leaves an undispatched, recoverable preview.</summary>
    [Fact]
    public async Task FirstGatewayDisappearanceBeforeDeliveryAllowsResubmissionAfterRestoration()
    {
        StubTenantCommandGateway gateway = new();
        ITenantQueryGateway queries = CreateCapableQueryGateway();
        RegisterServices(gateway, queries);
        Services.AddSingleton<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton<TenantCommandAttemptTracker>();
        bool disappear = false;
        int resolutions = 0;
        Services.AddTransient<ITenantCommandGateway>(_ => disappear && ++resolutions > 1 ? null! : gateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker attempts = Services.GetRequiredService<TenantCommandAttemptTracker>();
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active && ++acquisitions == 1)
            {
                disappear = true;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(0);
        gateway.StatusCallCount.ShouldBe(0);
        queries.ReceivedCalls().ShouldBeEmpty();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        attempts.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Find("[data-testid='tenants-remove-member-safe-message']").TextContent.ShouldBe("Tenant command support is unavailable.");

        disappear = false;
        cut.Render();
        cut.FindAll("[data-testid='tenants-remove-member-safe-message']").ShouldBeEmpty();
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        acquisitions.ShouldBe(2);
    }

    /// <summary>Abandoned retries keep the delivered identity independently of the renderer and its admission.</summary>
    /// <param name="refusal">The stage that abandons the undispatched retry.</param>
    /// <param name="faultRelease">Whether safety cleanup faults.</param>
    [Theory]
    [InlineData("freshness", false)]
    [InlineData("freshness", true)]
    [InlineData("confirmation", false)]
    [InlineData("confirmation", true)]
    [InlineData("acquisition-refused", false)]
    [InlineData("acquisition-fault", false)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    public async Task AbandonedRemovalRetryRetainsIdentityAcrossRendererReplacement(string refusal, bool faultRelease)
    {
        var delivered = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int deliveries = 0;
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => ++deliveries == 1
                ? Task.FromResult(TenantCommandSubmissionResult.Failed("Delivery outcome needs recovery.") with
                {
                    MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                }) : delivered.Task,
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)
            {
                CommittedEventSequence = 2,
            },
        };
        RegisterServices(gateway, CreateCapableQueryGateway());
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                if (++acquisitions != 2)
                {
                    return Task.FromResult(true);
                }
                return refusal switch
                {
                    "acquisition-refused" => Task.FromResult(false),
                    "acquisition-fault" => Task.FromException<bool>(new InvalidOperationException("Admission failed")),
                    _ => acquired.Task,
                };
            }
            if (++releases == 2 && refusal is not "acquisition-refused" and not "acquisition-fault")
            {
                releaseEntered.TrySetResult();
                return released.Task;
            }
            return Task.FromResult(true);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.Failed);
        Task retry = cut.Find("form").SubmitAsync();
        if (refusal is not "acquisition-refused" and not "acquisition-fault")
        {
            cut.WaitForAssertion(() => acquisitions.ShouldBe(2));
            if (refusal is "confirmation")
            {
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("changed");
            }
            else
            {
                cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
            }
            acquired.SetResult(true);
            await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (refusal is "dispose")
            {
                cut.Dispose();
            }
            if (faultRelease)
            {
                released.SetException(new InvalidOperationException("Release failed"));
            }
            else
            {
                released.SetResult(true);
            }
        }
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        if (refusal is not "dispose")
        {
            cut.Dispose();
        }
        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(0);
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        tracker.Find<TenantRemoveMemberCommandSnapshot>("tenant.alpha").ShouldBe(original);

        int freshAdmissions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                freshAdmissions++;
            }
            return Task.FromResult(true);
        });
        replacement.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        replacement.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        replacement.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        replacement.Instance.Snapshot.BaselineProjectionVersion.ShouldBe(original.BaselineProjectionVersion);
        await replacement.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        freshAdmissions.ShouldBe(0);
        replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task resubmission = replacement.Find("form").SubmitAsync();
        replacement.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(2));
        freshAdmissions.ShouldBe(1);
        gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        delivered.SetResult(TenantCommandSubmissionResult.Accepted(original.MessageId!, "correlation-1"));
        await resubmission;
        replacement.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        TenantDetail after = Detail("tenant.alpha", [new("owner-user", TenantRole.TenantOwner), new("second-owner", TenantRole.TenantOwner)]);
        await replacement.InvokeAsync(() => replacement.Instance.ApplyProjectionEvidence(Proof(after, "tenant-sequence:2")));
        replacement.WaitForAssertion(() =>
        {
            replacement.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        });
        replacement.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        gateway.RemoveMemberCallCount.ShouldBe(2);
    }

    /// <summary>Blank correlation variants use the same retained retry contract as missing correlation.</summary>
    /// <param name="correlationId">The missing or blank gateway correlation value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RefusedRemovalRetryWithBlankCorrelationRetainsOriginalIdentity(string? correlationId)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Recover delivery") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                CorrelationId = correlationId,
            },
        };
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderLeaseRecoveryFlow(active => Task.FromResult(!active || ++acquisitions != 2));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        await cut.Find("form").SubmitAsync();
        cut.Dispose();
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        replacement.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        replacement.Instance.Snapshot.CorrelationId.ShouldBe(correlationId);
        replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await replacement.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(2);
        gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
    }

    /// <summary>Old callback completion cannot remove a replacement retry's exact admission or evidence.</summary>
    /// <param name="stage">Whether acquisition or release remains pending during replacement.</param>
    /// <param name="faultCallback">Whether the old callback faults.</param>
    [Theory]
    [InlineData("acquisition", false)]
    [InlineData("acquisition", true)]
    [InlineData("release", false)]
    [InlineData("release", true)]
    public async Task PendingRetryCallbackCannotOverwriteReplacementOwnership(string stage, bool faultCallback)
    {
        var delivered = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int deliveries = 0;
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => ++deliveries == 1
                ? Task.FromResult(TenantCommandSubmissionResult.Failed("Recover delivery") with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV" })
                : delivered.Task,
        };
        RegisterServices(gateway);
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<RemoveTenantMemberFlow> old = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                return ++acquisitions == 2 ? acquired.Task : Task.FromResult(true);
            }
            if (++releases == 2)
            {
                releaseEntered.TrySetResult();
                return released.Task;
            }
            return Task.FromResult(true);
        });
        old.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await old.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = old.Instance.Snapshot;
        Task oldRetry = old.Find("form").SubmitAsync();
        old.WaitForAssertion(() => acquisitions.ShouldBe(2));
        if (stage is "release")
        {
            old.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
            acquired.SetResult(true);
            await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        ((IDisposable)old.Instance).Dispose();
        tracker.Find<TenantRemoveMemberCommandSnapshot>("tenant.alpha").ShouldBe(original);
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderLeaseRecoveryFlow(_ => Task.FromResult(true));
        replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        Task replacementRetry = replacement.Find("form").SubmitAsync();
        replacement.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(2));
        TenantRemoveMemberCommandSnapshot expected = replacement.Instance.Snapshot;
        TenantRetainedCommandAttempt attempt = (TenantRetainedCommandAttempt)typeof(RemoveTenantMemberFlow)
            .GetField("_retainedAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(replacement.Instance)!;
        TaskCompletionSource<bool> pending = stage is "release" ? released : acquired;
        if (faultCallback)
        {
            pending.SetException(new InvalidOperationException("Old callback failed"));
        }
        else
        {
            pending.SetResult(true);
        }
        await oldRetry.WaitAsync(TimeSpan.FromSeconds(5));
        replacement.Instance.Snapshot.ShouldBe(expected);
        tracker.IsAttached(attempt, replacement.Instance).ShouldBeTrue();
        tracker.Find<TenantRemoveMemberCommandSnapshot>("tenant.alpha").ShouldBe(expected);
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        gateway.LastRemoveMemberMessageId.ShouldBe(original.MessageId);
        delivered.SetResult(TenantCommandSubmissionResult.Accepted(original.MessageId!, "replacement-correlation"));
        await replacementRetry.WaitAsync(TimeSpan.FromSeconds(5));
        replacement.Instance.Snapshot.CorrelationId.ShouldBe("replacement-correlation");
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    /// <summary>Restoring a failed identity cannot bypass the replacement's current safety gates.</summary>
    /// <param name="blocker">The current condition that refuses removal.</param>
    [Theory]
    [InlineData("denied")]
    [InlineData("unknown")]
    [InlineData("stale")]
    [InlineData("viewport")]
    [InlineData("unknown-role")]
    [InlineData("disabled")]
    [InlineData("audit")]
    public async Task RestoredFailedRemovalStillRequiresCurrentSafetyEvidence(string blocker)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Recover delivery") with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV" },
        };
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> old = RenderLeaseRecoveryFlow(active => Task.FromResult(!active || ++acquisitions != 2));
        old.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await old.Find("form").SubmitAsync();
        TenantRemoveMemberCommandSnapshot original = old.Instance.Snapshot;
        await old.Find("form").SubmitAsync();
        old.Dispose();
        int freshAdmissions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderLeaseRecoveryFlow(active =>
        {
            if (active)
            {
                freshAdmissions++;
            }
            return Task.FromResult(true);
        });
        if (blocker is "denied" or "unknown")
        {
            TenantLifecycleAuthorizationReflectionState authority = blocker is "denied"
                ? TenantLifecycleAuthorizationReflectionState.MissingPermission : TenantLifecycleAuthorizationReflectionState.Indeterminate;
            replacement.Render(p => p.Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(authority)));
        }
        else if (blocker is "stale")
        {
            replacement.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        }
        else if (blocker is "viewport")
        {
            Services.GetRequiredService<TenantHighImpactViewportObservation>().Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        }
        else if (blocker is "unknown-role")
        {
            replacement.Render(p => p.Add(x => x.Member, new TenantMember("reader-user", TenantRole.Unknown)));
        }
        else if (blocker is "disabled")
        {
            replacement.Render(p => p.Add(x => x.Detail, Detail("tenant.alpha") with { Status = TenantStatus.Disabled }));
        }
        else
        {
            replacement.Render(p => p.Add(x => x.AuditProofCapabilityAvailable, false));
        }
        replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await replacement.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        freshAdmissions.ShouldBe(0);
        replacement.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        replacement.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        replacement.Dispose();
        Services.GetRequiredService<TenantCommandAttemptTracker>().Find<TenantRemoveMemberCommandSnapshot>("tenant.alpha")!.MessageId.ShouldBe(original.MessageId);
    }

    private IRenderedComponent<RemoveTenantMemberFlow> RenderLeaseRecoveryFlow(
        Func<bool, Task<bool>> lease,
        ReadModelFreshnessState freshness = ReadModelFreshnessState.Current)
        => Render<RemoveTenantMemberFlow>(p => p
            .Add(x => x.IsAuthorized, true)
            .Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.AuditProofCapabilityAvailable, true)
            .Add(x => x.Detail, Detail("tenant.alpha"))
            .Add(x => x.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, freshness)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1")
            .Add(x => x.CommandActivityLease, lease));

    /// <summary>
    /// Verifies that an activity-lease refusal reports a retry, whose identity may already have reached the server,
    /// as unverifiable, and a first attempt, which sent nothing, as not started.
    /// </summary>
    /// <param name="isRetry">Whether the refused submission retries an ambiguous attempt.</param>
    /// <param name="expectedAuditState">The audit state the refusal must report.</param>
    [Theory]
    [InlineData(true, TenantCommandAuditState.AuditUnavailable)]
    [InlineData(false, TenantCommandAuditState.NotStarted)]
    public void ActivityLeaseRefusalReportsARetryAsUnverifiableAndAFirstAttemptAsNotStarted(
        bool isRetry,
        TenantCommandAuditState expectedAuditState)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Submission outcome is ambiguous.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active => Task.FromResult(!active || (isRetry && ++acquisitions != 2))));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        if (isRetry)
        {
            cut.Find("form").Submit();
            cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        }

        // The retry reuses an identity that may already have reached the server; the refused lease blocks it
        // before dispatch, and the audit dimension reports the unknown status instead of "not started". A first
        // attempt refused before dispatch sent nothing, so no audit state is implied.
        cut.Find("form").Submit();

        TenantCommandLifecycleState expectedState = isRetry
            ? TenantCommandLifecycleState.Failed
            : TenantCommandLifecycleState.Previewed;
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(expectedState);
            cut.Instance.Snapshot.SafeMessage.ShouldBe("A tenant command is already in progress.");
        });
        cut.Instance.Snapshot.MessageId.ShouldBe(isRetry ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : null);
        cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        cut.FindAll("[data-testid='tenants-remove-member-audit'] [data-testid='tenants-audit-availability']").Count
            .ShouldBe(isRetry ? 1 : 0);
        gateway.RemoveMemberCallCount.ShouldBe(isRetry ? 1 : 0);

        if (isRetry)
        {
            // Once the lease is granted again, the next submit re-dispatches the same identity instead of minting one.
            cut.Find("form").Submit();
            cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(2));
            gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        }
    }

    [Fact]
    public void Retry_whose_command_gateway_disappears_after_the_lease_reports_the_possibly_delivered_attempt_as_unverifiable()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Submission outcome is ambiguous.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        bool gatewayAvailable = true;
        Services.AddTransient<ITenantCommandGateway>(_ => gatewayAvailable ? gateway : null!);
        int acquisitions = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active =>
            {
                // The retry takes the lease, and the command surface disappears before dispatch.
                if (active && ++acquisitions == 2)
                {
                    gatewayAvailable = false;
                }

                return Task.FromResult(true);
            }));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));

        cut.Find("form").Submit();

        // The blocked retry keeps the failed attempt it retried: that identity may already have reached the
        // server, so its status is unknown and it stays the identity the next submit reuses.
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
            cut.Instance.Snapshot.SafeMessage.ShouldBe("Tenant command support is unavailable.");
        });
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        gateway.RemoveMemberCallCount.ShouldBe(1);

        gatewayAvailable = true;
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(2));
        gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
    }

    /// <summary>
    /// Verifies that audit refresh requires a retained message and correlation identity.
    /// </summary>
    /// <param name="requeryable">Whether the submission supplies enough identity to read status again.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RefreshIsOfferedOnlyWhileTheAttemptCanBeRequeried(bool requeryable)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = requeryable
                ? TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")
                : TenantCommandSubmissionResult.Failed("Submission failed before it could be verified.") with
                {
                    MessageId = "message-1",
                },
            Status = new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(requeryable
            ? TenantCommandAuditState.AuditPending
            : TenantCommandAuditState.AuditUnavailable));
        cut.WaitForAssertion(
            () => cut.FindAll("[data-testid='tenants-remove-member-audit'] [data-recovery-verb='refresh']").Count.ShouldBe(requeryable ? 1 : 0),
            TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-remove-member-audit'] [data-testid='tenants-audit-availability']");
    }

    [Fact]
    public void Audit_refresh_that_resolves_to_not_started_moves_focus_to_the_lifecycle_section()
    {
        int statusCalls = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            // The first lookup cannot be read; the user's Refresh then finds the command still processing.
            StatusAsync = (_, _) => Task.FromResult(++statusCalls == 1
                ? TenantCommandStatusResult.Unknown("Command status is unavailable.")
                : new TenantCommandStatusResult(CommandStatus.Processing, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));
        string lifecycleReferenceId = ((ElementReference)typeof(RemoveTenantMemberFlow)
            .GetField("_lifecycleElement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Id;
        int focusCallsBeforeRefresh = FocusCalls().Count;

        cut.Find("[data-testid='tenants-remove-member-audit'] [data-recovery-verb='refresh']").Click();

        // The polite NotStarted state unmounts the control and its focused Refresh button, so focus lands on the
        // lifecycle section instead of falling back to the document body.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted));
        cut.Instance.Snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Polite);
        cut.FindAll("[data-testid='tenants-remove-member-audit'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
        cut.WaitForAssertion(() => FocusCalls().Count.ShouldBeGreaterThan(focusCallsBeforeRefresh));
        FocusCalls().Last().ShouldBe(lifecycleReferenceId);
    }

    [Fact]
    public void Retry_failed_before_dispatch_keeps_the_reused_identity_unverifiable()
    {
        int submissions = 0;
        StubTenantCommandGateway gateway = new()
        {
            // The first attempt failed after the message was sent; the retry of that identity then fails before
            // dispatch and reports no message id of its own.
            RemoveMemberAsync = _ => Task.FromResult(++submissions == 1
                ? TenantCommandSubmissionResult.Failed("Submission outcome is ambiguous.") with
                {
                    MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                }
                : TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference")),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));

        cut.Find("form").Submit();

        // The retry sent nothing, but the earlier attempt with the same identity may have reached the server: the
        // audit dimension stays unknown instead of disappearing as "not started".
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(2));
        gateway.LastRemoveMemberMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.Find("[data-testid='tenants-remove-member-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("unavailable");
    }

    private List<string> FocusCalls()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Select(reference => reference.Id ?? string.Empty)];

    [Fact]
    public void Programmatic_submit_while_unable_to_verify_recovers_status_without_dispatching()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.TimedOut, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        int statusCalls = gateway.StatusCallCount;

        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(statusCalls + 1);
    }

    [Fact]
    public void Continue_read_only_releases_activity_and_closes_the_remove_flow()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.TimedOut, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        List<bool> activity = [];
        int closeCount = 0;
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active =>
            {
                activity.Add(active);
                return Task.FromResult(true);
            })
            .Add(p => p.OnCloseRequested, () => closeCount++));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));

        cut.Find("[data-testid='tenants-remove-member-continue-read-only']").Click();

        activity.ShouldBe([true, false]);
        closeCount.ShouldBe(1);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
    }

    [Fact]
    public async Task Continue_read_only_does_not_reconstruct_the_preview_from_a_parameter_rerender_mid_dismissAsync()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.TimedOut, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantDetail detail = Detail("tenant.alpha");
        TenantMember member = new("reader-user", TenantRole.TenantReader);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.Member, member)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active => active
                ? Task.FromResult(true)
                : release.Task));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));

        // Continue-read-only sets _dismissed and moves the snapshot to Idle before awaiting the (here,
        // still-pending) lease release -- reproducing the yield window the parent-render race regression
        // needs. Drive the click on a background task so this thread can interleave a parameter re-render
        // while that await is suspended.
        Task clickTask = Task.Run(() => cut.Find("[data-testid='tenants-remove-member-continue-read-only']").Click());
        cut.WaitForState(() => cut.Instance.Snapshot.State is TenantCommandLifecycleState.Idle);

        // Same parameters (Member/Detail unchanged, preview inputs still complete) -- the kind of re-render
        // that would fire mid-await from a parent StateHasChanged during lease release/acquisition.
        cut.Render(parameters => parameters
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.Member, member)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active => active
                ? Task.FromResult(true)
                : release.Task));

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);

        release.SetResult(true);
        await clickTask;

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
    }

    [Theory]
    [InlineData(TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale, "Refresh current tenant detail")]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Unknown, "Refresh current tenant detail")]
    [InlineData(TenantDetailSurfaceKind.Degraded, ReadModelFreshnessState.Current, "Refresh current tenant detail")]
    [InlineData(TenantDetailSurfaceKind.Unavailable, ReadModelFreshnessState.Current, "Refresh current tenant detail")]
    [InlineData(TenantDetailSurfaceKind.Unknown, ReadModelFreshnessState.Current, "Refresh current tenant detail")]
    public void Remove_flow_fails_closed_without_partial_preview_when_context_is_unavailable(
        TenantDetailSurfaceKind surfaceKind,
        ReadModelFreshnessState freshness,
        string expectedReason)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, surfaceKind)
            .Add(p => p.Freshness, freshness));

        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        string reason = cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent;
        reason.ShouldContain(expectedReason, Case.Insensitive);
        reason.ShouldNotContain("not authorized", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-remove-member-preview-item']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
        cut.Instance.Snapshot.IsPreviewComplete.ShouldBeFalse();
        cut.Render(p => p
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current));
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));
    }

    [Fact]
    public void Remove_member_true_authorization_failure_still_renders_permission_reason()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsAuthorized, false)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.MissingPermission));

        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent
            .ShouldContain("not authorized", Case.Insensitive);
    }

    [Fact]
    public void Cancel_and_escape_request_close_without_submitting()
    {
        int closeCount = 0;
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-remove-member-cancel']").Click();
        cut.Find("[data-testid='tenants-remove-member-flow']").KeyDown("Escape");

        closeCount.ShouldBe(2);
        gateway.RemoveMemberCallCount.ShouldBe(0);
    }

    [Fact]
    public void Escape_while_submitting_does_not_close_or_dispatch_again()
    {
        int closeCount = 0;
        TaskCompletionSource<TenantCommandSubmissionResult> pendingSubmission = new();
        StubTenantCommandGateway gateway = new()
        {
            RemoveMemberAsync = _ => pendingSubmission.Task,
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));

        cut.Find("[data-testid='tenants-remove-member-flow']").KeyDown("Escape");
        cut.Find("[data-testid='tenants-remove-member-cancel']").GetAttribute("disabled").ShouldNotBeNull();

        closeCount.ShouldBe(0);
        gateway.RemoveMemberCallCount.ShouldBe(1);

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Failed("Command submission cancelled by the test."));
    }

    [Fact]
    public void Missing_audit_proof_capability_fail_closes_before_dispatch()
    {
        RegisterServices(new StubTenantCommandGateway(), new UnavailableTenantQueryGateway());

        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent
            .ShouldContain("missing audit proof");
        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldNotBeNull();
    }

    [Fact]
    public void Confirmed_removal_with_matching_audit_row_renders_wp2a_receipt()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };

        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                DateTimeOffset from = request.From ?? DateTimeOffset.UtcNow.AddMinutes(-1);
                TenantAuditRow match = new(
                    "evt-remove-1",
                    "UserRemovedFromTenant",
                    AuditEventCategory.Access,
                    "actor-1",
                    from.AddSeconds(30),
                    request.TenantId,
                    "reader-user",
                    request.TenantId,
                    "UserRemovedFromTenant",
                    "userId: reader-user",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    QueryResponseProvenance.ProjectionBacked,
                    new TenantAuditNarrative(UserId: "reader-user")) { CommandCorrelationId = "correlation-1", AggregateSequence = 2 };
                return TenantAuditSnapshot.Ready(
                    [match],
                    nextCursor: null,
                    hasMore: false,
                    eTag: "etag-1",
                    ReadModelFreshnessState.Current,
                    request) with
                {
                    Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "opaque-audit-snapshot",
            CallerScope = "test-caller",
                };
            });
        RegisterServices(gateway, queryGateway);
        BunitJSModuleInterop clipboard = JSInterop.SetupModule("./js/tenantsClipboard.js");

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        });
        cut.Find("[data-testid='tenants-audit-receipt']");
        cut.FindComponent<AuditEvidenceReceipt>().Instance.Receipt!.CommandReference.ShouldBeNull();
        cut.Find("[data-testid='tenants-audit-availability']").GetAttribute("data-state").ShouldBe("available");

        cut.Find("[data-surface-testid='tenants-audit-receipt-copy']").Click();
        cut.WaitForAssertion(() => clipboard.Invocations.Count(invocation => invocation.Identifier is "writeText")
            .ShouldBe(1));
        string copied = clipboard.Invocations.Single(invocation => invocation.Identifier is "writeText")
            .Arguments[0]!.ToString()!;
        copied.ShouldContain("Actor: actor-1");
        copied.ShouldContain("Target: reader-user");
        copied.ShouldContain("Outcome: User removed from tenant");
        copied.ShouldContain("Audit reference: evt-remove-1");
        cut.Find("[data-testid='tenants-audit-receipt-close']").TextContent.Trim().ShouldBe("Close receipt");

        // The receipt's recovery action shares the Close callback: it dismisses in place and never navigates.
        cut.Find("[data-testid='tenants-audit-receipt'] .audit-evidence-receipt__action").Click();
        Uri auditUri = new(Services.GetRequiredService<NavigationManager>().Uri);
        auditUri.AbsolutePath.ShouldBe("/");
        cut.Find("[data-testid='tenants-command-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("message-1");
    }

    [Fact]
    public void Confirmed_removal_follows_opaque_audit_pages_before_promoting_ready_proof()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        List<TenantAuditRequest> requests = [];
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                requests.Add(request);
                if (request.Cursor is null)
                {
                    return TenantAuditSnapshot.Ready(
                        [],
                        nextCursor: "opaque-page-2",
                        hasMore: true,
                        eTag: "etag-1",
                        ReadModelFreshnessState.Current,
                        request) with
                    {
                        Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "opaque-audit-snapshot",
            CallerScope = "test-caller",
                    };
                }

                request.Cursor.ShouldBe("opaque-page-2");
                return CurrentAuditPage([StrongRemovalRow(request)], request, "etag-2");
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-page-2"]);
        cut.Find("[data-testid='tenants-audit-receipt']");
    }

    [Fact]
    public void ConcurrentEarlierRemovalDoesNotHideExactAttemptProof()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        List<TenantAuditRequest> requests = [];
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                requests.Add(request);
                if (request.Cursor is null)
                {
                    TenantAuditRow weak = StrongRemovalRow(request) with
                    {
                        CommandCorrelationId = "another-operator-command",
                    };
                    return CurrentAuditPage([weak], request, "etag-weak") with
                    {
                        NextCursor = "strong-page",
                        HasMore = true,
                    };
                }

                request.Cursor.ShouldBe("strong-page");
                return CurrentAuditPage([StrongRemovalRow(request)], request, "etag-strong");
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        requests.Select(static request => request.Cursor).ShouldBe([null, "strong-page"]);
        cut.Find("[data-testid='tenants-audit-receipt']");
    }

    [Fact]
    public void Matching_row_on_stale_audit_page_stays_delayed_without_receipt()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                return TenantAuditSnapshot.Stale(
                    [StrongRemovalRow(request)],
                    nextCursor: null,
                    hasMore: false,
                    eTag: "etag-stale",
                    request) with
                {
                    Lifecycle = ProjectionLifecycleState.Stale,
                    ProjectionVersion = "audit-v1",
                };
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed));
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public void Audit_proof_walk_stops_at_page_cap_and_fails_closed()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        int calls = 0;
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                string nextCursor = $"audit-page-{++calls}";
                return CurrentAuditPage([], request, $"etag-{calls}") with
                {
                    NextCursor = nextCursor,
                    HasMore = true,
                };
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() =>
        {
            calls.ShouldBe(CursorHistory.DefaultMaximum);
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        });
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Audit_proof_walk_is_cancelled_when_flow_is_disposed_and_never_promotes_proof()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        var queryStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                CancellationToken token = call.ArgAt<CancellationToken>(2);
                queryStarted.TrySetResult(token);
                return pending.Task;
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);
        CancellationToken cancellationToken = await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cut.Instance.Dispose();

        await WaitUntilAsync(() => cancellationToken.IsCancellationRequested, TimeSpan.FromSeconds(2));
        cancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Projection_refresh_coalesced_during_status_only_refresh_is_not_dropped()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        int projectionRefreshCount = 0;
        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.OnProjectionRefreshRequested, () =>
            {
                liveProjectionVersion = "tenant-sequence:2";
                projectionRefreshCount++;
            })
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId), liveProjectionVersion))));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        projectionRefreshCount = 0;

        var statusStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusReads = 0;
        gateway.StatusAsync = (_, _) =>
        {
            if (Interlocked.Increment(ref statusReads) == 1)
            {
                statusStarted.TrySetResult();
                return releaseStatus.Task;
            }

            return Task.FromResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        };

        Task statusOnlyRefresh = cut.InvokeAsync(() => cut.Instance.HandleAuthoritativeRefreshNudgeAsync());
        await statusStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.Find("[data-testid='tenants-remove-member-refresh']").Click();
        releaseStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        await statusOnlyRefresh.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() => projectionRefreshCount.ShouldBeGreaterThan(0));
        statusReads.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown, QueryResponseProvenance.ProjectionBacked)]
    [InlineData(ProjectionLifecycleState.Current, QueryResponseProvenance.HandlerComputed)]
    public void Matching_audit_row_without_current_projection_provenance_stays_delayed_without_receipt(
        ProjectionLifecycleState rowLifecycle,
        QueryResponseProvenance provenance)
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                TenantAuditRow weakRow = StrongRemovalRow(request) with
                {
                    Lifecycle = rowLifecycle,
                    Provenance = provenance,
                };
                return CurrentAuditPage([weakRow], request, "etag-weak");
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed));
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public void Pending_audit_refresh_can_recover_later_ready_proof()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        int auditCalls = 0;
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                auditCalls++;
                return auditCalls == 1
                    ? TenantAuditSnapshot.Empty(
                        isAuthorizationScoped: true,
                        ReadModelFreshnessState.Current,
                        eTag: "etag-empty",
                        request) with { Lifecycle = ProjectionLifecycleState.Current, ProjectionVersion = "opaque-audit-snapshot", CallerScope = "test-caller" }
                    : CurrentAuditPage([StrongRemovalRow(request)], request, "etag-ready");
            });
        RegisterServices(gateway, queryGateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending));

        cut.Find("[data-recovery-verb='refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            auditCalls.ShouldBe(2);
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
            cut.Find("[data-testid='tenants-audit-receipt']");
        });
    }

    [Fact]
    public void Confirmed_removal_without_matching_audit_stays_pending_without_receipt()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        RegisterServices(gateway);

        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                liveProjectionVersion = "tenant-sequence:2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                    request.TenantId,
                    [
                        new TenantMember("owner-user", TenantRole.TenantOwner),
                        new TenantMember("second-owner", TenantRole.TenantOwner),
                    ]), liveProjectionVersion));
            }));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
        });
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-availability']").GetAttribute("data-state").ShouldBe("pending");
    }

    [Fact]
    public void Confirmed_removal_with_unauthorized_audit_keeps_confirmed_without_available()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => TenantAuditSnapshot.Unauthorized(call.ArgAt<TenantAuditRequest>(0)));
        RegisterServices(gateway, queryGateway);

        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                liveProjectionVersion = "tenant-sequence:2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                    request.TenantId,
                    [
                        new TenantMember("owner-user", TenantRole.TenantOwner),
                        new TenantMember("second-owner", TenantRole.TenantOwner),
                    ]), liveProjectionVersion));
            }));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        });
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-availability']").GetAttribute("data-state").ShouldBe("unavailable");

        cut.FindAll("[data-recovery-verb='escalate']").ShouldBeEmpty();
        cut.Find("[data-recovery-verb='continuereadonly']").Click();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
        cut.FindAll("[data-testid='tenants-audit-availability']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unavailable", TenantCommandAuditState.AuditUnavailable)]
    [InlineData("error", TenantCommandAuditState.AuditUnavailable)]
    [InlineData("throws", TenantCommandAuditState.AuditUnavailable)]
    [InlineData("invalid-cursor", TenantCommandAuditState.AuditDelayed)]
    [InlineData("loading", TenantCommandAuditState.AuditDelayed)]
    public void Failed_audit_reads_after_confirmation_are_unavailable_while_unready_pages_stay_delayed(
        string auditOutcome,
        TenantCommandAuditState expectedAuditState)
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                return auditOutcome switch
                {
                    "unavailable" => TenantAuditSnapshot.Unavailable(request),
                    "error" => TenantAuditSnapshot.Error(request),
                    "throws" => throw new InvalidOperationException("The audit read failed."),
                    "invalid-cursor" => TenantAuditSnapshot.InvalidCursor(request),
                    "loading" => TenantAuditSnapshot.Loading(request.TenantId),
                    _ => throw new ArgumentOutOfRangeException(nameof(auditOutcome), auditOutcome, null),
                };
            });
        RegisterServices(gateway, queryGateway);

        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);

        // A read that failed leaves the audit status unreadable (Severe, assertive); a page that is not ready yet
        // only delays the proof (Warning, polite). The confirmed removal itself never changes.
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
            cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        });
        cut.Find("[data-testid='tenants-remove-member-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state")
            .ShouldBe(expectedAuditState is TenantCommandAuditState.AuditUnavailable ? "unavailable" : "delayed");
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_lookup_wait_for_its_replay_and_never_exhaust_the_retry_limit()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1"));
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending));
        const string refresh = "[data-testid='tenants-remove-member-audit'] [data-recovery-verb='refresh']";
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        TaskCompletionSource nudgeLookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseNudgeLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusReads = 0;
        gateway.StatusAsync = async (_, _) =>
        {
            if (Interlocked.Increment(ref statusReads) == 1)
            {
                nudgeLookupStarted.SetResult();
                await releaseNudgeLookup.Task.ConfigureAwait(false);
            }

            return new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true);
        };

        // A SignalR nudge starts a slow lookup; the shared control's Refresh then merges into it.
        Task nudge = cut.InvokeAsync(() => cut.Instance.HandleAuthoritativeRefreshNudgeAsync());
        await nudgeLookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        merged.IsCompleted.ShouldBeFalse();

        // The merged click waits for the running lookup and its replay; nothing counted as an unchanged retry yet.
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        releaseNudgeLookup.SetResult();
        await nudge.WaitAsync(TimeSpan.FromSeconds(5));
        await merged.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusReads).ShouldBe(2);
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        // The completed merged recovery used one retry. Two later recoveries must complete and consume the rest.
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusReads).ShouldBe(3);
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusReads).ShouldBe(4);
        cut.FindAll(refresh).ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-availability-retry-limit']");
    }

    [Theory]
    [InlineData("other-correlation", TenantCommandAuditState.AuditPending)]
    [InlineData("other-sequence", TenantCommandAuditState.AuditPending)]
    [InlineData("missing-correlation", TenantCommandAuditState.AuditPending)]
    [InlineData("missing-sequence", TenantCommandAuditState.AuditPending)]
    [InlineData("wrong-page-scope", TenantCommandAuditState.AuditDelayed)]
    [InlineData("wrong-page-caller", TenantCommandAuditState.AuditDelayed)]
    [InlineData("missing-page-version", TenantCommandAuditState.AuditDelayed)]
    [InlineData("later-page-version-drift", TenantCommandAuditState.AuditDelayed)]
    [InlineData("later-page-source-switch", TenantCommandAuditState.AuditDelayed)]
    [InlineData("later-page-stale", TenantCommandAuditState.AuditDelayed)]
    [InlineData("duplicate-exact-row", TenantCommandAuditState.AuditDelayed)]
    public void RemovalReceiptRejectsConcurrentOrInconsistentAttemptEvidence(string caseKind, TenantCommandAuditState expected)
    {
        ArgumentNullException.ThrowIfNull(caseKind);
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        int pages = 0;
        queryGateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                pages++;
                TenantAuditRequest request = call.Arg<TenantAuditRequest>();
                TenantAuditRow row = StrongRemovalRow(request);
                row = caseKind switch
                {
                    "other-correlation" => row with { CommandCorrelationId = "another-operator-command" },
                    "other-sequence" => row with { AggregateSequence = 3 },
                    "missing-correlation" => row with { CommandCorrelationId = null },
                    "missing-sequence" => row with { AggregateSequence = null },
                    _ => row,
                };
                TenantAuditSnapshot page = CurrentAuditPage([row], request, "etag-page-" + pages);
                if (caseKind.StartsWith("later-page-", StringComparison.Ordinal) && request.Cursor is null)
                {
                    return page with { HasMore = true, NextCursor = "page-2" };
                }
                return caseKind switch
                {
                    "wrong-page-scope" => page with { TenantId = "unrelated-tenant" },
                    "wrong-page-caller" => page with { CallerScope = "other-caller" },
                    "missing-page-version" => page with { ProjectionVersion = null, ETag = null },
                    "later-page-version-drift" => page with { Rows = [], ProjectionVersion = "different-audit-version" },
                    "later-page-source-switch" => page with { Rows = [], ProjectionVersion = null, ETag = "opaque-audit-snapshot" },
                    "later-page-stale" => page with { Rows = [], Freshness = ReadModelFreshnessState.Stale },
                    "duplicate-exact-row" => page with { Rows = [row, row] },
                    _ => page,
                };
            });
        RegisterServices(gateway, queryGateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(expected));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
        pages.ShouldBe(caseKind.StartsWith("later-page-", StringComparison.Ordinal) ? 2 : 1);
    }

    [Fact]
    public void ExactRemovalProofAcceptsClockSkewWithOnlyOpaqueETagMetadataAndRedactsProvenance()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.Arg<TenantAuditRequest>();
                request.From.ShouldBeNull();
                TenantAuditRow row = StrongRemovalRow(request) with { Timestamp = DateTimeOffset.UtcNow.AddHours(-2) };
                return CurrentAuditPage([row], request, "opaque-validator") with { ProjectionVersion = null };
            });
        RegisterServices(gateway, queryGateway);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        cut.Markup.ShouldNotContain("correlation-1");
        cut.Markup.ShouldNotContain("CommandCorrelationId");
        cut.Markup.ShouldNotContain("AggregateSequence");
    }

    [Theory]
    [InlineData("test-caller", true, false, false, true)]
    [InlineData("other-caller", true, false, false, false)]
    [InlineData(null, false, true, false, false)]
    [InlineData("test-caller", false, false, true, false)]
    public void ReadyRemovalReceiptRemountPreservesProofButShowsOnlyCurrentAuthorizedCallerEvidence(
        string? caller, bool available, bool pending, bool denied, bool visible)
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queries = Substitute.For<ITenantQueryGateway>();
        queries.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => CurrentAuditPage([StrongRemovalRow(call.Arg<TenantAuditRequest>())], call.Arg<TenantAuditRequest>(), "ready-audit"));
        RegisterServices(gateway, queries);
        IRenderedComponent<RemoveTenantMemberFlow> first = RenderConfirmedRemoval(gateway);
        first.WaitForElement("[data-testid='tenants-audit-receipt']");
        TenantRemoveMemberCommandSnapshot original = first.Instance.Snapshot;
        string reference = first.FindComponent<AuditEvidenceReceipt>().Instance.Receipt.ShouldNotBeNull().AuditReference;
        first.Instance.Dispose();
        IRenderedComponent<RemoveTenantMemberFlow> replacement = RenderConfirmedRemoval(gateway, caller, available, pending, denied, submit: false);
        replacement.Instance.Snapshot.ShouldBe(original);
        replacement.Instance.Snapshot.RemovalProof!.AuditReference.ShouldBe(reference);
        if (visible)
        {
            replacement.FindComponent<AuditEvidenceReceipt>().Instance.Receipt.ShouldNotBeNull().AuditReference.ShouldBe(reference);
            replacement.Find("[data-testid='tenants-audit-receipt']");
        }
        else
        {
            replacement.FindComponents<AuditEvidenceReceipt>().ShouldBeEmpty();
            replacement.FindAll("[data-testid='tenants-audit-receipt']").ShouldBeEmpty();
            replacement.Markup.ShouldNotContain(reference);
            replacement.Markup.ShouldNotContain("actor-1");
        }
        gateway.RemoveMemberCallCount.ShouldBe(1);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    [Fact]
    public async Task AuthoritativeRemovalConfirmationPreservesQualifiedReceiptAcrossLateStatusAndReleasesOnlyItsLease()
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queries = Substitute.For<ITenantQueryGateway>();
        queries.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => CurrentAuditPage([StrongRemovalRow(call.Arg<TenantAuditRequest>())], call.Arg<TenantAuditRequest>(), "ready-audit"));
        RegisterServices(gateway, queries);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway, projectionAvailable: false);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        var heldStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.StatusAsync = (_, _) => heldStatus.Task;
        Task refresh = cut.InvokeAsync(() => cut.Instance.HandleAuthoritativeRefreshNudgeAsync());
        await WaitUntilAsync(() => gateway.StatusCallCount == 2, TimeSpan.FromSeconds(5));
        TenantDetail after = Detail("tenant.alpha", [new("owner-user", TenantRole.TenantOwner), new("second-owner", TenantRole.TenantOwner)]);
        await cut.InvokeAsync(() => cut.Instance.ApplyProjectionEvidence(Proof(after, "tenant-sequence:2")));
        cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        TenantRemoveMemberCommandSnapshot confirmed = cut.Instance.Snapshot;
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.IsLocked(key).ShouldBeFalse();
        object foreignOwner = new();
        gate.TryAcquireLease(key, foreignOwner, out TenantAggregateCommandLease? foreign).ShouldBeTrue();
        heldStatus.SetResult(new(CommandStatus.Processing, HasVerifiedCommandIdentity: true));
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.RemovalProof.ShouldBe(confirmed.RemovalProof);
        cut.FindComponent<AuditEvidenceReceipt>().Instance.Receipt.ShouldNotBeNull().AuditReference.ShouldBe(confirmed.RemovalProof!.AuditReference);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsLocked(key).ShouldBeTrue();
        foreign!.TryAbandonBeforeDispatch(foreignOwner).ShouldBeTrue();
    }

    /// <summary>Real Fluxor viewport effects feed measured admission into the removal renderer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReviewRemovalFluxorViewportEffectControlsAdmission(bool unknown)
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        Services.RemoveAll<TenantHighImpactViewportObservation>();
        Services.AddScoped<TenantHighImpactViewportObservation>();
        Services.AddFluxor(options => options.ScanAssemblies(typeof(TenantHighImpactViewportEffects).Assembly));
        IStore store = Services.GetRequiredService<IStore>();
        await store.InitializeAsync();
        IDispatcher dispatcher = Services.GetRequiredService<IDispatcher>();
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(p => p
            .Add(x => x.Detail, Detail("tenant.alpha"))
            .Add(x => x.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1")
            .Add(x => x.IsAuthorized, true).Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.AuditProofCapabilityAvailable, true).Add(x => x.TargetPlatformStandingResolved, true));
        viewport.HasMeasurement.ShouldBeFalse();
        await cut.InvokeAsync(() => (Task)typeof(RemoveTenantMemberFlow).GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, null)!);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        await cut.InvokeAsync(() => dispatcher.Dispatch(new Hexalith.FrontComposer.Shell.State.Navigation.ViewportTierChangedAction(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Tablet)));
        cut.WaitForAssertion(() =>
        {
            viewport.HasMeasurement.ShouldBeTrue();
            viewport.State.ShouldBe(TenantHighImpactViewportState.Safe);
        });
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeFalse();
        await cut.InvokeAsync(() => dispatcher.Dispatch(new Hexalith.FrontComposer.Shell.State.Navigation.ViewportTierChangedAction(unknown
            ? (Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier)255 : Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone)));
        cut.WaitForAssertion(() => viewport.State.ShouldBe(unknown ? TenantHighImpactViewportState.Unknown : TenantHighImpactViewportState.Unsafe));
        await cut.InvokeAsync(() => (Task)typeof(RemoveTenantMemberFlow).GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, null)!);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-lifecycle']");
        cut.Find("[data-testid='tenants-remove-member-audit']");
        await cut.InvokeAsync(() => dispatcher.Dispatch(new Hexalith.FrontComposer.Shell.State.Navigation.ViewportTierChangedAction(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop)));
        cut.WaitForAssertion(() => viewport.State.ShouldBe(TenantHighImpactViewportState.Safe));
        await cut.Find("form").SubmitAsync();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Instance.Snapshot.Intent.ShouldNotBeNull().UserId.ShouldBe("reader-user");
    }

    /// <summary>Measured Phone or Unknown preserves exact accepted removal evidence and its qualified receipt.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveRemovalViewportChangesRetainConfirmedTrackingAndProof(bool unknown)
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway reads = Substitute.For<ITenantQueryGateway>();
        reads.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.Arg<TenantAuditRequest>();
                return CurrentAuditPage([StrongRemovalRow(request)], request, "audit-v2");
            });
        RegisterServices(gateway, reads);
        IRenderedComponent<RemoveTenantMemberFlow> cut = RenderConfirmedRemoval(gateway);
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        TenantRemoveMemberCommandSnapshot original = cut.Instance.Snapshot;
        original.RemovalProof.ShouldNotBeNull();
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        await cut.InvokeAsync(() => viewport.Observe(unknown ? (Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier)255
            : Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone));
        viewport.HasMeasurement.ShouldBeTrue();
        viewport.State.ShouldBe(unknown ? TenantHighImpactViewportState.Unknown : TenantHighImpactViewportState.Unsafe);
        await cut.InvokeAsync(() => (Task)typeof(RemoveTenantMemberFlow).GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, null)!);
        gateway.RemoveMemberCallCount.ShouldBe(1);
        cut.Instance.Snapshot.MessageId.ShouldBe(original.MessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBe(original.CorrelationId);
        cut.Instance.Snapshot.Intent.ShouldBe(original.Intent);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        cut.Instance.Snapshot.RemovalProof.ShouldBeSameAs(original.RemovalProof);
        cut.Find("[data-testid='tenants-remove-member-lifecycle']");
        cut.Find("[data-testid='tenants-remove-member-audit']");
    }

    /// <summary>Exports actual localized removal states and proves measured admission without browser command egress.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public async Task LiveRemovalExportsLocalizedMeasuredViewportFixtures(string culture)
    {
        var gateway = new StubTenantCommandGateway();
        RegisterServices(gateway);
        var viewport = new TenantHighImpactViewportObservation();
        Services.AddSingleton(viewport);
        var factory = new Microsoft.Extensions.Localization.ResourceManagerStringLocalizerFactory(
            Microsoft.Extensions.Options.Options.Create(new LocalizationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new CultureBoundTenantsLocalizer(
            factory.Create(typeof(TenantsResources)), CultureInfo.GetCultureInfo(culture)));
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(p => p
            .Add(x => x.Detail, Detail("tenant.alpha"))
            .Add(x => x.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1")
            .Add(x => x.IsAuthorized, true).Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.AuditProofCapabilityAvailable, true).Add(x => x.TargetPlatformStandingResolved, true));
        MethodInfo submit = typeof(RemoveTenantMemberFlow).GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await cut.InvokeAsync(() => (Task)submit.Invoke(cut.Instance, null)!);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        string unknown = cut.Markup;
        cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeTrue();
        await cut.InvokeAsync(() => viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Tablet));
        cut.WaitForElement("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeFalse();
        string safe = cut.Markup;
        await cut.InvokeAsync(() => viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone));
        await cut.InvokeAsync(() => (Task)submit.Invoke(cut.Instance, null)!);
        gateway.RemoveMemberCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-remove-member-lifecycle']");
        cut.Find("[data-testid='tenants-remove-member-audit']");
        string? directory = Environment.GetEnvironmentVariable("TENANTS_LIVE_ADMISSION_FIXTURE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"tenant-live-removal-unknown-{culture}.html"), unknown);
            File.WriteAllText(Path.Combine(directory, $"tenant-live-removal-safe-{culture}.html"), safe);
            File.WriteAllText(Path.Combine(directory, $"tenant-live-removal-resized-{culture}.html"), cut.Markup);
        }
    }

    private void RegisterServices(StubTenantCommandGateway gateway, ITenantQueryGateway? queryGateway = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(gateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway ?? CreateCapableQueryGateway());
    }

    private static ITenantQueryGateway CreateCapableQueryGateway(TenantAuditSnapshot? audit = null)
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => audit ?? TenantAuditSnapshot.Empty(
                isAuthorizationScoped: true,
                Hexalith.EventStore.Client.Projections.ReadModelFreshnessState.Current,
                eTag: null,
                call.ArgAt<TenantAuditRequest>(0)) with
            {
                Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "opaque-audit-snapshot",
            CallerScope = "test-caller",
            });
        return gateway;
    }

    private IRenderedComponent<RemoveTenantMemberFlow> RenderConfirmedRemoval(StubTenantCommandGateway gateway, string? callerScope = "test-caller", bool auditAvailable = true, bool auditPending = false, bool auditDenied = false, bool submit = true, bool projectionAvailable = true)
    {
        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<CascadingValue<string?>> wrapper = Render<CascadingValue<string?>>(scope => scope
            .Add(p => p.Name, "CurrentTenantCallerScope").Add(p => p.Value, callerScope)
            .AddChildContent<CascadingValue<bool>>(available => available
            .Add(p => p.Name, "AuditReadAvailable").Add(p => p.Value, auditAvailable)
            .AddChildContent<CascadingValue<bool>>(pending => pending
            .Add(p => p.Name, "AuditReadPending").Add(p => p.Value, auditPending)
            .AddChildContent<CascadingValue<bool>>(denied => denied
            .Add(p => p.Name, "AuditReadDenied").Add(p => p.Value, auditDenied)
            .AddChildContent<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                if (!projectionAvailable)
                {
                    return Task.FromResult<TenantDetailSnapshot?>(null);
                }
                liveProjectionVersion = "tenant-sequence:2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(
                    request.TenantId,
                    [
                        new TenantMember("owner-user", TenantRole.TenantOwner),
                        new TenantMember("second-owner", TenantRole.TenantOwner),
                    ]), liveProjectionVersion));
            }))))));
        IRenderedComponent<RemoveTenantMemberFlow> cut = wrapper.FindComponent<RemoveTenantMemberFlow>();
        if (!submit)
        {
            return cut;
        }
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        gateway.RemoveMemberCallCount.ShouldBe(1);
        return cut;
    }

    private IRenderedComponent<RemoveTenantMemberFlow> RenderConfirmedRemovalWithAuditReturn(
        string returnUrl,
        ITenantsBffComposition composition,
        bool auditReadAvailable = true,
        bool auditReadDenied = false,
        string? auditReadUnavailableReason = null)
    {
        StubTenantCommandGateway gateway = CompletedGateway();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                return CurrentAuditPage([StrongRemovalRow(request)], request, "etag-ready");
            });
        RegisterServices(gateway, queryGateway);
        Services.AddSingleton(composition);

        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<CascadingValue<bool>> wrapper = Render<CascadingValue<bool>>(parameters => parameters
            .Add(p => p.Name, "AuditReadDenied")
            .Add(p => p.Value, auditReadDenied)
            .AddChildContent<CascadingValue<string?>>(reason => reason
            .Add(p => p.Name, "AuditReadUnavailableReason")
            .Add(p => p.Value, auditReadUnavailableReason)
            .AddChildContent<CascadingValue<bool>>(available => available
            .Add(p => p.Name, "AuditReadAvailable")
            .Add(p => p.Value, auditReadAvailable)
            .AddChildContent<CascadingValue<string>>(nested => nested
                .Add(p => p.Name, "DetailAuditReturnUrl")
                .Add(p => p.Value, returnUrl)
                .AddChildContent<CascadingValue<string>>(scope => scope
                .Add(p => p.Name, "CurrentTenantCallerScope").Add(p => p.Value, "test-caller")
                .AddChildContent<RemoveTenantMemberFlow>(child => child
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
                    .Add(p => p.AuditProofCapabilityAvailable, true)
                    .Add(p => p.Detail, Detail("tenant.alpha"))
                    .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
                    .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
                    .Add(p => p.Freshness, ReadModelFreshnessState.Current)
                    .Add(p => p.ProjectionVersion, "tenant-sequence:1")
                    .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
                    .Add(p => p.ProjectionEvidenceProvider, request =>
                    {
                        liveProjectionVersion = "tenant-sequence:2";
                        return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId,
                        [
                            new TenantMember("owner-user", TenantRole.TenantOwner),
                            new TenantMember("second-owner", TenantRole.TenantOwner),
                        ]), liveProjectionVersion));
                    })))))));
        IRenderedComponent<RemoveTenantMemberFlow> cut = wrapper.FindComponent<RemoveTenantMemberFlow>();
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        if (auditReadAvailable && !auditReadDenied)
        {
            cut.WaitForElement("[data-testid='tenants-audit-receipt']");
        }
        return cut;
    }

    private static StubTenantCommandGateway CompletedGateway()
        => new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };

    private static TenantAuditSnapshot CurrentAuditPage(
        IReadOnlyList<TenantAuditRow> rows,
        TenantAuditRequest request,
        string eTag)
        => TenantAuditSnapshot.Ready(
            rows,
            nextCursor: null,
            hasMore: false,
            eTag,
            ReadModelFreshnessState.Current,
            request) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "opaque-audit-snapshot",
            CallerScope = "test-caller",
        };

    private static TenantAuditRow StrongRemovalRow(TenantAuditRequest request)
    {
        DateTimeOffset from = request.From ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        return new(
            "evt-remove-1",
            "UserRemovedFromTenant",
            AuditEventCategory.Access,
            "actor-1",
            from.AddSeconds(30),
            request.TenantId,
            "reader-user",
            request.TenantId,
            "UserRemovedFromTenant",
            "userId: reader-user",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked,
            new TenantAuditNarrative(UserId: "reader-user")) { CommandCorrelationId = "correlation-1", AggregateSequence = 2 };
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Tenants.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test output.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The awaited condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public void Unproven_audit_proof_capability_fails_closed_by_default()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        // AuditProofCapabilityAvailable is deliberately not supplied. Gateway registration alone is not
        // capability evidence, so a parent that never proves it must not get a dispatchable removal.
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Instance.AuditProofCapabilityAvailable.ShouldBeFalse();
        cut.Find("[data-testid='tenants-remove-member-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent
            .ShouldContain("missing audit proof", Case.Insensitive);

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();

        gateway.RemoveMemberCallCount.ShouldBe(0);
    }

    [Fact]
    public void Confirmed_removal_does_not_raise_a_target_absent_unavailability()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        RegisterServices(gateway);

        TenantDetail after = Detail(
            "tenant.alpha",
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("second-owner", TenantRole.TenantOwner),
            ]);

        string liveProjectionVersion = "tenant-sequence:1";
        IRenderedComponent<RemoveTenantMemberFlow> cut = Render<RemoveTenantMemberFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.Member, new TenantMember("reader-user", TenantRole.TenantReader))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.ProjectionVersionProvider, () => liveProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, _ =>
            {
                liveProjectionVersion = "tenant-sequence:2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(after, liveProjectionVersion));
            }));

        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.RemoveMemberCallCount.ShouldBe(1));

        // The parent keeps the flow mounted after the removed row leaves the page. Reporting that
        // by-design absence as an assertive unavailability would contradict the confirmed outcome.
        cut.Render(parameters => parameters
            .Add(p => p.Detail, after)
            .Add(p => p.ProjectionVersion, "tenant-sequence:2"));

        cut.WaitForAssertion(() =>
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.FindAll("[data-testid='tenants-remove-member-unavailable-reason']").ShouldBeEmpty();
    }

    private static TenantDetail Detail(string tenantId)
        => Detail(
            tenantId,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("second-owner", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
                new TenantMember("User/CaseSensitive.01", TenantRole.TenantReader),
            ]);

    private static TenantDetail Detail(string tenantId, IReadOnlyList<TenantMember> members)
        => new(
            tenantId,
            "Alpha",
            "Tenant alpha description",
            TenantStatus.Active,
            members,
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public TenantCommandSubmissionResult Submission { get; init; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TenantCommandStatusResult Status { get; init; }
            = TenantCommandStatusResult.Unknown("Command status is unavailable.");

        public Func<TenantCommandTrackingHandle, CancellationToken, Task<TenantCommandStatusResult>>? StatusAsync { get; set; }

        public Func<RemoveUserFromTenant, Task<TenantCommandSubmissionResult>>? RemoveMemberAsync { get; init; }

        public RemoveUserFromTenant? LastRemoveMemberRequest { get; private set; }

        public string? LastRemoveMemberMessageId { get; private set; }

        public int RemoveMemberCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
        {
            RemoveMemberCallCount++;
            LastRemoveMemberRequest = request;
            LastRemoveMemberMessageId = messageId;
            return RemoveMemberAsync is null ? Task.FromResult(Submission) : RemoveMemberAsync(request);
        }

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            handle.AggregateId.ShouldNotBeNullOrWhiteSpace();
            handle.ExpectedDomain.ShouldBe(TenantIdentity.Domain);
            StatusCallCount++;
            return StatusAsync is null ? Task.FromResult(Status) : StatusAsync(handle, cancellationToken);
        }
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.RemoveMember.Title"] = "Remove tenant member",
            ["Tenants.RemoveMember.Description"] = "Preview removal of user {1} from tenant {0}. Current confirmed role is {2}.",
            ["Tenants.RemoveMember.Preview.Title"] = "Consequence preview",
            ["Tenants.RemoveMember.Preview.Tenant"] = "Tenant",
            ["Tenants.RemoveMember.Preview.TargetUser"] = "Target user",
            ["Tenants.RemoveMember.Preview.CurrentRole"] = "Current role",
            ["Tenants.RemoveMember.Preview.OwnerCount"] = "Owner count",
            ["Tenants.RemoveMember.Preview.AccessPath"] = "Affected access path",
            ["Tenants.RemoveMember.Preview.AccessPath.Value"] = "Tenant membership for the visible tenant only.",
            ["Tenants.RemoveMember.Preview.Freshness"] = "Freshness",
            ["Tenants.RemoveMember.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.RemoveMember.Preview.RecoveryPath.Value"] = "Wait, refresh, inspect audit when available, or continue read-only.",
            ["Tenants.RemoveMember.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.RemoveMember.Preview.AuditExpectation.Value"] = "Audit evidence is requested after projection confirmation and may be pending, delayed, or unavailable.",
            ["Tenants.RemoveMember.Preview.PlatformStanding"] = "Platform standing",
            ["Tenants.RemoveMember.Preview.PlatformStanding.Known"] = "Also a global administrator. Tenant membership removal does not change platform administrator authority.",
            ["Tenants.RemoveMember.Preview.PlatformStanding.NotReflected"] = "Not reflected as a global administrator in the current complete projection.",
            ["Tenants.RemoveMember.Preview.PlatformStanding.Unknown"] = "Global-administrator standing is unproven in this view and is not guessed.",
            ["Tenants.RemoveMember.Preview.ConsequencesVersusUnknowns"] = "Known consequences versus unknowns",
            ["Tenants.RemoveMember.Preview.ConsequencesVersusUnknowns.Value"] = "Known consequence: tenant membership is removed only after projection confirmation proves the target user is absent. Known unknowns: session revocation, downstream enforcement, and token invalidation are not proven by this flow.",
            ["Tenants.RemoveMember.Preview.Blocked.Required"] = "Consequence preview is incomplete. Refresh current tenant detail before confirming removal.",
            ["Tenants.RemoveMember.Freshness.Current"] = "Current",
            ["Tenants.RemoveMember.Freshness.Stale"] = "Stale",
            ["Tenants.RemoveMember.Freshness.Unknown"] = "Unknown",
            ["Tenants.RemoveMember.OwnerContext.NoOwners"] = "0 visible owners; owner context is unavailable.",
            ["Tenants.RemoveMember.OwnerContext.LastOwner"] = "{0} visible owner; removing this member can leave zero visible owners.",
            ["Tenants.RemoveMember.OwnerContext.MultipleOwners"] = "{0} visible owners.",
            ["Tenants.RemoveMember.OwnerRisk.LastOwner"] = "Warning: {0} visible owner remains. Last-owner tenant membership removal is allowed, but it needs deliberate confirmation.",
            ["Tenants.RemoveMember.OwnerRisk.Accessible"] = "Elevated last-owner removal warning for {0} visible owner.",
            ["Tenants.RemoveMember.GlobalAdminRisk.Known"] = "Platform administrator authority is reflected for this user. This flow removes tenant membership only and will not remove global-administrator authority.",
            ["Tenants.RemoveMember.GlobalAdminRisk.Accessible"] = "Platform authority risk context.",
            ["Tenants.RemoveMember.Confirmation.Label"] = "Type the target user id to confirm removal",
            ["Tenants.RemoveMember.Confirmation.Elevated.Label"] = "Elevated risk: type the target user id exactly to confirm removal",
            ["Tenants.RemoveMember.Confirmation.Help"] = "Type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.RemoveMember.Confirmation.Elevated.Help"] = "Elevated risk: type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.RemoveMember.Confirm"] = "Remove member",
            ["Tenants.RemoveMember.Refresh"] = "Refresh status",
            ["Tenants.RemoveMember.Cancel"] = "Cancel",
            ["Tenants.RemoveMember.Lifecycle.Title"] = "Remove member command lifecycle",
            ["Tenants.RemoveMember.Validation.ConfirmationRequired"] = "Type {0} exactly before removing this member.",
            ["Tenants.RemoveMember.Validation.ElevatedConfirmationRequired"] = "Elevated risk requires typing {0} exactly before removing this member.",
            ["Tenants.RemoveMember.Unavailable.Authorization"] = "You are not authorized to remove members from this tenant.",
            ["Tenants.RemoveMember.Unavailable.Narrow"] = "Removal is read-only until the browser measures a safe tablet or desktop viewport.",
            ["Tenants.RemoveMember.Unavailable.Freshness"] = "Refresh current tenant detail before removing a member.",
            ["Tenants.RemoveMember.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow removing members.",
            ["Tenants.RemoveMember.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.RemoveMember.Unavailable.InFlight"] = "A tenant command is already in progress.",
            ["Tenants.RemoveMember.Unavailable.Identity"] = "Tenant id and target user id are required before the preview can open.",
            ["Tenants.RemoveMember.Unavailable.UnknownRole"] = "The current role is unknown, so remove member fails closed until projection evidence is refreshed.",
            ["Tenants.RemoveMember.Unavailable.TargetAbsent"] = "The target user is already absent from the current confirmed projection.",
            ["Tenants.Members.UnavailableReason.MissingAuditProof"] = "missing audit proof",
            ["Tenants.RemoveMember.Role.TenantOwner"] = "Tenant owner",
            ["Tenants.RemoveMember.Role.TenantContributor"] = "Tenant contributor",
            ["Tenants.RemoveMember.Role.TenantReader"] = "Tenant reader",
            ["Tenants.RemoveMember.Role.Unknown"] = "Unknown role",
            ["Tenants.RemoveMember.AlreadyApplied.BeforeSubmit"] = "User {0} is already absent from the current confirmed projection; no remove command was submitted.",
            ["Tenants.RemoveMember.DuplicatePrevented.Message"] = "A remove-member command is already in progress for this flow.",
            ["Tenants.RemoveMember.Confirm.AlreadyApplied.PreExisting"] = "Projection evidence confirms the target user was already absent before this attempt; no removal success is asserted.",
            ["Tenants.RemoveMember.Confirm.AlreadyApplied.RejectedAbsence"] = "Projection evidence confirms the target user is already absent; no command result or audit proof is asserted.",
            ["Tenants.RemoveMember.Confirm.UnableToVerify.MissingBaseline"] = "Member absence matched without a pre-submit baseline, so this attempt cannot be confirmed.",
            ["Tenants.RemoveMember.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.RemoveMember.State.Idle"] = "No remove-member preview opened.",
            ["Tenants.RemoveMember.State.Previewed"] = "Consequence preview ready; no command has been submitted.",
            ["Tenants.RemoveMember.State.RequestSent"] = "Remove-member request sent.",
            ["Tenants.RemoveMember.State.Accepted"] = "Accepted by EventStore; waiting for member removal processing.",
            ["Tenants.RemoveMember.State.ProjectionPending"] = "Projection pending; the target user is not confirmed absent yet.",
            ["Tenants.RemoveMember.State.Confirmed"] = "Projection confirmed the target user is absent from the tenant members.",
            ["Tenants.RemoveMember.State.Rejected"] = "Remove-member command rejected.",
            ["Tenants.RemoveMember.State.AlreadyApplied"] = "Already applied; projection evidence shows the target user is absent.",
            ["Tenants.RemoveMember.State.DuplicatePrevented"] = "Duplicate remove-member submission prevented.",
            ["Tenants.RemoveMember.State.Failed"] = "Remove-member command submission failed.",
            ["Tenants.RemoveMember.State.Degraded"] = "Remove-member command result is degraded and needs review.",
            ["Tenants.RemoveMember.State.UnableToVerify"] = "Unable to verify the remove-member command result.",
            ["Tenants.Audit.EntryPoint.Accessible.Command"] = "Inspect audit for tenant {1} ({0})",
            ["Tenants.Audit.EntryPoint.CommandReason"] = "Command-specific proof is not available here; open the tenant audit list and use the visible audit state.",
            ["Tenants.Audit.EntryPoint.Label"] = "Audit evidence",
            ["Tenants.Audit.EntryPoint.Unavailable.ScopeRequired"] = "Tenant scope is required before audit evidence can be opened.",
            ["Tenants.Audit.EntryPoint.Unavailable.StaleScope"] = "Refresh tenant scope before opening audit evidence.",
            ["Tenants.Audit.EntryPoint.Unavailable.InvalidContext"] = "The audit context is invalid. Return to the originating page and select a current tenant.",
            ["Tenants.Audit.EntryPoint.Unavailable.Disconnected"] = "The audit read service is disconnected. Refresh when the connection returns.",
            ["Tenants.Audit.Availability.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Audit.Availability.Action.InspectAudit"] = "Inspect audit",
            ["Tenants.Audit.Availability.Action.Refresh"] = "Retry status lookup",
            ["Tenants.Audit.Availability.ActionsLabel"] = "Audit availability recovery actions",
            ["Tenants.Audit.Availability.Reason.MissingSupport"] = "In-panel audit verification is not available for this command, so this panel cannot match an audit record to the attempt. The recorded outcome above is unchanged.",
            ["Tenants.Audit.Availability.Reason.Unavailable"] = "The audit status could not be read or verified after the command was sent. This does not mean the record does not exist, and no proof is claimed.",
            ["Tenants.Audit.Availability.State.Delayed"] = "Audit delayed",
            ["Tenants.Audit.Availability.State.Available"] = "Audit available",
            ["Tenants.Audit.Availability.State.MissingSupport"] = "Missing implementation support",
            ["Tenants.Audit.Availability.State.Pending"] = "Audit pending",
            ["Tenants.Audit.Availability.State.Unavailable"] = "Audit unavailable",
            ["Tenants.Audit.Receipt.Title"] = "Audit evidence receipt",
            ["Tenants.Audit.Receipt.Copy"] = "Copy full audit receipt summary",
            ["Tenants.Audit.Receipt.Close"] = "Close receipt",
            ["Tenants.Audit.Receipt.Summary"] = "Actor: {actor} | Target: {target} | Tenant scope: {scope} | Outcome: {outcome} | Timestamp: {timestamp} | Projection marker: {projection} | Audit reference: {auditReference}",
            ["Tenants.Audit.Receipt.Outcome.UserRemovedFromTenant"] = "User removed from tenant",
            ["Tenants.Audit.Receipt.Field.Actor"] = "Actor",
            ["Tenants.Audit.Receipt.Field.Target"] = "Target",
            ["Tenants.Audit.Receipt.Field.Scope"] = "Tenant scope",
            ["Tenants.Audit.Receipt.Field.Outcome"] = "Outcome",
            ["Tenants.Audit.Receipt.Field.Timestamp"] = "Timestamp",
            ["Tenants.Audit.Receipt.Field.ProjectionMarker"] = "Projection marker",
            ["Tenants.Audit.Receipt.Field.Reference"] = "Audit reference",
            ["Tenants.Audit.Receipt.Field.CommandReference"] = "Command reference",
            ["Tenants.Audit.Receipt.ActionsLabel"] = "Audit receipt recovery actions",
            ["Tenants.Audit.Receipt.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Audit.Receipt.Action.InspectAudit"] = "Inspect audit",
            ["Tenants.Audit.Receipt.Action.Retry"] = "Retry",
            ["Tenants.Audit.Freshness.Current"] = "Current",
            ["Tenants.Audit.Freshness.Stale"] = "Stale",
            ["Tenants.Audit.Freshness.Unknown"] = "Unknown",
            ["Tenants.Audit.Receipt.State.Ready"] = "Audit evidence is ready to cite.",
            ["Tenants.RemoveMember.Recovery.Idle"] = "Open the preview when current projection evidence is available.",
            ["Tenants.RemoveMember.Recovery.Previewed"] = "Confirm deliberately, cancel, or continue read-only.",
            ["Tenants.RemoveMember.Recovery.RequestSent"] = "Wait for command status and projection refresh.",
            ["Tenants.RemoveMember.Recovery.Accepted"] = "Wait, refresh status, or continue read-only until projection confirms absence.",
            ["Tenants.RemoveMember.Recovery.ProjectionPending"] = "Refresh the member projection; do not treat the row as removed until absence is confirmed.",
            ["Tenants.RemoveMember.Recovery.Confirmed"] = "Continue read-only or inspect audit when evidence becomes available.",
            ["Tenants.RemoveMember.Recovery.Rejected"] = "Refresh projection evidence or request permission before retrying.",
            ["Tenants.RemoveMember.Recovery.AlreadyApplied"] = "Continue read-only or refresh current membership evidence before taking any further action.",
            ["Tenants.RemoveMember.Recovery.DuplicatePrevented"] = "Wait for the in-flight command, retry status lookup, or continue read-only.",
            ["Tenants.RemoveMember.Recovery.Failed"] = "Retry after checking current projection evidence.",
            ["Tenants.RemoveMember.Recovery.Degraded"] = "Wait, retry status lookup, inspect audit when available, or continue read-only.",
            ["Tenants.RemoveMember.Recovery.UnableToVerify"] = "Refresh, retry status lookup, or continue read-only.",
            ["Tenants.Audit.Availability.Reason.Pending"] = "The command's events are stored, but its audit record is not readable yet. It normally appears shortly, and no proof is claimed until it does.",
            ["Tenants.Audit.Availability.Reason.Delayed"] = "The audit record is taking longer than expected to become readable. No proof is claimed until it can be read.",
            ["Tenants.Audit.Availability.RetryLimit"] = "Repeated retries left this state unchanged, so retrying is no longer offered here.",
            ["Tenants.Audit.Receipt.Availability.Unavailable.Reason"] = "This audit read could not verify the requested evidence. This does not mean the record does not exist, and the recorded outcome is unchanged.",
            ["Tenants.Audit.Recovery.Action.Escalate"] = "Escalate without diagnostics",
        };

        public LocalizedString this[string name]
            => new(name, Values.TryGetValue(name, out string? value) ? value : name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.CurrentCulture, Values.TryGetValue(name, out string? value) ? value : name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Values.Select(v => new LocalizedString(v.Key, v.Value));
    }
}
