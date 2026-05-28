using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.3 / AC #5 — positive-coverage tests for <see cref="DispositionLabelMapper"/>. Drift
/// against the server-side state machine is covered by
/// <c>DispositionLabelParityTests</c> in <c>Hexalith.Folders.Tests</c>.
/// </summary>
public sealed class DispositionLabelMapperTests
{
    public static TheoryData<LifecycleState, bool, OperatorDispositionLabel> DispositionCases() => new()
    {
        { LifecycleState.Requested, false, OperatorDispositionLabel.Auto_recovering },
        { LifecycleState.Preparing, false, OperatorDispositionLabel.Auto_recovering },
        { LifecycleState.Ready, false, OperatorDispositionLabel.Available },
        { LifecycleState.Ready, true, OperatorDispositionLabel.Degraded_but_serving },
        { LifecycleState.Locked, false, OperatorDispositionLabel.Degraded_but_serving },
        { LifecycleState.Changes_staged, false, OperatorDispositionLabel.Degraded_but_serving },
        { LifecycleState.Dirty, false, OperatorDispositionLabel.Awaiting_human },
        { LifecycleState.Committed, false, OperatorDispositionLabel.Auto_recovering },
        { LifecycleState.Failed, false, OperatorDispositionLabel.Terminal_until_intervention },
        { LifecycleState.Inaccessible, false, OperatorDispositionLabel.Terminal_until_intervention },
        { LifecycleState.Unknown_provider_outcome, false, OperatorDispositionLabel.Awaiting_human },
        { LifecycleState.Reconciliation_required, false, OperatorDispositionLabel.Awaiting_human },
    };

    [Theory]
    [MemberData(nameof(DispositionCases))]
    public void ResolveDisposition_MatchesC6Matrix_ForEveryLifecycleState(
        LifecycleState state,
        bool hasProjectionLagEvidence,
        OperatorDispositionLabel expected)
        => DispositionLabelMapper
            .ResolveDisposition(state, hasProjectionLagEvidence)
            .ShouldBe(expected);

    [Fact]
    public void ResolveDisposition_IsTotal_ForEveryLifecycleState_AndProjectionLagBranch()
    {
        foreach (LifecycleState state in Enum.GetValues<LifecycleState>())
        {
            Should.NotThrow(() => { _ = DispositionLabelMapper.ResolveDisposition(state, hasProjectionLagEvidence: false); });
            Should.NotThrow(() => { _ = DispositionLabelMapper.ResolveDisposition(state, hasProjectionLagEvidence: true); });
        }
    }

    public static TheoryData<OperatorDispositionLabel, BadgeSlot> SlotCases() => new()
    {
        { OperatorDispositionLabel.Auto_recovering, BadgeSlot.Info },
        { OperatorDispositionLabel.Available, BadgeSlot.Success },
        { OperatorDispositionLabel.Degraded_but_serving, BadgeSlot.Warning },
        { OperatorDispositionLabel.Awaiting_human, BadgeSlot.Warning },
        { OperatorDispositionLabel.Terminal_until_intervention, BadgeSlot.Danger },
    };

    [Theory]
    [MemberData(nameof(SlotCases))]
    public void ResolveSlot_MatchesAcceptedBadgeSlotForEveryDisposition(
        OperatorDispositionLabel label,
        BadgeSlot expected)
        => DispositionLabelMapper.ResolveSlot(label).ShouldBe(expected);

    [Fact]
    public void ResolveSlot_IsTotal_ForEveryOperatorDispositionLabel()
    {
        foreach (OperatorDispositionLabel label in Enum.GetValues<OperatorDispositionLabel>())
        {
            Should.NotThrow(() => { _ = DispositionLabelMapper.ResolveSlot(label); });
        }
    }

    public static TheoryData<OperatorDispositionLabel, string> LabelCases() => new()
    {
        { OperatorDispositionLabel.Auto_recovering, "Auto-recovering" },
        { OperatorDispositionLabel.Available, "Available" },
        { OperatorDispositionLabel.Degraded_but_serving, "Degraded but serving" },
        { OperatorDispositionLabel.Awaiting_human, "Awaiting human" },
        { OperatorDispositionLabel.Terminal_until_intervention, "Terminal until intervention" },
    };

    [Theory]
    [MemberData(nameof(LabelCases))]
    public void ResolveLabel_ReturnsExpectedEnglishLabelForEveryDisposition(
        OperatorDispositionLabel label,
        string expected)
        => DispositionLabelMapper.ResolveLabel(label).ShouldBe(expected);

    [Fact]
    public void ResolveLabel_IsTotal_ForEveryOperatorDispositionLabel()
    {
        foreach (OperatorDispositionLabel label in Enum.GetValues<OperatorDispositionLabel>())
        {
            Should.NotThrow(() => { _ = DispositionLabelMapper.ResolveLabel(label); });
        }
    }

    [Theory]
    [MemberData(nameof(LifecycleStates))]
    public void ResolveTechnicalStateLabel_MatchesEnumMemberValue_ForEveryLifecycleState(LifecycleState state)
    {
        string expected = ReadEnumMemberValue(state);
        DispositionLabelMapper.ResolveTechnicalStateLabel(state).ShouldBe(expected);
    }

    [Fact]
    public void UnknownEnumValues_ThrowArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            { _ = DispositionLabelMapper.ResolveDisposition((LifecycleState)int.MaxValue); });
        Should.Throw<ArgumentOutOfRangeException>(() =>
            { _ = DispositionLabelMapper.ResolveTechnicalStateLabel((LifecycleState)int.MaxValue); });
        Should.Throw<ArgumentOutOfRangeException>(() =>
            { _ = DispositionLabelMapper.ResolveSlot((OperatorDispositionLabel)int.MaxValue); });
        Should.Throw<ArgumentOutOfRangeException>(() =>
            { _ = DispositionLabelMapper.ResolveLabel((OperatorDispositionLabel)int.MaxValue); });
    }

    public static TheoryData<LifecycleState> LifecycleStates()
    {
        TheoryData<LifecycleState> data = new();
        foreach (LifecycleState value in Enum.GetValues<LifecycleState>())
        {
            data.Add(value);
        }

        return data;
    }

    private static string ReadEnumMemberValue(LifecycleState state)
    {
        string name = Enum.GetName(state)!;
        FieldInfo field = typeof(LifecycleState).GetField(name)!;
        EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()!;
        return attribute.Value!;
    }
}
