using System.Globalization;
using System.Reflection;

using Bunit;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Tenants;
using Hexalith.Tenants.UI.Components.Tenants.Members;
using Hexalith.Tenants.UI.Components.Tenants.Metadata;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Hexalith.Tenants.UI.Resources;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

/// <summary>Exercises held delivery and renderer replacement against the real shared circuit retention.</summary>
public sealed class TenantRetainedCommandFlowTests : FluentBunitContext
{
    public TenantRetainedCommandFlowTests()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        Services.AddSingleton(viewport);
    }

    /// <summary>Keyed-only failures keep their localized explanation through initial and reused delivery paths.</summary>
    [Theory]
    [InlineData("add", "en", "initial")]
    [InlineData("add", "fr", "initial")]
    [InlineData("change", "en", "initial")]
    [InlineData("change", "fr", "initial")]
    [InlineData("add", "en", "failed-resubmit")]
    [InlineData("add", "fr", "failed-resubmit")]
    [InlineData("change", "en", "failed-resubmit")]
    [InlineData("change", "fr", "failed-resubmit")]
    [InlineData("add", "en", "ambiguous-retry")]
    [InlineData("add", "fr", "ambiguous-retry")]
    [InlineData("change", "en", "ambiguous-retry")]
    [InlineData("change", "fr", "ambiguous-retry")]
    public async Task KeyedMemberFailurePreservesLocalizedExplanationAndRetryEvidence(string kind, string culture, string path)
    {
        const string key = "Tenants.Commands.Unavailable.InvalidTrackingReference";
        const string baseline = "tenant-sequence:1";
        const string previousMessage = "Earlier delivery could not be verified.";
        string expectedExplanation = culture is "fr"
            ? "Cette commande n'a pas pu être envoyée car son identifiant de suivi n'était pas valide. Actualisez le locataire et relancez l'action."
            : "This command could not be submitted because its tracking identifier was not valid. Refresh the tenant and start the action again.";
        List<(object Intent, string MessageId)> deliveries = [];
        ITenantCommandGateway gateway = RegisterGateway((intent, id) =>
        {
            deliveries.Add((intent, id));
            return Task.FromResult(path is not "initial" && deliveries.Count == 1
                ? TenantCommandSubmissionResult.Failed(previousMessage) with
                {
                    MessageId = id,
                    IsAmbiguousFailure = path is "ambiguous-retry",
                }
                : TenantCommandSubmissionResult.FailedWithKey(key));
        });
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(provider => new CultureBoundTenantsLocalizer(
            provider.GetRequiredService<IStringLocalizerFactory>().Create(typeof(TenantsResources)),
            CultureInfo.GetCultureInfo(culture)));
        string expectedAnnouncement = (kind, culture) switch
        {
            ("add", "fr") => "Échec de la soumission de la commande d'ajout de membre.",
            ("change", "fr") => "Échec de la soumission de la commande de changement de rôle.",
            ("add", _) => "Add-member command submission failed.",
            _ => "Change-role command submission failed.",
        };
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, baseline);
        Submit(kind, cut, send: false);
        await InvokeSubmissionAsync(cut, component);
        object original = Snapshot(component);
        Property<string>(original, "BaselineProjectionVersion").ShouldBe(baseline);
        if (path is not "initial")
        {
            Property<TenantCommandLifecycleState>(original, "State").ShouldBe(path is "ambiguous-retry"
                ? TenantCommandLifecycleState.RequestSent : TenantCommandLifecycleState.Failed);
            Property<string>(original, "SafeMessage").ShouldBe(previousMessage);
            await InvokeSubmissionAsync(cut, component);
        }

        string prefix = kind is "add" ? "add-member" : "change-role";
        string stateKey = kind is "add" ? "Tenants.AddMember.State.Failed" : "Tenants.ChangeRole.State.Failed";
        IStringLocalizer<TenantsResources> localizer = Services.GetRequiredService<IStringLocalizer<TenantsResources>>();
        localizer[key].ResourceNotFound.ShouldBeFalse();
        localizer[key].Value.ShouldBe(expectedExplanation);
        localizer[stateKey].ResourceNotFound.ShouldBeFalse();
        localizer[stateKey].Value.ShouldBe(expectedAnnouncement);
        cut.WaitForAssertion(() =>
        {
            object failed = Snapshot(component);
            Property<TenantCommandLifecycleState>(failed, "State").ShouldBe(TenantCommandLifecycleState.Failed);
            Property<string>(failed, "SafeMessage").ShouldBeNull();
            Property<string>(failed, "SafeMessageKey").ShouldBe(key);
            Property<string>(failed, "MessageId").ShouldBe(deliveries[0].MessageId);
            Property<string>(failed, "CorrelationId").ShouldBeNull();
            Property<object>(failed, "Intent").ShouldBe(deliveries[0].Intent);
            Property<string>(failed, "BaselineProjectionVersion").ShouldBe(baseline);
            Property<TenantCommandAuditState>(failed, "AuditState").ShouldBe(path is "initial"
                ? TenantCommandAuditState.NotStarted : TenantCommandAuditState.AuditUnavailable);
            cut.Find($"[data-testid='tenants-{prefix}-safe-message']").TextContent.ShouldBe(expectedExplanation);
            var announcement = cut.Find($"[data-testid='tenants-{prefix}-live-region']");
            announcement.TextContent.Trim().ShouldBe(expectedAnnouncement);
            announcement.GetAttribute("aria-live").ShouldBe("assertive");
            string auditSelector = $"[data-testid='tenants-{prefix}-audit'] [data-testid='tenants-audit-availability']";
            if (path is "initial")
            {
                cut.FindAll(auditSelector).ShouldBeEmpty();
            }
            else
            {
                cut.Find(auditSelector).GetAttribute("data-state").ShouldBe("unavailable");
            }
        });
        int expectedDeliveries = path is "initial" ? 1 : 2;
        DispatchCount(gateway, kind).ShouldBe(expectedDeliveries);
        deliveries.Count.ShouldBe(expectedDeliveries);
        deliveries[0].MessageId.ShouldNotBeNullOrWhiteSpace();
        foreach (var delivery in deliveries)
        {
            delivery.ShouldBe(deliveries[0]);
        }
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantCommandGateway.GetStatusAsync)).ShouldBe(0);
        cut.Markup.ShouldNotContain(key);
        cut.Markup.ShouldNotContain(previousMessage);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("create", false)]
    [InlineData("create", true)]
    [InlineData("add", false)]
    [InlineData("add", true)]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    public async Task HeldDeliverySurvivesReplacementBeforeOrAfterCompletion(string kind, bool adoptBeforeCompletion)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, _) => delivery.Task);
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        object initial = Snapshot(component);
        Property<string>(initial, "MessageId").ShouldNotBeNullOrWhiteSpace();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        gate.TryAcquire(TenantCommandAggregateLock.ForTenant("tenant.beta"), new object()).ShouldBeTrue();
        gate.TryAcquire(TenantCommandAggregateLock.ForTenant("tenant.alpha"), new object()).ShouldBeFalse();
        ((IDisposable)component).Dispose();
        IRenderedComponent<IComponent>? replacement = null;
        object? replacementComponent = null;
        if (adoptBeforeCompletion)
        {
            (replacement, replacementComponent) = RenderFlow(kind);
            if (kind is "create")
            {
                Submit(kind, replacement);
            }
            Property<TenantCommandLifecycleState>(Snapshot(replacementComponent), "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
        }
        delivery.SetResult(TenantCommandSubmissionResult.Accepted(Property<string>(initial, "MessageId")!, "correlation-original"));
        await WaitForAsync(() => ReadRetained(tracker, kind) is { } retained
            && Property<string>(retained, "CorrelationId") is "correlation-original");
        if (!adoptBeforeCompletion)
        {
            (replacement, replacementComponent) = RenderFlow(kind);
            if (kind is "create")
            {
                Submit(kind, replacement);
            }
        }
        await WaitForAsync(() => Property<string>(Snapshot(replacementComponent!), "CorrelationId") is "correlation-original");
        object latest = Snapshot(replacementComponent!);
        Property<string>(latest, "MessageId").ShouldBe(Property<string>(initial, "MessageId"));
        Property<string>(latest, "BaselineProjectionVersion").ShouldBe(Property<string>(initial, "BaselineProjectionVersion"));
        Property<object>(latest, "Intent").ShouldBe(Property<object>(initial, "Intent"));
        if (kind is "create" or "remove" or "metadata")
        {
            Property<DateTimeOffset?>(latest, "AttemptStartedAtUtc").ShouldBe(Property<DateTimeOffset?>(initial, "AttemptStartedAtUtc"));
        }
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(1);
        // A different typed surface cannot replace the retained attempt or obtain its admission.
        (IRenderedComponent<IComponent> competitor, _) = RenderFlow(kind is "create" ? "add" : "create");
        Submit(kind is "create" ? "add" : "create", competitor);
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind is "create" ? "add" : "create")).ShouldBe(0);
    }

    /// <summary>Unavailable parameters and reentry cannot replace a request whose delivery is pending.</summary>
    [Theory]
    [InlineData("create", "freshness", true)]
    [InlineData("create", "freshness", false)]
    [InlineData("create", "command-surface", true)]
    [InlineData("create", "command-surface", false)]
    [InlineData("add", "freshness", true)]
    [InlineData("add", "freshness", false)]
    [InlineData("add", "command-surface", true)]
    [InlineData("add", "command-surface", false)]
    [InlineData("change", "freshness", true)]
    [InlineData("change", "freshness", false)]
    [InlineData("change", "command-surface", true)]
    [InlineData("change", "command-surface", false)]
    public async Task HeldSubmissionPreservesOriginalEvidenceWhenAvailabilityChanges(string kind, string deniedGate, bool accepted)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        object? dispatchedIntent = null;
        string? dispatchedMessageId = null;
        ITenantCommandGateway gateway = RegisterGateway((intent, id) =>
        {
            dispatchedIntent = intent;
            dispatchedMessageId = id;
            return delivery.Task;
        });
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        try
        {
            await WaitForAsync(() => DispatchCount(gateway, kind) == 1);
            object initial = Snapshot(component);
            string messageId = Property<string>(initial, "MessageId").ShouldNotBeNull();
            messageId.ShouldNotBeNullOrWhiteSpace();
            dispatchedMessageId.ShouldBe(messageId);
            dispatchedIntent.ShouldBe(Property<object>(initial, "Intent"));
            FieldInfo attemptField = component.GetType().GetField("_retainedAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!;
            TenantRetainedCommandAttempt originalAttempt = ((TenantRetainedCommandAttempt?)attemptField.GetValue(component)).ShouldNotBeNull();
            TenantAggregateCommandLease originalLease = originalAttempt.Lease;
            object originalOwner = originalAttempt.LeaseOwner;
            Property<TenantCommandLifecycleState>(initial, "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
            Property<string>(initial, "BaselineProjectionVersion").ShouldBe("tenant-sequence:1");
            ReadRetained(tracker, kind).ShouldBeSameAs(initial);

            var parameters = new Dictionary<string, object?>();
            if (deniedGate is "command-surface")
            {
                parameters["IsCommandSurfaceAvailable"] = false;
            }
            else if (kind is "create")
            {
                parameters["IsFresh"] = false;
            }
            else
            {
                parameters["Freshness"] = ReadModelFreshnessState.Stale;
            }
            await cut.InvokeAsync(() => ((IComponent)component).SetParametersAsync(ParameterView.FromDictionary(parameters)));
            Snapshot(component).ShouldBeSameAs(initial);
            await InvokeSubmissionAsync(cut, component).WaitAsync(TimeSpan.FromSeconds(5));
            Snapshot(component).ShouldBeSameAs(initial);
            ReadRetained(tracker, kind).ShouldBeSameAs(initial);
            tracker.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
            attemptField.GetValue(component).ShouldBeSameAs(originalAttempt);
            tracker.IsAttached(originalAttempt, component).ShouldBeTrue();
            originalAttempt.Lease.ShouldBeSameAs(originalLease);
            originalLease.CurrentOwner.ShouldBeSameAs(originalOwner);
            originalLease.IsDispatchMarked.ShouldBeTrue();
            originalLease.IsReleased.ShouldBeFalse();
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
            gate.TryAcquire(TenantCommandAggregateLock.ForTenant("tenant.alpha"), new object()).ShouldBeFalse();
            DispatchCount(gateway, kind).ShouldBe(1);

            delivery.SetResult(accepted
                ? TenantCommandSubmissionResult.Accepted(messageId, "correlation-original")
                : TenantCommandSubmissionResult.Failed("Command submission failed safely."));
            await submission.WaitAsync(TimeSpan.FromSeconds(5));
            object latest = Snapshot(component);
            Property<string>(latest, "MessageId").ShouldBe(messageId);
            Property<object>(latest, "Intent").ShouldBe(Property<object>(initial, "Intent"));
            Property<string>(latest, "BaselineProjectionVersion").ShouldBe(Property<string>(initial, "BaselineProjectionVersion"));
            Property<string>(latest, "CorrelationId").ShouldBe(accepted ? "correlation-original" : null);
            Property<TenantCommandLifecycleState>(latest, "State").ShouldBe(accepted
                ? TenantCommandLifecycleState.Accepted : TenantCommandLifecycleState.Failed);
            Property<string>(latest, "SafeMessage").ShouldBe(accepted ? null : "Command submission failed safely.");
            Property<string>(latest, "SafeMessageKey").ShouldBeNull();
            if (initial is TenantCreateCommandSnapshot originalCreate)
            {
                TenantCreateCommandSnapshot latestCreate = latest.ShouldBeOfType<TenantCreateCommandSnapshot>();
                latestCreate.BaselineTenantAbsent.ShouldBe(originalCreate.BaselineTenantAbsent);
                latestCreate.BaselineDetailProjectionVersion.ShouldBe(originalCreate.BaselineDetailProjectionVersion);
                latestCreate.AttemptStartedAtUtc.ShouldBe(originalCreate.AttemptStartedAtUtc);
            }
            else
            {
                Property<bool>(latest, "BaselinePostconditionMet").ShouldBe(Property<bool>(initial, "BaselinePostconditionMet"));
                Property<object>(latest, "LastConfirmedMemberProjection").ShouldBe(Property<object>(initial, "LastConfirmedMemberProjection"));
                if (kind is "change")
                {
                    Property<TenantRole>(latest, "CurrentConfirmedRole").ShouldBe(Property<TenantRole>(initial, "CurrentConfirmedRole"));
                    Property<int>(latest, "OwnerCount").ShouldBe(Property<int>(initial, "OwnerCount"));
                }
            }
            ReadRetained(tracker, kind).ShouldBeSameAs(latest);
            attemptField.GetValue(component).ShouldBeSameAs(originalAttempt);
            tracker.IsAttached(originalAttempt, component).ShouldBeTrue();
            originalAttempt.Lease.ShouldBeSameAs(originalLease);
            originalLease.IsReleased.ShouldBe(!accepted);
            if (accepted)
            {
                originalLease.CurrentOwner.ShouldBeSameAs(originalOwner);
                originalLease.IsDispatchMarked.ShouldBeTrue();
            }
            tracker.HasPendingOwnership("tenant.alpha").ShouldBe(accepted);
            gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBe(accepted);
            DispatchCount(gateway, kind).ShouldBe(1);
        }
        finally
        {
            delivery.TrySetResult(TenantCommandSubmissionResult.Failed("Test cleanup."));
            await submission.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("metadata")]
    public async Task AmbiguousRetryUsesTheOriginalIntentIdentityAndBaseline(string kind)
    {
        List<(object Intent, string MessageId)> deliveries = [];
        RegisterGateway((intent, id) =>
        {
            deliveries.Add((intent, id));
            return Task.FromResult(deliveries.Count == 1
                ? TenantCommandSubmissionResult.Failed("Delivery is uncertain.") with { MessageId = id, IsAmbiguousFailure = true }
                : TenantCommandSubmissionResult.Accepted(id, "correlation-original"));
        });
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        object initial = Snapshot(component);
        Property<TenantCommandLifecycleState>(initial, "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
        ((IDisposable)component).Dispose();
        (IRenderedComponent<IComponent> replacement, object replacementComponent) = RenderFlow(kind, "tenant-sequence:900");
        if (kind is "create")
        {
            Submit(kind, replacement);
        }
        else
        {
            replacement.Find("form").Submit();
        }
        await WaitForAsync(() => deliveries.Count == 2);
        deliveries[1].ShouldBe(deliveries[0]);
        object latest = Snapshot(replacementComponent);
        Property<string>(latest, "BaselineProjectionVersion").ShouldBe(Property<string>(initial, "BaselineProjectionVersion"));
        Property<object>(latest, "Intent").ShouldBe(Property<object>(initial, "Intent"));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    [InlineData("metadata")]
    public void RefusedPreDispatchAdmissionDoesNotRetainALease(string kind)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, _) = RenderFlow(kind, lease: _ => Task.FromResult(false));
        Submit(kind, cut);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(0);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    [InlineData("metadata")]
    public async Task HeldStatusCompletesOwnerlessWithoutReleasingAReplacementLease(string kind)
    {
        var status = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "correlation-original")));
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>()).Returns(status.Task);
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        await WaitForAsync(() => gateway.ReceivedCalls().Any(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.GetStatusAsync)));
        ((IDisposable)component).Dispose();
        status.SetResult(new(CommandStatus.Rejected, HasVerifiedCommandIdentity: true));
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        await WaitForAsync(() => ReadRetained(tracker, kind) is { } retained
            && Property<TenantCommandLifecycleState>(retained, "State") is TenantCommandLifecycleState.Rejected);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.IsLocked(key).ShouldBeFalse();
        object foreignOwner = new();
        gate.TryAcquire(key, foreignOwner).ShouldBeTrue();
        ((IDisposable)component).Dispose();
        gate.IsLocked(key).ShouldBeTrue();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    [Theory]
    [InlineData("create", "authorization")]
    [InlineData("create", "freshness")]
    [InlineData("add", "authorization")]
    [InlineData("add", "freshness")]
    [InlineData("change", "authorization")]
    [InlineData("change", "freshness")]
    [InlineData("metadata", "authorization")]
    [InlineData("metadata", "freshness")]
    public void AmbiguousRecoveryHonorsExistingAuthorizationAndFreshnessGates(string kind, string deniedGate)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(
            TenantCommandSubmissionResult.Failed("Delivery is uncertain.") with { MessageId = id, IsAmbiguousFailure = true }));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        object original = Snapshot(component);
        if (deniedGate is "authorization")
        {
            component.GetType().GetProperty("IsAuthorized")!.SetValue(component, false);
        }
        else if (kind is "create")
        {
            component.GetType().GetProperty("IsFresh")!.SetValue(component, false);
        }
        else
        {
            component.GetType().GetProperty("Freshness")!.SetValue(component, ReadModelFreshnessState.Stale);
        }
        cut.Find("form").Submit();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(1);
        Property<string>(Snapshot(component), "MessageId").ShouldBe(Property<string>(original, "MessageId"));
        Property<object>(Snapshot(component), "Intent").ShouldBe(Property<object>(original, "Intent"));
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    public async Task ContinueDuringHeldStatusCannotResurrectThePanelOrReleaseAForeignLease(string kind)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "correlation-original")));
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandStatusResult.Unknown("Temporarily unavailable")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        var held = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>()).Returns(held.Task);
        MethodInfo refresh = component.GetType().GetMethod("RefreshStatusAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task refreshTask = cut.InvokeAsync(() => (Task)refresh.Invoke(component, null)!);
        await WaitForAsync(() => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.GetStatusAsync)) == 2);
        MethodInfo continueReadOnly = component.GetType().GetMethod("ContinueReadOnlyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await cut.InvokeAsync(() => (Task)continueReadOnly.Invoke(component, null)!);
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.Idle);
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        object qualifiedTerminalOwner = new();
        tracker.TryAdopt<object>("tenant.alpha", qualifiedTerminalOwner, out TenantRetainedCommandAttempt? oldAttempt, out object? retained).ShouldBeTrue();
        object terminal = ApplyStatus(retained!, new(CommandStatus.Rejected, HasVerifiedCommandIdentity: true));
        tracker.TryUpdate(oldAttempt!, retained!, terminal).ShouldBeTrue();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object foreignOwner = new();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.TryAcquireLease(key, foreignOwner, out TenantAggregateCommandLease? foreignLease).ShouldBeTrue();
        foreignLease!.TryMarkDispatched(foreignOwner).ShouldBeTrue();
        held.SetResult(new(CommandStatus.Processing, HasVerifiedCommandIdentity: true));
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.Idle);
        Property<TenantCommandLifecycleState>(ReadRetained(tracker, kind)!, "State").ShouldBe(TenantCommandLifecycleState.Rejected);
        gate.IsLocked(key).ShouldBeTrue();
        foreignLease.TryReleaseTerminal(foreignOwner, TenantCommandLifecycleState.Rejected).ShouldBeTrue();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("metadata")]
    public async Task CurrentAuthoritativeConfirmationSurvivesLateProcessingStatusAndProtectsForeignAdmission(string kind)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "correlation-original")));
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        var held = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>()).Returns(held.Task);
        MethodInfo refresh = component.GetType().GetMethod("RefreshStatusAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task refreshTask = cut.InvokeAsync(() => (Task)refresh.Invoke(component, null)!);
        await WaitForAsync(() => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.GetStatusAsync)) == 2);
        MethodInfo applyProjection = component.GetType().GetMethod("ApplyProjectionEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object proof;
        if (kind is "create")
        {
            proof = TenantCommandProofFixtures.ListProof(new("tenant.alpha", "Alpha", TenantStatus.Active), "tenant-sequence:2")!;
        }
        else
        {
            TenantDetail detail = new("tenant.alpha", kind is "metadata" ? "Renamed" : "Alpha", "Original", TenantStatus.Active,
                [new("owner", TenantRole.TenantOwner), new(kind is "add" ? "new-reader" : "reader", kind is "add" ? TenantRole.TenantReader : TenantRole.TenantContributor)],
                new Dictionary<string, string>(), DateTimeOffset.UtcNow);
            proof = TenantCommandProofFixtures.Proof(detail, "tenant-sequence:2")!;
        }
        await cut.InvokeAsync(() => applyProjection.Invoke(component, [proof]));
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.Confirmed);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        object foreignOwner = new();
        gate.TryAcquireLease(key, foreignOwner, out TenantAggregateCommandLease? foreign).ShouldBeTrue();
        held.SetResult(new(CommandStatus.Processing, HasVerifiedCommandIdentity: true));
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.Confirmed);
        gate.IsLocked(key).ShouldBeTrue();
        foreign!.TryAbandonBeforeDispatch(foreignOwner).ShouldBeTrue();
    }

    private static object ApplyStatus(object snapshot, TenantCommandStatusResult status)
        => snapshot.GetType().GetMethod("ApplyStatus")!.Invoke(snapshot, [status])!;

    [Theory]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    [InlineData("metadata")]
    public async Task DisposedPreDispatchAcquisitionCannotMutateAReplacementSubmission(string kind)
    {
        var firstAcquisition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquisitionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementDelivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, _) => replacementDelivery.Task);
        int acquisitions = 0;
        Func<bool, Task<bool>> lease = active =>
        {
            if (active && Interlocked.Increment(ref acquisitions) == 1)
            {
                acquisitionEntered.TrySetResult();
                return firstAcquisition.Task;
            }
            return Task.FromResult(true);
        };
        (IRenderedComponent<IComponent> first, object firstComponent) = RenderFlow(kind, lease: lease);
        Submit(kind, first, send: false);
        Task initialSubmit = first.Find("form").SubmitAsync();
        await acquisitionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ((IDisposable)firstComponent).Dispose();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        string key = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.IsLocked(key).ShouldBeFalse();
        (IRenderedComponent<IComponent> replacement, object component) = RenderFlow(kind, lease: lease);
        Submit(kind, replacement, send: false);
        Task nextSubmit = replacement.Find("form").SubmitAsync();
        await WaitForAsync(() => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)) == 1);
        FieldInfo submitting = component.GetType().GetField("_isSubmitting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((bool)submitting.GetValue(component)!).ShouldBeTrue();
        firstAcquisition.SetResult(true);
        await initialSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        ((bool)submitting.GetValue(component)!).ShouldBeTrue();
        gate.IsLocked(key).ShouldBeTrue();
        object pending = Snapshot(component);
        replacementDelivery.SetResult(TenantCommandSubmissionResult.Accepted(Property<string>(pending, "MessageId")!, "replacement-correlation"));
        await nextSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        Property<string>(Snapshot(component), "CorrelationId").ShouldBe("replacement-correlation");
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(1);
        gate.IsLocked(key).ShouldBeTrue();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    [InlineData("metadata")]
    public void MissingCircuitAdmissionFailsClosedThroughRenderingSubmissionAndDisposal(string kind)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        if (kind is "metadata")
        {
            cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        }
        else
        {
            Submit(kind, cut);
        }
        ((IDisposable)component).Dispose();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(0);
    }

    [Theory]
    [InlineData("add", "user")]
    [InlineData("add", "role")]
    [InlineData("add", "invalid-role")]
    [InlineData("change", "role")]
    [InlineData("change", "invalid-role")]
    [InlineData("metadata", "name")]
    [InlineData("metadata", "description")]
    public void ChangedFormCannotSilentlyRedispatchTheOriginalAmbiguousIntent(string kind, string changedField)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(
            TenantCommandSubmissionResult.Failed("Delivery is uncertain.") with { MessageId = id, IsAmbiguousFailure = true }));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        object original = Snapshot(component);
        if (changedField is "user")
        {
            cut.Find("[data-testid='tenants-add-member-user-id']").Change("different-user");
        }
        else if (changedField is "role" or "invalid-role")
        {
            ChangeRoleSelection(cut, kind is "add" ? "tenants-add-member-role" : "tenants-change-role-new-role",
                changedField is "invalid-role" ? "Unknown" : nameof(TenantRole.TenantOwner));
        }
        else
        {
            cut.Find(changedField is "name" ? "[data-testid='tenants-edit-metadata-name']" : "[data-testid='tenants-edit-metadata-description']")
                .Change("A different intended value");
        }
        cut.Find("form").Submit();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(1);
        object latest = Snapshot(component);
        Property<object>(latest, "Intent").ShouldBe(Property<object>(original, "Intent"));
        Property<string>(latest, "MessageId").ShouldBe(Property<string>(original, "MessageId"));
        Property<string>(latest, "BaselineProjectionVersion").ShouldBe(Property<string>(original, "BaselineProjectionVersion"));
        Property<TenantCommandLifecycleState>(latest, "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    [InlineData("metadata")]
    public async Task FaultedOldActivityAcquisitionCannotDetachTheNewRouteAttempt(string kind)
    {
        var oldAcquisition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextDelivery = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, _) => nextDelivery.Task);
        int acquisitions = 0;
        Func<bool, Task<bool>> lease = active =>
        {
            if (active && Interlocked.Increment(ref acquisitions) == 1)
            {
                oldEntered.TrySetResult();
                return oldAcquisition.Task;
            }
            return Task.FromResult(true);
        };
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, lease: lease);
        Submit(kind, cut, send: false);
        Task oldSubmit = cut.Find("form").SubmitAsync();
        await oldEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantDetail oldDetail = (TenantDetail)component.GetType().GetProperty("Detail")!.GetValue(component)!;
        await cut.InvokeAsync(() => ((IComponent)component).SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["Detail"] = oldDetail with { TenantId = "tenant.beta" } })));
        if (kind is "remove")
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBeNullOrEmpty();
            await cut.Find("form").SubmitAsync();
            gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(0);
            Submit(kind, cut, send: false);
        }
        Task newSubmit = cut.Find("form").SubmitAsync();
        await WaitForAsync(() => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)) == 1);
        object newSnapshot = Snapshot(component);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeTrue();
        oldAcquisition.SetException(new InvalidOperationException("Old parent callback faulted"));
        await oldSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        component.GetType().GetField("_retainedAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(component).ShouldNotBeNull();
        nextDelivery.SetResult(TenantCommandSubmissionResult.Accepted(Property<string>(newSnapshot, "MessageId")!, "new-route-correlation"));
        await newSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        Property<string>(Snapshot(component), "CorrelationId").ShouldBe("new-route-correlation");
        Property<object>(Snapshot(component), "Intent").ShouldBe(Property<object>(newSnapshot, "Intent"));
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    public async Task AmbiguousDeliveryNudgePreservesOriginalIdRecovery(string kind)
    {
        List<(object Intent, string Id)> deliveries = [];
        ITenantCommandGateway gateway = RegisterGateway((intent, id) =>
        {
            deliveries.Add((intent, id));
            return Task.FromResult(deliveries.Count == 1
                ? TenantCommandSubmissionResult.Failed("Uncertain") with { MessageId = id, IsAmbiguousFailure = true }
                : TenantCommandSubmissionResult.Accepted(id, "original-correlation"));
        });
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind);
        Submit(kind, cut);
        object original = Snapshot(component);
        MethodInfo nudge = component.GetType().GetMethod("HandleAuthoritativeRefreshNudgeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await cut.InvokeAsync(() => (Task)nudge.Invoke(component, null)!);
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.GetStatusAsync)).ShouldBe(0);
        cut.Find("form").Submit();
        await WaitForAsync(() => deliveries.Count == 2);
        deliveries[1].ShouldBe(deliveries[0]);
        Property<string>(Snapshot(component), "BaselineProjectionVersion").ShouldBe(Property<string>(original, "BaselineProjectionVersion"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemountedAmbiguousRemovalRequiresLiteralConfirmationAndCompletePreview(bool incompletePreview)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(
            TenantCommandSubmissionResult.Failed("Uncertain") with { MessageId = id, IsAmbiguousFailure = true }));
        bool complete = true;
        IStringLocalizer<TenantsResources> localizer = Substitute.For<IStringLocalizer<TenantsResources>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(),
            !complete && call.Arg<string>() is "Tenants.RemoveMember.Preview.AccessPath.Value" ? "" : call.Arg<string>()));
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call => new LocalizedString(call.ArgAt<string>(0), call.ArgAt<string>(0)));
        Services.AddSingleton(localizer);
        (IRenderedComponent<IComponent> first, object firstComponent) = RenderFlow("remove");
        Submit("remove", first);
        object original = Snapshot(firstComponent);
        ((IDisposable)firstComponent).Dispose();
        complete = !incompletePreview;
        (IRenderedComponent<IComponent> replacement, object component) = RenderFlow("remove");
        if (incompletePreview)
        {
            replacement.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader");
        }
        await replacement.Find("form").SubmitAsync();
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.RemoveUserFromTenantAsync)).ShouldBe(1);
        Property<string>(Snapshot(component), "MessageId").ShouldBe(Property<string>(original, "MessageId"));
        Property<object>(Snapshot(component), "Intent").ShouldBe(Property<object>(original, "Intent"));
        Property<TenantCommandLifecycleState>(Snapshot(component), "State").ShouldBe(TenantCommandLifecycleState.RequestSent);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("add", true)]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    public async Task HeldAdmissionCannotDispatchAfterSuppliedEligibilityDenial(string kind, bool changedRoute)
    {
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, lease: active => active ? approval.Task : Task.FromResult(true));
        Submit(kind, cut, send: false);
        Task submission = cut.Find("form").SubmitAsync();
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        await WaitForAsync(() => gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")));
        TenantDetail detail = (TenantDetail)component.GetType().GetProperty("Detail")!.GetValue(component)!;
        await cut.InvokeAsync(() => ((IComponent)component).SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["IsAuthorized"] = false, ["Detail"] = detail with { TenantId = changedRoute ? "tenant.beta" : detail.TenantId } })));
        approval.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(0);
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeFalse();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("add", true)]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    public async Task CallbackOnlyFaultAbandonsOnlyItsCapturedPreDispatchAttempt(string kind, bool replace)
    {
        var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        EventCallback<bool> activity = EventCallback.Factory.Create<bool>(this, active => active && Interlocked.Increment(ref acquisitions) == 1 ? callback.Task : Task.CompletedTask);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "replacement-correlation")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, activity: activity);
        Submit(kind, cut, send: false);
        Task old = cut.Find("form").SubmitAsync();
        await WaitForAsync(() => acquisitions == 1);
        if (replace)
        {
            ((IDisposable)component).Dispose();
            (IRenderedComponent<IComponent> replacement, object replacementComponent) = RenderFlow(kind, activity: activity);
            Submit(kind, replacement);
            component = replacementComponent;
        }
        callback.SetException(new InvalidOperationException("Admission callback faulted"));
        await old.WaitAsync(TimeSpan.FromSeconds(5));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBe(replace);
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind)).ShouldBe(replace ? 1 : 0);
        if (replace)
        {
            Property<string>(Snapshot(component), "CorrelationId").ShouldBe("replacement-correlation");
        }
    }

    [Theory]
    [InlineData("caller-a", true)]
    [InlineData("caller-b", false)]
    [InlineData(null, false)]
    public void RetainedMetadataReadPresentationUsesCurrentAuthorizedCallerDetail(string? currentCaller, bool sameCaller)
    {
        RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        TenantDetail privateDetail = new("tenant.alpha", "Caller A name", "Caller A description", TenantStatus.Active,
            [new("owner", TenantRole.TenantOwner)], new Dictionary<string, string>(), DateTimeOffset.UtcNow);
        TenantUpdateMetadataCommandSnapshot pending = (TenantUpdateMetadataCommandSnapshot.Idle(privateDetail.Name, privateDetail.Description, privateDetail)
            with { LastConfirmedCallerScope = "caller-a" })
            .RequestSent(new("tenant.alpha", "Original intent", "Original intent description"), "tenant-sequence:1")
            .Accepted(TenantCommandSubmissionResult.Accepted("original-message", "original-correlation"));
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        object owner = new();
        tracker.TryBegin("tenant.alpha", owner, pending, out TenantRetainedCommandAttempt? attempt).ShouldBeTrue();
        tracker.TryStartDelivery(attempt!, owner).ShouldBeTrue();
        tracker.TryCompleteDelivery(attempt!, pending, pending).ShouldBeTrue();
        tracker.Detach(attempt, owner);
        TenantDetail currentDetail = privateDetail with { Name = "Current authorized name", Description = "Current authorized description" };
        IRenderedComponent<CascadingValue<string?>> wrapper = Render<CascadingValue<string?>>(p => p
            .Add(x => x.Name, "CurrentTenantCallerScope").Add(x => x.Value, currentCaller)
            .AddChildContent<EditTenantMetadataFlow>(child => child
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)).Add(x => x.Detail, currentDetail)
                .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)
                .Add(x => x.Lifecycle, ProjectionLifecycleState.Current).Add(x => x.ProjectionVersion, "tenant-sequence:900")));
        IRenderedComponent<EditTenantMetadataFlow> cut = wrapper.FindComponent<EditTenantMetadataFlow>();
        string display = cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent;
        display.ShouldContain(sameCaller ? privateDetail.Name : currentDetail.Name);
        display.ShouldContain(sameCaller ? privateDetail.Description! : currentDetail.Description!);
        if (!sameCaller)
        {
            display.ShouldNotContain("Caller A");
        }
        cut.Instance.Snapshot.ShouldBe(pending);
        cut.Instance.Snapshot.Intent.ShouldBe(pending.Intent);
        cut.Instance.Snapshot.MessageId.ShouldBe(pending.MessageId);
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBe(pending.BaselineProjectionVersion);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Fact]
    public async Task CallerBoundLocalMetadataReadIsWithheldWhileCurrentCallerResolutionIsPending()
    {
        RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "original-correlation")));
        TenantDetail originalDetail = new("tenant.alpha", "Caller A name", "Caller A description", TenantStatus.Active,
            [new("owner", TenantRole.TenantOwner)], new Dictionary<string, string>(), DateTimeOffset.UtcNow);
        TenantDetail currentDetail = originalDetail with { Name = "Current authorized name", Description = "Current authorized description" };
        Action<ComponentParameterCollectionBuilder<EditTenantMetadataFlow>> originalParameters = child => child
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.Detail, originalDetail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1");
        IRenderedComponent<CascadingValue<string?>> wrapper = Render<CascadingValue<string?>>(p => p
            .Add(x => x.Name, "CurrentTenantCallerScope").Add(x => x.Value, "caller-a").AddChildContent(originalParameters));
        IRenderedComponent<EditTenantMetadataFlow> cut = wrapper.FindComponent<EditTenantMetadataFlow>();
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Original command intent");
        await cut.Find("form").SubmitAsync();
        TenantUpdateMetadataCommandSnapshot original = cut.Instance.Snapshot;
        original.LastConfirmedCallerScope.ShouldBe("caller-a");
        wrapper.Render(p => p.Add(x => x.Value, (string?)null).AddChildContent<EditTenantMetadataFlow>(child => child
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.Detail, currentDetail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:900")));
        string display = cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent;
        display.ShouldContain(currentDetail.Name);
        display.ShouldContain(currentDetail.Description!);
        display.ShouldNotContain("Caller A");
        cut.Instance.Snapshot.ShouldBe(original);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("freshness")]
    [InlineData("command-surface")]
    public async Task CreateRechecksSuppliedEligibilityAfterAwaitedBaseline(string deniedInput)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        var baseline = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.GetRequiredService<ITenantQueryGateway>().GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>()).Returns(baseline.Task);
        IRenderedComponent<CreateTenantFlow> cut = Render<CreateTenantFlow>(p => p.Add(x => x.IsAuthorized, true)
            .Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)));
        cut.Find("[data-testid='tenants-create-tenant-id']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-create-name']").Change("Alpha");
        Task submission = cut.Find("form").SubmitAsync();
        await WaitForAsync(() => Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")));
        cut.Render(p => p.Add(x => x.IsAuthorized, deniedInput is not "authorization")
            .Add(x => x.IsFresh, deniedInput is not "freshness").Add(x => x.IsCommandSurfaceAvailable, deniedInput is not "command-surface"));
        baseline.SetResult(TenantDetailSnapshot.NotFound("tenant.alpha"));
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.CreateTenantAsync)).ShouldBe(0);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    /// <summary>Changing only the current member invalidates a held role/removal admission.</summary>
    [Theory]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    public async Task ReviewAdmissionMemberReplacementAloneCancelsOlderAuthority(string kind, bool changedRole)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: token => { observed = token; entered.TrySetResult(); return answer.Task; });
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantMember member = (TenantMember)component.GetType().GetProperty("Member")!.GetValue(component)!;
        TenantMember next = changedRole ? member with { Role = TenantRole.TenantOwner } : member with { UserId = "different-literal-member" };
        await cut.InvokeAsync(() => ((IComponent)component).SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Member"] = next })));
        await submission.WaitAsync(TimeSpan.FromSeconds(2));
        observed.CanBeCanceled.ShouldBeTrue();
        observed.IsCancellationRequested.ShouldBeTrue();
        answer.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await cut.InvokeAsync(() => Task.CompletedTask);
        DispatchCount(gateway, kind).ShouldBe(0);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    /// <summary>All command families cancel noncooperative authority on disposal or real parameter invalidation.</summary>
    [Theory]
    [MemberData(nameof(ReviewCancellationCases))]
    public async Task ReviewAdmissionNonCompletingAuthorityIsCancelled(string kind, bool disposed)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: token => { observed = token; entered.TrySetResult(); return answer.Task; });
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (disposed) ((IDisposable)component).Dispose();
        else await InvalidateAdmissionAsync(cut, component, "tenant");
        await submission.WaitAsync(TimeSpan.FromSeconds(2));
        observed.CanBeCanceled.ShouldBeTrue();
        observed.IsCancellationRequested.ShouldBeTrue();
        DispatchCount(gateway, kind).ShouldBe(0);
        answer.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await cut.InvokeAsync(() => Task.CompletedTask);
        DispatchCount(gateway, kind).ShouldBe(0);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    /// <summary>Provides disposal and parameter invalidation for each command family.</summary>
    public static TheoryData<string, bool> ReviewCancellationCases
    {
        get
        {
            var cases = new TheoryData<string, bool>();
            foreach (string kind in new[] { "create", "metadata", "add", "change", "remove" })
            {
                cases.Add(kind, true);
                cases.Add(kind, false);
            }
            return cases;
        }
    }

    /// <summary>Metadata cancel remains usable before dispatch while a provider never completes.</summary>
    [Fact]
    public async Task ReviewMetadataCancelCancelsHeldInitialAuthorityWithoutDispatch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow("metadata", authority: token => { observed = token; entered.TrySetResult(); return answer.Task; });
        Submit("metadata", cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-edit-metadata-cancel']").HasAttribute("disabled").ShouldBeFalse();
        await cut.Find("[data-testid='tenants-edit-metadata-cancel']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await submission.WaitAsync(TimeSpan.FromSeconds(2));
        observed.IsCancellationRequested.ShouldBeTrue();
        cut.FindAll("[data-testid='tenants-edit-metadata-name']").ShouldBeEmpty();
        answer.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await cut.InvokeAsync(() => Task.CompletedTask);
        DispatchCount(gateway, "metadata").ShouldBe(0);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    /// <summary>The real ten-second deadline bounds a never-completing initial or post-lease provider.</summary>
    [Theory]
    [InlineData("metadata", false)]
    [InlineData("remove", true)]
    public async Task ReviewAdmissionRealDeadlineRefusesNonCompletingProvider(string kind, bool postLease)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        int resolutions = 0;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: token =>
        {
            if (postLease && ++resolutions == 1) return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            observed = token;
            entered.TrySetResult();
            return never.Task;
        });
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await submission.WaitAsync(TimeSpan.FromSeconds(13));
        elapsed.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(8));
        observed.IsCancellationRequested.ShouldBeTrue();
        DispatchCount(gateway, kind).ShouldBe(0);
        Property<string>(Snapshot(component), "SafeMessage").ShouldNotBeNullOrWhiteSpace();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>A held ambiguous retry cannot dispatch over newer authoritative tracking or terminal proof.</summary>
    [Theory]
    [MemberData(nameof(ReviewRetryEvidenceCases))]
    public async Task ReviewAmbiguousRetryPreservesEvidenceArrivingDuringAuthority(string kind, bool terminal, bool formEntry)
    {
        bool hold = false;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Failed("Uncertain") with { MessageId = id, IsAmbiguousFailure = true }));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: _ =>
        {
            if (!hold) return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            entered.TrySetResult();
            return answer.Task;
        });
        Submit(kind, cut);
        object original = Snapshot(component);
        DispatchCount(gateway, kind).ShouldBe(1);
        hold = true;
        Task retry = formEntry ? InvokeSubmissionAsync(cut, component) : cut.InvokeAsync(() => (Task)component.GetType()
            .GetMethod("RetryDeliveryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null)!);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantRetainedCommandAttempt attempt = (TenantRetainedCommandAttempt)component.GetType().GetField("_retainedAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
        object newer = original.GetType().GetMethod("Accepted")!.Invoke(original, [TenantCommandSubmissionResult.Accepted(Property<string>(original, "MessageId")!, "arrived-tracking")])!;
        if (terminal)
        {
            newer = ApplyStatus(newer, new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 });
            TenantDetail detail = new("tenant.alpha", kind is "metadata" ? "Renamed" : "Alpha", "Original", TenantStatus.Active,
                kind is "remove" ? [new TenantMember("owner", TenantRole.TenantOwner)]
                    : [new TenantMember("owner", TenantRole.TenantOwner), new TenantMember(kind is "add" ? "new-reader" : "reader", kind is "add" ? TenantRole.TenantReader : TenantRole.TenantContributor)],
                new Dictionary<string, string>(), DateTimeOffset.UtcNow);
            TenantDetailSnapshot proof = TenantCommandProofFixtures.Proof(detail, "tenant-sequence:2")!;
            newer = kind switch
            {
                "metadata" => ((TenantUpdateMetadataCommandSnapshot)newer).ConfirmProjection(proof),
                "add" => ((TenantAddMemberCommandSnapshot)newer).ConfirmProjection(proof),
                "change" => ((TenantChangeRoleCommandSnapshot)newer).ConfirmProjection(proof),
                _ => ((TenantRemoveMemberCommandSnapshot)newer).ConfirmProjection(proof),
            };
            Property<TenantCommandLifecycleState>(newer, "State").ShouldBe(TenantCommandLifecycleState.Confirmed);
        }
        await cut.InvokeAsync(() => tracker.TryUpdate(attempt, original, newer).ShouldBeTrue());
        await WaitForAsync(() => ReferenceEquals(Snapshot(component), newer));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantAggregateCommandLease? foreign = null;
        object foreignOwner = new();
        if (terminal) gate.TryAcquireLease(TenantCommandAggregateLock.ForTenant("tenant.alpha"), foreignOwner, out foreign).ShouldBeTrue();
        answer.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        DispatchCount(gateway, kind).ShouldBe(1);
        Snapshot(component).ShouldBeSameAs(newer);
        Property<string>(Snapshot(component), "MessageId").ShouldBe(Property<string>(original, "MessageId"));
        Property<object>(Snapshot(component), "Intent").ShouldBe(Property<object>(original, "Intent"));
        Property<string>(Snapshot(component), "CorrelationId").ShouldBe("arrived-tracking");
        gate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBe(!terminal);
        if (terminal) foreign!.TryAbandonBeforeDispatch(foreignOwner).ShouldBeTrue();
    }

    /// <summary>Covers both form and lifecycle retry entry with tracking and terminal evidence.</summary>
    public static TheoryData<string, bool, bool> ReviewRetryEvidenceCases
    {
        get
        {
            var cases = new TheoryData<string, bool, bool>();
            foreach (string kind in new[] { "metadata", "add", "change", "remove" })
            {
                cases.Add(kind, false, false);
                cases.Add(kind, false, true);
                cases.Add(kind, true, false);
                cases.Add(kind, true, true);
            }
            return cases;
        }
    }

    /// <summary>Omitting authority inputs never authorizes a standalone command renderer or direct submit.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("metadata")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    public async Task LiveAdmissionDefaultsAreFailClosedForRenderedAndDirectCommands(string kind)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        TenantDetail detail = new("tenant.alpha", "Alpha", "Original", TenantStatus.Active,
            [new TenantMember("owner", TenantRole.TenantOwner), new TenantMember("reader", TenantRole.TenantReader)],
            new Dictionary<string, string>(), DateTimeOffset.UtcNow);
        IRenderedComponent<IComponent> cut = kind switch
        {
            "create" => Render<CreateTenantFlow>(),
            "metadata" => Render<EditTenantMetadataFlow>(p => p.Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current)),
            "add" => Render<AddTenantMemberFlow>(p => p.Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)),
            "change" => Render<ChangeTenantMemberRoleFlow>(p => p.Add(x => x.Detail, detail).Add(x => x.Member, detail.Members[1]).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)),
            _ => Render<RemoveTenantMemberFlow>(p => p.Add(x => x.Detail, detail).Add(x => x.Member, detail.Members[1]).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.AuditProofCapabilityAvailable, true).Add(x => x.TargetPlatformStandingResolved, true)),
        };
        object component = cut.Instance;
        component.GetType().GetProperty("IsAuthorized")!.GetValue(component).ShouldBe(false);
        component.GetType().GetProperty("AuthorizationReflection")!.GetValue(component).ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        await InvokeSubmissionAsync(cut, component);
        DispatchCount(gateway, kind).ShouldBe(0);
        cut.Markup.ShouldContain("Current authority could not be verified");
    }

    /// <summary>Every command family fails closed on fresh denied or indeterminate admission.</summary>
    [Theory]
    [MemberData(nameof(AdmissionRefusals))]
    public async Task LiveAdmissionRefusesFreshAuthorityBeforeDispatch(string kind, TenantLifecycleAuthorizationReflectionState denied)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: _ => Task.FromResult(denied));
        Submit(kind, cut, send: false);
        await InvokeSubmissionAsync(cut, component);
        DispatchCount(gateway, kind).ShouldBe(0);
        Property<string>(Snapshot(component), "SafeMessage").ShouldNotBeNullOrWhiteSpace();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>Provides all command families and both unavailable authority outcomes.</summary>
    public static TheoryData<string, TenantLifecycleAuthorizationReflectionState> AdmissionRefusals
    {
        get
        {
            var rows = new TheoryData<string, TenantLifecycleAuthorizationReflectionState>();
            foreach (string kind in new[] { "create", "metadata", "add", "change", "remove" })
            {
                rows.Add(kind, TenantLifecycleAuthorizationReflectionState.MissingPermission);
                rows.Add(kind, TenantLifecycleAuthorizationReflectionState.Indeterminate);
            }
            return rows;
        }
    }

    /// <summary>Late authority cannot admit an intent invalidated by caller, tenant, or live authority changes.</summary>
    [Theory]
    [MemberData(nameof(AdmissionTransitions))]
    public async Task LiveAdmissionRejectsSupersededAuthorityAfterAwait(string kind, string transition)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: _ => { entered.TrySetResult(); return result.Task; });
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await InvalidateAdmissionAsync(cut, component, transition);
        result.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        DispatchCount(gateway, kind).ShouldBe(0);
    }

    /// <summary>Provides caller, literal tenant, and authority invalidations for every command family.</summary>
    public static TheoryData<string, string> AdmissionTransitions
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (string kind in new[] { "create", "metadata", "add", "change", "remove" })
            foreach (string transition in new[] { "caller", "tenant", "authority" })
            {
                rows.Add(kind, transition);
            }
            return rows;
        }
    }

    /// <summary>Admission rechecks authority after the parent grants the aggregate activity lease.</summary>
    [Theory]
    [InlineData("metadata")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    public async Task LiveAdmissionRejectsRevocationWhileAwaitingParentLease(string kind)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantLifecycleAuthorizationReflectionState live = TenantLifecycleAuthorizationReflectionState.Authorized;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind,
            lease: active => { if (!active) return Task.FromResult(true); entered.TrySetResult(); return lease.Task; },
            authority: _ => Task.FromResult(live));
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        live = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        lease.SetResult(true);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        DispatchCount(gateway, kind).ShouldBe(0);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>Ambiguous delivery retains the exact attempt while fresh authority refuses redelivery.</summary>
    [Theory]
    [MemberData(nameof(AdmissionRefusals))]
    public async Task LiveAdmissionReauthorizesAmbiguousDeliveryRetry(string kind, TenantLifecycleAuthorizationReflectionState denied)
    {
        TenantLifecycleAuthorizationReflectionState live = TenantLifecycleAuthorizationReflectionState.Authorized;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Failed("Uncertain") with { MessageId = id, IsAmbiguousFailure = true }));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: _ => Task.FromResult(live));
        Submit(kind, cut);
        await WaitForAsync(() => DispatchCount(gateway, kind) == 1);
        object original = Snapshot(component);
        live = denied;
        await InvokeSubmissionAsync(cut, component);
        DispatchCount(gateway, kind).ShouldBe(1);
        Property<string>(Snapshot(component), "MessageId").ShouldBe(Property<string>(original, "MessageId"));
        Property<object>(Snapshot(component), "Intent").ShouldBe(Property<object>(original, "Intent"));
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    /// <summary>Removal cannot trust a seed, unknown measurement, unavailable observer, or phone viewport.</summary>
    [Theory]
    [InlineData("unavailable")]
    [InlineData("unknown")]
    [InlineData("seeded-safe")]
    [InlineData("phone")]
    [InlineData("measured-unknown")]
    public async Task LiveRemovalRequiresAnActualSafeMeasurementForDirectSubmission(string viewportState)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        TenantHighImpactViewportObservation? viewport = viewportState is "unavailable" ? null
            : new TenantHighImpactViewportObservation(viewportState is "seeded-safe" ? TenantHighImpactViewportState.Safe : TenantHighImpactViewportState.Unknown);
        if (viewportState is "phone") viewport!.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone);
        if (viewportState is "measured-unknown")
        {
            viewport!.Observe((Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier)255);
            viewport.HasMeasurement.ShouldBeTrue();
            viewport.State.ShouldBe(TenantHighImpactViewportState.Unknown);
        }
        Services.AddSingleton<TenantHighImpactViewportObservation>(_ => viewport!);
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow("remove");
        await InvokeSubmissionAsync(cut, component);
        DispatchCount(gateway, "remove").ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-unavailable-reason']").TextContent.ShouldContain("browser measures");
    }

    /// <summary>A measured tablet or desktop unlocks an initially unknown authorized removal preview.</summary>
    [Theory]
    [InlineData(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Tablet)]
    [InlineData(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop)]
    public async Task LiveRemovalUnknownToMeasuredSafeDispatchesWithExistingAttempt(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier tier)
    {
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "original-correlation")));
        var viewport = new TenantHighImpactViewportObservation();
        Services.AddSingleton(viewport);
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow("remove");
        DispatchCount(gateway, "remove").ShouldBe(0);
        await cut.InvokeAsync(() => viewport.Observe(tier));
        cut.WaitForElement("[data-testid='tenants-remove-member-confirmation']");
        Submit("remove", cut);
        await WaitForAsync(() => DispatchCount(gateway, "remove") == 1);
        Property<string>(Snapshot(component), "MessageId").ShouldNotBeNullOrWhiteSpace();
        Property<string>(Snapshot(component), "CorrelationId").ShouldBe("original-correlation");
    }

    /// <summary>Resizing during fresh authority or parent lease invalidates the pending removal.</summary>
    [Theory]
    [InlineData("before", false)]
    [InlineData("before", true)]
    [InlineData("authority", false)]
    [InlineData("authority", true)]
    [InlineData("lease", false)]
    [InlineData("lease", true)]
    public async Task LiveRemovalResizeRefusesDispatchAndPreservesReadOnlyEvidence(string phase, bool unknown)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Desktop);
        Services.AddSingleton(viewport);
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow("remove",
            lease: active => { if (!active || phase is not "lease") return Task.FromResult(true); entered.TrySetResult(); return lease.Task; },
            authority: _ => { if (phase is not "authority") return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized); entered.TrySetResult(); return authority.Task; });
        Submit("remove", cut, send: false);
        Task? submission = phase is "before" ? null : InvokeSubmissionAsync(cut, component);
        if (submission is not null) await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => viewport.Observe(unknown ? (Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier)255 : Hexalith.FrontComposer.Shell.State.Navigation.ViewportTier.Phone));
        authority.TrySetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        lease.TrySetResult(true);
        await (submission ?? InvokeSubmissionAsync(cut, component)).WaitAsync(TimeSpan.FromSeconds(5));
        DispatchCount(gateway, "remove").ShouldBe(0);
        cut.Find("[data-testid='tenants-remove-member-lifecycle']");
        cut.Find("[data-testid='tenants-remove-member-audit']");
    }

    /// <summary>Disposal during the fresh post-lease lookup cannot dispatch an orphaned intent.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("metadata")]
    [InlineData("add")]
    [InlineData("change")]
    [InlineData("remove")]
    public async Task LiveAdmissionDisposalDuringPostLeaseAuthorityNeverDispatches(string kind)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authority = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow(kind, authority: _ =>
        {
            if (++reads == 1) return Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
            entered.TrySetResult();
            return authority.Task;
        });
        Submit(kind, cut, send: false);
        Task submission = InvokeSubmissionAsync(cut, component);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ((IDisposable)component).Dispose();
        authority.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        DispatchCount(gateway, kind).ShouldBe(0);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    /// <summary>A freshly refused create after baseline capture releases only its undispatched admission.</summary>
    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate)]
    public async Task LiveCreateRevocationAfterBaselineAbandonsUnsentOwnership(TenantLifecycleAuthorizationReflectionState denied)
    {
        int reads = 0;
        ITenantCommandGateway gateway = RegisterGateway((_, id) => Task.FromResult(TenantCommandSubmissionResult.Accepted(id, "unused")));
        (IRenderedComponent<IComponent> cut, object component) = RenderFlow("create", authority: _ => Task.FromResult(
            ++reads == 1 ? TenantLifecycleAuthorizationReflectionState.Authorized : denied));
        Submit("create", cut, send: false);
        await InvokeSubmissionAsync(cut, component);
        reads.ShouldBe(2);
        DispatchCount(gateway, "create").ShouldBe(0);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    private static int DispatchCount(ITenantCommandGateway gateway, string kind)
        => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name == DispatchName(kind));

    private static Task InvokeSubmissionAsync(IRenderedComponent<IComponent> cut, object component)
        => cut.InvokeAsync(() => (Task)component.GetType().GetMethod("SubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null)!);

    private static Task InvalidateAdmissionAsync(IRenderedComponent<IComponent> cut, object component, string transition)
        => cut.InvokeAsync(() =>
        {
            if (transition is "caller") component.GetType().GetProperty("CurrentCallerScope")!.SetValue(component, "caller-next");
            var values = new Dictionary<string, object?>();
            if (transition is "authority") values["AuthorizationReflection"] = TenantLifecycleAuthorizationReflectionState.MissingPermission;
            if (transition is "tenant")
            {
                if (component is CreateTenantFlow) cut.Find("[data-testid='tenants-create-tenant-id']").Change("tenant.next");
                else values["Detail"] = ((TenantDetail)component.GetType().GetProperty("Detail")!.GetValue(component)!) with { TenantId = "tenant.next" };
            }
            return ((IComponent)component).SetParametersAsync(ParameterView.FromDictionary(values));
        });

    private ITenantCommandGateway RegisterGateway(Func<object, string, Task<TenantCommandSubmissionResult>> submit)
    {
        Services.AddLocalization();
        ITenantCommandGateway gateway = Substitute.For<ITenantCommandGateway>();
        gateway.CreateTenantAsync(Arg.Any<CreateTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => submit(call.ArgAt<CreateTenant>(0), call.ArgAt<string>(1)));
        gateway.AddUserToTenantAsync(Arg.Any<AddUserToTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => submit(call.ArgAt<AddUserToTenant>(0), call.ArgAt<string>(1)));
        gateway.ChangeUserRoleAsync(Arg.Any<ChangeUserRole>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => submit(call.ArgAt<ChangeUserRole>(0), call.ArgAt<string>(1)));
        gateway.RemoveUserFromTenantAsync(Arg.Any<RemoveUserFromTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => submit(call.ArgAt<RemoveUserFromTenant>(0), call.ArgAt<string>(1)));
        gateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => submit(call.ArgAt<UpdateTenant>(0), call.ArgAt<string>(1)));
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)));
        Services.AddSingleton(gateway);
        Services.AddSingleton(Substitute.For<ITenantQueryGateway>());
        return gateway;
    }

    private (IRenderedComponent<IComponent> Cut, object Component) RenderFlow(string kind, string baseline = "tenant-sequence:1", Func<bool, Task<bool>>? lease = null, EventCallback<bool> activity = default, Func<CancellationToken, Task<TenantLifecycleAuthorizationReflectionState>>? authority = null)
    {
        authority ??= _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        TenantDetail detail = new("tenant.alpha", "Alpha", "Original", TenantStatus.Active,
            [new TenantMember("owner", TenantRole.TenantOwner), new TenantMember("reader", TenantRole.TenantReader)],
            new Dictionary<string, string>(), DateTimeOffset.UtcNow);
        switch (kind)
        {
            case "create":
                IRenderedComponent<CreateTenantFlow> create = Render<CreateTenantFlow>(p => p
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, authority).Add(x => x.BaselineTenantAbsent, true).Add(x => x.BaselineProjectionVersion, baseline));
                return (create, create.Instance);
            case "add":
                IRenderedComponent<AddTenantMemberFlow> add = Render<AddTenantMemberFlow>(p => p
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, authority).Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.ProjectionVersion, baseline).Add(x => x.CommandActivityLease, lease).Add(x => x.OnCommandActivityChanged, activity));
                return (add, add.Instance);
            case "change":
                IRenderedComponent<ChangeTenantMemberRoleFlow> change = Render<ChangeTenantMemberRoleFlow>(p => p
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, authority).Add(x => x.Detail, detail).Add(x => x.Member, detail.Members[1]).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.ProjectionVersion, baseline).Add(x => x.CommandActivityLease, lease).Add(x => x.OnCommandActivityChanged, activity));
                return (change, change.Instance);
            case "remove":
                IRenderedComponent<RemoveTenantMemberFlow> remove = Render<RemoveTenantMemberFlow>(p => p
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, authority).Add(x => x.Detail, detail).Add(x => x.Member, detail.Members[1]).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.ProjectionVersion, baseline).Add(x => x.AuditProofCapabilityAvailable, true).Add(x => x.TargetPlatformStandingResolved, true).Add(x => x.CommandActivityLease, lease).Add(x => x.OnCommandActivityChanged, activity));
                return (remove, remove.Instance);
            default:
                IRenderedComponent<EditTenantMetadataFlow> metadata = Render<EditTenantMetadataFlow>(p => p
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, authority).Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current).Add(x => x.ProjectionVersion, baseline).Add(x => x.ProjectionEvidenceProvider, _ => Task.FromResult(TenantCommandProofFixtures.Proof(detail, baseline))).Add(x => x.CommandActivityLease, lease).Add(x => x.OnCommandActivityChanged, activity));
                return (metadata, metadata.Instance);
        }
    }

    private static void Submit(string kind, IRenderedComponent<IComponent> cut, bool send = true)
    {
        switch (kind)
        {
            case "create":
                cut.Find("[data-testid='tenants-create-tenant-id']").Change("tenant.alpha");
                cut.Find("[data-testid='tenants-create-name']").Change("Alpha");
                break;
            case "add":
                cut.Find("[data-testid='tenants-add-member-user-id']").Change("new-reader");
                ChangeRoleSelection(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
                break;
            case "change":
                ChangeRoleSelection(cut, "tenants-change-role-new-role", nameof(TenantRole.TenantContributor));
                break;
            case "remove":
                cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader");
                break;
            case "metadata":
                if (cut.FindAll("[data-testid='tenants-edit-metadata-open']").Count > 0)
                {
                    cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
                }
                cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Renamed");
                break;
        }
        if (send)
        {
            cut.Find("form").Submit();
        }
    }

    private static void ChangeRoleSelection(IRenderedComponent<IComponent> cut, string testId, string value)
    {
        Microsoft.FluentUI.AspNetCore.Components.FluentSelect<string, string> select = cut
            .FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentSelect<string, string>>()
            .Single(rendered => rendered.Instance.AdditionalAttributes?.TryGetValue("data-testid", out object? id) is true
                && Equals(id, testId)).Instance;
        cut.InvokeAsync(() => select.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static object Snapshot(object component)
        => component.GetType().GetProperty("Snapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(component)!;

    private static T? Property<T>(object snapshot, string property)
        => (T?)snapshot.GetType().GetProperty(property)!.GetValue(snapshot);

    private static object? ReadRetained(TenantCommandAttemptTracker tracker, string kind)
        => kind switch
        {
            "create" => tracker.Find<TenantCreateCommandSnapshot>("tenant.alpha"),
            "add" => tracker.Find<TenantAddMemberCommandSnapshot>("tenant.alpha"),
            "change" => tracker.Find<TenantChangeRoleCommandSnapshot>("tenant.alpha"),
            "remove" => tracker.Find<TenantRemoveMemberCommandSnapshot>("tenant.alpha"),
            _ => tracker.Find<TenantUpdateMetadataCommandSnapshot>("tenant.alpha"),
        };

    private static string DispatchName(string kind)
        => kind switch
        {
            "create" => nameof(ITenantCommandGateway.CreateTenantAsync),
            "add" => nameof(ITenantCommandGateway.AddUserToTenantAsync),
            "change" => nameof(ITenantCommandGateway.ChangeUserRoleAsync),
            "remove" => nameof(ITenantCommandGateway.RemoveUserFromTenantAsync),
            _ => nameof(ITenantCommandGateway.UpdateTenantAsync),
        };

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
