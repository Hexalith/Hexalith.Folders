using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #5 / AC #12 — totality for the Trust Matrix state mapper and the per-dimension
/// derivers over their SDK enums (never a silently-defaulted tile).
/// </summary>
public sealed class TrustDimensionMappingTests
{
    public static TheoryData<TrustDimensionState> DimensionStates => [.. Enum.GetValues<TrustDimensionState>()];

    public static TheoryData<TenantAccessState> AccessStates => [.. Enum.GetValues<TenantAccessState>()];

    public static TheoryData<OperatorDispositionLabel> Dispositions => [.. Enum.GetValues<OperatorDispositionLabel>()];

    public static TheoryData<LockState> LockStates => [.. Enum.GetValues<LockState>()];

    public static TheoryData<ProviderOutcomeState> ProviderOutcomes => [.. Enum.GetValues<ProviderOutcomeState>()];

    [Theory]
    [MemberData(nameof(DimensionStates))]
    public void Mapper_SlotAndLabel_AreTotal(TrustDimensionState state)
    {
        _ = TrustDimensionStateMapper.ResolveSlot(state);
        TrustDimensionStateMapper.ResolveLabel(state).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Mapper_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => TrustDimensionStateMapper.ResolveLabel((TrustDimensionState)999));

    [Theory]
    [MemberData(nameof(AccessStates))]
    public void FromAuthorization_IsTotal(TenantAccessState access)
        => Should.NotThrow(() => TrustDimensionDeriver.FromAuthorization(access));

    [Theory]
    [MemberData(nameof(Dispositions))]
    public void FromDisposition_IsTotal(OperatorDispositionLabel disposition)
        => Should.NotThrow(() => TrustDimensionDeriver.FromDisposition(disposition));

    [Theory]
    [MemberData(nameof(LockStates))]
    public void FromLockState_IsTotal(LockState lockState)
        => Should.NotThrow(() => TrustDimensionDeriver.FromLockState(lockState));

    [Theory]
    [MemberData(nameof(ProviderOutcomes))]
    public void FromProviderOutcome_IsTotal(ProviderOutcomeState outcome)
        => Should.NotThrow(() => TrustDimensionDeriver.FromProviderOutcome(outcome));

    [Fact]
    public void NullEvidence_IsUnknown()
    {
        TrustDimensionDeriver.FromLockState(null).ShouldBe(TrustDimensionState.Unknown);
        TrustDimensionDeriver.FromProviderOutcome(null).ShouldBe(TrustDimensionState.Unknown);
    }

    [Fact]
    public void Derivers_ThrowOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TrustDimensionDeriver.FromDisposition((OperatorDispositionLabel)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TrustDimensionDeriver.FromLockState((LockState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TrustDimensionDeriver.FromProviderOutcome((ProviderOutcomeState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TrustDimensionDeriver.FromAuthorization((TenantAccessState)999));
    }
}
