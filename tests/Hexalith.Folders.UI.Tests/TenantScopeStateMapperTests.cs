using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #2 / AC #12 — totality + behaviour for the tenant-scope access mapper.
/// </summary>
public sealed class TenantScopeStateMapperTests
{
    public static TheoryData<EffectivePermissionsAuthorizationOutcome> Outcomes => [.. Enum.GetValues<EffectivePermissionsAuthorizationOutcome>()];

    public static TheoryData<TenantAccessState> AccessStates => [.. Enum.GetValues<TenantAccessState>()];

    [Fact]
    public void Resolve_Null_IsUnknown()
        => TenantScopeStateMapper.Resolve(null).ShouldBe(TenantAccessState.Unknown);

    [Fact]
    public void Resolve_DeniedSafe_IsDenied()
        => TenantScopeStateMapper.Resolve(Permissions(EffectivePermissionsAuthorizationOutcome.Denied_safe)).ShouldBe(TenantAccessState.Denied);

    [Fact]
    public void Resolve_Allowed_WithAdminister_IsAllowed()
        => TenantScopeStateMapper.Resolve(Permissions(
            EffectivePermissionsAuthorizationOutcome.Allowed,
            FolderPermissionLevel.Administer)).ShouldBe(TenantAccessState.Allowed);

    [Fact]
    public void Resolve_Allowed_WithoutAdminister_IsPartial()
        => TenantScopeStateMapper.Resolve(Permissions(
            EffectivePermissionsAuthorizationOutcome.Allowed,
            FolderPermissionLevel.Read)).ShouldBe(TenantAccessState.Partial);

    [Fact]
    public void Resolve_Allowed_ButStale_IsStale()
        => TenantScopeStateMapper.Resolve(Permissions(
            EffectivePermissionsAuthorizationOutcome.Allowed,
            stale: true,
            FolderPermissionLevel.Administer)).ShouldBe(TenantAccessState.Stale);

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void Resolve_IsTotalOverOutcomes(EffectivePermissionsAuthorizationOutcome outcome)
        => Should.NotThrow(() => TenantScopeStateMapper.Resolve(Permissions(outcome)));

    [Theory]
    [MemberData(nameof(AccessStates))]
    public void SlotAndLabel_AreTotal(TenantAccessState state)
    {
        _ = TenantScopeStateMapper.ResolveSlot(state);
        TenantScopeStateMapper.ResolveLabel(state).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ResolveLabel_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => TenantScopeStateMapper.ResolveLabel((TenantAccessState)999));

    private static EffectivePermissions Permissions(
        EffectivePermissionsAuthorizationOutcome outcome,
        params FolderPermissionLevel[] levels)
        => Permissions(outcome, stale: false, levels);

    private static EffectivePermissions Permissions(
        EffectivePermissionsAuthorizationOutcome outcome,
        bool stale,
        params FolderPermissionLevel[] levels)
        => new()
        {
            FolderId = "folder-1",
            AuthorizationOutcome = outcome,
            Permissions = [.. levels],
            Freshness = new FreshnessMetadata
            {
                Stale = stale,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };
}
