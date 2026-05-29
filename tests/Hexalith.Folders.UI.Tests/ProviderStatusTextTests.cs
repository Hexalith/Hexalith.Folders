using System.Globalization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.7 / AC #4 / AC #6 / AC #7 / AC #8 / AC #12 — totality sentinels for the provider status-text
/// mappers. Every switch over an SDK enum is exercised as a <b>total</b> <see cref="TheoryData{T}"/> over
/// <see cref="Enum.GetValues{TEnum}()"/> (proving every declared member resolves), paired with a
/// <c>ThrowsOnUndefined</c> fact proving an undefined value throws <see cref="ArgumentOutOfRangeException"/>
/// — never a silent default. A contract drift therefore becomes a failing test, not a mis-rendered status.
/// </summary>
public sealed class ProviderStatusTextTests
{
    public static TheoryData<ProviderCapabilityState> CapabilityStates => [.. Enum.GetValues<ProviderCapabilityState>()];

    public static TheoryData<RepositoryBindingBindingState> BindingStates => [.. Enum.GetValues<RepositoryBindingBindingState>()];

    public static TheoryData<ProviderOutcomeState> OutcomeStates => [.. Enum.GetValues<ProviderOutcomeState>()];

    public static TheoryData<ProviderReadinessStatus> ReadinessStatuses => [.. Enum.GetValues<ProviderReadinessStatus>()];

    public static TheoryData<ProviderCapabilityName> CapabilityNames => [.. Enum.GetValues<ProviderCapabilityName>()];

    public static TheoryData<SensitiveMetadataTier> SensitiveTiers => [.. Enum.GetValues<SensitiveMetadataTier>()];

    [Theory]
    [MemberData(nameof(CapabilityStates))]
    public void ResolveCapabilityState_IsTotal(ProviderCapabilityState state)
    {
        ProviderStatusText.ResolveCapabilityStateLabel(state).ShouldNotBeNullOrWhiteSpace();
        _ = ProviderStatusText.ResolveCapabilityStateSlot(state);
    }

    [Fact]
    public void ResolveCapabilityState_ThrowsOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveCapabilityStateLabel((ProviderCapabilityState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveCapabilityStateSlot((ProviderCapabilityState)999));
    }

    [Theory]
    [MemberData(nameof(BindingStates))]
    public void ResolveBindingState_IsTotal(RepositoryBindingBindingState state)
    {
        ProviderStatusText.ResolveBindingStateLabel(state).ShouldNotBeNullOrWhiteSpace();
        _ = ProviderStatusText.ResolveBindingStateSlot(state);
    }

    [Fact]
    public void ResolveBindingState_ThrowsOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveBindingStateLabel((RepositoryBindingBindingState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveBindingStateSlot((RepositoryBindingBindingState)999));
    }

    [Theory]
    [MemberData(nameof(OutcomeStates))]
    public void ResolveOutcomeState_IsTotal(ProviderOutcomeState state)
    {
        ProviderStatusText.ResolveOutcomeStateLabel(state).ShouldNotBeNullOrWhiteSpace();
        _ = ProviderStatusText.ResolveOutcomeStateSlot(state);
    }

    [Fact]
    public void ResolveOutcomeState_ThrowsOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveOutcomeStateLabel((ProviderOutcomeState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveOutcomeStateSlot((ProviderOutcomeState)999));
    }

    [Theory]
    [MemberData(nameof(ReadinessStatuses))]
    public void ResolveReadinessStatus_IsTotal(ProviderReadinessStatus status)
    {
        ProviderStatusText.ResolveReadinessStatusLabel(status).ShouldNotBeNullOrWhiteSpace();
        _ = ProviderStatusText.ResolveReadinessStatusSlot(status);
    }

    [Fact]
    public void ResolveReadinessStatus_ThrowsOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveReadinessStatusLabel((ProviderReadinessStatus)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveReadinessStatusSlot((ProviderReadinessStatus)999));
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void ResolveCapabilityName_IsTotal(ProviderCapabilityName capability)
        => ProviderStatusText.ResolveCapabilityNameLabel(capability).ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void ResolveCapabilityName_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveCapabilityNameLabel((ProviderCapabilityName)999));

    [Theory]
    [MemberData(nameof(SensitiveTiers))]
    public void ResolveSensitiveMetadataTier_IsTotal(SensitiveMetadataTier tier)
        => ProviderStatusText.ResolveSensitiveMetadataTierLabel(tier).ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void ResolveSensitiveMetadataTier_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => ProviderStatusText.ResolveSensitiveMetadataTierLabel((SensitiveMetadataTier)999));

    [Fact]
    public void Labels_AreStableUnderEnUsCulture()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            ProviderStatusText.ResolveCapabilityStateLabel(ProviderCapabilityState.Temporarily_unavailable).ShouldBe("Temporarily unavailable");
            ProviderStatusText.ResolveCapabilityNameLabel(ProviderCapabilityName.Branch_ref_policy).ShouldBe("Branch & ref policy");
            ProviderStatusText.ResolveBindingStateLabel(RepositoryBindingBindingState.Unknown_provider_outcome).ShouldBe("Unknown provider outcome");
            ProviderStatusText.ResolveSensitiveMetadataTierLabel(SensitiveMetadataTier.Credential_sensitive).ShouldBe("Credential-sensitive");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void StatusSlots_AreNonColorOnly_AndCarryTheAwaitingHumanTreatment()
    {
        // Capability support differences (FR57) render with distinct semantic slots.
        ProviderStatusText.ResolveCapabilityStateSlot(ProviderCapabilityState.Supported).ShouldBe(BadgeSlot.Success);
        ProviderStatusText.ResolveCapabilityStateSlot(ProviderCapabilityState.Unsupported).ShouldBe(BadgeSlot.Danger);

        // unknown_provider_outcome / reconciliation_required → Warning (awaiting-human treatment, AC #8) —
        // never a neutral unbadged "Unknown".
        ProviderStatusText.ResolveBindingStateSlot(RepositoryBindingBindingState.Unknown_provider_outcome).ShouldBe(BadgeSlot.Warning);
        ProviderStatusText.ResolveBindingStateSlot(RepositoryBindingBindingState.Reconciliation_required).ShouldBe(BadgeSlot.Warning);
        ProviderStatusText.ResolveBindingStateSlot(RepositoryBindingBindingState.Failed).ShouldBe(BadgeSlot.Danger);
        ProviderStatusText.ResolveBindingStateSlot(RepositoryBindingBindingState.Bound).ShouldBe(BadgeSlot.Success);
        ProviderStatusText.ResolveOutcomeStateSlot(ProviderOutcomeState.Unknown_provider_outcome).ShouldBe(BadgeSlot.Warning);
    }

    [Fact]
    public void ResolveCapabilityStateSlot_TemporarilyUnavailable_IsWarning_NotDanger()
    {
        // AC #8: temporarily-unavailable is a transient state distinct from unsupported/failed — it must
        // render Warning (UX-DR14), never Danger, so the FR57 matrix never presents a transient gap as a
        // hard capability failure.
        ProviderStatusText.ResolveCapabilityStateSlot(ProviderCapabilityState.Temporarily_unavailable).ShouldBe(BadgeSlot.Warning);
    }

    [Fact]
    public void ResolveBindingStateSlot_Requested_IsInfo_InFlight()
    {
        // AC #4 / AC #8: an in-flight (requested) binding is neither bound (Success) nor failed (Danger) —
        // it renders the Info "in-flight" slot so an unconfirmed binding is never presented as bound.
        ProviderStatusText.ResolveBindingStateSlot(RepositoryBindingBindingState.Requested).ShouldBe(BadgeSlot.Info);
    }

    [Fact]
    public void ResolveReadinessStatusSlot_MapsReadyToSuccess_DegradedToWarning_FailedToDanger()
    {
        // AC #8: degraded != unavailable — Degraded must resolve to Warning, never Danger (which would
        // over-escalate a degraded provider to a hard outage). Ready/Failed anchor both ends.
        ProviderStatusText.ResolveReadinessStatusSlot(ProviderReadinessStatus.Ready).ShouldBe(BadgeSlot.Success);
        ProviderStatusText.ResolveReadinessStatusSlot(ProviderReadinessStatus.Degraded).ShouldBe(BadgeSlot.Warning);
        ProviderStatusText.ResolveReadinessStatusSlot(ProviderReadinessStatus.Failed).ShouldBe(BadgeSlot.Danger);
    }

    [Fact]
    public void ResolveOutcomeStateSlot_ReconciliationRequiredIsWarning_AndKnownFailureIsDanger()
    {
        // AC #8: reconciliation_required carries the awaiting-human Warning treatment (never neutral/Info);
        // known_failure is a hard Danger. These pin the ProviderOutcomeState slots specifically (distinct
        // from the RepositoryBindingBindingState mapper covered above).
        ProviderStatusText.ResolveOutcomeStateSlot(ProviderOutcomeState.Reconciliation_required).ShouldBe(BadgeSlot.Warning);
        ProviderStatusText.ResolveOutcomeStateSlot(ProviderOutcomeState.Known_failure).ShouldBe(BadgeSlot.Danger);
    }

    [Fact]
    public void ResolveOutcomeStateLabel_UnknownProviderOutcome_IsHonestDistinctLabel_NotNeutralUnknown()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            // AC #8: unknown_provider_outcome is surfaced as its own honest, distinct label — never
            // collapsed into a bare neutral "Unknown" (the forbidden wording).
            ProviderStatusText.ResolveOutcomeStateLabel(ProviderOutcomeState.Unknown_provider_outcome)
                .ShouldBe("Unknown provider outcome");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
