using System.Reflection;

using Bunit;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
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

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class SetTenantConfigurationFlowTests : FluentBunitContext
{
    [Fact]
    public void TenantNamedGlobalAdministratorsConfirmsConfigurationThroughStatusGateway()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IEventStoreGatewayClient client = Substitute.For<IEventStoreGatewayClient>();
        client.SubmitCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResponse("correlation-configuration"));
        using HttpClient statusClient = new(new TenantConfigurationStatusHandler())
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        };
        TenantCommandGateway gateway = new(client, Substitute.For<IUlidFactory>(), statusClient);
        Services.AddLocalization();
        Services.AddSingleton<ITenantCommandGateway>(gateway);
        Services.AddSingleton(new TenantSetConfigurationAttemptTracker());
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "global-administrators", TenantStatus.Active, false, ["billing"], [],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<SetTenantConfigurationFlow> cut = Render<SetTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.PreviewEvidenceProvider, intent => Task.FromResult(
                Preview(intent, TenantSetConfigurationCurrentState.Different, "tenant-sequence:41")))
            .Add(p => p.ProjectionEvidenceProvider, intent => Task.FromResult(
                Proof(intent, TenantConfigurationProjectionProofKind.SetConfirmed, "tenant-sequence:42"))));

        CompleteForm(cut, "mode", "enabled");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Find("[data-testid='tenants-config-set-lifecycle']").TextContent
            .ShouldContain("confirmed", Case.Insensitive);
    }

    [Fact]
    public void Audit_entry_with_invalid_detail_return_stays_disabled_instead_of_using_bare_detail()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway, Context(["billing"]), intent => Preview(intent, TenantSetConfigurationCurrentState.Absent));
        typeof(SetTenantConfigurationFlow).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, TenantSetConfigurationCommandSnapshot.Idle() with
            {
                AuditState = TenantCommandAuditState.AuditAvailable,
            });
        cut.Render();

        AngleSharp.Dom.IElement entry = cut.Find("[data-testid='tenants-command-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull();
        entry.GetAttribute("href").ShouldBeNull();
        cut.Find("#" + entry.GetAttribute("aria-describedby")).TextContent
            .ShouldContain("invalid", Case.Insensitive);
    }

    [Fact]
    public void Namespace_and_suffix_are_composed_once_with_literal_case_and_preview_is_ten_fact_redacted()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        TenantSetConfigurationIntent? observed = null;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["Billing"]),
            intent =>
            {
                observed = intent;
                return Preview(intent, TenantSetConfigurationCurrentState.Different);
            });

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("Mode");
        cut.Find("[data-testid='tenants-config-set-value']").Change("Raw-Secret");

        observed.ShouldNotBeNull();
        observed.NamespacePrefix.ShouldBe("Billing");
        observed.KeySuffix.ShouldBe("Mode");
        observed.FullKey.ShouldBe("Billing.Mode");
        cut.FindAll("[data-testid='tenants-config-set-preview-item']").Count.ShouldBe(10);
        cut.Find("[data-testid='tenants-config-set-preview-key']").TextContent.ShouldBe("Billing.Mode");
        cut.Find("[data-testid='tenants-config-set-preview']").TextContent.ShouldNotContain("Raw-Secret", Case.Sensitive);
    }

    [Fact]
    public void Non_global_operator_selects_only_server_reflected_namespace()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        TenantSetConfigurationIntent? observed = null;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing", "security"]),
            intent =>
            {
                observed = intent;
                return Preview(intent, TenantSetConfigurationCurrentState.Absent);
            });

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-config-set-namespace", "security");
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("Mode");
        cut.Find("[data-testid='tenants-config-set-value']").Change("enabled");

        observed.ShouldNotBeNull().FullKey.ShouldBe("security.Mode");
        cut.Find("[data-testid='tenants-config-set-namespace']").TagName.ShouldBe("FLUENT-DROPDOWN");
    }

    [Fact]
    public void Global_administrator_enters_literal_namespace_without_normalization()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        TenantSetConfigurationIntent? observed = null;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["*"], globalAdministrator: true),
            intent =>
            {
                observed = intent;
                return Preview(intent, TenantSetConfigurationCurrentState.Absent);
            });

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-namespace']").Change("Custom.Space");
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("Key");
        cut.Find("[data-testid='tenants-config-set-value']").Change("value");

        observed.ShouldNotBeNull().FullKey.ShouldBe("Custom.Space.Key");
        cut.Find("[data-testid='tenants-config-set-namespace']").TagName.ShouldBe("FLUENT-TEXT-INPUT");
    }

    /// <summary>
    /// Verifies that both close paths return focus to a rendered launcher or the lifecycle section.
    /// </summary>
    /// <param name="continueReadOnly">Whether to close through the audit recovery instead of Cancel.</param>
    /// <param name="launcherAvailable">Whether the host still permits rendering the Open launcher.</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ClosingTheEditorFocusesOpenWhenAvailableAndTheLifecycleSectionOtherwise(
        bool continueReadOnly,
        bool launcherAvailable)
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.StatusAsync = _ => Task.FromResult(new TenantCommandStatusResult(
            CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));
        int closeCount = 0;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different),
            intent => Proof(intent, TenantConfigurationProjectionProofKind.SetConfirmed, "tenant-sequence:42"));
        cut.Render(parameters => parameters.Add(p => p.OnCloseRequested, () => closeCount++));
        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        if (!launcherAvailable)
        {
            cut.Render(parameters => parameters
                .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
                .Add(p => p.Freshness, ReadModelFreshnessState.Stale));
        }

        int focusCallsBeforeClose = FocusCalls().Count;
        cut.Find(continueReadOnly
            ? "[data-testid='tenants-config-set-audit'] [data-recovery-verb='continuereadonly']"
            : "[data-testid='tenants-config-set-cancel']").Click();

        cut.FindAll("[data-testid='tenants-config-set-key-suffix']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-config-set-open']").Count.ShouldBe(launcherAvailable ? 1 : 0);
        cut.FindAll("[data-testid='tenants-config-set-audit'] [data-recovery-verb='continuereadonly']").ShouldBeEmpty();
        cut.WaitForAssertion(() => closeCount.ShouldBe(1));
        cut.WaitForAssertion(() => FocusCalls().Count.ShouldBeGreaterThan(focusCallsBeforeClose));
        string expectedReferenceId = ((ElementReference)typeof(SetTenantConfigurationFlow)
            .GetField(launcherAvailable ? "_openElement" : "_lifecycleElement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Id;
        FocusCalls().Last().ShouldBe(expectedReferenceId);
        gateway.SetConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public void Global_administrator_open_focuses_the_required_namespace_before_the_key()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["*"], globalAdministrator: true),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Absent));

        cut.Find("[data-testid='tenants-config-set-open']").Click();

        ElementReference namespaceReference = (ElementReference)(cut.Instance.GetType()
            .GetField("_namespaceElement", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(cut.Instance)
            ?? throw new InvalidOperationException("Namespace focus reference is unavailable."));
        string focusedReferenceId = JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Last()
            .Id;

        focusedReferenceId.ShouldBe(namespaceReference.Id);
    }

    [Theory]
    [InlineData(248, true)]
    [InlineData(249, false)]
    public void Full_key_enforces_256_character_boundary_after_single_composition(int suffixLength, bool valid)
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Absent));

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change(new string('k', suffixLength));
        cut.Find("[data-testid='tenants-config-set-value']").Change("value");

        cut.FindAll("[data-testid='tenants-config-set-preview']").Any().ShouldBe(valid);
    }

    [Theory]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public void Value_enforces_1024_character_boundary(int valueLength, bool valid)
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Absent));

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("mode");
        cut.Find("[data-testid='tenants-config-set-value']").Change(new string('v', valueLength));

        cut.FindAll("[data-testid='tenants-config-set-preview']").Any().ShouldBe(valid);
    }

    [Fact]
    public void Exact_matching_preview_is_already_applied_without_dispatch()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Matching));

        CompleteForm(cut, "mode", "secret");
        cut.Find("form").Submit();

        gateway.SetConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.AlreadyApplied);
        cut.Markup.ShouldNotContain("secret", Case.Sensitive);
        cut.Find("[data-testid='tenants-config-set-submit']").GetAttribute("disabled").ShouldNotBeNull();
    }

    [Fact]
    public void Dispatch_uses_caller_message_id_aggregate_status_and_causal_projection_advancement()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        string projectionVersion = "tenant-sequence:41";
        TenantCommandTrackingHandle? statusHandle = null;
        gateway.StatusAsync = handle =>
        {
            statusHandle = handle;
            return Task.FromResult(new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true));
        };
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different, "tenant-sequence:41"),
            intent => Proof(intent, TenantConfigurationProjectionProofKind.SetConfirmed, projectionVersion));

        CompleteForm(cut, "mode", "secret");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        gateway.SetConfigurationCallCount.ShouldBe(1);
        gateway.LastMessageId.ShouldNotBeNullOrWhiteSpace();
        gateway.LastSetConfigurationRequest.ShouldNotBeNull().Key.ShouldBe("billing.mode");
        statusHandle.ShouldNotBeNull().AggregateId.ShouldBe("tenant.alpha");
        cut.Markup.ShouldNotContain("secret", Case.Sensitive);

        projectionVersion = "tenant-sequence:42";
        cut.Find("[data-testid='tenants-config-set-refresh']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.SetConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public void Rejected_submission_implies_no_audit_state()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.SubmissionFactory = (_, _) => TenantCommandSubmissionResult.Rejected(
            "You are not authorized to set configuration for this tenant.",
            "InsufficientPermissions");
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Rejected));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-audit-availability']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("proof-read-throws")]
    [InlineData("proof-read-cancelled")]
    [InlineData("projection-refresh-throws")]
    public void A_failed_projection_read_after_events_are_stored_keeps_the_audit_record_pending(string failure)
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.StatusAsync = _ => Task.FromResult(new TenantCommandStatusResult(
            CommandStatus.EventsStored,
            HasVerifiedCommandIdentity: true));
        IRenderedComponent<SetTenantConfigurationFlow> cut = Render<SetTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, Context(["billing"]))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.PreviewEvidenceProvider, intent => Task.FromResult(Preview(intent, TenantSetConfigurationCurrentState.Different)))
            .Add(p => p.OnProjectionRefreshRequested, (Func<Task>)(() => failure == "projection-refresh-throws"
                ? Task.FromException(new InvalidOperationException("Projection refresh failed."))
                : Task.CompletedTask))
            .Add(p => p.ProjectionEvidenceProvider, intent => failure switch
            {
                "proof-read-throws" => throw new InvalidOperationException("Projection read failed."),
                "proof-read-cancelled" => Task.FromCanceled<TenantConfigurationProjectionProof>(new CancellationToken(canceled: true)),
                _ => Task.FromResult(Proof(intent, TenantConfigurationProjectionProofKind.SetNotConfirmed, "tenant-sequence:41")),
            }));

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();

        // The projection read failed, so the command is unverified; the events-stored audit dimension stands.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.SafeMessageKey
            .ShouldBe("Tenants.Configuration.Set.UnableToVerify.ProjectionRefresh"));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
        cut.Find("[data-testid='tenants-config-set-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("pending");
    }

    [Fact]
    public async Task Retention_expiry_delays_audit_and_withdraws_refresh_for_the_released_attempt()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.SubmissionFactory = (_, messageId) => TenantCommandSubmissionResult.Ambiguous(
            messageId,
            "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        gateway.StatusAsync = _ => Task.FromResult(TenantCommandStatusResult.Pending("Status is propagating."));
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue());
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.Find("[data-testid='tenants-config-set-audit'] [data-recovery-verb='refresh']");

        await cut.InvokeAsync(() => cut.Instance.ExpireRetainedAttemptAsync(
            cut.Instance.Snapshot.AttemptStartedAtUtc!.Value
                + TenantSetConfigurationCommandSnapshot.MaximumRetainedAttemptDuration));

        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse());
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        cut.Find("[data-testid='tenants-config-set-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("delayed");
        cut.FindAll("[data-testid='tenants-config-set-audit'] [data-recovery-verb='refresh']").ShouldBeEmpty();
    }

    [Fact]
    public void Abandoning_a_retained_attempt_stays_unavailable_and_continue_read_only_then_closes_the_editor()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.SubmissionFactory = (_, messageId) => TenantCommandSubmissionResult.Ambiguous(
            messageId,
            "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        gateway.StatusAsync = _ => Task.FromResult(TenantCommandStatusResult.Pending("Status is propagating."));
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));
        const string audit = "[data-testid='tenants-config-set-audit']";

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();

        // The retained ambiguous attempt can be re-queried and cannot be closed: Refresh is offered, Continue
        // read-only is not.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        cut.FindAll($"{audit} [data-recovery-verb='continuereadonly']").ShouldBeEmpty();

        // Operator abandonment is not retention expiry: the audit record stays unavailable, never delayed.
        cut.Find("[data-testid='tenants-config-set-abandon']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse(), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-recovery-verb='refresh']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        cut.Find($"{audit} [data-testid='tenants-audit-availability']").GetAttribute("data-state").ShouldBe("unavailable");
        cut.Find($"{audit} [data-recovery-verb='continuereadonly']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-config-set-key-suffix']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        gateway.SetConfigurationCallCount.ShouldBe(1);

        // With the editor closed there is nothing left to continue from, so the recovery is withdrawn.
        cut.WaitForAssertion(
            () => cut.FindAll($"{audit} [data-recovery-verb='continuereadonly']").ShouldBeEmpty(),
            TimeSpan.FromSeconds(5));
        cut.Find($"{audit} [data-testid='tenants-audit-availability']");
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_lookup_wait_for_it_then_run_their_own_and_never_exhaust_the_retry_limit()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.SubmissionFactory = (_, messageId) => TenantCommandSubmissionResult.Ambiguous(
            messageId,
            "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        TaskCompletionSource lookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusCalls = 0;
        gateway.StatusAsync = async _ =>
        {
            if (Interlocked.Increment(ref statusCalls) == 1)
            {
                lookupStarted.SetResult();
                await releaseLookup.Task.ConfigureAwait(false);
            }

            return TenantCommandStatusResult.Pending("Status is propagating.");
        };
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));
        const string refresh = "[data-testid='tenants-config-set-audit'] [data-recovery-verb='refresh']";

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        // A SignalR nudge starts a slow lookup; the shared control's Refresh then merges into it.
        Task nudge = cut.InvokeAsync(() => cut.Instance.ApplySignalRNudgeAsync());
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());
        cut.Find(refresh).Click();
        cut.Find(refresh).Click();

        // The merged click waits for the running lookup; nothing counted as an unchanged retry yet.
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        releaseLookup.SetResult();
        await nudge.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();
        // The nudge's lookup started before the click, so the merged click runs one lookup of its own after it:
        // a click is never counted as a retry that no lookup served. Three clicks while it waited are one request.
        cut.WaitForAssertion(() => Volatile.Read(ref statusCalls).ShouldBe(2), TimeSpan.FromSeconds(5));

        // Once the merged refresh has settled, no later lookup followed the one it ran.
        await merged.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusCalls).ShouldBe(2);
        gateway.SetConfigurationCallCount.ShouldBe(1);
    }

    [Fact]
    public void Audit_refresh_that_resolves_to_not_started_moves_focus_to_the_lifecycle_section()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        int statusCalls = 0;
        // The first lookup cannot be read; the user's Refresh then finds the command still processing.
        gateway.StatusAsync = _ => Task.FromResult(++statusCalls == 1
            ? TenantCommandStatusResult.Unknown("Command status is unavailable.")
            : new TenantCommandStatusResult(CommandStatus.Processing, HasVerifiedCommandIdentity: true));
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));
        const string audit = "[data-testid='tenants-config-set-audit']";

        CompleteForm(cut, "mode", "value");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Find($"{audit} [data-recovery-verb='refresh']"), TimeSpan.FromSeconds(5));
        string lifecycleReferenceId = ((ElementReference)typeof(SetTenantConfigurationFlow)
            .GetField("_lifecycleElement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Id;
        int focusCallsBeforeRefresh = FocusCalls().Count;

        cut.Find($"{audit} [data-recovery-verb='refresh']").Click();

        // The polite NotStarted state unmounts the control and its focused Refresh button, so focus lands on the
        // lifecycle section instead of falling back to the document body.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted), TimeSpan.FromSeconds(5));
        cut.Instance.Snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Polite);
        cut.Instance.Snapshot.FocusTarget.ShouldNotBe(TenantCommandFocusTarget.Lifecycle);
        cut.WaitForAssertion(() => cut.FindAll($"{audit} [data-testid='tenants-audit-availability']").ShouldBeEmpty(), TimeSpan.FromSeconds(5));
        SpinWait.SpinUntil(() => FocusCalls().Count > focusCallsBeforeRefresh, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        FocusCalls().Last().ShouldBe(lifecycleReferenceId);
    }

    private List<string> FocusCalls()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Select(reference => reference.Id ?? string.Empty)];

    [Fact]
    public void Ambiguous_attempt_is_adopted_and_reconciled_after_remount_without_redispatch()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        gateway.SubmissionFactory = (_, messageId) => TenantCommandSubmissionResult.Ambiguous(
            messageId,
            "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        Func<TenantSetConfigurationIntent, TenantSetConfigurationPreview> preview = intent =>
            Preview(intent, TenantSetConfigurationCurrentState.Different);
        Func<TenantSetConfigurationIntent, TenantConfigurationProjectionProof> proof = intent =>
            Proof(intent, TenantConfigurationProjectionProofKind.SetConfirmed, "tenant-sequence:42");

        IRenderedComponent<SetTenantConfigurationFlow> first = RenderFlow(gateway, Context(["billing"]), preview, proof);
        CompleteForm(first, "mode", "secret");
        first.Find("form").Submit();
        first.WaitForAssertion(() => first.Instance.Snapshot.RetainsAttempt.ShouldBeTrue());
        first.Dispose();

        IRenderedComponent<SetTenantConfigurationFlow> remounted = RenderFlow(gateway, Context(["billing"]), preview, proof);
        remounted.WaitForAssertion(() => remounted.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.SetConfigurationCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(1);
        gateway.LastStatusHandle.ShouldNotBeNull().MessageId.ShouldBe(gateway.LastMessageId);
        gateway.LastStatusHandle.CorrelationId.ShouldBe(gateway.LastMessageId);
    }

    [Theory]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("zero\u200bwidth")]
    [InlineData("non\u00a0breaking")]
    [InlineData("tag\U000E0001character")]
    public void Unsafe_key_suffix_is_blocked_before_preview_or_dispatch(string suffix)
    {
        StubTenantCommandGateway gateway = RegisterServices();
        int previewCalls = 0;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent =>
            {
                previewCalls++;
                return Preview(intent, TenantSetConfigurationCurrentState.Absent);
            });

        CompleteForm(cut, suffix, "value");

        previewCalls.ShouldBe(0);
        gateway.SetConfigurationCallCount.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-config-set-preview']").ShouldBeEmpty();
    }

    [Fact]
    public void Interior_ascii_space_is_preserved_in_the_literal_key()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        TenantSetConfigurationIntent? observed = null;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent =>
            {
                observed = intent;
                return Preview(intent, TenantSetConfigurationCurrentState.Absent);
            });

        CompleteForm(cut, "service mode", "value");

        observed.ShouldNotBeNull().FullKey.ShouldBe("billing.service mode");
        cut.Find("[data-testid='tenants-config-set-preview-key']").TextContent
            .ShouldBe("billing.service mode");
    }

    [Fact]
    public void Canceled_preview_evidence_fails_closed_without_escaping_the_event_handler()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Absent),
            previewAsync: _ => Task.FromException<TenantSetConfigurationPreview>(new TaskCanceledException()));

        CompleteForm(cut, "mode", "value");

        cut.FindAll("[data-testid='tenants-config-set-preview']").ShouldBeEmpty();
        gateway.SetConfigurationCallCount.ShouldBe(0);
    }

    [Fact]
    public void Invalid_utf16_value_is_blocked_before_preview_or_dispatch()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        int previewCalls = 0;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent =>
            {
                previewCalls++;
                return Preview(intent, TenantSetConfigurationCurrentState.Absent);
            });

        CompleteForm(cut, "mode", "\uD800");

        previewCalls.ShouldBe(0);
        gateway.SetConfigurationCallCount.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-config-set-preview']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Expiry_and_abandonment_do_not_release_ownership_while_dispatch_is_running()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        var dispatch = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.SubmissionAsync = (_, _) => dispatch.Task;
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            Context(["billing"]),
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different));
        CompleteForm(cut, "mode", "value");

        Task submit = cut.InvokeAsync(() => cut.Find("form").Submit());
        cut.WaitForAssertion(() => gateway.SetConfigurationCallCount.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Instance.ExpireRetainedAttemptAsync(DateTimeOffset.UtcNow.AddMinutes(6)));

        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        cut.Find("[data-testid='tenants-config-set-abandon']").GetAttribute("disabled").ShouldNotBeNull();
        dispatch.SetResult(TenantCommandSubmissionResult.Ambiguous(
            gateway.LastMessageId!,
            "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous"));
        await submit;
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
    }

    [Fact]
    public async Task Freshness_change_during_submit_preview_blocks_before_lease_and_dispatch()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        var previewGate = new TaskCompletionSource<TenantSetConfigurationPreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool holdPreview = false;
        Func<TenantSetConfigurationIntent, Task<TenantSetConfigurationPreview>> provider = intent => holdPreview
            ? previewGate.Task
            : Task.FromResult(Preview(intent, TenantSetConfigurationCurrentState.Different));
        TenantConfigurationManagementContext context = Context(["billing"]);
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            context,
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different),
            previewAsync: provider);
        CompleteForm(cut, "mode", "value");
        holdPreview = true;

        Task submit = cut.InvokeAsync(() => cut.Find("form").Submit());
        await Task.Yield();
        cut.Render(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(p => p.Freshness, ReadModelFreshnessState.Stale)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.PreviewEvidenceProvider, provider)
            .Add(p => p.ProjectionEvidenceProvider, intent => Task.FromResult(
                Proof(intent, TenantConfigurationProjectionProofKind.SetNotConfirmed, "tenant-sequence:41"))));
        previewGate.SetResult(Preview(cut.Instance.Snapshot.Intent!, TenantSetConfigurationCurrentState.Different));
        await submit;

        gateway.SetConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();
    }

    [Fact]
    public async Task Tenant_change_during_submit_preview_cannot_dispatch_the_previous_tenant()
    {
        StubTenantCommandGateway gateway = RegisterServices();
        var previewGate = new TaskCompletionSource<TenantSetConfigurationPreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previewEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantSetConfigurationIntent? pendingIntent = null;
        bool holdPreview = false;
        Func<TenantSetConfigurationIntent, Task<TenantSetConfigurationPreview>> provider = intent => holdPreview
            ? HoldPreview(intent)
            : Task.FromResult(Preview(intent, TenantSetConfigurationCurrentState.Different));
        TenantConfigurationManagementContext firstContext = Context(["billing"]);
        IRenderedComponent<SetTenantConfigurationFlow> cut = RenderFlow(
            gateway,
            firstContext,
            intent => Preview(intent, TenantSetConfigurationCurrentState.Different),
            previewAsync: provider);
        CompleteForm(cut, "mode", "value");
        holdPreview = true;

        Task submit = cut.InvokeAsync(() => cut.Find("form").Submit());
        await previewEntered.Task;
        TenantConfigurationManagementContext nextContext = TenantConfigurationManagementContext.Available(
            "tenant.beta",
            TenantStatus.Active,
            isGlobalAdministrator: false,
            ["billing"],
            [],
            TenantConfigurationAuthorityState.TenantOwner);
        cut.Render(parameters => parameters
            .Add(p => p.Context, nextContext)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.PreviewEvidenceProvider, provider));
        previewGate.SetResult(Preview(pendingIntent!, TenantSetConfigurationCurrentState.Different));
        await submit;

        gateway.SetConfigurationCallCount.ShouldBe(0);
        cut.Instance.Snapshot.RetainsAttempt.ShouldBeFalse();

        Task<TenantSetConfigurationPreview> HoldPreview(TenantSetConfigurationIntent intent)
        {
            pendingIntent = intent;
            previewEntered.SetResult();
            return previewGate.Task;
        }
    }

    [Fact]
    public void Styles_keep_narrow_fail_closed_forced_colors_and_fluent_two_tokens()
    {
        string styles = File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Configuration",
            "SetTenantConfigurationFlow.razor.css"));

        styles.ShouldContain("@media (max-width: 767px)");
        styles.ShouldContain("::deep .tenants-config-set__form");
        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain("var(--colorBrandStroke1");
        styles.ShouldNotContain("--accent-fill-rest");
    }

    private StubTenantCommandGateway RegisterServices()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var gateway = new StubTenantCommandGateway();
        Services.AddLocalization();
        Services.AddSingleton<ITenantCommandGateway>(gateway);
        Services.AddSingleton(new TenantSetConfigurationAttemptTracker());
        return gateway;
    }

    private IRenderedComponent<SetTenantConfigurationFlow> RenderFlow(
        StubTenantCommandGateway gateway,
        TenantConfigurationManagementContext context,
        Func<TenantSetConfigurationIntent, TenantSetConfigurationPreview> preview,
        Func<TenantSetConfigurationIntent, TenantConfigurationProjectionProof>? proof = null,
        Func<TenantSetConfigurationIntent, Task<TenantSetConfigurationPreview>>? previewAsync = null)
        => Render<SetTenantConfigurationFlow>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.PreviewEvidenceProvider, previewAsync ?? (intent => Task.FromResult(preview(intent))))
            .Add(p => p.ProjectionEvidenceProvider, intent => Task.FromResult(
                proof?.Invoke(intent)
                    ?? Proof(intent, TenantConfigurationProjectionProofKind.SetNotConfirmed, "tenant-sequence:41"))));

    private static void CompleteForm(
        IRenderedComponent<SetTenantConfigurationFlow> cut,
        string suffix,
        string value)
    {
        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change(suffix);
        cut.Find("[data-testid='tenants-config-set-value']").Change(value);
    }

    private static TenantConfigurationManagementContext Context(
        IReadOnlyList<string> prefixes,
        bool globalAdministrator = false)
        => TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            globalAdministrator,
            prefixes,
            [],
            globalAdministrator
                ? TenantConfigurationAuthorityState.GlobalAdministrator
                : TenantConfigurationAuthorityState.TenantOwner);

    private static TenantSetConfigurationPreview Preview(
        TenantSetConfigurationIntent intent,
        TenantSetConfigurationCurrentState currentState,
        string projectionVersion = "tenant-sequence:41")
        => TenantSetConfigurationPreview.Create(
            intent,
            TenantStatus.Active,
            currentState,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            projectionVersion,
            isAuthorized: true);

    private static TenantConfigurationProjectionProof Proof(
        TenantSetConfigurationIntent intent,
        TenantConfigurationProjectionProofKind kind,
        string projectionVersion)
        => TenantConfigurationProjectionProof.Create(
            intent.TenantId,
            kind,
            projectionVersion,
            intent.AttemptFingerprint);

    private static string ProjectRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Hexalith.Tenants.slnx"))) return directory;
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate project root.");
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedSetConfigurationDispatch => true;
        public Func<SetTenantConfiguration, string, TenantCommandSubmissionResult>? SubmissionFactory { get; set; }
        public Func<SetTenantConfiguration, string, Task<TenantCommandSubmissionResult>>? SubmissionAsync { get; set; }
        public Func<TenantCommandTrackingHandle, Task<TenantCommandStatusResult>>? StatusAsync { get; set; }
        public SetTenantConfiguration? LastSetConfigurationRequest { get; private set; }
        public string? LastMessageId { get; private set; }
        public int SetConfigurationCallCount { get; private set; }
        public int StatusCallCount { get; private set; }
        public TenantCommandTrackingHandle? LastStatusHandle { get; private set; }

        public async Task<TenantCommandSubmissionResult> SetTenantConfigurationTrackedAsync(
            SetTenantConfiguration request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            SetConfigurationCallCount++;
            LastSetConfigurationRequest = request;
            LastMessageId = messageId;
            return SubmissionAsync is not null
                ? await SubmissionAsync(request, messageId)
                : SubmissionFactory?.Invoke(request, messageId)
                    ?? TenantCommandSubmissionResult.Accepted(messageId, "correlation-1");
        }

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(
            SetTenantConfiguration request,
            CancellationToken cancellationToken = default)
            => SetTenantConfigurationTrackedAsync(request, "legacy-message", cancellationToken);

        public Task<TenantCommandStatusResult> GetStatusAsync(
            TenantCommandTrackingHandle handle,
            CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            LastStatusHandle = handle;
            return StatusAsync?.Invoke(handle) ?? Task.FromResult(new TenantCommandStatusResult(
                    CommandStatus.Completed,
                    EventCount: 1,
                    HasVerifiedCommandIdentity: true));
        }

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
    }

}
