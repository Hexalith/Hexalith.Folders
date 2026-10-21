using System.Globalization;
using System.Reflection;

using Bunit;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.FrontComposer.Contracts.Lifecycle;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Tenants.Lifecycle;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Tenants.UI.State.TenantCommands;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class TenantLifecycleActionAvailabilityTests : FluentBunitContext
{
    [Theory]
    [InlineData(TenantStatus.Active, TenantStatus.Disabled, "disable")]
    [InlineData(TenantStatus.Disabled, TenantStatus.Active, "enable")]
    public void TenantNamedGlobalAdministratorsConfirmsLifecycleThroughStatusGateway(
        TenantStatus currentStatus, TenantStatus intendedStatus, string action)
    {
        IEventStoreGatewayClient client = Substitute.For<IEventStoreGatewayClient>();
        client.SubmitCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResponse("correlation-configuration"));
        using HttpClient statusClient = new(new TenantConfigurationStatusHandler())
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        };
        TenantCommandGateway gateway = new(client, Substitute.For<IUlidFactory>(), statusClient);
        RegisterServices(gateway);
        int proofReads = 0;
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.TenantId, "global-administrators")
            .Add(p => p.Detail, Detail("global-administrators", currentStatus))
            .Add(p => p.ProjectionVersion, "tenant-sequence:41")
            .Add(p => p.CurrentStatus, currentStatus)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceConnected, true)
            .Add(p => p.IsCommandSurfaceAvailable, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                ++proofReads == 1
                    ? Proof(request.TenantId, currentStatus, "tenant-sequence:41")
                    : Proof(request.TenantId, intendedStatus, "tenant-sequence:42"))));

        cut.Find($"[data-testid='tenants-lifecycle-{action}']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("global-administrators");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Find("[data-testid='tenants-lifecycle-confirmed-status']").TextContent
            .ShouldContain(intendedStatus.ToString());
    }

    [Theory]
    [MemberData(nameof(FocusExceptions))]
    public async Task Lifecycle_focus_helpers_swallow_supported_js_failures(Exception exception)
    {
        var gateway = new StubTenantCommandGateway();
        RegisterServices(gateway);
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetException(exception);
        IRenderedComponent<TenantLifecycleActionAvailability> availability = RenderLifecycleAvailability([]);
        IRenderedComponent<TenantLifecycleCommandFlow> flow = RenderLifecycleFlow();
        availability.Find("[data-testid='tenants-lifecycle-disable']").Click();
        TenantLifecycleCommandSnapshot availabilityState = availability
            .FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
        TenantLifecycleCommandSnapshot flowState = flow.Instance.Snapshot;
        foreach ((Type ComponentType, object Instance, string FieldName) target in new[]
        {
            (typeof(TenantLifecycleActionAvailability), (object)availability.Instance, "_disableElement"),
            (typeof(TenantLifecycleCommandFlow), (object)flow.Instance, "_lifecycleElement"),
        })
        {
            MethodInfo helper = target.ComponentType.GetMethod(
                "FocusSafelyAsync",
                BindingFlags.Static | BindingFlags.NonPublic).ShouldNotBeNull();
            ElementReference element = (ElementReference)target.ComponentType.GetField(
                target.FieldName,
                BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull().GetValue(target.Instance)!;
            Task invocation = (Task)helper.Invoke(null, [element])!;
            await invocation;
        }

        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
        availability.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.ShouldBe(availabilityState);
        flow.Instance.Snapshot.ShouldBe(flowState);
    }

    public static TheoryData<Exception> FocusExceptions
        => new()
        {
            new JSDisconnectedException("Circuit disconnected."),
            new JSException("Focus target detached."),
        };

    [Theory]
    [InlineData("authority")]
    [InlineData("proof")]
    public void Submit_preflight_deadline_rejects_never_completing_providers_and_recovers_controls(
        string provider)
    {
        var gateway = new StubTenantCommandGateway();
        RegisterServices(gateway);
        var neverAuthority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverProof = new TaskCompletionSource<TenantDetailSnapshot?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => provider == "authority"
                ? neverAuthority.Task
                : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => provider == "proof"
                ? neverProof.Task
                : Task.FromResult<TenantDetailSnapshot?>(Proof(
                    "tenant.alpha",
                    TenantStatus.Active,
                    "tenant-sequence:41"))));
        cut.Instance.ExternalOperationTimeout = TimeSpan.FromMilliseconds(30);

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        string expectedKey = provider == "authority"
            ? "Tenants.Lifecycle.Unavailable.MissingPermission"
            : "Tenants.Lifecycle.Unavailable.ProofRead";
        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey.ShouldBe(expectedKey));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").GetAttribute("disabled").ShouldBeNull();
        cut.Find("[data-testid='tenants-lifecycle-cancel']").GetAttribute("disabled").ShouldBeNull();
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Lifecycle_availability_renders_stable_selectors_visible_reasons_and_disabled_actions()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Indeterminate)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Unresolved));

        cut.Find("[data-testid='tenants-lifecycle-actions']");
        cut.Find("[data-testid='tenants-lifecycle-state']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-freshness']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-lifecycle-projection-lifecycle-badge']")
            .TextContent.Trim()
            .ShouldBe("Current");
        cut.Find("[data-testid='tenants-lifecycle-governance-gate']").TextContent.ShouldContain("Unresolved");
        cut.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.FindAll("[data-testid$='-unavailable-reason']").Count.ShouldBe(2);
        cut.FindAll("[data-testid$='-unavailable-reason']")
            .ShouldAllBe(static reason => reason.GetAttribute("tabindex") == "0");
        cut.Find("[data-testid='tenants-lifecycle-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Markup.ShouldNotContain("<form", Case.Insensitive);
        cut.Markup.ShouldNotContain("type=\"submit\"", Case.Insensitive);
        // Assert against visible text: Fluent v5 badge tokens (color="success", --colorStatusSuccessForeground)
        // live in attributes/styles and would false-trigger a raw-markup "Success" guard.
        cut.VisibleText().ShouldNotContain("Success", Case.Insensitive);
        cut.Markup.ShouldNotContain("accepted", Case.Insensitive);
        cut.VisibleText().ShouldNotContain("confirmed", Case.Insensitive);
    }

    [Fact]
    public void Open_lifecycle_confirm_closes_when_tenant_identity_changes()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-command-flow']");

        cut.Render(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.beta")
            .Add(component => component.Detail, Detail("tenant.beta", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Facts_header_keeps_lifecycle_badge_independent_of_freshness()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Stale)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-freshness-badge']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-lifecycle-projection-lifecycle-badge']")
            .TextContent.Trim()
            .ShouldBe("Stale");
        (cut.Find("[data-testid='tenants-lifecycle-projection-lifecycle-badge']").GetAttribute("class") ?? string.Empty)
            .ShouldContain("projection-lifecycle-badge--stale");
    }

    [Fact]
    public void Governance_blocking_keeps_lifecycle_command_unavailable_with_high_impact_reason()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Unresolved));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-actions']").TextContent.ShouldContain("read-only", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-actions']").TextContent.ShouldContain("aggregate admission", Case.Insensitive);
    }

    [Fact]
    public void Disabled_tenant_keeps_disabled_projection_truth_and_blocks_enable_without_optimistic_transition()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.disabled")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Disabled)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Unresolved));

        cut.Find("[data-testid='tenants-lifecycle-state']").TextContent.ShouldContain("Disabled");
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Disabled");
        cut.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("#tenants-lifecycle-enable-reason").TextContent.ShouldContain("read-only", Case.Insensitive);
        cut.Find("#tenants-lifecycle-enable-reason").TextContent.ShouldContain("aggregate admission", Case.Insensitive);
        // Unresolved governance remains the action blocker while independently authoritative same-state
        // truth is rendered alongside it as a domain outcome rather than replacing it.
        cut.Find("#tenants-lifecycle-disable-reason").TextContent.ShouldContain("read-only", Case.Insensitive);
        cut.Find("#tenants-lifecycle-disable-reason").TextContent.ShouldContain("aggregate admission", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable-domain-outcome']").TextContent
            .ShouldContain("already has the requested lifecycle state", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable-domain-outcome']")
            .GetAttribute("id").ShouldBe("tenants-lifecycle-disable-domain-outcome");
        cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("aria-describedby")
            .ShouldBe("tenants-lifecycle-disable-reason tenants-lifecycle-disable-recovery tenants-lifecycle-disable-domain-outcome");
        cut.Markup.ShouldNotContain("confirmed", Case.Insensitive);
    }

    [Fact]
    public void Same_state_action_names_the_safe_expected_domain_outcome()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("#tenants-lifecycle-enable-reason").GetAttribute("data-reason-category")
            .ShouldBe(TenantHighImpactReasonCategoryNames.LifecycleStateAlreadySet);
        cut.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-lifecycle-enable-reason tenants-lifecycle-enable-recovery");
        string reasonText = cut.Find("#tenants-lifecycle-enable-reason").TextContent;
        reasonText.ShouldContain("already has the requested lifecycle state", Case.Insensitive);
        reasonText.Split(
            "already has the requested lifecycle state",
            StringSplitOptions.None).Length.ShouldBe(2);
        cut.Find("[data-testid='tenants-lifecycle-live-region']").GetAttribute("aria-live").ShouldBe("polite");
    }

    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate)]
    public void Unauthorized_or_indeterminate_same_state_lifecycle_has_no_domain_outcome_companion(
        TenantLifecycleAuthorizationReflectionState authority)
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, authority)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("#tenants-lifecycle-enable-reason").GetAttribute("data-reason-category")
            .ShouldBe(TenantHighImpactUnavailableReason.MissingPermission.ToString());
        cut.FindAll("[data-testid='tenants-lifecycle-enable-domain-outcome']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-lifecycle-enable-reason tenants-lifecycle-enable-recovery");
    }

    [Fact]
    public void Narrow_safety_context_keeps_available_direction_unavailable_with_visible_mobile_reason()
    {
        RegisterServices();

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.IsNarrowSafetyContext, true));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-actions']").TextContent.ShouldContain("measured viewport");
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-freshness']").TextContent.ShouldContain("Current");
    }

    [Fact]
    public void Lifecycle_component_uses_existing_command_gateway_and_does_not_submit_from_availability_model()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string component = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Lifecycle",
            "TenantLifecycleActionAvailability.razor"));
        string flow = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Lifecycle",
            "TenantLifecycleCommandFlow.razor"));
        string commandGateway = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Services",
            "Gateways",
            "ITenantCommandGateway.cs"));

        component.ShouldNotContain("SubmitCommand");
        flow.ShouldContain("ITenantCommandGateway");
        flow.ShouldContain("EnableTenantTrackedAsync");
        flow.ShouldContain("DisableTenantTrackedAsync");
        commandGateway.ShouldContain("EnableTenantAsync");
        commandGateway.ShouldContain("DisableTenantAsync");
    }

    [Fact]
    public void Ready_authorized_lifecycle_flow_requires_confirmation_and_confirms_only_after_projection_evidence()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"),
            Status = new TenantCommandStatusResult(Hexalith.EventStore.Contracts.Commands.CommandStatus.Completed, EventCount: 1),
        };
        List<bool> activity = [];
        int proofReads = 0;
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                ++proofReads == 1
                    ? Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41")
                    : Proof(request.TenantId, TenantStatus.Disabled, "tenant-sequence:42"))));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();

        cut.Find("[data-testid='tenants-lifecycle-preview']");
        cut.Find("[data-testid='tenants-lifecycle-focus-start']");
        cut.Find("[data-testid='tenants-lifecycle-focus-end']");
        cut.FindAll("[data-testid='tenants-lifecycle-preview-item']").Count.ShouldBe(10);
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("wrong");
        cut.Find("form").Submit();

        cut.Find("[data-testid='tenants-lifecycle-validation']").TextContent.ShouldContain("tenant.alpha");
        gateway.DisableSubmissions.ShouldBe(0);

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.LastDisableRequest.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.LastStatusHandle.ShouldNotBeNull().AggregateId.ShouldBe("tenant.alpha");
        activity.ShouldBe([true, false]);
        cut.Find("[data-testid='tenants-lifecycle-confirmed-status']").TextContent.ShouldContain("Disabled");
        cut.Markup.ShouldNotContain("correlation-life", Case.Insensitive);
    }

    [Fact]
    public void Cancel_and_escape_close_lifecycle_preview_without_gateway_submission()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"),
        };
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        string disableLauncherReferenceId = CapturedElementReferenceId(cut.Instance, "_disableElement");
        disableLauncherReferenceId.ShouldNotBeNullOrWhiteSpace();

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-preview']");
        int focusCallsBeforeCancel = FocusedElementIds().Count;
        cut.Find("[data-testid='tenants-lifecycle-cancel']").Click();

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();
        IReadOnlyList<string> focusedAfterCancel = FocusedElementIds();
        focusedAfterCancel.Count.ShouldBe(focusCallsBeforeCancel + 1);
        focusedAfterCancel[^1].ShouldBe(disableLauncherReferenceId);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        int focusCallsBeforeEscape = FocusedElementIds().Count;
        cut.Find("[data-testid='tenants-lifecycle-command-flow']").KeyDown("Escape");

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        cut.Markup.ShouldNotContain("correlation-life", Case.Insensitive);
        IReadOnlyList<string> focusedAfterEscape = FocusedElementIds();
        focusedAfterEscape.Count.ShouldBe(focusCallsBeforeEscape + 1);
        focusedAfterEscape[^1].ShouldBe(disableLauncherReferenceId);
    }

    [Theory]
    [MemberData(nameof(FocusExceptions))]
    public void Cancel_and_escape_close_lifecycle_preview_when_focus_restore_throws(Exception exception)
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"),
        };
        RegisterServices(gateway);
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetException(exception);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-preview']");
        cut.Find("[data-testid='tenants-lifecycle-cancel']").Click();

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-command-flow']").KeyDown("Escape");

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        cut.Markup.ShouldNotContain("correlation-life", Case.Insensitive);
    }

    [Fact]
    public void Pending_attempt_cannot_be_dismissed_and_remount_adopts_handle_without_redispatch()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        List<bool> firstActivity = [];
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> first = RenderLifecycleAvailability(firstActivity);
        first.Find("[data-testid='tenants-lifecycle-disable']").Click();
        first.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        first.Find("form").Submit();

        first.WaitForAssertion(() => first.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.LastDisableMessageId.ShouldNotBeNullOrWhiteSpace();
        string stableMessageId = gateway.LastDisableMessageId!;
        first.Find("[data-testid='tenants-lifecycle-cancel']").Click();
        first.Find("[data-testid='tenants-lifecycle-command-flow']");
        first.Find("[data-testid='tenants-lifecycle-safe-message']").TextContent
            .ShouldContain("cannot be dismissed", Case.Insensitive);
        firstActivity.ShouldBe([true]);
        first.Dispose();

        List<bool> remountActivity = [];
        IRenderedComponent<TenantLifecycleActionAvailability> remounted = RenderLifecycleAvailability(remountActivity);

        remounted.WaitForAssertion(() => remounted.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.MessageId.ShouldBe(stableMessageId));
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(2);
        remountActivity.ShouldBe([true]);
        remounted.Find("[data-testid='tenants-lifecycle-enable']").GetAttribute("disabled").ShouldNotBeNull();
        remounted.Find("#tenants-lifecycle-disable-reason").GetAttribute("data-reason-category")
            .ShouldBe("RetainedAttempt");
    }

    [Fact]
    public void Dispatch_window_ignores_duplicate_and_cancel_then_remount_redispatches_the_same_message_id()
    {
        TaskCompletionSource<TenantCommandSubmissionResult> firstSubmission = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new();
        gateway.SubmissionProvider = (_, messageId, _) => gateway.DisableSubmissions == 1
            ? firstSubmission.Task
            : Task.FromResult(TenantCommandSubmissionResult.Accepted(messageId, "correlation-life"));
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleCommandFlow> first = RenderLifecycleFlow();
        first.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        first.Find("form").Submit();
        first.WaitForAssertion(() => first.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent));
        gateway.LastDisableMessageId.ShouldNotBeNullOrWhiteSpace();
        string firstMessageId = gateway.LastDisableMessageId!;
        TenantLifecycleCommandSnapshot dispatching = first.Instance.Snapshot;

        first.Find("form").Submit();
        first.Instance.Snapshot.ShouldBe(dispatching);
        gateway.DisableSubmissions.ShouldBe(1);
        first.Find("[data-testid='tenants-lifecycle-cancel']").Click();
        first.Instance.Snapshot.ShouldBe(dispatching);
        first.Dispose();

        IRenderedComponent<TenantLifecycleCommandFlow> remounted = RenderLifecycleFlow();
        remounted.WaitForAssertion(() => gateway.DisableSubmissions.ShouldBe(2));

        gateway.LastDisableMessageId.ShouldBe(firstMessageId);
        firstSubmission.SetResult(TenantCommandSubmissionResult.Accepted(firstMessageId, "correlation-first"));
    }

    [Theory]
    [InlineData(true, "tenant")]
    [InlineData(true, "operation")]
    [InlineData(true, "evidence")]
    [InlineData(false, "tenant")]
    [InlineData(false, "operation")]
    [InlineData(false, "evidence")]
    public void Submit_preflight_rejects_context_changes_during_each_provider(
        bool changeDuringAuthority,
        string mutation)
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        RegisterServices(gateway);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var proof = new TaskCompletionSource<TenantDetailSnapshot?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int proofCalls = 0;
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => changeDuringAuthority
                ? authority.Task
                : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) =>
            {
                proofCalls++;
                return proof.Task;
            }));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        if (changeDuringAuthority)
        {
            cut.Find("[data-testid='tenants-lifecycle-confirmation']").GetAttribute("disabled").ShouldNotBeNull();
        }
        else
        {
            cut.WaitForAssertion(() => proofCalls.ShouldBe(1));
        }

        switch (mutation)
        {
            case "tenant":
                cut.Render(parameters => parameters.Add(
                    component => component.Detail,
                    Detail("tenant.beta", TenantStatus.Active)));
                break;
            case "operation":
                cut.Render(parameters => parameters.Add(
                    component => component.Availability,
                    AvailableLifecycle(TenantStatus.Disabled, TenantLifecycleOperation.EnableTenant)));
                break;
            default:
                TenantLifecycleAvailability stale = AvailableLifecycle(
                    TenantStatus.Active,
                    TenantLifecycleOperation.DisableTenant) with
                {
                    Evidence = HighImpactEvidence(
                        TenantHighImpactAction.DisableTenant) with
                    {
                        Freshness = TenantHighImpactFreshnessState.Stale,
                        HasCurrentBaseline = false,
                    },
                };
                cut.Render(parameters => parameters.Add(component => component.Availability, stale));
                break;
        }

        if (changeDuringAuthority)
        {
            authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        }
        else
        {
            proof.SetResult(Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"));
        }

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State
            .ShouldNotBe(TenantCommandLifecycleState.RequestSent));
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
        if (changeDuringAuthority)
        {
            proofCalls.ShouldBe(0);
        }
    }

    [Fact]
    public void Blocked_preflight_ignores_edit_cancel_escape_and_duplicate_submit()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        RegisterServices(gateway);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int authorityCalls = 0;
        int closeCalls = 0;
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnCloseRequested, () => closeCalls++)
            .Add(component => component.AuthorizationReflectionProvider, _ =>
            {
                authorityCalls++;
                return authority.Task;
            })
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"))));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        TenantLifecycleCommandSnapshot beforePreflight = cut.Instance.Snapshot;

        cut.Find("form").Submit();
        cut.WaitForAssertion(() => authorityCalls.ShouldBe(1));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-cancel']").GetAttribute("disabled").ShouldNotBeNull();

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.beta");
        cut.Find("[data-testid='tenants-lifecycle-cancel']").Click();
        cut.Find("[data-testid='tenants-lifecycle-command-flow']").KeyDown("Escape");
        cut.Find("form").Submit();

        cut.Instance.Snapshot.ShouldBe(beforePreflight);
        closeCalls.ShouldBe(0);
        authorityCalls.ShouldBe(1);
        gateway.DisableSubmissions.ShouldBe(0);

        authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.WaitForAssertion(() => gateway.DisableSubmissions.ShouldBe(1));
        authorityCalls.ShouldBe(1);
        gateway.EnableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Submit_rechecks_context_after_async_activity_lease_acquisition()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        RegisterServices(gateway);
        var acquisition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<bool> leaseRequests = [];
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.CommandActivityLease, active =>
            {
                leaseRequests.Add(active);
                return active ? acquisition.Task : Task.FromResult(true);
            })
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"))));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => leaseRequests.ShouldBe([true]));

        cut.Render(parameters => parameters.Add(
            component => component.Detail,
            Detail("tenant.beta", TenantStatus.Active)));
        acquisition.SetResult(true);

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Lifecycle.Unavailable.PreviewChanged"));
        leaseRequests.ShouldBe([true, false]);
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Submit_refused_activity_lease_dispatches_nothing_and_names_the_aggregate_as_in_flight()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        TenantLifecycleAttemptTracker tracker = new();
        RegisterServices(gateway, tracker);
        List<bool> leaseRequests = [];
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.CommandActivityLease, active =>
            {
                leaseRequests.Add(active);
                return Task.FromResult(!active);
            })
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"))));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Commands.Unavailable.AggregateInFlight"));
        leaseRequests.ShouldBe([true]);
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
        tracker.FindDispatchIntent("tenant.alpha").ShouldBeNull();
    }

    [Theory]
    [InlineData("dispatch", 0, 1, "Tenants.Lifecycle.UnableToVerify.StatusTimeout", TenantCommandAuditState.AuditDelayed)]
    [InlineData("status", 1, 1, "Tenants.Lifecycle.UnableToVerify.StatusTimeout", TenantCommandAuditState.AuditDelayed)]
    // A timed-out proof read is a projection read, so the audit dimension never turns unavailable. The terminal
    // outcome releases the attempt, though, so the pending record nothing will advance any more is delayed.
    [InlineData("proof", 1, 2, "Tenants.Lifecycle.UnableToVerify.ProofRead", TenantCommandAuditState.AuditDelayed)]
    public void Attempt_deadline_terminalizes_and_releases_activity_for_never_completing_io(
        string stage,
        int expectedStatusCalls,
        int expectedProofCalls,
        string expectedSafeMessageKey,
        TenantCommandAuditState expectedAuditState)
    {
        var neverSubmission = new TaskCompletionSource<TenantCommandSubmissionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverStatus = new TaskCompletionSource<TenantCommandStatusResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverProof = new TaskCompletionSource<TenantDetailSnapshot?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedCancellation = default;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1),
            SubmissionProvider = stage == "dispatch"
                ? (_, _, cancellationToken) =>
                {
                    observedCancellation = cancellationToken;
                    return neverSubmission.Task;
                }
                : null,
            StatusProvider = stage == "status"
                ? (_, cancellationToken) =>
                {
                    observedCancellation = cancellationToken;
                    return neverStatus.Task;
                }
                : null,
        };
        List<bool> activity = [];
        int proofCalls = 0;
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, cancellationToken) =>
            {
                proofCalls++;
                if (stage == "proof" && proofCalls == 2)
                {
                    observedCancellation = cancellationToken;
                    return neverProof.Task;
                }

                return Task.FromResult<TenantDetailSnapshot?>(Proof(
                    "tenant.alpha",
                    TenantStatus.Active,
                    "tenant-sequence:41"));
            }));
        cut.Instance.ExternalOperationTimeout = TimeSpan.FromMilliseconds(30);

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe(expectedSafeMessageKey);
        // Dispatch, status, and proof-read deadlines all end the attempt without proof, so its audit record is delayed.
        cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        activity.ShouldBe([true, false]);
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(expectedStatusCalls);
        proofCalls.ShouldBe(expectedProofCalls);
        observedCancellation.CanBeCanceled.ShouldBeTrue();
        cut.WaitForAssertion(() => observedCancellation.IsCancellationRequested.ShouldBeTrue());
    }

    [Fact]
    public void Authority_preflight_deadline_cancels_the_forwarded_token_and_blocks_with_missing_permission()
    {
        var neverAuthority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedCancellation = default;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, cancellationToken =>
            {
                observedCancellation = cancellationToken;
                return neverAuthority.Task;
            })
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(Proof(
                "tenant.alpha",
                TenantStatus.Active,
                "tenant-sequence:41"))));
        cut.Instance.ExternalOperationTimeout = TimeSpan.FromMilliseconds(30);

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Lifecycle.Unavailable.MissingPermission"));
        gateway.DisableSubmissions.ShouldBe(0);
        observedCancellation.CanBeCanceled.ShouldBeTrue();
        cut.WaitForAssertion(() => observedCancellation.IsCancellationRequested.ShouldBeTrue());
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("proof")]
    public void Preflight_stray_cancellation_unrelated_to_the_deadline_degrades_safely_instead_of_crashing(
        string stage)
    {
        // A cancellation that does not come from AwaitWithinDeadlineAsync's own timeoutCts (e.g. an internal
        // HttpClient timeout inside an injected provider) must not propagate raw out of the submit handler --
        // it must be treated like any other unexpected fault, not rethrown and left to tear down the circuit.
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"),
        };
        RegisterServices(gateway);
        var recordingLoggerFactory = new RecordingLoggerFactory();
        Services.AddSingleton<ILoggerFactory>(recordingLoggerFactory);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => stage == "authority"
                ? Task.FromException<TenantLifecycleAuthorizationReflectionState>(
                    new OperationCanceledException("unrelated cancellation"))
                : Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => stage == "proof"
                ? Task.FromException<TenantDetailSnapshot?>(new OperationCanceledException("unrelated cancellation"))
                : Task.FromResult<TenantDetailSnapshot?>(Proof(
                    "tenant.alpha",
                    TenantStatus.Active,
                    "tenant-sequence:41"))));

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");

        Should.NotThrow(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey.ShouldNotBeNullOrWhiteSpace());
        gateway.DisableSubmissions.ShouldBe(0);
        cut.WaitForAssertion(() => recordingLoggerFactory.Entries.ShouldContain(
            entry => entry.ReasonCode == $"lifecycle-{stage}-preflight-fault" && entry.Exception is OperationCanceledException));
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<(string ReasonCode, Exception? Exception)> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(RecordingLoggerFactory factory) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                string reasonCode = state is IEnumerable<KeyValuePair<string, object>> pairs
                    ? pairs.FirstOrDefault(pair => pair.Key == "LifecycleReasonCode").Value?.ToString() ?? string.Empty
                    : string.Empty;
                factory.Entries.Add((reasonCode, exception));
            }
        }
    }

    [Fact]
    public void Disposing_mid_dispatch_still_releases_the_lease_once_the_pending_call_settles()
    {
        // The old guard (`if (_disposed) { return !isActive; }`) made a disposed-component release a silent
        // no-op. Dispose while the lease is held and the gateway call is still pending, then let it settle,
        // and confirm the release callback still fires with false.
        var pendingSubmission = new TaskCompletionSource<TenantCommandSubmissionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubTenantCommandGateway
        {
            SubmissionProvider = (_, _, _) => pendingSubmission.Task,
        };
        RegisterServices(gateway);
        List<bool> leaseCalls = [];
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41")))
            .Add(component => component.CommandActivityLease, isActive =>
            {
                leaseCalls.Add(isActive);
                return Task.FromResult(true);
            }));

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => leaseCalls.ShouldContain(true));
        cut.Dispose();
        pendingSubmission.SetResult(TenantCommandSubmissionResult.Accepted("ignored", "correlation-life"));

        SpinWait.SpinUntil(() => leaseCalls.Contains(false), TimeSpan.FromSeconds(5))
            .ShouldBeTrue("the release callback should still fire with false after the pending dispatch settles");
    }

    [Fact]
    public void Refresh_arriving_mid_submit_is_queued_not_dropped_and_is_replayed_once_dispatch_settles()
    {
        // Before this commit, RefreshStatusAsync unconditionally dropped a nudge/refresh that arrived while
        // _isSubmitting was true. It must now be queued (never lost) and replayed once submission settles.
        var pendingSubmission = new TaskCompletionSource<TenantCommandSubmissionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string? dispatchedMessageId = null;
        var gateway = new StubTenantCommandGateway
        {
            SubmissionProvider = (_, messageId, _) =>
            {
                dispatchedMessageId = messageId;
                return pendingSubmission.Task;
            },
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent));
        gateway.StatusCalls.ShouldBe(0);
        dispatchedMessageId.ShouldNotBeNullOrWhiteSpace();

        // Arrives mid-submit: must be queued rather than silently dropped, and must not call the gateway while
        // dispatch still owns the attempt.
        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();
        gateway.StatusCalls.ShouldBe(0);
        GetPrivateField<TenantLifecycleCommandFlow, int>(cut.Instance, "_refreshReplayRequested").ShouldBe(1);

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Accepted(dispatchedMessageId!, "correlation-life"));

        cut.WaitForAssertion(() => gateway.StatusCalls.ShouldBeGreaterThan(0));
        cut.WaitForAssertion(() => GetPrivateField<TenantLifecycleCommandFlow, int>(cut.Instance, "_refreshReplayRequested")
            .ShouldBe(0));
    }

    [Theory]
    [MemberData(nameof(AmbiguousSubmissionExceptions))]
    public void Ambiguous_submission_failure_is_localized_and_retry_reuses_dispatch_identity(
        Exception submissionException)
    {
        var gateway = new StubTenantCommandGateway
        {
            SubmissionException = submissionException,
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent));
        gateway.DisableSubmissions.ShouldBe(1);
        activity.ShouldBe([true]);
        cut.VisibleText().ShouldNotContain("raw transport", Case.Insensitive);
        cut.VisibleText().ShouldNotContain("token", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-recovery']").TextContent
            .ShouldContain("same command identity", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-recovery']").TextContent
            .ShouldContain($"{(int)TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration.TotalMinutes} minutes");
        string firstMessageId = gateway.LastDisableMessageId!;

        gateway.SubmissionException = null;
        gateway.Submission = TenantCommandSubmissionResult.Accepted(
            "ignored-by-stub",
            "correlation-life");
        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => gateway.DisableSubmissions.ShouldBe(2));
        gateway.LastDisableMessageId.ShouldBe(firstMessageId);
    }

    public static TheoryData<Exception> AmbiguousSubmissionExceptions
        => new()
        {
            new InvalidOperationException("raw transport stack token"),
            new HttpRequestException("raw transport stack token"),
            new OperationCanceledException("raw timeout stack token"),
        };

    [Fact]
    public void Whitespace_ambiguous_resource_key_uses_localized_fallback()
    {
        var gateway = new StubTenantCommandGateway
        {
            SubmissionProvider = (_, messageId, _) => Task.FromResult(new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.RequestSent,
                MessageId: messageId,
                SafeMessageKey: "   ",
                IsAmbiguousFailure: true)),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Lifecycle.SubmissionEvidence.Ambiguous"));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
    }

    [Fact]
    public void Expired_redispatch_terminalizes_without_calling_gateway()
    {
        var gateway = new StubTenantCommandGateway();
        List<bool> activity = [];
        RegisterServices(gateway);
        TenantDetail detail = Detail("tenant.alpha", TenantStatus.Active);
        var intent = new TenantLifecycleCommandRequest(
            "tenant.alpha",
            TenantLifecycleOperation.DisableTenant);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow(activity);
        TenantLifecycleCommandSnapshot expired = TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(intent, detail, "tenant-sequence:41")
            .RequestSent(
                intent,
                detail,
                "tenant-sequence:41",
                "message-life",
                DateTimeOffset.UtcNow - TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration);
        SetPrivateField(cut.Instance, "_snapshot", expired);
        SetPrivateField(cut.Instance, "_hasRaisedCommandActivity", true);

        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Lifecycle.UnableToVerify.StatusTimeout"));
        // Retention expiry delays the audit record; it is not an unavailable or unverifiable status.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        gateway.DisableSubmissions.ShouldBe(0);
        activity.ShouldBe([false]);
    }

    [Fact]
    public void Retained_adoption_retries_after_a_missed_lookup_and_resets_for_a_literal_tenant_change()
    {
        var tracker = new TenantLifecycleAttemptTracker();
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(
                CommandStatus.Processing,
                HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway, tracker);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);

        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        cut.Render();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.MessageId.ShouldBe("message-life"));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);

        TenantDetail beta = Detail("tenant.beta", TenantStatus.Active) with { Name = "Beta" };
        TenantLifecycleAvailability betaAvailability = AvailableLifecycle(
            TenantStatus.Active,
            TenantLifecycleOperation.DisableTenant) with
        {
            TenantId = "tenant.beta",
            Evidence = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
            {
                TenantId = "tenant.beta",
            },
        };
        cut.Render(parameters => parameters
            .Add(component => component.Detail, beta)
            .Add(component => component.Availability, betaAvailability)
            .Add(component => component.ProjectionVersion, "tenant-sequence:51")
            .Add(component => component.IsCommandSurfaceAvailable, true));

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.Intent.ShouldNotBeNull().TenantId.ShouldBe("tenant.beta");
        cut.Instance.Snapshot.MessageId.ShouldBeNull();
    }

    [Fact]
    public void Preview_rendering_stays_on_one_capture_until_refresh_atomically_rebaselines_it()
    {
        TenantDetailSnapshot proof = Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41");
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(proof)));

        TenantDetail mutated = Detail("tenant.alpha", TenantStatus.Active) with { Name = "Mutable page name" };
        cut.Render(parameters => parameters
            .Add(component => component.Detail, mutated)
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:42")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(proof)));

        cut.Find("[data-testid='tenants-lifecycle-preview-identity']").TextContent
            .ShouldContain("Alpha");
        cut.Find("[data-testid='tenants-lifecycle-preview-identity']").TextContent
            .ShouldNotContain("Mutable page name");

        proof = TenantDetailSnapshot.Ready(
            mutated with { Name = "Authoritative refreshed name" },
            eTag: null,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:42");
        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-preview-identity']")
            .TextContent.ShouldContain("Authoritative refreshed name"));
        cut.Instance.Snapshot.PreviewProjectionVersion.ShouldBe("tenant-sequence:42");
    }

    [Fact]
    public void Invalid_lifecycle_operation_is_blocked_before_aggregate_admission()
    {
        var gateway = new StubTenantCommandGateway();
        List<bool> activity = [];
        RegisterServices(gateway);
        TenantLifecycleAvailability invalid = AvailableLifecycle(
            TenantStatus.Active,
            TenantLifecycleOperation.DisableTenant) with
        {
            Operation = (TenantLifecycleOperation)999,
        };
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, invalid)
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.CommandActivityLease, active =>
            {
                activity.Add(active);
                return Task.FromResult(true);
            }));

        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
        activity.ShouldBeEmpty();
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.PreviewIncomplete");
    }

    [Fact]
    public void Invalid_retained_snapshot_shape_fails_safely_at_the_component_boundary()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        TenantLifecycleCommandSnapshot malformed = cut.Instance.Snapshot with
        {
            State = TenantCommandLifecycleState.RequestSent,
            MessageId = null,
            CorrelationId = null,
            AttemptStartedAtUtc = DateTimeOffset.UtcNow,
        };
        MethodInfo setSnapshot = typeof(TenantLifecycleCommandFlow)
            .GetMethod("SetSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Should.NotThrow(() => setSnapshot.Invoke(cut.Instance, [malformed]));

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.TrackingMismatch");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Fact]
    public void Tracker_rejection_of_an_expired_attempt_is_a_timeout_with_a_delayed_audit_record()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        TenantLifecycleCommandSnapshot expired = PendingLifecycleAttempt() with
        {
            AttemptStartedAtUtc = DateTimeOffset.UtcNow
                - TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration
                - TimeSpan.FromMinutes(1),
        };
        MethodInfo setSnapshot = typeof(TenantLifecycleCommandFlow)
            .GetMethod("SetSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;

        cut.InvokeAsync(() => setSnapshot.Invoke(cut.Instance, [expired]));

        // The tracker refuses the expired attempt: that is retention expiry, not a tracking mismatch.
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.StatusTimeout");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
    }

    [Fact]
    public void Accepted_tracking_mismatch_fails_closed_without_status_poll_and_releases_activity()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", string.Empty),
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.TrackingMismatch"));
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(0);
        activity.ShouldBe([true, false]);
    }

    [Fact]
    public void Status_transport_failure_is_retryable_and_retains_activity()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            StatusException = new InvalidOperationException("raw status payload token"),
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            gateway.StatusCalls.ShouldBe(1);
            TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
            snapshot.SafeMessage.ShouldNotBeNull().ShouldContain("could not be read yet", Case.Insensitive);
            snapshot.SafeMessageKey.ShouldBeNull();
        });
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Accepted);
        gateway.DisableSubmissions.ShouldBe(1);
        activity.ShouldBe([true]);
        cut.VisibleText().ShouldNotContain("raw status", Case.Insensitive);
        cut.VisibleText().ShouldNotContain("token", Case.Insensitive);
    }

    [Fact]
    public void Pending_status_propagation_retains_attempt_and_activity_without_redispatch()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = TenantCommandStatusResult.Pending("Status is not available yet."),
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            gateway.StatusCalls.ShouldBe(1);
            TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
            snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
            snapshot.SafeMessage.ShouldBeNull();
            snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.StatusEvidence.Pending");
        });
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(1);
        activity.ShouldBe([true]);
        cut.Find("[data-testid='tenants-lifecycle-safe-message']").TextContent
            .ShouldContain("not available yet", Case.Insensitive);
    }

    [Fact]
    public void Pending_status_can_be_explicitly_abandoned_and_releases_activity()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = TenantCommandStatusResult.Pending("Status is not available yet."),
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.StatusCalls.ShouldBe(1));
        cut.Find("#tenants-lifecycle-confirmation-help").TextContent
            .ShouldContain("cannot close", Case.Insensitive);

        cut.Find("[data-testid='tenants-lifecycle-abandon']").Click();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(1);
        activity.ShouldBe([true, false]);
        cut.Find("[data-testid='tenants-lifecycle-safe-message']").TextContent
            .ShouldContain("stopped explicitly", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("form").Submit();
        gateway.DisableSubmissions.ShouldBe(1);
    }

    [Fact]
    public void Expired_retained_attempt_is_pruned_on_gated_launcher_access_and_does_not_keep_activity()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = TenantCommandStatusResult.Pending("Status is not available yet."),
        };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var tracker = new TenantLifecycleAttemptTracker(() => now);
        tracker.Remember(PendingLifecycleAttempt() with { AttemptStartedAtUtc = now }).ShouldBeTrue();
        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        List<bool> activity = [];
        RegisterServices(gateway, tracker);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        tracker.Find("tenant.alpha").ShouldBeNull();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();
        activity.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(CommandStatus.Rejected, TenantCommandLifecycleState.Rejected)]
    [InlineData(CommandStatus.TimedOut, TenantCommandLifecycleState.UnableToVerify)]
    [InlineData(CommandStatus.PublishFailed, TenantCommandLifecycleState.Degraded)]
    public void Terminal_non_success_refresh_invokes_parent_authoritative_refresh_without_repolling(
        CommandStatus status,
        TenantCommandLifecycleState expectedState)
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(status, HasVerifiedCommandIdentity: true),
            PreserveStatusIdentityEvidence = true,
        };
        int parentRefreshes = 0;
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnProjectionRefreshRequested, () => parentRefreshes++)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"))));
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(expectedState));
        gateway.StatusCalls.ShouldBe(1);
        parentRefreshes.ShouldBe(0);

        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => parentRefreshes.ShouldBe(1));
        gateway.StatusCalls.ShouldBe(1);
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State.ShouldBe(expectedState);
    }

    [Fact]
    public void Retained_attempt_without_projection_provider_terminalizes_clears_tracker_and_is_not_repolled_on_remount()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        List<bool> activity = [];
        RegisterServices(gateway, tracker);

        IRenderedComponent<TenantLifecycleActionAvailability> first = RenderWithoutProofProvider(activity);

        first.WaitForAssertion(() => first.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        first.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Lifecycle.UnableToVerify.ProofRead");
        tracker.Find("tenant.alpha").ShouldBeNull();
        gateway.StatusCalls.ShouldBe(1);
        activity.ShouldBe([true, false]);
        first.Dispose();

        IRenderedComponent<TenantLifecycleActionAvailability> remounted = RenderWithoutProofProvider([]);

        remounted.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        tracker.Find("tenant.alpha").ShouldBeNull();
        gateway.StatusCalls.ShouldBe(1);

        IRenderedComponent<TenantLifecycleActionAvailability> RenderWithoutProofProvider(List<bool> activityEvents)
            => Render<TenantLifecycleActionAvailability>(parameters => parameters
                .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
                .Add(component => component.TenantId, "tenant.alpha")
                .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
                .Add(component => component.ProjectionVersion, "tenant-sequence:41")
                .Add(component => component.CurrentStatus, TenantStatus.Active)
                .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
                .Add(component => component.Freshness, ReadModelFreshnessState.Current)
                .Add(component => component.IsCommandSurfaceConnected, true)
                .Add(component => component.IsCommandSurfaceAvailable, true)
                .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
                .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
                .Add(component => component.OnCommandActivityChanged, active => activityEvents.Add(active)));
    }

    [Fact]
    public void Retained_attempt_without_projection_provider_reports_its_released_audit_record_delayed()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.EventsStored),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        // No proof reader is a provenance failure, not a failed audit read, so the audit state never turns
        // unavailable. The unverified lifecycle outcome is terminal, though: the attempt is released, nothing will
        // advance its pending audit record any more, and the record is reported delayed rather than pending forever.
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.ProofRead"), TimeSpan.FromSeconds(5));
        TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
        snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        snapshot.RetainsAttempt.ShouldBeFalse();
        snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("delayed"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Ambiguous_dispatch_offers_refresh_but_no_continue_read_only_until_the_attempt_is_released()
    {
        var gateway = new StubTenantCommandGateway
        {
            SubmissionException = new InvalidOperationException("raw dispatch failure"),
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);
        const string audit = "[data-testid='tenants-lifecycle-audit']";

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        // The retained ambiguous attempt can be re-queried, and it cannot be closed: Refresh is offered, Continue
        // read-only is not.
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        cut.FindAll($"{audit} [data-recovery-verb='continuereadonly']").ShouldBeEmpty();

        // Abandoning releases the attempt: nothing is left to re-query, and Continue read-only closes the dialog.
        cut.Find("[data-testid='tenants-lifecycle-abandon']").Click();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.RetainsAttempt.ShouldBeFalse());
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.AuditState
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-recovery-verb='refresh']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='continuereadonly']"), TimeSpan.FromSeconds(5));
        cut.Find($"{audit} [data-recovery-verb='continuereadonly']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty());
        gateway.DisableSubmissions.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that audit refresh restores focus when a readable processing status unmounts its recovery control.
    /// </summary>
    [Fact]
    public void AuditRefreshThatResolvesToNotStartedMovesFocusToTheLifecycleSection()
    {
        int statusCalls = 0;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            StatusProvider = (_, _) => Task.FromResult(++statusCalls == 1
                ? TenantCommandStatusResult.RetryableFailure("Command status could not be read yet.")
                : new TenantCommandStatusResult(CommandStatus.Processing, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        const string audit = "[data-testid='tenants-lifecycle-audit']";
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        string lifecycleReferenceId = CapturedElementReferenceId(cut.Instance, "_lifecycleElement");
        int focusCallsBeforeRefresh = FocusedElementIds().Count;

        cut.Find($"{audit} [data-recovery-verb='refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted));
        cut.Instance.Snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Polite);
        cut.Instance.Snapshot.FocusTarget.ShouldNotBe(TenantCommandFocusTarget.Lifecycle);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-testid='tenants-audit-availability']").ShouldBeEmpty());
        cut.WaitForAssertion(() => FocusedElementIds().Count.ShouldBeGreaterThan(focusCallsBeforeRefresh));
        FocusedElementIds().Last().ShouldBe(lifecycleReferenceId);
        gateway.StatusCalls.ShouldBe(2);
        gateway.DisableSubmissions.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_lookup_wait_for_its_replay_and_never_exhaust_the_retry_limit()
    {
        TaskCompletionSource nudgeLookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseNudgeLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusCalls = 0;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            StatusProvider = async (_, _) =>
            {
                if (Interlocked.Increment(ref statusCalls) == 2)
                {
                    nudgeLookupStarted.SetResult();
                    await releaseNudgeLookup.Task.ConfigureAwait(false);
                }

                return new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true);
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        const string refresh = "[data-testid='tenants-lifecycle-audit'] [data-recovery-verb='refresh']";

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending));
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

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
        Volatile.Read(ref statusCalls).ShouldBe(3);
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(1);

        // The completed merged recovery used one retry. Two later recoveries must complete and consume the rest.
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusCalls).ShouldBe(4);
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusCalls).ShouldBe(5);
        cut.FindAll(refresh).ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-availability-retry-limit']");
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_redispatch_wait_for_it_and_never_exhaust_the_retry_limit()
    {
        var gateway = new StubTenantCommandGateway
        {
            SubmissionException = new InvalidOperationException("raw dispatch failure"),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = RenderLifecycleFlow();
        const string refresh = "[data-testid='tenants-lifecycle-audit'] [data-recovery-verb='refresh']";

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        // The running refresh re-dispatches the retained RequestSent attempt, and that dispatch hangs.
        TaskCompletionSource dispatchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseDispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.SubmissionException = null;
        gateway.SubmissionProvider = async (_, messageId, _) =>
        {
            dispatchStarted.TrySetResult();
            await releaseDispatch.Task.ConfigureAwait(false);
            return TenantCommandSubmissionResult.Ambiguous(messageId, "Tenants.Lifecycle.SubmissionEvidence.Ambiguous");
        };
        Task nudge = cut.InvokeAsync(() => cut.Instance.HandleAuthoritativeRefreshNudgeAsync());
        await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        merged.IsCompleted.ShouldBeFalse();

        // The click merged into the running re-dispatch waits for it; nothing counted as an unchanged retry yet.
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        releaseDispatch.SetResult();
        await nudge.WaitAsync(TimeSpan.FromSeconds(5));
        await merged.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        gateway.DisableSubmissions.ShouldBeGreaterThanOrEqualTo(2);

        int dispatchesAfterMerged = gateway.DisableSubmissions;
        // The gate must reopen after the merged click, and each completed recovery advances the retry budget.
        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        gateway.DisableSubmissions.ShouldBe(dispatchesAfterMerged + 1);
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        await cut.Find(refresh).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));
        gateway.DisableSubmissions.ShouldBe(dispatchesAfterMerged + 2);
        cut.FindAll(refresh).ShouldBeEmpty();
        cut.Find("[data-testid='tenants-audit-availability-retry-limit']");
    }

    [Fact]
    public void Gateway_status_identity_mismatch_is_terminal_without_projection_success()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: false),
            PreserveStatusIdentityEvidence = true,
        };
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = RenderLifecycleAvailability(activity);

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.TrackingMismatch"));
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        gateway.StatusCalls.ShouldBe(1);
        activity.ShouldBe([true, false]);
    }

    [Fact]
    public void Null_submit_proof_is_a_proof_read_failure_and_does_not_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, HighImpactEvidence(TenantHighImpactAction.DisableTenant))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(null)));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
        snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.ProofRead");
        snapshot.SafeMessageKey.ShouldNotBe("Tenants.Lifecycle.Unavailable.PreviewChanged");
        cut.Find("[data-testid='tenants-lifecycle-refresh']").GetAttribute("disabled").ShouldBeNull();
    }

    [Fact]
    public void Unavailable_submit_proof_is_a_proof_read_failure_and_does_not_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, HighImpactEvidence(TenantHighImpactAction.DisableTenant))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                TenantDetailSnapshot.Unavailable("Raw proof failure."))));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
        snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.ProofRead");
        snapshot.RecoveryKey.ShouldBe("Tenants.HighImpact.Recovery.StaleData");
    }

    [Fact]
    public void Preview_refresh_rebaselines_before_submit()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(CommandStatus.Received),
        };
        TenantDetailSnapshot proof = Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41");
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(proof)));
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();

        proof = Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:42");
        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.PreviewProjectionVersion.ShouldBe("tenant-sequence:42"));
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.DisableSubmissions.ShouldBe(1));
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.BaselineProjectionVersion
            .ShouldBe("tenant-sequence:42");
    }

    [Fact]
    public void Retained_opposite_intent_blocks_submit_without_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        TenantDetail disabled = Detail("tenant.alpha", TenantStatus.Disabled);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, disabled)
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Disabled,
                TenantLifecycleOperation.EnableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:42")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Disabled, "tenant-sequence:42"))));

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.RetainedDifferentIntent");
        cut.Instance.Snapshot.RecoveryKey.ShouldBe("Tenants.Lifecycle.Retained.Recovery");
        gateway.EnableSubmissions.ShouldBe(0);
        gateway.DisableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Missing_typed_evidence_blocks_submit_even_when_legacy_fields_claim_availability()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        var legacyOnly = new TenantLifecycleAvailability(
            "tenant.alpha",
            TenantStatus.Active,
            TenantLifecycleOperation.DisableTenant,
            ReadModelFreshnessState.Current,
            TenantDetailSurfaceKind.Ready,
            IsCommandSurfaceConnected: true,
            TenantLifecycleGovernanceReadiness.Ready,
            TenantLifecycleAuthorizationReflectionState.Authorized,
            IsUnavailable: false,
            TenantLifecycleUnavailableReasonCategory.None,
            "Tenants.HighImpact.Available",
            ExpectedDomainOutcomeKey: null,
            TenantCommandFocusTarget.Submit,
            TenantCommandLiveRegionPoliteness.Polite,
            Evidence: null);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, legacyOnly)
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true));

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.PreviewIncomplete");
    }

    [Fact]
    public void Post_acceptance_proof_cancellation_preserves_the_tracked_attempt_and_activity()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1),
        };
        int proofReads = 0;
        List<bool> activity = [];
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, HighImpactEvidence(TenantHighImpactAction.DisableTenant))
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => ++proofReads == 1
                ? Task.FromResult<TenantDetailSnapshot?>(Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"))
                : Task.FromException<TenantDetailSnapshot?>(new OperationCanceledException("proof read canceled"))));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.ProofRead"));
        TenantLifecycleCommandSnapshot snapshot = cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot;
        snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        snapshot.RetainsAttempt.ShouldBeTrue();
        snapshot.MessageId.ShouldBe(gateway.LastDisableMessageId);
        activity.ShouldBe([true]);
    }

    [Fact]
    public async Task Authoritative_refresh_nudge_reconciles_without_redispatch_or_recursive_parent_refresh()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            Status = new TenantCommandStatusResult(CommandStatus.Received),
        };
        TenantStatus projectionStatus = TenantStatus.Active;
        int parentRefreshes = 0;
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnProjectionRefreshRequested, () => { parentRefreshes++; })
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(
                    request.TenantId,
                    projectionStatus,
                    projectionStatus is TenantStatus.Active ? "tenant-sequence:41" : "tenant-sequence:42"))));
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));

        gateway.Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1);
        projectionStatus = TenantStatus.Disabled;
        await cut.InvokeAsync(cut.Instance.ApplySignalRNudgeAsync);

        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Confirmed);
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(2);
        parentRefreshes.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_arriving_during_an_in_flight_refresh_is_replayed_without_losing_confirmation()
    {
        TaskCompletionSource<TenantCommandStatusResult> blockedStatus = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int statusRead = 0;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            StatusProvider = (_, _) => ++statusRead switch
            {
                1 => Task.FromResult(new TenantCommandStatusResult(
                    CommandStatus.Received,
                    HasVerifiedCommandIdentity: true)),
                2 => blockedStatus.Task,
                _ => Task.FromResult(new TenantCommandStatusResult(
                    CommandStatus.Completed,
                    EventCount: 1,
                    HasVerifiedCommandIdentity: true)),
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(
                    request.TenantId,
                    gateway.StatusCalls >= 3 ? TenantStatus.Disabled : TenantStatus.Active,
                    gateway.StatusCalls >= 3 ? "tenant-sequence:42" : "tenant-sequence:41"))));
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.StatusCalls.ShouldBe(1));

        Task firstRefresh = cut.InvokeAsync(() => cut.Find("[data-testid='tenants-lifecycle-refresh']").Click());
        SpinWait.SpinUntil(() => gateway.StatusCalls == 2, TimeSpan.FromSeconds(2)).ShouldBeTrue();
        await cut.InvokeAsync(cut.Instance.ApplySignalRNudgeAsync);
        blockedStatus.SetResult(new TenantCommandStatusResult(
            CommandStatus.EventsStored,
            HasVerifiedCommandIdentity: true));
        await firstRefresh;

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.StatusCalls.ShouldBe(3);
        gateway.DisableSubmissions.ShouldBe(1);
    }

    [Fact]
    public async Task SignalR_during_submit_reconciliation_is_serialized_and_replayed_to_confirmation()
    {
        var blockedStatus = new TaskCompletionSource<TenantCommandStatusResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int statusRead = 0;
        int proofRead = 0;
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored-by-stub", "correlation-life"),
            StatusProvider = (_, _) => ++statusRead == 1
                ? blockedStatus.Task
                : Task.FromResult(new TenantCommandStatusResult(
                    CommandStatus.Completed,
                    EventCount: 1,
                    HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(
                TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) =>
            {
                proofRead++;
                bool confirmed = proofRead >= 3;
                return Task.FromResult<TenantDetailSnapshot?>(Proof(
                    request.TenantId,
                    confirmed ? TenantStatus.Disabled : TenantStatus.Active,
                    confirmed ? "tenant-sequence:42" : "tenant-sequence:41"));
            }));
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => gateway.StatusCalls.ShouldBe(1));

        await cut.InvokeAsync(cut.Instance.ApplySignalRNudgeAsync);
        blockedStatus.SetResult(new TenantCommandStatusResult(
            CommandStatus.EventsStored,
            HasVerifiedCommandIdentity: true));

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.DisableSubmissions.ShouldBe(1);
        gateway.StatusCalls.ShouldBe(2);
        proofRead.ShouldBe(3);
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.LastConfirmedStatus
            .ShouldBe(TenantStatus.Disabled);
    }

    [Fact]
    public void Open_lifecycle_flow_rechecks_projection_lifecycle_when_parent_evidence_changes()
    {
        var gateway = new StubTenantCommandGateway();
        int proofCalls = 0;
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.ProjectionEvidenceProvider, (request, _) =>
            {
                proofCalls++;
                return Task.FromResult<TenantDetailSnapshot?>(Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"));
            }));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-preview']");
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-confirm']").GetAttribute("disabled").ShouldBeNull();

        cut.Render(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Stale));

        cut.Find("[data-testid='tenants-lifecycle-flow-unavailable-reason']").TextContent
            .ShouldContain("current authoritative data", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-recovery']").TextContent
            .ShouldContain("Refresh the authoritative tenant data", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-recovery']").TextContent
            .ShouldNotContain("submit only after confirmation is exact", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("form").Submit();
        gateway.DisableSubmissions.ShouldBe(0);
        proofCalls.ShouldBe(0);
    }

    [Fact]
    public void Command_surface_unavailable_disables_lifecycle_action_without_gateway_call()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"),
        };
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, false)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("#tenants-lifecycle-disable-reason").TextContent.ShouldContain("A tenant command is already in flight or the lifecycle");
        cut.Find("#tenants-lifecycle-disable-reason").GetAttribute("data-reason-category")
            .ShouldBe(TenantHighImpactReasonCategoryNames.InFlightOrCommandSurface);
        cut.Find("[data-testid='tenants-lifecycle-disable-recovery']").TextContent
            .ShouldContain("Wait for the current tenant command to finish", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();

        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Markup.ShouldNotContain("correlation-life", Case.Insensitive);
    }

    [Fact]
    public void Gateway_without_tracked_dispatch_capability_blocks_before_confirmation_or_preflight()
    {
        var gateway = new StubTenantCommandGateway
        {
            SupportsTrackedLifecycleDispatch = false,
        };
        int authorityCalls = 0;
        int proofCalls = 0;
        RegisterServices(gateway);
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.AuthorizationReflectionProvider, _ =>
            {
                authorityCalls++;
                return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            })
            .Add(component => component.ProjectionEvidenceProvider, (request, _) =>
            {
                proofCalls++;
                return Task.FromResult<TenantDetailSnapshot?>(Proof(
                    request.TenantId,
                    TenantStatus.Active,
                    "tenant-sequence:41"));
            }));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();

        cut.Find("[data-testid='tenants-lifecycle-flow-unavailable-reason']")
            .TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.Find("[data-testid='tenants-lifecycle-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("form").Submit();
        authorityCalls.ShouldBe(0);
        proofCalls.ShouldBe(0);
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.EnableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Missing_projection_version_disables_lifecycle_with_visible_refresh_recovery()
    {
        var gateway = new StubTenantCommandGateway();
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("#tenants-lifecycle-disable-reason").TextContent
            .ShouldContain("current authoritative data", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable-recovery']").TextContent
            .ShouldContain("Refresh the authoritative tenant data", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
    }

    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission, true, "verify your role")]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Authorized, false, "lifecycle command connection")]
    public void Retained_attempt_resume_requires_authority_and_command_connectivity(
        TenantLifecycleAuthorizationReflectionState authority,
        bool isConnected,
        string expectedRecovery)
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, isConnected)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, authority)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("#tenants-lifecycle-disable-reason").GetAttribute("data-reason-category")
            .ShouldBe(TenantHighImpactReasonCategoryNames.RetainedAttempt);
        cut.Find("[data-testid='tenants-lifecycle-disable-recovery']").TextContent
            .ShouldContain(expectedRecovery, Case.Insensitive);
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public void Retained_attempt_blocked_from_resume_offers_a_launcher_abandon_control()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        List<bool> activity = [];

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, false)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active)));

        // The flow that owns the in-dialog abandon control cannot be opened in this state, so the launcher
        // must offer the escape or the attempt is unresumable and unabandonable at once.
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-lifecycle-disable-abandon-hint']").TextContent
            .ShouldContain("Stop tracking", Case.Insensitive);
        var abandon = cut.Find("[data-testid='tenants-lifecycle-disable-abandon']");
        abandon.GetAttribute("disabled").ShouldBeNull();
        abandon.GetAttribute("aria-describedby").ShouldBe("tenants-lifecycle-disable-abandon-hint");
        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("aria-describedby")
            .ShouldNotBeNull().ShouldContain("tenants-lifecycle-disable-abandon-hint");

        abandon.Click();

        tracker.Find("tenant.alpha").ShouldBeNull();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        activity.ShouldContain(false);
        gateway.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public void Retained_attempt_abandon_releases_the_lease_through_the_command_activity_lease_seam()
    {
        // Every production caller (TenantDetailPage) wires CommandActivityLease, not OnCommandActivityChanged.
        // The sibling test above only exercises the OnCommandActivityChanged fallback branch.
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        List<bool> leaseCalls = [];

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, false)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.CommandActivityLease, isActive =>
            {
                leaseCalls.Add(isActive);
                return Task.FromResult(true);
            }));

        var abandon = cut.Find("[data-testid='tenants-lifecycle-disable-abandon']");
        abandon.Click();

        tracker.Find("tenant.alpha").ShouldBeNull();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        leaseCalls.ShouldContain(false);
    }

    [Fact]
    public void Same_route_retained_attempt_expiry_lowers_the_command_activity_lease()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        // Capture the clock after the snapshot so the injected now is not earlier than the attempt start,
        // which Remember rejects.
        TenantLifecycleCommandSnapshot attempt = PendingLifecycleAttempt();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TenantLifecycleAttemptTracker tracker = new(() => now);
        tracker.Remember(attempt).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        List<bool> activity = [];

        // Disconnected so the flow never mounts: this is the case where nothing else can lower the lease,
        // because the page reclaims a stale key only from OnParametersSetAsync and the route is not changing.
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, false)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active)));

        activity.ShouldNotContain(false);

        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        cut.Render(parameters => parameters.Add(component => component.CurrentStatus, TenantStatus.Active));

        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        activity.ShouldContain(false);

        // Latched on the present-to-absent transition only, so a later render does not lower it again.
        int lowered = activity.Count(static active => !active);
        cut.Render(parameters => parameters.Add(component => component.CurrentStatus, TenantStatus.Active));
        activity.Count(static active => !active).ShouldBe(lowered);
    }

    [Fact]
    public void Same_route_retained_attempt_expiry_lowers_the_lease_through_the_command_activity_lease_seam()
    {
        // Every production caller (TenantDetailPage) wires CommandActivityLease, not OnCommandActivityChanged.
        // The sibling test above only exercises the OnCommandActivityChanged fallback branch.
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleCommandSnapshot attempt = PendingLifecycleAttempt();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TenantLifecycleAttemptTracker tracker = new(() => now);
        tracker.Remember(attempt).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        List<bool> leaseCalls = [];

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, false)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.CommandActivityLease, isActive =>
            {
                leaseCalls.Add(isActive);
                return Task.FromResult(true);
            }));

        leaseCalls.ShouldNotContain(false);

        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        cut.Render(parameters => parameters.Add(component => component.CurrentStatus, TenantStatus.Active));

        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        leaseCalls.ShouldContain(false);
    }

    [Fact]
    public void Absent_retained_attempt_never_lowers_the_command_activity_lease()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleAttemptTracker tracker = new();
        RegisterServices(gateway, tracker);
        List<bool> activity = [];

        // A submit acquires the aggregate lease before BeginDispatch registers the attempt, so the tracker is
        // legitimately empty while the lease is held. Lowering it on an absent attempt would pull the lease
        // out from under an in-flight command; only a present-to-absent transition may release.
        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, false)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Render(parameters => parameters.Add(component => component.CurrentStatus, TenantStatus.Active));
        cut.Render(parameters => parameters.Add(component => component.CurrentStatus, TenantStatus.Active));

        activity.ShouldNotContain(false);
        cut.FindAll("[data-testid='tenants-lifecycle-disable-abandon']").ShouldBeEmpty();
    }

    [Fact]
    public void Authorized_connected_retained_attempt_bypasses_read_only_freshness_and_viewport_gates()
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Processing),
        };
        TenantLifecycleAttemptTracker tracker = new();
        tracker.Remember(PendingLifecycleAttempt()).ShouldBeTrue();
        RegisterServices(gateway, tracker);
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            Freshness = TenantHighImpactFreshnessState.Stale,
            Viewport = TenantHighImpactViewportState.Unsafe,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, disable)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, false)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized));

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.MessageId.ShouldBe("message-life"));
        gateway.StatusCalls.ShouldBe(1);
        gateway.DisableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Lifecycle_command_activity_lock_is_held_until_projection_confirms()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"),
            Status = new TenantCommandStatusResult(Hexalith.EventStore.Contracts.Commands.CommandStatus.Completed, EventCount: 1),
        };
        TenantStatus projectionStatus = TenantStatus.Active;
        List<bool> activity = [];
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.Availability, AvailableLifecycle(TenantStatus.Active, TenantLifecycleOperation.DisableTenant))
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(
                    request.TenantId,
                    projectionStatus,
                    projectionStatus is TenantStatus.Active ? "tenant-sequence:41" : "tenant-sequence:42"))));

        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        activity.ShouldBe([true]);
        cut.Instance.Snapshot.LastConfirmedStatus.ShouldBe(TenantStatus.Active);

        projectionStatus = TenantStatus.Disabled;
        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() =>
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        activity.ShouldBe([true, false]);
        cut.Instance.Snapshot.LastConfirmedStatus.ShouldBe(TenantStatus.Disabled);
    }

    [Fact]
    public void Gateway_unavailable_status_refresh_releases_lifecycle_command_activity_lock()
    {
        RegisterServices();
        List<bool> activity = [];
        TenantDetail detail = Detail("tenant.alpha", TenantStatus.Active);
        TenantLifecycleAvailability availability = AvailableLifecycle(TenantStatus.Active, TenantLifecycleOperation.DisableTenant);

        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, detail)
            .Add(component => component.Availability, availability)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active)));

        TenantLifecycleCommandSnapshot tracked = TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41")
            .RequestSent(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41",
                "message-life")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"));
        SetPrivateField(cut.Instance, "_snapshot", tracked);
        SetPrivateField(cut.Instance, "_hasRaisedCommandActivity", true);
        cut.Render(parameters => parameters
            .Add(component => component.Detail, detail)
            .Add(component => component.Availability, availability)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        activity.ShouldBe([false]);
    }

    [Theory]
    [InlineData(TenantStatus.Active, TenantCommandLifecycleState.UnableToVerify, "Tenants.Lifecycle.UnableToVerify.StatusTimeout", TenantCommandAuditState.AuditDelayed)]
    [InlineData(TenantStatus.Disabled, TenantCommandLifecycleState.Confirmed, null, TenantCommandAuditState.MissingSupport)]
    public void Expired_event_evidence_gets_one_last_chance_projection_reconciliation(
        TenantStatus proofStatus,
        TenantCommandLifecycleState expectedState,
        string? expectedSafeMessageKey,
        TenantCommandAuditState expectedAuditState)
    {
        var gateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        List<bool> activity = [];
        TenantDetail detail = Detail("tenant.alpha", TenantStatus.Active);
        TenantLifecycleAvailability availability = AvailableLifecycle(
            TenantStatus.Active,
            TenantLifecycleOperation.DisableTenant);

        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, detail)
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.Availability, availability)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(
                    request.TenantId,
                    proofStatus,
                    proofStatus is TenantStatus.Disabled ? "tenant-sequence:42" : "tenant-sequence:41"))));
        TenantLifecycleCommandSnapshot tracked = TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41")
            .RequestSent(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41",
                "message-life")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-life", "correlation-life")) with
        {
            AttemptStartedAtUtc = DateTimeOffset.UtcNow
                - TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration,
        };
        SetPrivateField(cut.Instance, "_snapshot", tracked);
        SetPrivateField(cut.Instance, "_hasRaisedCommandActivity", true);

        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(expectedState);
            cut.Instance.Snapshot.SafeMessageKey.ShouldBe(expectedSafeMessageKey);
        });
        cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        gateway.StatusCalls.ShouldBe(1);
        activity.ShouldBe([false]);
    }

    [Fact]
    public void Hung_status_preserves_expired_event_evidence_for_last_chance_projection_confirmation()
    {
        var neverStatus = new TaskCompletionSource<TenantCommandStatusResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubTenantCommandGateway
        {
            StatusProvider = (_, _) => neverStatus.Task,
        };
        RegisterServices(gateway);
        List<bool> activity = [];
        TenantDetail detail = Detail("tenant.alpha", TenantStatus.Active);
        TenantLifecycleAvailability availability = AvailableLifecycle(
            TenantStatus.Active,
            TenantLifecycleOperation.DisableTenant);
        IRenderedComponent<TenantLifecycleCommandFlow> cut = Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, detail)
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.Availability, availability)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Disabled, "tenant-sequence:42"))));
        TenantLifecycleCommandSnapshot tracked = TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41")
            .RequestSent(
                new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
                detail,
                "tenant-sequence:41",
                "message-life")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"))
            .ApplyStatus(new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true)) with
        {
            AttemptStartedAtUtc = DateTimeOffset.UtcNow
                - TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration,
        };
        tracked.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        tracked.HasCommandEventEvidence.ShouldBeTrue();
        SetPrivateField(cut.Instance, "_snapshot", tracked);
        SetPrivateField(cut.Instance, "_hasRaisedCommandActivity", true);

        cut.Find("[data-testid='tenants-lifecycle-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot.LastConfirmedStatus.ShouldBe(TenantStatus.Disabled);
        gateway.StatusCalls.ShouldBe(1);
        neverStatus.Task.IsCompleted.ShouldBeFalse();
        activity.ShouldBe([false]);
    }

    [Fact]
    public void Lifecycle_rejection_displays_safe_non_success_state_without_projection_success_or_raw_details()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Rejected("The tenant lifecycle already matches the requested state.", "TenantLifecycleStateAlreadySet"),
        };
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"))));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.AlreadyApplied));
        // A rejected (already-set) command stored nothing: no audit state is implied and the control stays hidden.
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] [data-testid='tenants-lifecycle-state']").TextContent.ShouldContain("Already", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] [data-testid='tenants-lifecycle-safe-message']").TextContent.ShouldContain("already matches");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] [data-testid='tenants-lifecycle-confirmed-status']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] [data-testid='tenants-lifecycle-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("bearer ", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation", Case.Insensitive);
        // Visible text only — avoids the Fluent success-color token false positive (see VisibleText).
        cut.VisibleText().ShouldNotContain("Success", Case.Insensitive);
    }

    [Fact]
    public void Shared_evidence_renders_associated_facts_and_unknown_viewport_creates_no_flow_or_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        TenantHighImpactActionEvidence enable = HighImpactEvidence(TenantHighImpactAction.EnableTenant);
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            Viewport = TenantHighImpactViewportState.Unknown,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.EnableEvidence, enable)
            .Add(component => component.DisableEvidence, disable));

        cut.Find("[data-testid='tenants-lifecycle-enable-availability']");
        cut.Find("[data-testid='tenants-lifecycle-disable-availability']");
        cut.Find("[data-testid='tenants-lifecycle-disable-identity']").TextContent.ShouldContain("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-disable-status']").TextContent.ShouldContain("Active");
        cut.Find("[data-testid='tenants-lifecycle-disable-freshness']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-lifecycle-disable-action']").TextContent.ShouldContain("Disable");
        cut.Find("[data-testid='tenants-lifecycle-disable-reason']").TextContent.ShouldContain("viewport", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable-recovery']").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
        gateway.DisableSubmissions.ShouldBe(0);
    }

    [Fact]
    public void Eligible_typed_lifecycle_evidence_opens_the_complete_preview_before_confirmation_input()
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantHighImpactActionEvidence enable = HighImpactEvidence(TenantHighImpactAction.EnableTenant);
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            IsInputComplete = false,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, enable)
            .Add(component => component.DisableEvidence, disable));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();

        cut.Find("[data-testid='tenants-lifecycle-preview']");
        cut.FindAll("[data-testid='tenants-lifecycle-preview-item']").Count.ShouldBe(10);
        cut.Find("[data-testid='tenants-lifecycle-confirm']").GetAttribute("disabled").ShouldNotBeNull();
        cut.VisibleText().ShouldNotContain("tenant-sequence:41", Case.Sensitive);
    }

    [Theory]
    [InlineData(TenantHighImpactFreshnessState.Aging, "Aging")]
    [InlineData(TenantHighImpactFreshnessState.Refreshing, "Refreshing")]
    public void Aging_and_refreshing_typed_lifecycle_evidence_remain_eligible_with_visible_friction(
        TenantHighImpactFreshnessState freshness,
        string expectedText)
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            Freshness = freshness,
            HasCurrentBaseline = true,
            IsInputComplete = false,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, disable));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable-freshness']").TextContent.ShouldContain(expectedText);
        cut.Find("[data-testid='tenants-lifecycle-disable-reason']").TextContent
            .ShouldContain("aging or refreshing", Case.Insensitive);
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-preview']");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mismatched_tenant_or_action_evidence_fails_closed_without_rendering_foreign_facts(bool tenantMismatch)
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantHighImpactActionEvidence supplied = HighImpactEvidence(
            tenantMismatch ? TenantHighImpactAction.DisableTenant : TenantHighImpactAction.EnableTenant) with
        {
            TenantId = tenantMismatch ? "tenant.beta" : "tenant.alpha",
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.DisableEvidence, supplied));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable-identity']").TextContent.ShouldBe("tenant.alpha");
        cut.Markup.ShouldNotContain("tenant.beta", Case.Sensitive);
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Undefined_typed_evidence_fails_closed_without_throwing_or_leaking_a_resource_key()
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            Freshness = (TenantHighImpactFreshnessState)999,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, disable));

        cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-lifecycle-disable-freshness']").TextContent.ShouldBe("Unknown");
        cut.Markup.ShouldNotContain("Tenants.HighImpact.Freshness.999", Case.Sensitive);
    }

    [Theory]
    [InlineData(true, TenantHighImpactUnavailableReason.MissingConsequencePreview, "consequence preview")]
    [InlineData(false, TenantHighImpactUnavailableReason.MissingAuditProof, "required proof")]
    public void Lifecycle_ui_preserves_canonical_preview_and_proof_reasons(
        bool missingPreview,
        TenantHighImpactUnavailableReason expectedReason,
        string expectedText)
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            Preview = missingPreview
                ? TenantHighImpactPreviewEvidence.Missing
                : TenantHighImpactPreviewEvidence.Ready,
            Proof = missingPreview
                ? TenantHighImpactProofEvidence.NotRequired
                : (TenantHighImpactProofEvidence)999,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, disable));

        cut.Find("#tenants-lifecycle-disable-reason").GetAttribute("data-reason-category")
            .ShouldBe(expectedReason.ToString());
        cut.Find("[data-testid='tenants-lifecycle-disable-reason']").TextContent
            .ShouldContain(expectedText, Case.Insensitive);
    }

    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData((TenantLifecycleAuthorizationReflectionState)999)]
    public void Submit_time_lifecycle_authority_revocation_or_undefined_evidence_blocks_before_activity_or_gateway(
        TenantLifecycleAuthorizationReflectionState currentAuthorization)
    {
        StubTenantCommandGateway gateway = new();
        List<bool> activity = [];
        RegisterServices(gateway);

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, HighImpactEvidence(TenantHighImpactAction.DisableTenant))
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(currentAuthorization))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"))));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        activity.ShouldBeEmpty();
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Find("[data-testid='tenants-lifecycle-safe-message']").TextContent
            .ShouldContain("authority", Case.Insensitive);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("tenant")]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("status")]
    [InlineData("freshness")]
    [InlineData("lifecycle")]
    [InlineData("surface")]
    public void Changed_authoritative_preview_fact_blocks_before_activity_or_gateway(string mismatch)
    {
        StubTenantCommandGateway gateway = new();
        List<bool> activity = [];
        RegisterServices(gateway);
        TenantDetail baseline = Detail("tenant.alpha", TenantStatus.Active);
        TenantDetailSnapshot proof = mismatch switch
        {
            "version" => Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:42"),
            "tenant" => Proof("Tenant.Alpha", TenantStatus.Active, "tenant-sequence:41"),
            "name" => TenantDetailSnapshot.Ready(
                baseline with { Name = "Renamed" },
                eTag: null,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41"),
            "description" => TenantDetailSnapshot.Ready(
                baseline with { Description = "Changed description" },
                eTag: null,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41"),
            "status" => Proof("tenant.alpha", TenantStatus.Disabled, "tenant-sequence:41"),
            "freshness" => Proof(
                "tenant.alpha",
                TenantStatus.Active,
                "tenant-sequence:41",
                ReadModelFreshnessState.Stale),
            "lifecycle" => Proof(
                "tenant.alpha",
                TenantStatus.Active,
                "tenant-sequence:41",
                lifecycle: ProjectionLifecycleState.Stale),
            "surface" => TenantDetailSnapshot.Stale(
                baseline,
                eTag: null,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41"),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, baseline)
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
            .Add(component => component.DisableEvidence, HighImpactEvidence(TenantHighImpactAction.DisableTenant))
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(proof)));

        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.DisableSubmissions.ShouldBe(0);
        activity.ShouldBeEmpty();
        cut.Find("[data-testid='tenants-lifecycle-safe-message']").TextContent
            .ShouldContain("changed", Case.Insensitive);
    }

    [Fact]
    public void Enable_flow_dispatches_only_enable_with_the_stable_message_id()
    {
        var gateway = new StubTenantCommandGateway
        {
            Submission = TenantCommandSubmissionResult.Failed("Safe terminal failure."),
        };
        RegisterServices(gateway);
        TenantHighImpactActionEvidence enable = HighImpactEvidence(TenantHighImpactAction.EnableTenant) with
        {
            TenantStatus = TenantStatus.Disabled,
        };
        TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
        {
            TenantStatus = TenantStatus.Disabled,
        };

        IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Disabled))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.EnableEvidence, enable)
            .Add(component => component.DisableEvidence, disable)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Disabled, "tenant-sequence:41"))));

        cut.Find("[data-testid='tenants-lifecycle-enable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("form").Submit();

        gateway.EnableSubmissions.ShouldBe(1);
        gateway.DisableSubmissions.ShouldBe(0);
        gateway.LastEnableRequest.ShouldNotBeNull().Operation.ShouldBe(TenantLifecycleOperation.EnableTenant);
        NUlid.Ulid.TryParse(gateway.LastEnableMessageId, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("en", "Active", "Aging", "Disable tenant", "aging or refreshing", "No recovery")]
    [InlineData("fr", "Actif", "Vieillissante", "Désactiver le locataire", "vieillit ou est en cours", "Aucune récupération")]
    public void English_and_french_lifecycle_facts_keep_whole_associated_strings(
        string cultureName,
        string expectedStatus,
        string expectedFreshness,
        string expectedAction,
        string expectedReason,
        string expectedRecovery)
    {
        CultureInfo priorCulture = CultureInfo.CurrentCulture;
        CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Services.AddLocalization();
            Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
            TenantHighImpactActionEvidence disable = HighImpactEvidence(TenantHighImpactAction.DisableTenant) with
            {
                Freshness = TenantHighImpactFreshnessState.Aging,
                IsInputComplete = false,
            };

            IRenderedComponent<TenantLifecycleActionAvailability> cut = Render<TenantLifecycleActionAvailability>(parameters => parameters
                .Add(component => component.TenantId, "tenant.alpha")
                .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
                .Add(component => component.EnableEvidence, HighImpactEvidence(TenantHighImpactAction.EnableTenant))
                .Add(component => component.DisableEvidence, disable));

            cut.Find("[data-testid='tenants-lifecycle-disable-identity']").TextContent.ShouldBe("tenant.alpha");
            cut.Find("[data-testid='tenants-lifecycle-disable-status']").TextContent.ShouldContain(expectedStatus);
            cut.Find("[data-testid='tenants-lifecycle-disable-freshness']").TextContent.ShouldContain(expectedFreshness);
            cut.Find("[data-testid='tenants-lifecycle-disable-action']").TextContent.ShouldContain(expectedAction);
            cut.Find("[data-testid='tenants-lifecycle-disable-reason']").TextContent.ShouldContain(expectedReason);
            cut.Find("[data-testid='tenants-lifecycle-disable-recovery']").TextContent.ShouldContain(expectedRecovery);
        }
        finally
        {
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
        }
    }

    private static TenantHighImpactActionEvidence HighImpactEvidence(TenantHighImpactAction action)
        => new(
            "tenant.alpha",
            action,
            TenantHighImpactEvaluationStage.PreviewEntry,
            TenantStatus.Active,
            TenantHighImpactFreshnessState.Current,
            HasCurrentBaseline: true,
            TenantDetailSurfaceKind.Ready,
            ProjectionLifecycleState.Current,
            TenantHighImpactAuthorityEvidence.Authorized,
            TenantHighImpactNamespaceScopeEvidence.NotRequired,
            TenantHighImpactSupportEvidence.Ready,
            TenantHighImpactAdmissionEvidence.Available,
            TenantHighImpactPreviewEvidence.Ready,
            TenantHighImpactProofEvidence.NotRequired,
            TenantHighImpactViewportState.Safe,
            IsInputComplete: true,
            TenantHighImpactTargetState.NotApplicable,
            "tenant-sequence:41");

    private void RegisterServices(
        ITenantCommandGateway? gateway = null,
        TenantLifecycleAttemptTracker? attemptTracker = null)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        if (attemptTracker is null)
        {
            Services.AddSingleton<TenantLifecycleAttemptTracker>();
        }
        else
        {
            Services.AddSingleton(attemptTracker);
        }

        if (gateway is not null)
        {
            Services.AddSingleton(gateway);
        }
    }

    private static TenantLifecycleCommandSnapshot PendingLifecycleAttempt()
    {
        TenantDetail detail = Detail("tenant.alpha", TenantStatus.Active);
        var intent = new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant);
        return TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(intent, detail, "tenant-sequence:41")
            .RequestSent(intent, detail, "tenant-sequence:41", "message-life")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"));
    }

    private IRenderedComponent<TenantLifecycleActionAvailability> RenderLifecycleAvailability(List<bool> activity)
        => Render<TenantLifecycleActionAvailability>(parameters => parameters
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.TenantId, "tenant.alpha")
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.CurrentStatus, TenantStatus.Active)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.IsCommandSurfaceConnected, true)
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(component => component.GovernanceReadiness, TenantLifecycleGovernanceReadiness.Ready)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.OnProjectionRefreshRequested, () => Task.CompletedTask)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (request, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof(request.TenantId, TenantStatus.Active, "tenant-sequence:41"))));

    private IRenderedComponent<TenantLifecycleCommandFlow> RenderLifecycleFlow()
        => Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"))));

    private IRenderedComponent<TenantLifecycleCommandFlow> RenderLifecycleFlow(List<bool> activity)
        => Render<TenantLifecycleCommandFlow>(parameters => parameters
            .Add(component => component.Detail, Detail("tenant.alpha", TenantStatus.Active))
            .Add(component => component.Availability, AvailableLifecycle(
                TenantStatus.Active,
                TenantLifecycleOperation.DisableTenant))
            .Add(component => component.ProjectionVersion, "tenant-sequence:41")
            .Add(component => component.IsCommandSurfaceAvailable, true)
            .Add(component => component.OnCommandActivityChanged, active => activity.Add(active))
            .Add(component => component.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(component => component.ProjectionEvidenceProvider, (_, _) => Task.FromResult<TenantDetailSnapshot?>(
                Proof("tenant.alpha", TenantStatus.Active, "tenant-sequence:41"))));

    private static TenantDetail Detail(string tenantId, TenantStatus status)
        => new(
            tenantId,
            "Alpha",
            "Tenant alpha description",
            status,
            [],
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));

    private static TenantDetailSnapshot Proof(
        string tenantId,
        TenantStatus status,
        string projectionVersion,
        ReadModelFreshnessState freshness = ReadModelFreshnessState.Current,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current)
        => TenantDetailSnapshot.Ready(
            Detail(tenantId, status),
            eTag: null,
            freshness,
            lifecycle,
            projectionVersion);

    private static TenantLifecycleAvailability AvailableLifecycle(TenantStatus currentStatus, TenantLifecycleOperation operation)
        => new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                currentStatus,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: ProjectionLifecycleState.Current)
            .Evaluate(operation);

    private static void SetPrivateField<TComponent, TValue>(
        TComponent component,
        string fieldName,
        TValue value)
        where TComponent : class
    {
        FieldInfo field = typeof(TComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(TComponent).FullName, fieldName);
        field.SetValue(component, value);
    }

    private static TValue GetPrivateField<TComponent, TValue>(TComponent component, string fieldName)
        where TComponent : class
    {
        FieldInfo field = typeof(TComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(TComponent).FullName, fieldName);
        return (TValue)field.GetValue(component)!;
    }

    private IReadOnlyList<string> FocusedElementIds()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.Count > 0 && invocation.Arguments[0] is ElementReference reference
                ? reference.Id
                : string.Empty)];

    private static string CapturedElementReferenceId(object component, string fieldName)
    {
        object value = component.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(component)
            ?? throw new InvalidOperationException(
                $"'{fieldName}' is not a field of {component.GetType().Name}; the focus assertion cannot identify its target.");
        return ((ElementReference)value).Id;
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedLifecycleDispatch { get; set; } = true;

        public TenantCommandSubmissionResult Submission { get; set; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TenantCommandStatusResult Status { get; set; }
            = TenantCommandStatusResult.Unknown("Tenant command status is unavailable.");

        public Exception? SubmissionException { get; set; }

        public Exception? StatusException { get; init; }

        public bool PreserveStatusIdentityEvidence { get; init; }

        public Func<TenantCommandTrackingHandle, CancellationToken, Task<TenantCommandStatusResult>>? StatusProvider { get; init; }

        public Func<TenantLifecycleCommandRequest, string, CancellationToken, Task<TenantCommandSubmissionResult>>? SubmissionProvider { get; set; }

        public TenantLifecycleCommandRequest? LastDisableRequest { get; private set; }

        public string? LastDisableMessageId { get; private set; }

        public TenantLifecycleCommandRequest? LastEnableRequest { get; private set; }

        public string? LastEnableMessageId { get; private set; }

        public int EnableSubmissions { get; private set; }

        public int DisableSubmissions { get; private set; }

        public int StatusCalls { get; private set; }

        public TenantCommandTrackingHandle? LastStatusHandle { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> EnableTenantTrackedAsync(
            TenantLifecycleCommandRequest request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            LastEnableRequest = request;
            LastEnableMessageId = messageId;
            EnableSubmissions++;
            if (SubmissionException is not null)
            {
                return Task.FromException<TenantCommandSubmissionResult>(SubmissionException);
            }

            if (SubmissionProvider is not null)
            {
                return SubmissionProvider(request, messageId, cancellationToken);
            }

            return Task.FromResult(Submission with { MessageId = messageId });
        }

        public Task<TenantCommandSubmissionResult> DisableTenantTrackedAsync(
            TenantLifecycleCommandRequest request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            LastDisableRequest = request;
            LastDisableMessageId = messageId;
            DisableSubmissions++;
            if (SubmissionException is not null)
            {
                return Task.FromException<TenantCommandSubmissionResult>(SubmissionException);
            }

            if (SubmissionProvider is not null)
            {
                return SubmissionProvider(request, messageId, cancellationToken);
            }

            return Task.FromResult(Submission with { MessageId = messageId });
        }

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            LastStatusHandle = handle;
            if (StatusProvider is not null)
            {
                return StatusProvider(handle, cancellationToken);
            }

            return StatusException is null
                ? Task.FromResult(Status.Status is null || PreserveStatusIdentityEvidence
                    ? Status
                    : Status with { HasVerifiedCommandIdentity = true })
                : Task.FromException<TenantCommandStatusResult>(StatusException);
        }
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        public LocalizedString this[string name] => new(name, Values.TryGetValue(name, out string? value) ? value : name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.CurrentCulture, Values.TryGetValue(name, out string? value) ? value : name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Values.Select(static value => new LocalizedString(value.Key, value.Value));

        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.Lifecycle.Description"] = "Read-only high-impact lifecycle availability for tenant {0}. Commands remain unavailable until authority, freshness, command support, and governance are proven.",
            ["Tenants.Lifecycle.FactsLabel"] = "Tenant lifecycle safety facts",
            ["Tenants.Lifecycle.Freshness.Current"] = "Current",
            ["Tenants.Lifecycle.Freshness.Stale"] = "Stale",
            ["Tenants.Lifecycle.Freshness.Unknown"] = "Unknown",
            ["Tenants.Lifecycle.FreshnessLabel"] = "Freshness",
            ["Tenants.Lifecycle.Governance.Ready"] = "Ready",
            ["Tenants.Lifecycle.Governance.Unresolved"] = "Unresolved",
            ["Tenants.Lifecycle.GovernanceGateLabel"] = "Governance gate",
            ["Tenants.Lifecycle.Operation.DisableTenant"] = "Disable tenant",
            ["Tenants.Lifecycle.Operation.EnableTenant"] = "Enable tenant",
            ["Tenants.Lifecycle.StateLabel"] = "Lifecycle state",
            ["Tenants.Lifecycle.CurrentStatusLabel"] = "Current status",
            ["Tenants.Lifecycle.FreshnessLabel"] = "Freshness",
            ["Tenants.Lifecycle.Freshness.Current"] = "Current",
            ["Tenants.Lifecycle.Freshness.Stale"] = "Stale",
            ["Tenants.Lifecycle.Freshness.Unknown"] = "Unknown",
            ["Tenants.Lifecycle.Freshness.Aging"] = "Aging",
            ["Tenants.Lifecycle.Status.Active"] = "Active",
            ["Tenants.Lifecycle.Status.Disabled"] = "Disabled",
            ["Tenants.Lifecycle.Status.Unknown"] = "Unknown",
            ["Tenants.Lifecycle.Title"] = "Lifecycle command availability",
            ["Tenants.ProjectionLifecycle.Label"] = "Projection lifecycle",
            ["Tenants.ProjectionLifecycle.Current"] = "Current",
            ["Tenants.ProjectionLifecycle.Stale"] = "Stale",
            ["Tenants.ProjectionLifecycle.Unknown"] = "Unknown",
            ["Tenants.ProjectionLifecycle.Rebuilding"] = "Rebuilding",
            ["Tenants.ProjectionLifecycle.Degraded"] = "Degraded",
            ["Tenants.ProjectionLifecycle.Unavailable"] = "Unavailable",
            ["Tenants.ProjectionLifecycle.LocalOnly"] = "Local only",
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
            ["Tenants.Audit.EntryPoint.Accessible.Command"] = "Inspect audit for tenant {1} ({0})",
            ["Tenants.Audit.EntryPoint.CommandReason"] = "Command-specific proof is not available here; open the tenant audit list and use the visible audit state.",
            ["Tenants.Audit.EntryPoint.Label"] = "Audit evidence",
            ["Tenants.Audit.EntryPoint.Unavailable.ScopeRequired"] = "Tenant scope is required before audit evidence can be opened.",
            ["Tenants.Audit.EntryPoint.Unavailable.StaleScope"] = "Refresh tenant scope before opening audit evidence.",
            ["Tenants.Lifecycle.Cancel"] = "Cancel",
            ["Tenants.Lifecycle.Abandon"] = "Stop tracking",
            ["Tenants.Lifecycle.Confirmation.Help"] = "Type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.Lifecycle.Confirmation.Help.Pending"] = "The lifecycle attempt for {0} is pending. Cancel or Escape cannot close this dialog; refresh status or stop tracking explicitly.",
            ["Tenants.Lifecycle.Confirmation.Label"] = "Type the tenant id to confirm",
            ["Tenants.Lifecycle.Confirm"] = "Confirm {0}",
            ["Tenants.Lifecycle.ConfirmedStatus"] = "Last confirmed lifecycle: {0}",
            ["Tenants.Lifecycle.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.Lifecycle.Preview.AuditExpectation.Value"] = "Audit evidence is expected from the command/event pipeline and is shown as pending until explicit evidence is available.",
            ["Tenants.Lifecycle.Preview.AuthorizationGovernance"] = "Authorization and governance facts",
            ["Tenants.Lifecycle.Preview.AuthorizationGovernance.Value"] = "Server-reflected global-administrator authority and the approved reversible availability-control governance path are present.",
            ["Tenants.Lifecycle.Preview.CommandSurface"] = "Command-surface readiness",
            ["Tenants.Lifecycle.Preview.CommandSurface.Value"] = "The existing tenant command gateway is connected and one-at-a-time command submission is enforced.",
            ["Tenants.Lifecycle.Preview.CurrentLifecycle"] = "Current lifecycle",
            ["Tenants.Lifecycle.Preview.Description"] = "Review the full lifecycle consequence preview for tenant {0} before submitting.",
            ["Tenants.Lifecycle.Preview.IntendedLifecycle"] = "Intended lifecycle",
            ["Tenants.Lifecycle.Preview.KnownConsequences"] = "Known consequences",
            ["Tenants.Lifecycle.Preview.KnownConsequences.EnableTenant"] = "Tenant availability is restored only after exact-command event evidence and a newer tenant projection confirm Active.",
            ["Tenants.Lifecycle.Preview.KnownConsequences.DisableTenant"] = "Tenant availability is suspended as reversible availability control only after exact-command event evidence and a newer tenant projection confirm Disabled. Tenant data is not deleted or purged.",
            ["Tenants.Lifecycle.Preview.KnownUnknowns"] = "Known unknowns",
            ["Tenants.Lifecycle.Preview.KnownUnknowns.Value"] = "Downstream consumers may observe tenant availability asynchronously; audit proof may arrive after projection confirmation.",
            ["Tenants.Lifecycle.Preview.ListTitle"] = "Lifecycle consequence preview",
            ["Tenants.Lifecycle.Preview.ProjectionEvidence"] = "Freshness and projection evidence",
            ["Tenants.Lifecycle.Preview.ProjectionEvidence.Value"] = "The last confirmed tenant projection shows {0} with {1} freshness. Confirmation requires exact-command event evidence and a strictly newer internal projection marker from an authoritative re-query.",
            ["Tenants.Lifecycle.Unavailable.ProofRead"] = "Fresh authoritative lifecycle proof could not be read. Refresh tenant detail before submitting.",
            ["Tenants.Lifecycle.Unavailable.PreviewChanged"] = "Tenant identity, lifecycle, freshness, or projection evidence changed while the preview was open. Refresh and review a new complete preview before submitting.",
            ["Tenants.Lifecycle.Unavailable.RetainedDifferentIntent"] = "A different lifecycle intent is already pending for this tenant. Reconcile that retained attempt before choosing another lifecycle action.",
            ["Tenants.Lifecycle.Retained.Resume"] = "This lifecycle attempt is still pending. Open it to resume authoritative reconciliation without submitting again.",
            ["Tenants.Lifecycle.Dismissal.Pending"] = "This lifecycle attempt is still pending and cannot be dismissed. Refresh status until it reaches a terminal, support-safe outcome.",
            ["Tenants.Lifecycle.UnableToVerify.Status"] = "The lifecycle command status could not be verified.",
            ["Tenants.Lifecycle.UnableToVerify.Abandoned"] = "Tracking was stopped explicitly. The command outcome remains unverified; refresh tenant detail before starting another action.",
            ["Tenants.Lifecycle.UnableToVerify.StatusTimeout"] = "The lifecycle attempt exceeded its verification time limit. Its outcome remains unverified; refresh tenant detail before starting another action.",
            ["Tenants.Lifecycle.StatusEvidence.Pending"] = "The lifecycle command is accepted, but status evidence is not available yet. Keep this attempt open and refresh.",
            ["Tenants.Lifecycle.StatusEvidence.RetryableFailure"] = "Lifecycle status could not be read yet because of a temporary connection or response problem. Keep this attempt open and refresh.",
            ["Tenants.Lifecycle.SubmissionEvidence.Ambiguous"] = "The lifecycle command may have been delivered, but no acceptance response was received. Keep this attempt open and refresh to retry safely with the same command identity.",
            ["Tenants.Lifecycle.UnableToVerify.MissingEventEvidence"] = "The lifecycle command completed without exact event evidence, so the requested outcome cannot be confirmed.",
            ["Tenants.Lifecycle.UnableToVerify.MissingBaseline"] = "The lifecycle outcome cannot be verified because its pre-submit projection baseline is unavailable.",
            ["Tenants.Lifecycle.UnableToVerify.TrackingMismatch"] = "The lifecycle gateway returned tracking evidence for a different logical attempt. No lifecycle success is asserted.",
            ["Tenants.Lifecycle.UnableToVerify.ProofRead"] = "Authoritative lifecycle proof could not be read. No lifecycle success is asserted.",
            ["Tenants.Lifecycle.Message.Rejected"] = "The lifecycle command was rejected. Refresh tenant detail before taking another action.",
            ["Tenants.Lifecycle.Message.Rejected.InsufficientPermissions"] = "You are not authorized to submit tenant lifecycle commands.",
            ["Tenants.Lifecycle.Message.Rejected.TenantDisabled"] = "This tenant is disabled, so this lifecycle command cannot be completed.",
            ["Tenants.Lifecycle.Message.Rejected.TenantNotFound"] = "The tenant was not found. Refresh tenant detail before taking another action.",
            ["Tenants.Lifecycle.Message.Rejected.TenantLifecycleStateAlreadySet"] = "The tenant lifecycle already matches the requested state. Refresh tenant detail before taking another action.",
            ["Tenants.Lifecycle.Message.Failed"] = "The lifecycle command failed before its outcome could be verified.",
            ["Tenants.Lifecycle.Message.Degraded"] = "The lifecycle command was accepted, but publication could not be verified.",
            ["Tenants.Lifecycle.Message.UnableToVerify"] = "The lifecycle command outcome could not be verified. Refresh before taking another action.",
            ["Tenants.Lifecycle.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.Lifecycle.Preview.RecoveryPath.Value"] = "Submit the opposite lifecycle command after refreshed projection evidence shows the current state.",
            ["Tenants.Lifecycle.Preview.TenantIdentity"] = "Tenant identity",
            ["Tenants.Lifecycle.Preview.Title"] = "{0} consequence preview",
            ["Tenants.Lifecycle.Recovery.Confirmed"] = "Use the refreshed tenant projection as the source of truth.",
            ["Tenants.Lifecycle.Recovery.Previewed"] = "Cancel to return without submitting, or submit only after confirmation is exact.",
            ["Tenants.Lifecycle.Refresh"] = "Refresh status",
            ["Tenants.Lifecycle.Result.Title"] = "Lifecycle command result",
            ["Tenants.Lifecycle.State.Confirmed"] = "Projection confirmed the lifecycle change.",
            ["Tenants.Lifecycle.State.Failed"] = "Lifecycle command failed before it could be verified.",
            ["Tenants.Lifecycle.State.Previewed"] = "Preview ready; no lifecycle command has been submitted.",
            ["Tenants.Lifecycle.State.UnableToVerify"] = "Lifecycle command outcome is unable to verify.",
            ["Tenants.Lifecycle.Unavailable.AlreadyActive"] = "{1} is unavailable for tenant {0} because the current projection already shows Active. If submitted by another surface, the safe domain outcome is {2}; continue read-only or refresh.",
            ["Tenants.Lifecycle.Unavailable.AlreadyDisabled"] = "{1} is unavailable for tenant {0} because the current projection already shows Disabled. If submitted by another surface, the safe domain outcome is {2}; continue read-only or refresh.",
            ["Tenants.Lifecycle.Unavailable.CommandSurface"] = "{1} is unavailable for tenant {0} because lifecycle command support is not connected. Continue read-only or escalate command-surface readiness.",
            ["Tenants.Lifecycle.Unavailable.Governance"] = "{1} is unavailable for tenant {0} because high-impact lifecycle governance is not ready. Continue read-only until the platform gate is cleared.",
            ["Tenants.Lifecycle.Unavailable.MissingPermission"] = "{1} is unavailable for tenant {0} because server-side global-administrator authority is not proven. Refresh first; if access is still not proven, request permission or continue read-only.",
            ["Tenants.Lifecycle.Unavailable.Mobile"] = "{1} is unavailable for tenant {0} because this viewport cannot preserve the full high-impact safety context. Continue read-only on this view.",
            ["Tenants.Lifecycle.Unavailable.InFlightOrCommandSurface"] = "A tenant command is already in flight or the lifecycle command surface is not connected.",
            ["Tenants.Lifecycle.Unavailable.InFlightOrCommandSurface.Recovery"] = "Wait for the current tenant command to finish, or restore the lifecycle command connection before continuing.",
            ["Tenants.Lifecycle.Unavailable.Identity"] = "Tenant identity is incomplete, so lifecycle submission is blocked.",
            ["Tenants.Lifecycle.Unavailable.PreviewIncomplete"] = "The consequence preview is incomplete, so lifecycle submission is blocked. Refresh tenant detail and review a new complete preview.",
            ["Tenants.Lifecycle.Retained.AbandonUnreachable"] = "This lifecycle attempt cannot be opened for reconciliation right now. Stop tracking to release this tenant for other commands; the command outcome remains unverified.",
            ["Tenants.Lifecycle.Retained.Recovery"] = "Open the retained attempt and refresh status until authoritative reconciliation reaches a terminal outcome.",
            ["Tenants.Lifecycle.Dispatch.Recovery"] = "Refresh to safely retry the same command identity. The unresolved dispatch expires after {0} minutes; you may stop tracking if you accept that its outcome remains unknown.",
            ["Tenants.Lifecycle.Unavailable.ProjectionLifecycle"] = "{1} is unavailable for tenant {0} because the projection lifecycle is not current. Continue read-only and refresh projection evidence.",
            ["Tenants.Lifecycle.Unavailable.SameState"] = "The current projection already shows {0}; lifecycle submission is blocked.",
            ["Tenants.Lifecycle.Unavailable.StaleFreshness"] = "{1} is unavailable for tenant {0} because tenant freshness is stale or unknown. Refresh before considering lifecycle action availability.",
            ["Tenants.Lifecycle.Unavailable.UnknownStatus"] = "{1} is unavailable for tenant {0} because the tenant lifecycle state is unknown. Continue read-only and refresh projection evidence.",
            ["Tenants.Lifecycle.UnavailableReason.HighImpactFlowNotReady"] = "high-impact flow not ready",
            ["Tenants.Lifecycle.UnavailableReason.MissingLifecycleSupport"] = "missing lifecycle support",
            ["Tenants.Lifecycle.UnavailableReason.MissingPermission"] = "missing permission",
            ["Tenants.Lifecycle.UnavailableReason.None"] = "available",
            ["Tenants.Lifecycle.UnavailableReason.StaleData"] = "stale data",
            ["Tenants.Lifecycle.Validation.ConfirmationRequired"] = "Type tenant id {0} exactly before submitting this lifecycle command.",
            ["Tenants.HighImpact.Action.EnableTenant"] = "Enable tenant",
            ["Tenants.HighImpact.Action.DisableTenant"] = "Disable tenant",
            ["Tenants.HighImpact.Available"] = "This action is available.",
            ["Tenants.HighImpact.AvailableWithFriction"] = "This action is available, but the projection is aging or refreshing. Review the last-confirmed facts carefully.",
            ["Tenants.HighImpact.Unavailable.MissingPermission"] = "This action is unavailable because the required authority or namespace scope is not reflected.",
            ["Tenants.HighImpact.Unavailable.StaleData"] = "This action is unavailable because current authoritative data is not available.",
            ["Tenants.HighImpact.Unavailable.MissingLifecycleSupport"] = "This action is unavailable because its command lifecycle support is not ready.",
            ["Tenants.HighImpact.Unavailable.MissingConsequencePreview"] = "This action is unavailable until its complete consequence preview is ready.",
            ["Tenants.HighImpact.Unavailable.MissingAuditProof"] = "This action is unavailable because its required proof is not ready.",
            ["Tenants.HighImpact.Unavailable.HighImpactFlowNotReady"] = "This high-impact action is read-only until the measured viewport and aggregate admission are safe.",
            ["Tenants.HighImpact.Recovery.None"] = "No recovery is required.",
            ["Tenants.HighImpact.Recovery.MissingPermission"] = "Refresh authority first. If access is still not proven, ask an administrator to verify your role and the exact namespace grant.",
            ["Tenants.HighImpact.Recovery.StaleData"] = "Refresh the authoritative tenant data and review the last-confirmed facts.",
            ["Tenants.HighImpact.Recovery.MissingLifecycleSupport"] = "Restore the action's command lifecycle support, then retry.",
            ["Tenants.HighImpact.Recovery.MissingConsequencePreview"] = "Complete the safe preview facts and required confirmation inputs.",
            ["Tenants.HighImpact.Recovery.MissingAuditProof"] = "Restore the proof source declared by this action, then retry.",
            ["Tenants.HighImpact.Recovery.HighImpactFlowNotReady"] = "Use a wider measured viewport or wait for the tenant's current command to finish.",
            ["Tenants.HighImpact.DomainOutcome.LifecycleStateAlreadySet"] = "The tenant already has the requested lifecycle state.",
            ["Tenants.HighImpact.Freshness.Unknown"] = "Unknown",
            ["Tenants.HighImpact.Freshness.Current"] = "Current",
            ["Tenants.HighImpact.Freshness.Refreshing"] = "Refreshing with a current baseline",
            ["Tenants.HighImpact.Freshness.Aging"] = "Aging",
            ["Tenants.HighImpact.Freshness.Stale"] = "Stale",
            ["Tenants.Audit.Availability.State.Available"] = "Audit available",
            ["Tenants.Audit.Availability.Reason.Pending"] = "The command's events are stored, but its audit record is not readable yet. It normally appears shortly, and no proof is claimed until it does.",
            ["Tenants.Audit.Availability.Reason.Delayed"] = "The audit record is taking longer than expected to become readable. No proof is claimed until it can be read.",
            ["Tenants.Audit.Availability.RetryLimit"] = "Repeated retries left this state unchanged, so retrying is no longer offered here.",
            ["Tenants.Audit.Receipt.Availability.Unavailable.Reason"] = "This audit read could not verify the requested evidence. This does not mean the record does not exist, and the recorded outcome is unchanged.",
            ["Tenants.Audit.Recovery.Action.Escalate"] = "Escalate without diagnostics",
        };
    }
}
