using System.Globalization;
using System.Reflection;

using Bunit;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.FrontComposer.Contracts.Lifecycle;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.Components.Tenants.Configuration;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class RemoveTenantConfigurationFlowTests : FluentBunitContext
{
    [Fact]
    public void TenantNamedGlobalAdministratorsConfirmsRemovalThroughStatusGateway()
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
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("global-administrators",
                new Dictionary<string, string> { ["billing.mode"] = "enabled" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, intent => Task.FromResult(
                Proof(intent, TenantConfigurationProjectionProofKind.RemoveConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Find("[data-testid='tenants-config-remove-state']").TextContent
            .ShouldContain("Projection confirmed");
    }

    [Fact]
    public void Remove_configuration_flow_blocks_a_target_absent_from_the_safe_rows_without_borrowing_a_value()
    {
        // The target sits inside the authorized `billing` namespace but has no safe row — how an
        // unapproved or undefined-policy key reaches this flow. It must block, and it must not display
        // a sibling row's value: this context is the component's only configuration input.
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string>
            {
                ["billing.mode"] = "sibling-row-value",
            }))
            .Add(p => p.TargetKey, "billing.endpoint")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-flow']");
        cut.Find("[data-testid='tenants-config-remove-focus-start']");
        cut.Find("[data-testid='tenants-config-remove-focus-end']");
        cut.FindAll("[data-testid='tenants-config-remove-preview']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-config-remove-preview-blocked']").TextContent.ShouldContain("not visible", Case.Insensitive);
        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-config-remove-live-region']").GetAttribute("aria-live").ShouldBe("polite");
        cut.Markup.ShouldNotContain("sibling-row-value", Case.Insensitive);
        cut.Markup.ShouldNotContain("audit available", Case.Insensitive);
        cut.Markup.ShouldNotContain("receipt", Case.Insensitive);
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Remove_preview_reports_the_longest_authorized_prefix_not_the_first_dot_segment()
    {
        // The consumer namespace is the longest matching authorized prefix, exactly as the safe composer and
        // the set flow resolve it. Splitting on the first dot made this destructive-command preview claim
        // scope `app` for a caller granted only `app.feature`, contradicting both the read grid and the
        // sibling set preview over the same key -- and overstating the blast radius of a destructive action.
        //
        // The grant here is deliberately multi-segment: with a single-segment grant the two implementations
        // return the same string and the test proves nothing.
        RegisterServices(new StubTenantCommandGateway());

        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["app.feature"],
            [new TenantConfigurationSafeRow("app", "app.feature.flag", "enabled")],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "app.feature.flag")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        string rendered = cut.Find("[data-testid='tenants-config-remove-preview-namespace']").TextContent.Trim();
        rendered.ShouldBe("app.feature");
        rendered.ShouldNotBe("app");
    }

    [Fact]
    public void Remove_configuration_preview_redacts_the_current_value_to_a_presence_classification()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-preview-current-state']").TextContent.ShouldContain("Present");
        cut.Markup.ShouldNotContain("trial");
        cut.FindAll("[data-testid='tenants-config-copy-reference']").ShouldBeEmpty();
    }

    [Fact]
    public void Complete_remove_preview_renders_exactly_ten_stable_redacted_facts()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-preview-item']").Count.ShouldBe(10);
        foreach (string testId in new[]
        {
            "tenants-config-remove-tenant-id",
            "tenants-config-remove-preview-namespace",
            "tenants-config-remove-preview-key",
            "tenants-config-remove-preview-current-state",
            "tenants-config-remove-preview-effect",
            "tenants-config-remove-preview-freshness",
            "tenants-config-remove-preview-authorization",
            "tenants-config-remove-preview-consequences",
            "tenants-config-remove-preview-audit",
            "tenants-config-remove-preview-recovery",
        })
        {
            cut.FindAll($"[data-testid='{testId}']").ShouldHaveSingleItem();
        }

        cut.Markup.ShouldNotContain("trial", Case.Sensitive);
    }

    [Fact]
    public void Remove_configuration_preview_excludes_a_key_absent_from_the_safe_targets()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.password")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-preview-current-state']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-config-remove-preview-blocked']").TextContent.ShouldContain("not visible", Case.Insensitive);
        cut.Markup.ShouldNotContain("trial");
        cut.FindAll("[data-testid='tenants-config-copy-reference']").ShouldBeEmpty();
    }

    [Fact]
    public void Missing_or_hidden_target_blocks_submission_without_gateway_call_or_success_state()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "security.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-preview']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-config-remove-preview-blocked']").TextContent
            .ShouldContain("not visible", Case.Insensitive);
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
    }

    [Fact]
    public void Evidence_aware_remove_with_exact_confirmation_and_current_reauthorization_dispatches_exactly_once()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
        };
        RegisterServices(gateway);
        TenantConfigurationManagementContext context = Context(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "trial" });

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.Evidence, Evidence()));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(1);
        gateway.LastRemoveConfigurationRequest.ShouldNotBeNull();
        gateway.LastRemoveConfigurationRequest.Key.ShouldBe("billing.mode");
    }

    [Fact]
    public void Ambiguous_remove_submission_retains_the_attempt_without_redispatch_and_renders_its_safe_message()
    {
        // Every other submit test hands the flow an Accepted result, so the branch that decides whether an
        // undeliverable dispatch keeps or drops its tracked identity was never executed. Downgrading it to
        // a non-retaining Blocked snapshot -- which releases the lease and re-enables submit after a 503
        // that may already have removed the key -- passed the whole suite.
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Ambiguous(
                "stub-message-id",
                "Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous"),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.Evidence, Evidence()));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(1);
        string retainedMessageId = cut.Instance.Snapshot.MessageId.ShouldNotBeNull();
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous");

        // The keyed message is the operator's only explanation on this path, and no test rendered that
        // paragraph: reverting DisplaySafeMessage to Snapshot.SafeMessage blanked it silently, because every
        // keyed transition nulls SafeMessage.
        cut.Find("[data-testid='tenants-config-remove-safe-message']").TextContent
            .ShouldBe("Submission delivery is uncertain. Do not submit again; refresh status or stop local reconciliation.");
        cut.Find("[data-testid='tenants-config-remove-abandon']");

        // Resubmitting must not mint a second dispatch or a second identity.
        cut.Find("form").Submit();
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
        cut.Instance.Snapshot.MessageId.ShouldBe(retainedMessageId);
    }

    [Fact]
    public void Evidence_aware_authorized_missing_remove_records_expected_outcome_without_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        TenantConfigurationManagementContext initial = Context(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "trial" });
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, initial)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.Evidence, Evidence())
            .Add(p => p.PreviewEvidenceProvider, intent => Task.FromResult(
                TenantRemoveConfigurationPreview.Create(
                    intent,
                    TenantStatus.Active,
                    TenantRemoveConfigurationCurrentState.Absent,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:11",
                    isAuthorized: true))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBeNull();

        // ShouldNotBe("ConfigurationKeyNotFound") passed for literally any string. Assert the copy the
        // operator actually reads, and prove the rejection vocabulary is absent by substring.
        string safeMessage = cut.Instance.Snapshot.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldBe("The selected key is absent from current authorized evidence. Refresh and select a visible key; no removal was submitted.");
        safeMessage.ShouldNotContain("ConfigurationKeyNotFound");
        safeMessage.ShouldNotContain("AlreadyApplied");
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void Remove_configuration_requires_current_projection_lifecycle(ProjectionLifecycleState lifecycle)
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, lifecycle));

        cut.Find("[data-testid='tenants-config-remove-unavailable-reason']").TextContent
            .ShouldContain("projection-confirmed lifecycle", Case.Insensitive);
        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldNotBeNull();
    }

    /// <summary>
    /// Pins decision D-F's clause ORDER on this flow, which nothing did: every render in this file used
    /// <see cref="TenantDetailSurfaceKind.Ready"/>, so the surface clause never fired and hoisting the
    /// lifecycle clause back above it changed no outcome here. Every failed read also carries a non-Current
    /// lifecycle, so with the wrong order an operator whose read simply failed is told to refresh the
    /// projection lifecycle. The discriminator is the absence of the lifecycle reason.
    /// </summary>
    [Theory]
    [InlineData(TenantDetailSurfaceKind.Unavailable, ProjectionLifecycleState.Unavailable)]
    [InlineData(TenantDetailSurfaceKind.Unknown, ProjectionLifecycleState.Unknown)]
    [InlineData(TenantDetailSurfaceKind.Degraded, ProjectionLifecycleState.Degraded)]
    public void A_failed_read_reports_the_projection_state_rather_than_the_projection_lifecycle(
        TenantDetailSurfaceKind surfaceKind,
        ProjectionLifecycleState lifecycle)
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, surfaceKind)
            .Add(p => p.Freshness, ReadModelFreshnessState.Unknown)
            .Add(p => p.Lifecycle, lifecycle));

        string reason = cut.Find("[data-testid='tenants-config-remove-unavailable-reason']").TextContent;
        reason.ShouldContain("unavailable or degraded", Case.Insensitive);
        reason.ShouldNotContain("projection-confirmed lifecycle", Case.Insensitive);
        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldNotBeNull();
    }

    /// <summary>
    /// Freshness half of the same clause-order contract: a stale or unknown-freshness read on an otherwise
    /// ready surface must report freshness, not the projection lifecycle, even though its lifecycle is also
    /// non-Current.
    /// </summary>
    [Theory]
    [InlineData(TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Stale)]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown)]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Rebuilding)]
    public void A_stale_or_unknown_read_reports_freshness_rather_than_the_projection_lifecycle(
        TenantDetailSurfaceKind surfaceKind,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle)
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, surfaceKind)
            .Add(p => p.Freshness, freshness)
            .Add(p => p.Lifecycle, lifecycle));

        string reason = cut.Find("[data-testid='tenants-config-remove-unavailable-reason']").TextContent;
        reason.ShouldContain("Refresh current tenant detail", Case.Insensitive);
        reason.ShouldNotContain("unavailable or degraded", Case.Insensitive);
        reason.ShouldNotContain("projection-confirmed lifecycle", Case.Insensitive);
        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldNotBeNull();
    }

    [Fact]
    public void Confirmation_text_must_match_literal_key_before_gateway_submission()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("Billing.Mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-config-remove-validation']").TextContent.ShouldContain("billing.mode");
        cut.Find("[data-testid='tenants-config-remove-live-region']").GetAttribute("aria-live").ShouldBe("polite");

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("[data-testid='tenants-config-remove-submit']").GetAttribute("disabled").ShouldBeNull();
    }

    [Fact]
    public void Keyboard_submission_confirms_only_after_status_and_projection_absence()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(
            TenantCommandLifecycleState.Confirmed,
            $"safe key: {cut.Instance.Snapshot.SafeMessageKey}; message: {cut.Instance.Snapshot.SafeMessage}"));
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
        gateway.LastRemoveConfigurationRequest.ShouldNotBeNull().ShouldBe(
            new RemoveTenantConfiguration("tenant.alpha", "billing.mode"));
        cut.Find("[data-testid='tenants-config-remove-state']").TextContent.ShouldContain("Projection confirmed");
        cut.Find("[data-testid='tenants-config-remove-live-region']").GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void Projection_that_still_contains_key_does_not_optimistically_delete_or_confirm()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        List<bool> commandActivity = [];

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, isActive => commandActivity.Add(isActive))
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveNotConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        commandActivity.ShouldNotBeEmpty();
        commandActivity[^1].ShouldBeTrue();
        cut.Find("[data-testid='tenants-config-remove-state']").TextContent.ShouldContain("Projection pending");
        cut.Markup.ShouldContain("billing.mode");
    }

    [Fact]
    public void A_settling_removal_requires_explicit_abandon_before_dismissal()
    {
        // ProjectionPending is not terminal and is not self-healing: it is left only by a RemoveConfirmed
        // proof, which the gateway declines whenever authorization, provenance, freshness or lifecycle
        // evidence is missing. Blocking dismissal on IsOwnedCommandInFlight -- which covers Accepted and
        // ProjectionPending -- therefore left this role="dialog" aria-modal="true" section with no exit for
        // the whole projection-lag window, and permanently when the proof never landed: cancel disabled,
        // Escape swallowed by CloseAsync, and the focus sentinels bouncing focus back in.
        //
        // Dismissal is bounded now, and it must release the activity lease on the way out: the parent
        // landmark keeps both flows mounted while a child owns command activity, so a dismissal that
        // skipped that would leave the landmark and the detail page believing a command was still settling.
        List<bool> commandActivity = [];
        int closeRequests = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, isActive => commandActivity.Add(isActive))
            .Add(p => p.OnCloseRequested, () => closeRequests++)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveNotConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        commandActivity[^1].ShouldBeTrue();

        cut.Find("[data-testid='tenants-config-remove-cancel']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-config-remove-abandon']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        commandActivity[^1].ShouldBeFalse();

        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();

        cut.WaitForAssertion(() => closeRequests.ShouldBe(1));
    }

    [Fact]
    public async Task A_status_refresh_that_completes_after_abandon_cannot_reacquire_command_activity()
    {
        TaskCompletionSource<TenantCommandStatusResult> statusGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
            StatusGate = statusGate,
            GateFromCall = 2,
        };
        RegisterServices(gateway);
        List<bool> commandActivity = [];
        int closeRequests = 0;

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, isActive => commandActivity.Add(isActive))
            .Add(p => p.OnCloseRequested, () => closeRequests++)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveNotConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        cut.Find("[data-testid='tenants-config-remove-refresh']").Click();
        cut.WaitForAssertion(() => gateway.GetStatusCallCount.ShouldBe(2));

        cut.Find("[data-testid='tenants-config-remove-abandon']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse());
        commandActivity[^1].ShouldBeFalse();
        int activityCountAfterAbandon = commandActivity.Count;

        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();
        cut.WaitForAssertion(() => closeRequests.ShouldBe(1));

        statusGate.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));

        // The gated read has now returned inside the gateway, so the flow's own continuation is scheduled.
        await gateway.GateObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        // The correct behaviour is that nothing further happens, so there is no positive signal to wait for
        // and the absence is asserted over a bounded stability window instead. WaitForAssertion is wrong for
        // a negative: the state was already ProjectionPending before the gate released, so it returned on
        // its first evaluation and the activity assertion below ran before the continuation could have.
        await Task.Delay(TimeSpan.FromMilliseconds(250), Xunit.TestContext.Current.CancellationToken);

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        commandActivity.Count.ShouldBe(activityCountAfterAbandon);
        commandActivity[^1].ShouldBeFalse();
    }

    [Fact]
    public void Dismissal_never_turns_an_unconfirmed_removal_into_a_success_claim()
    {
        // The guard that created the trap existed to protect the non-collapse model. That protection has to
        // survive the fix: dismissal is allowed, but it must confirm nothing, advance no truth state and
        // render no success -- the operator has chosen to stop watching, which is not an outcome.
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveNotConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));

        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
    }

    [Fact]
    public async Task Refresh_is_disabled_while_a_refresh_is_in_flight_rather_than_silently_dropping_the_click()
    {
        // The re-entrancy guard returns before the status read, before ConfirmProjection and before the
        // activity update. With the button still enabled, a second click was silently discarded and the
        // control was indistinguishable from a dead one -- so the guard belongs in CanRefresh, not only in
        // the method. Reverting CanRefresh to omit `!_isRefreshing` leaves the button enabled here and the
        // second click reaches the gateway, taking GetStatusCallCount to 2.
        // RunContinuationsAsynchronously, like every sibling gate in this file. Without it SetResult runs the
        // awaiting continuation synchronously on the xUnit thread, and that continuation invokes
        // EventCallbacks and StateHasChanged -- the documented trap where work resuming off the Blazor
        // Dispatcher tears down the circuit.
        TaskCompletionSource<TenantCommandStatusResult> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
            StatusGate = gate,

            // The submit path reads status itself; gating that would hang the submit instead of the refresh.
            GateFromCall = 2,
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-remove-refresh']").GetAttribute("disabled").ShouldBeNull());

        cut.Find("[data-testid='tenants-config-remove-refresh']").Click();

        // The status read has not returned, so the control must report itself as busy rather than accept a
        // second click it would silently drop.
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-remove-refresh']").GetAttribute("disabled").ShouldNotBeNull());
        gateway.GetStatusCallCount.ShouldBe(2);

        // The second click the test is named for. It was never dispatched, so the closing assertion was
        // satisfied by a click that did not exist: the re-entrancy guard could have been deleted entirely
        // and nothing here would have noticed.
        cut.Find("[data-testid='tenants-config-remove-refresh']").Click();
        gateway.GetStatusCallCount.ShouldBe(2);

        gate.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));
        await gateway.GateObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        // The in-flight refresh has settled and re-enabled the control, which is a positive signal that the
        // resumed continuation ran -- so the count assertion below is now made after the window in which a
        // queued second read would have been issued.
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-remove-refresh']").GetAttribute("disabled").ShouldBeNull());
        gateway.GetStatusCallCount.ShouldBe(2);
    }

    [Fact]
    public void Projection_evidence_is_taken_before_the_parent_refresh_is_requested()
    {
        // Both halves of the ordering fix have to hold. The parent keeps the flow mounted when its row
        // leaves the context; the child must read its proof before asking the parent to refresh, or the
        // refresh drops the row first and the proof is taken against a context that no longer contains it.
        // Only the parent half was covered.
        List<string> order = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnProjectionRefreshRequested, () => order.Add("refresh"))
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                order.Add("proof");
                return Task.FromResult(Proof(request, TenantConfigurationProjectionProofKind.RemoveConfirmed));
            }));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => order.ShouldContain("proof"));
        order.ShouldContain("refresh");
        order.IndexOf("proof").ShouldBeLessThan(order.IndexOf("refresh"));
    }

    [Fact]
    public void Submission_time_policy_revocation_blocks_remove_before_gateway_dispatch()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int previewCalls = 0;

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.PreviewEvidenceProvider, intent => Task.FromResult(
                ++previewCalls == 1
                    ? Preview(intent)
                    : TenantRemoveConfigurationPreview.Create(
                        intent,
                        TenantStatus.Active,
                        TenantRemoveConfigurationCurrentState.Absent,
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current,
                        "tenant-sequence:11",
                        isAuthorized: true))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Find("[data-testid='tenants-config-remove-state']").TextContent.ShouldContain("Unable to verify", Case.Insensitive);
    }

    [Fact]
    public void Submit_uses_registered_query_preview_when_no_component_callback_is_wired()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(
                CommandStatus.Processing,
                HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(1);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
    }

    [Fact]
    public void Submit_fails_closed_when_submit_time_preview_refresh_throws()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int previewCalls = 0;

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.PreviewEvidenceProvider, intent => ++previewCalls == 1
                ? Task.FromResult(Preview(intent))
                : Task.FromException<TenantRemoveConfigurationPreview>(
                    new InvalidOperationException("policy backend unreachable"))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Markup.ShouldNotContain("policy backend unreachable");
        cut.Markup.ShouldNotContain("InvalidOperationException");
    }

    [Fact]
    public void Gateway_unavailable_status_refresh_retains_configuration_command_activity_lock()
    {
        RegisterServices();
        List<bool> commandActivity = [];
        TenantConfigurationManagementContext context = Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" });

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, active => commandActivity.Add(active)));

        TenantRemoveConfigurationCommandSnapshot tracked = TenantRemoveConfigurationCommandSnapshot
            .Idle()
            .Previewed(Preview(new TenantRemoveConfigurationIntent("tenant.alpha", "billing", "billing.mode")))
            .RequestSent(
                Preview(new TenantRemoveConfigurationIntent("tenant.alpha", "billing", "billing.mode")),
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                DateTimeOffset.UtcNow)
            .Accepted(TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"));
        SetPrivateField(cut.Instance, "_snapshot", tracked);
        SetPrivateField(cut.Instance, "_hasRaisedCommandActivity", true);
        cut.Render(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, active => commandActivity.Add(active)));

        cut.Find("[data-testid='tenants-config-remove-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue());
        commandActivity.ShouldBeEmpty();
    }

    [Fact]
    public void Configuration_key_not_found_rejection_renders_safe_rejected_state()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config-remove"),
            Status = new TenantCommandStatusResult(
                CommandStatus.Rejected,
                "The configuration key was not found.",
                "ConfigurationKeyNotFound",
                HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult(
                Proof(request, TenantConfigurationProjectionProofKind.RemoveConfirmed))));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Rejected));
        cut.Find("[data-testid='tenants-config-remove-state']").TextContent.ShouldContain("rejected", Case.Insensitive);
        cut.Find("[data-testid='tenants-config-remove-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Markup.ShouldNotContain("success", Case.Insensitive);
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation-config-remove", Case.Insensitive);

        // A rejection stored nothing: no audit state is implied and the shared control stays hidden.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-audit-availability']").ShouldBeEmpty();
    }

    [Fact]
    public void Tracker_refusal_of_an_expired_attempt_is_retention_expiry_with_a_delayed_audit_record()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        TenantRemoveConfigurationPreview preview = Preview(new TenantRemoveConfigurationIntent("tenant.alpha", "billing", "billing.mode"));
        TenantRemoveConfigurationCommandSnapshot expired = TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(preview)
            .RequestSent(
                preview,
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                DateTimeOffset.UtcNow
                    - TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration
                    - TimeSpan.FromMinutes(1));
        MethodInfo setSnapshot = typeof(RemoveTenantConfigurationFlow)
            .GetMethod("SetSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;

        cut.InvokeAsync(() => setSnapshot.Invoke(cut.Instance, [expired]));

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
    }

    [Fact]
    public void Tracker_refusal_of_a_live_attempt_is_abandonment_with_an_unavailable_audit_record()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        TenantRemoveConfigurationPreview preview = Preview(new TenantRemoveConfigurationIntent("tenant.alpha", "billing", "billing.mode"));
        TenantRemoveConfigurationAttemptTracker tracker = Services.GetRequiredService<TenantRemoveConfigurationAttemptTracker>();
        tracker.Remember(TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(preview)
            .RequestSent(preview, "01ARZ3NDEKTSV4RRFFQ69G5FAA", DateTimeOffset.UtcNow)).ShouldBeTrue();
        TenantRemoveConfigurationCommandSnapshot live = TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(preview)
            .RequestSent(preview, "01ARZ3NDEKTSV4RRFFQ69G5FAV", DateTimeOffset.UtcNow);
        MethodInfo setSnapshot = typeof(RemoveTenantConfigurationFlow)
            .GetMethod("SetSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;

        cut.InvokeAsync(() => setSnapshot.Invoke(cut.Instance, [live]));

        // The tracker refuses a live attempt that another identity already owns. That is not retention expiry:
        // the attempt is abandoned, and its status after dispatch stays unverifiable rather than delayed.
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Fact]
    public void Abandoning_a_retained_attempt_stays_unavailable_and_continue_read_only_then_closes_the_dialog()
    {
        int closeRequests = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Ambiguous(
                "stub-message-id",
                "Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous"),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCloseRequested, () => closeRequests++));
        const string audit = "[data-testid='tenants-config-remove-audit']";

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        // The retained ambiguous attempt can be re-queried and cannot be dismissed: Refresh is offered,
        // Continue read-only is not.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        cut.FindAll($"{audit} [data-recovery-verb='continuereadonly']").ShouldBeEmpty();

        // Operator abandonment is not retention expiry: the audit record stays unavailable, never delayed.
        cut.Find("[data-testid='tenants-config-remove-abandon']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse(), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-recovery-verb='refresh']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        cut.Find($"{audit} [data-testid='tenants-audit-availability']").GetAttribute("data-state").ShouldBe("unavailable");
        cut.Find($"{audit} [data-recovery-verb='continuereadonly']").Click();

        cut.WaitForAssertion(() => closeRequests.ShouldBe(1), TimeSpan.FromSeconds(5));
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_lookup_wait_for_it_then_run_their_own_and_never_exhaust_the_retry_limit()
    {
        TaskCompletionSource<TenantCommandStatusResult> releaseLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Ambiguous(
                "stub-message-id",
                "Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous"),
            StatusGate = releaseLookup,
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        const string refresh = "[data-testid='tenants-config-remove-audit'] [data-recovery-verb='refresh']";

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        // A SignalR nudge starts a slow lookup; the shared control's Refresh then merges into it.
        Task nudge = cut.InvokeAsync(() => cut.Instance.ApplySignalRNudgeAsync());
        SpinWait.SpinUntil(() => gateway.GetStatusCallCount == 1, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());
        cut.Find(refresh).Click();
        cut.Find(refresh).Click();

        // The merged click waits for the running lookup; nothing counted as an unchanged retry yet.
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        releaseLookup.SetResult(TenantCommandStatusResult.Pending("Status is propagating."));
        await nudge.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();
        // The nudge's lookup started before the click, so the merged click runs one lookup of its own after it:
        // a click is never counted as a retry that no lookup served. Three clicks while it waited are one request.
        cut.WaitForAssertion(() => gateway.GetStatusCallCount.ShouldBe(2), TimeSpan.FromSeconds(5));

        // Once the merged refresh has settled, no later lookup followed the one it ran.
        await merged.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.GetStatusCallCount.ShouldBe(2);
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public void Audit_refresh_that_resolves_to_not_started_moves_focus_to_the_lifecycle_section()
    {
        // The first lookup cannot be read; the user's Refresh then waits on the gate, which finds the command still
        // processing.
        TaskCompletionSource<TenantCommandStatusResult> refreshLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("stub-message-id", "correlation-1"),
            StatusGate = refreshLookup,
            GateFromCall = 2,
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        const string audit = "[data-testid='tenants-config-remove-audit']";

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        string lifecycleReferenceId = ((ElementReference)typeof(RemoveTenantConfigurationFlow)
            .GetField("_lifecycleElement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Id;
        int focusCallsBeforeRefresh = FocusCalls().Count;

        cut.Find($"{audit} [data-recovery-verb='refresh']").Click();
        refreshLookup.SetResult(new TenantCommandStatusResult(CommandStatus.Processing, HasVerifiedCommandIdentity: true));

        // The polite NotStarted state unmounts the control and its focused Refresh button, so focus lands on the
        // lifecycle section instead of falling back to the document body.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Polite);
        cut.Instance.Snapshot.FocusTarget.ShouldNotBe(TenantCommandFocusTarget.Lifecycle);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-testid='tenants-audit-availability']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        SpinWait.SpinUntil(() => FocusCalls().Count > focusCallsBeforeRefresh, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        FocusCalls().Last().ShouldBe(lifecycleReferenceId);
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
    }

    private List<string> FocusCalls()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Select(reference => reference.Id ?? string.Empty)];

    [Fact]
    public void Cancel_and_escape_close_without_committing_action()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);
        int closeCount = 0;

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();
        cut.Find("[data-testid='tenants-config-remove-flow']").KeyDown("Escape");

        closeCount.ShouldBe(2);
        gateway.RemoveConfigurationCallCount.ShouldBe(0);
    }

    [Fact]
    public void Css_contains_forced_colors_focus_and_narrow_layout_hooks()
    {
        string styles = File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Configuration",
            "RemoveTenantConfigurationFlow.razor.css"));

        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain(":focus-visible");
        styles.ShouldContain("tenants-config-remove__narrow");
        styles.ShouldContain("::deep .tenants-config-remove__form");
    }

    [Fact]
    public async Task Submit_time_preview_becoming_stale_cancels_without_dispatch_and_late_completion_is_inert()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        RegisterServices(gateway);
        TaskCompletionSource<TenantRemoveConfigurationPreview> previewGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int previewCalls = 0;
        TenantConfigurationManagementContext context = Context(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "redacted-secret" });

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.PreviewEvidenceProvider, intent => ++previewCalls == 1
                ? Task.FromResult(Preview(intent))
                : previewGate.Task));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => previewCalls.ShouldBe(2));

        cut.Render(parameters => parameters
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale));
        previewGate.SetResult(Preview(new TenantRemoveConfigurationIntent(
            "tenant.alpha",
            "billing",
            "billing.mode")));
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();
        cut.Find("[data-testid='tenants-config-remove-cancel']").GetAttribute("disabled").ShouldBeNull();
    }

    [Fact]
    public async Task Stale_surface_after_suspended_lease_releases_late_acquisition_without_dispatch()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
        };
        RegisterServices(gateway);
        TaskCompletionSource<bool> leaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<bool> leaseCalls = [];
        TenantConfigurationManagementContext context = Context(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "redacted-secret" });

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.CommandActivityLease, active =>
            {
                leaseCalls.Add(active);
                return active ? leaseGate.Task : Task.FromResult(true);
            }));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => leaseCalls.ShouldContain(true));

        cut.Render(parameters => parameters
            .Add(p => p.Freshness, ReadModelFreshnessState.Stale));
        leaseGate.SetResult(true);
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        gateway.RemoveConfigurationCallCount.ShouldBe(0);
        leaseCalls.ShouldContain(false);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();
    }

    [Fact]
    public void Same_tenant_target_change_invalidates_old_preview_and_renders_fresh_exact_intent()
    {
        RegisterServices(new StubTenantCommandGateway());
        TenantConfigurationManagementContext context = Context(
            "tenant.alpha",
            new Dictionary<string, string>
            {
                ["billing.mode"] = "redacted-one",
                ["billing.region"] = "redacted-two",
            });
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-preview-key']").TextContent.ShouldBe("billing.mode");

        cut.Render(parameters => parameters.Add(p => p.TargetKey, "billing.region"));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-config-remove-preview-key']").TextContent.ShouldBe("billing.region"));
        cut.Markup.ShouldNotContain("redacted-one");
        cut.Markup.ShouldNotContain("redacted-two");
    }

    [Fact]
    public void Projection_proof_failure_preserves_event_evidence_and_projection_pending_state()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"),
            Status = new TenantCommandStatusResult(
                CommandStatus.EventsPublished,
                EventCount: 1,
                HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context(
                "tenant.alpha",
                new Dictionary<string, string> { ["billing.mode"] = "redacted-secret" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionEvidenceProvider, _ => throw new HttpRequestException("proof unavailable")));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        cut.Instance.Snapshot.HasCommandEventEvidence.ShouldBeTrue();
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Configuration.Remove.UnableToVerify.ProjectionProof");
    }

    [Fact]
    public async Task Retained_attempt_expiry_cancels_hung_dispatch_releases_activity_and_ignores_late_result()
    {
        TaskCompletionSource<TenantCommandSubmissionResult> submissionGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            SubmissionGate = submissionGate,
        };
        RegisterServices(gateway);
        List<bool> activity = [];
        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context(
                "tenant.alpha",
                new Dictionary<string, string> { ["billing.mode"] = "redacted-secret" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent));

        await cut.InvokeAsync(() => cut.Instance.ExpireRetainedAttemptAsync(
            cut.Instance.Snapshot.AttemptStartedAtUtc!.Value
                + TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration));

        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse());
        activity[^1].ShouldBeFalse();
        cut.Find("[data-testid='tenants-config-remove-cancel']").GetAttribute("disabled").ShouldBeNull();

        // Retention expiry delays the audit record. The released attempt can no longer be re-queried, so the
        // shared control offers no Refresh that would only burn the retry bound.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        cut.Find("[data-testid='tenants-config-remove-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("delayed");
        cut.FindAll("[data-testid='tenants-config-remove-audit'] [data-recovery-verb='refresh']").ShouldBeEmpty();

        submissionGate.SetResult(TenantCommandSubmissionResult.Accepted(
            cut.Instance.Snapshot.MessageId ?? "01ARZ3NDEKTSV4RRFFQ69G5FAA",
            "late-correlation"));
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        activity[^1].ShouldBeFalse();
        gateway.RemoveConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_is_not_offered_while_a_retained_attempt_has_no_correlation_to_read()
    {
        // RefreshStatusAsync skips the status read without a correlation id, and the proof read and
        // projection nudge below it are gated on Accepted/ProjectionPending. An uncorrelated RequestSent
        // therefore performed no read, produced no message and changed no state -- a dead control on the
        // destructive flow's recovery path. Abandon must still be reachable.
        TaskCompletionSource<TenantCommandSubmissionResult> submissionGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            SubmissionGate = submissionGate,
        };
        RegisterServices(gateway);

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent));

        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Find("[data-testid='tenants-config-remove-refresh']").GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='tenants-config-remove-abandon']");

        // Once acceptance supplies a correlation id, the same control becomes live again.
        submissionGate.SetResult(TenantCommandSubmissionResult.Accepted(
            cut.Instance.Snapshot.MessageId ?? "01ARZ3NDEKTSV4RRFFQ69G5FAA",
            "correlation-config-remove"));
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => cut.Instance.Snapshot.CorrelationId.ShouldNotBeNull());
        cut.WaitForAssertion(() => cut
            .Find("[data-testid='tenants-config-remove-refresh']")
            .GetAttribute("disabled")
            .ShouldBeNull());
    }

    [Fact]
    public void Refresh_and_cancel_are_not_descendants_of_the_hidden_form()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<RemoveTenantConfigurationFlow> cut = Render<RemoveTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, Context("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))
            .Add(p => p.TargetKey, "billing.mode")
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-remove-cancel']").Closest(".tenants-config-remove__form").ShouldBeNull();
        cut.Find("[data-testid='tenants-config-remove-refresh']").Closest(".tenants-config-remove__form").ShouldBeNull();
        cut.Find("[data-testid='tenants-config-remove-submit']").Closest(".tenants-config-remove__form").ShouldNotBeNull();
    }

    private void RegisterServices(ITenantCommandGateway? gateway = null)
    {
        if (gateway is not null)
        {
            Services.AddSingleton(gateway);
        }

        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.SupportsRemoveConfigurationPreview.Returns(true);
        queryGateway.GetRemoveConfigurationPreviewAsync(
                Arg.Any<TenantRemoveConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Preview(call.Arg<TenantRemoveConfigurationIntent>())));
        queryGateway.GetRemoveConfigurationProjectionProofAsync(
                Arg.Any<TenantRemoveConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Proof(
                call.Arg<TenantRemoveConfigurationIntent>(),
                TenantConfigurationProjectionProofKind.RemoveNotConfirmed)));
        Services.AddSingleton(queryGateway);
        Services.AddSingleton(new TenantRemoveConfigurationAttemptTracker());

        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
    }

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

    private static TenantConfigurationManagementContext Context(
        string tenantId,
        IReadOnlyDictionary<string, string> configuration)
    {
        string[] authorizedPrefixes = configuration.Keys
            .Select(Namespace)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        // No filtering here. This helper previously dropped rows through a verbatim copy of the old
        // deny-list, which meant the "redaction" assertions below were satisfied by the fixture rather
        // than by the component under test. The context now contains exactly what a caller passes.
        TenantConfigurationSafeRow[] rows = configuration
            .Select(item => new TenantConfigurationSafeRow(Namespace(item.Key), item.Key, item.Value))
            .ToArray();
        return TenantConfigurationManagementContext.Available(
            tenantId,
            TenantStatus.Active,
            false,
            authorizedPrefixes,
            rows,
            TenantConfigurationAuthorityState.TenantOwner);
    }

    // First dot segment. This is the *display* grouping a safe row carries, and it is deliberately NOT the
    // authorized-namespace resolution the flow performs: keeping the two identical is what made
    // ResolveAuthorizedNamespace unprovable, because every fixture grant equalled the first dot segment and
    // both implementations agreed by construction. Tests that care about prefix resolution build their own
    // context with a multi-segment grant -- see
    // Remove_preview_reports_the_longest_authorized_prefix_not_the_first_dot_segment.
    private static string Namespace(string key)
    {
        int separator = key.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? key[..separator] : key;
    }


    private static TenantRemoveConfigurationPreview Preview(TenantRemoveConfigurationIntent intent)
        => TenantRemoveConfigurationPreview.Create(
            intent,
            TenantStatus.Active,
            TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:10",
            isAuthorized: true);

    private static TenantConfigurationProjectionProof Proof(
        TenantRemoveConfigurationIntent intent,
        TenantConfigurationProjectionProofKind kind)
        => TenantConfigurationProjectionProof.Create(
            intent.TenantId,
            kind,
            "tenant-sequence:11",
            intent.AttemptFingerprint);

    private static TenantHighImpactActionEvidence Evidence()
        => new(
            "tenant.alpha",
            TenantHighImpactAction.RemoveConfiguration,
            TenantHighImpactEvaluationStage.PreviewEntry,
            TenantStatus.Active,
            TenantHighImpactFreshnessState.Current,
            HasCurrentBaseline: true,
            TenantDetailSurfaceKind.Ready,
            ProjectionLifecycleState.Current,
            TenantHighImpactAuthorityEvidence.Authorized,
            TenantHighImpactNamespaceScopeEvidence.Authorized,
            TenantHighImpactSupportEvidence.Ready,
            TenantHighImpactAdmissionEvidence.Available,
            TenantHighImpactPreviewEvidence.Ready,
            TenantHighImpactProofEvidence.NotRequired,
            TenantHighImpactViewportState.Safe,
            IsInputComplete: false,
            TenantHighImpactTargetState.Unknown,
            ProjectionVersion: null);

    private static string ProjectRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Hexalith.Tenants.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate project root.");
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedRemoveConfigurationDispatch => true;
        public TenantCommandSubmissionResult Submission { get; init; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TaskCompletionSource<TenantCommandSubmissionResult>? SubmissionGate { get; init; }

        public TenantCommandStatusResult Status { get; init; }
            = TenantCommandStatusResult.Unknown("Command status is unavailable.");

        public RemoveTenantConfiguration? LastRemoveConfigurationRequest { get; private set; }

        public int RemoveConfigurationCallCount { get; private set; }

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

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(RemoveTenantConfiguration request, CancellationToken cancellationToken = default)
        {
            RemoveConfigurationCallCount++;
            LastRemoveConfigurationRequest = request;
            return Task.FromResult(Submission);
        }

        public async Task<TenantCommandSubmissionResult> RemoveTenantConfigurationTrackedAsync(
            RemoveTenantConfiguration request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            RemoveConfigurationCallCount++;
            LastRemoveConfigurationRequest = request;
            TenantCommandSubmissionResult submission = SubmissionGate is null
                ? Submission
                : await SubmissionGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return submission.State is TenantCommandLifecycleState.Accepted
                ? TenantCommandSubmissionResult.Accepted(messageId, submission.CorrelationId!)
                : submission with { MessageId = messageId };
        }

        public int GetStatusCallCount { get; private set; }

        /// <summary>
        /// When set, status resolution blocks on this from <see cref="GateFromCall"/> onwards, so a refresh
        /// can be observed while it is genuinely in flight.
        /// </summary>
        public TaskCompletionSource<TenantCommandStatusResult>? StatusGate { get; init; }

        /// <summary>
        /// The 1-based call number from which <see cref="StatusGate"/> applies. Defaults to gating every
        /// call; set it to 2 to let the submit path's own status read resolve normally.
        /// </summary>
        public int GateFromCall { get; init; } = 1;

        /// <summary>
        /// Completes once a gated status read has actually returned inside this gateway, which happens
        /// strictly before the caller's own continuation resumes. Tests asserting that nothing follows a
        /// released gate need this: without it they asserted the absence at an instant when the resumed
        /// continuation had provably not run yet, so the assertion passed for the wrong reason.
        /// </summary>
        public TaskCompletionSource GateObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            if (StatusGate is null || GetStatusCallCount < GateFromCall)
            {
                return Task.FromResult(Status);
            }

            return AwaitGateAsync(StatusGate);

            async Task<TenantCommandStatusResult> AwaitGateAsync(TaskCompletionSource<TenantCommandStatusResult> gate)
            {
                try
                {
                    return await gate.Task.ConfigureAwait(false);
                }
                finally
                {
                    _ = GateObserved.TrySetResult();
                }
            }
        }
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.Configuration.Remove.Title"] = "Remove configuration",
            ["Tenants.Configuration.Remove.Description"] = "Prepare a scoped configuration removal for tenant {0} with projection confirmation.",
            ["Tenants.Configuration.Remove.Submit"] = "Confirm removal",
            ["Tenants.Configuration.Remove.Refresh"] = "Refresh status",
            ["Tenants.Configuration.Remove.Cancel"] = "Cancel",
            ["Tenants.Configuration.Remove.Confirmation.Label"] = "Type the full configuration key to confirm removal",
            ["Tenants.Configuration.Remove.Confirmation.Help"] = "Type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.Configuration.Remove.Lifecycle.Title"] = "Configuration removal lifecycle",
            ["Tenants.Configuration.Remove.Unavailable.Authorization"] = "You are not authorized to remove configuration for this tenant.",
            ["Tenants.Configuration.Remove.Unavailable.ProjectionState"] = "Tenant detail is unavailable or degraded. Refresh current tenant detail before removing configuration.",
            ["Tenants.Configuration.Remove.Unavailable.Freshness"] = "Refresh current tenant detail before removing configuration.",
            ["Tenants.Configuration.Remove.Unavailable.ProjectionLifecycle"] = "Removing configuration requires a current, projection-confirmed lifecycle.",
            ["Tenants.Configuration.Remove.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow configuration removal.",
            ["Tenants.Configuration.Remove.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.Configuration.Remove.Unavailable.TargetMissing"] = "The selected key is absent from current authorized evidence. Refresh and select a visible key; no removal was submitted.",
            ["Tenants.Configuration.Remove.Preview.FreshnessAndLifecycle.Value"] = "Read model: {0}; projection lifecycle: {1}.",
            ["Tenants.Configuration.Remove.Preview.ConsequencesAndUnknowns.Value"] = "Authorized consumers may react after projection catch-up; downstream impact and command-specific audit proof remain unknown.",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Present"] = "The key is present; its value is not displayed.",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Absent"] = "The key is absent.",
            ["Tenants.Configuration.Remove.Lifecycle.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.Lifecycle.Current"] = "Current",
            ["Tenants.Configuration.Remove.Lifecycle.Stale"] = "Stale",
            ["Tenants.Configuration.Remove.Lifecycle.Rebuilding"] = "Rebuilding",
            ["Tenants.Configuration.Remove.Lifecycle.Degraded"] = "Degraded",
            ["Tenants.Configuration.Remove.Lifecycle.Unavailable"] = "Unavailable",
            ["Tenants.Configuration.Remove.Lifecycle.LocalOnly"] = "Local only",
            ["Tenants.Configuration.Remove.Unavailable.Identity"] = "Tenant identity is unavailable, so configuration removal fails closed.",
            ["Tenants.Configuration.Remove.Unavailable.Scope"] = "No authorized namespace prefix evidence is available from the current projection.",
            ["Tenants.Configuration.Remove.Unavailable.Target"] = "The selected configuration key is not visible in the current authorized projection. Refresh tenant detail before trying again.",
            ["Tenants.Configuration.Remove.Unavailable.Narrow"] = "Configuration removal is unavailable on narrow layouts because preview, tenant identity, freshness, target key, and confirmed configuration context must remain visible together.",
            ["Tenants.Configuration.Remove.Validation.KeyRequired"] = "Select a visible configuration key before previewing removal.",
            ["Tenants.Configuration.Remove.Validation.KeyVisible"] = "The selected key cannot be proven from the current authorized projection.",
            ["Tenants.Configuration.Remove.Validation.NamespaceScope"] = "The namespace prefix cannot be proven from the current authorized projection.",
            ["Tenants.Configuration.Remove.Validation.ConfirmationRequired"] = "Type {0} exactly before removing this configuration key.",
            ["Tenants.Configuration.Remove.Preview.Title"] = "Consequence preview",
            ["Tenants.Configuration.Remove.Preview.Blocked.Required"] = "Complete tenant identity, namespace, key, current state, freshness, authorization, and scope evidence before submitting.",
            ["Tenants.Configuration.Remove.Preview.Tenant"] = "Tenant",
            ["Tenants.Configuration.Remove.Preview.Namespace"] = "Namespace",
            ["Tenants.Configuration.Remove.Preview.Key"] = "Full key",
            ["Tenants.Configuration.Remove.Preview.CurrentState"] = "Current known state",
            ["Tenants.Configuration.Remove.Preview.IntendedEffect"] = "Intended effect",
            ["Tenants.Configuration.Remove.Preview.IntendedEffect.Value"] = "The selected configuration key will be removed only after command acceptance and projection proof.",
            ["Tenants.Configuration.Remove.Preview.Freshness"] = "Freshness evidence",
            ["Tenants.Configuration.Remove.Preview.Authorization"] = "Authorization and scope evidence",
            ["Tenants.Configuration.Remove.Preview.Authorization.Value"] = "The namespace prefix and key are visible in the authorized tenant projection; backend authorization still enforces the command.",
            ["Tenants.Configuration.Remove.Preview.KnownConsequences"] = "Known consequences versus unknowns",
            ["Tenants.Configuration.Remove.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.Configuration.Remove.Preview.AuditExpectation.Value"] = "Projection confirmation and audit evidence remain distinct; inspect the tenant audit surface separately.",
            ["Tenants.Configuration.Remove.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.Configuration.Remove.Preview.RecoveryPath.Value"] = "Refresh tenant detail, retry only from current projection proof, or submit a forward correction to set the key again.",
            ["Tenants.Configuration.Remove.Freshness.Current"] = "Current",
            ["Tenants.Configuration.Remove.Freshness.Aging"] = "Aging",
            ["Tenants.Configuration.Remove.Freshness.Refreshing"] = "Refreshing",
            ["Tenants.Configuration.Remove.Freshness.Stale"] = "Stale",
            ["Tenants.Configuration.Remove.Freshness.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.State.Idle"] = "No configuration removal command submitted.",
            ["Tenants.Configuration.Remove.State.Previewed"] = "Configuration removal preview ready.",
            ["Tenants.Configuration.Remove.State.RequestSent"] = "Configuration removal request sent.",
            ["Tenants.Configuration.Remove.State.Accepted"] = "Accepted by EventStore; waiting for configuration removal processing.",
            ["Tenants.Configuration.Remove.State.ProjectionPending"] = "Projection pending; the key is not confirmed removed yet.",
            ["Tenants.Configuration.Remove.State.Confirmed"] = "Projection confirmed the selected configuration key is removed.",
            ["Tenants.Configuration.Remove.State.Rejected"] = "Configuration removal command rejected.",
            ["Tenants.Configuration.Remove.State.Failed"] = "Configuration removal submission failed.",
            ["Tenants.Configuration.Remove.State.Degraded"] = "Configuration removal result is degraded and needs review.",
            ["Tenants.Configuration.Remove.State.UnableToVerify"] = "Unable to verify the configuration removal result.",
            ["Tenants.Audit.EntryPoint.Accessible.Command"] = "Inspect audit for tenant {1} ({0})",
            ["Tenants.Audit.EntryPoint.CommandReason"] = "Command-specific proof is not available here; open the tenant audit list and use the visible audit state.",
            ["Tenants.Audit.EntryPoint.Label"] = "Audit evidence",
            ["Tenants.Audit.EntryPoint.Unavailable.ScopeRequired"] = "Tenant scope is required before audit evidence can be opened.",
            ["Tenants.Audit.EntryPoint.Unavailable.StaleScope"] = "Refresh tenant scope before opening audit evidence.",
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
            ["Tenants.Configuration.Remove.Recovery.Idle"] = "Choose a visible key when current projection evidence and namespace scope are available.",
            ["Tenants.Configuration.Remove.Recovery.Previewed"] = "Confirm removal, cancel, or continue read-only.",
            ["Tenants.Configuration.Remove.Recovery.RequestSent"] = "Wait for command status and projection refresh.",
            ["Tenants.Configuration.Remove.Recovery.Accepted"] = "Wait, refresh status, or continue read-only until projection confirms removal.",
            ["Tenants.Configuration.Remove.Recovery.ProjectionPending"] = "Refresh tenant detail; do not display removal as complete until the key is absent from projection.",
            ["Tenants.Configuration.Remove.Recovery.Confirmed"] = "Continue read-only or inspect audit when evidence becomes available.",
            ["Tenants.Configuration.Remove.Recovery.Rejected"] = "Refresh projection evidence, request permission, start correction, or escalate.",
            ["Tenants.Configuration.Remove.Recovery.Failed"] = "Retry after checking current projection evidence or escalate.",
            ["Tenants.Configuration.Remove.Recovery.Degraded"] = "Wait, retry status lookup, inspect audit when available, or escalate.",
            ["Tenants.Configuration.Remove.Recovery.UnableToVerify"] = "Refresh, retry status lookup, continue read-only, or escalate.",
            ["Tenants.Configuration.Remove.Abandon"] = "Stop local reconciliation",
            ["Tenants.Configuration.Remove.Status.Pending"] = "Command status is not authoritative yet. Keep reconciling before taking another action.",
            ["Tenants.Configuration.Remove.Status.PublishFailed"] = "The removal command was accepted, but publication could not be verified.",
            ["Tenants.Configuration.Remove.Status.Rejected"] = "The configuration removal command was rejected.",
            ["Tenants.Configuration.Remove.Submission.Failed"] = "The removal command could not be submitted safely.",
            ["Tenants.Configuration.Remove.Submission.Rejected"] = "The removal command was rejected before acceptance.",
            ["Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous"] = "Submission delivery is uncertain. Do not submit again; refresh status or stop local reconciliation.",
            ["Tenants.Configuration.Remove.UnableToVerify.Abandoned"] = "Local reconciliation stopped. Refresh authoritative tenant detail before taking another action.",
            ["Tenants.Configuration.Remove.UnableToVerify.MissingEventEvidence"] = "Command status did not prove that a removal event was stored or published.",
            ["Tenants.Configuration.Remove.UnableToVerify.ProjectionProof"] = "Removal event evidence is retained, but projection confirmation could not be verified. Refresh and reconcile this attempt.",
            ["Tenants.Configuration.Remove.UnableToVerify.Status"] = "Removal command status could not be verified.",
            ["Tenants.Configuration.Remove.UnableToVerify.StatusTimeout"] = "Removal command status timed out before projection confirmation.",
            ["Tenants.Configuration.Remove.UnableToVerify.TrackingMismatch"] = "Command identity evidence did not match the retained removal attempt.",
            ["Tenants.Configuration.Remove.Unavailable.PreviewCapability"] = "Safe removal preview and proof support is unavailable. Refresh tenant detail before trying again.",
            ["Tenants.Configuration.Remove.Unavailable.PreviewEvidence"] = "A fresh, authorized and complete removal preview could not be verified. Refresh tenant detail and try again.",
            ["Tenants.Configuration.Remove.Unavailable.TrackedDispatch"] = "Tracked configuration removal dispatch is unavailable.",
            ["Tenants.Audit.Availability.State.Available"] = "Audit available",
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
