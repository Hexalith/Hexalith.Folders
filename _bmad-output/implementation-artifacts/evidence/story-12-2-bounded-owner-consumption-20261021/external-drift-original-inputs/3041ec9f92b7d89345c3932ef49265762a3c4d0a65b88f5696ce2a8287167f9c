using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.State;

public sealed class TenantLifecycleAvailabilityTests
{
    [Fact]
    public void Active_tenant_enable_is_same_state_expected_rejection_without_submission_success()
    {
        TenantLifecycleAvailability availability = CurrentInput(TenantStatus.Active)
            .Evaluate(TenantLifecycleOperation.EnableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.AlreadyActive");
        availability.ExpectedDomainOutcomeKey.ShouldBe("TenantLifecycleStateAlreadySet");
    }

    [Fact]
    public void Disabled_tenant_disable_is_same_state_expected_rejection_without_submission_success()
    {
        TenantLifecycleAvailability availability = CurrentInput(TenantStatus.Disabled)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.AlreadyDisabled");
        availability.ExpectedDomainOutcomeKey.ShouldBe("TenantLifecycleStateAlreadySet");
    }

    [Fact]
    public void Disabled_tenant_enable_keeps_disabled_projection_truth_and_blocks_on_governance()
    {
        TenantLifecycleAvailability availability = CurrentInput(TenantStatus.Disabled)
            .Evaluate(TenantLifecycleOperation.EnableTenant);

        availability.CurrentStatus.ShouldBe(TenantStatus.Disabled);
        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.HighImpactFlowNotReady);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.Governance");
        availability.ExpectedDomainOutcomeKey.ShouldBeNull();
    }

    [Fact]
    public void Unresolved_governance_blocks_available_lifecycle_direction_by_default()
    {
        TenantLifecycleAvailability availability = CurrentInput(TenantStatus.Active)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.HighImpactFlowNotReady);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.Governance");
    }

    [Fact]
    public void Aging_freshness_does_not_bypass_unresolved_governance()
    {
        const ReadModelFreshnessState freshness = ReadModelFreshnessState.Aging;
        TenantLifecycleAvailability availability = CurrentInput(
                TenantStatus.Active,
                freshness: freshness)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.Freshness.ShouldBe(freshness);
        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.HighImpactFlowNotReady);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.Governance");
    }

    [Theory]
    [InlineData(TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Current)]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Stale)]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Unknown)]
    public void Stale_or_unknown_freshness_blocks_lifecycle_actions(
        TenantDetailSurfaceKind surfaceKind,
        ReadModelFreshnessState freshness)
    {
        TenantLifecycleAvailability availability = CurrentInput(
                TenantStatus.Active,
                surfaceKind,
                freshness,
                TenantLifecycleGovernanceReadiness.Ready)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.StaleData);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.StaleFreshness");
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void Non_current_operational_lifecycle_blocks_actions_even_when_legacy_freshness_is_current(
        ProjectionLifecycleState lifecycle)
    {
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: lifecycle)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.StaleData);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.ProjectionLifecycle");
        availability.FocusTarget.ShouldBe(TenantCommandFocusTarget.Refresh);
    }

    [Theory]
    // Decision D-F (2026-07-31). The strict lifecycle gate is upheld, but it must be evaluated after the
    // surface and freshness clauses. Every failed-read snapshot -- Unavailable, Degraded, Stale -- also
    // carries a non-Current lifecycle, so with the lifecycle test first it answered for all of them and
    // Tenants.Lifecycle.Unavailable.StaleFreshness became unreachable: an operator whose read had simply
    // failed was told to refresh the projection lifecycle. Reverting the order makes every row here report
    // ...ProjectionLifecycle instead.
    [InlineData(TenantDetailSurfaceKind.Unavailable, ProjectionLifecycleState.Unavailable)]
    [InlineData(TenantDetailSurfaceKind.Degraded, ProjectionLifecycleState.Degraded)]
    [InlineData(TenantDetailSurfaceKind.Stale, ProjectionLifecycleState.Stale)]
    [InlineData(TenantDetailSurfaceKind.Unknown, ProjectionLifecycleState.Unknown)]
    public void A_failed_read_reports_its_own_failure_not_the_projection_lifecycle(
        TenantDetailSurfaceKind surfaceKind,
        ProjectionLifecycleState lifecycle)
    {
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Unknown,
                surfaceKind,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: lifecycle)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.StaleData);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.StaleFreshness");
        availability.FocusTarget.ShouldBe(TenantCommandFocusTarget.Refresh);
    }

    [Fact]
    public void An_unauthorized_read_reports_permission_not_the_projection_lifecycle()
    {
        // Authorization outranks the lifecycle gate too: an unauthorized surface carries no lifecycle
        // evidence, and "refresh the projection" is the wrong instruction for a permission problem.
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Unauthorized,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: ProjectionLifecycleState.Unknown)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingPermission);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.MissingPermission");
    }

    [Fact]
    public void Unauthorized_detail_surface_blocks_as_missing_permission()
    {
        TenantLifecycleAvailability availability = CurrentInput(
                TenantStatus.Active,
                TenantDetailSurfaceKind.Unauthorized,
                ReadModelFreshnessState.Current,
                TenantLifecycleGovernanceReadiness.Ready)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingPermission);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.MissingPermission");
    }

    [Theory]
    [InlineData(TenantDetailSurfaceKind.Degraded)]
    [InlineData(TenantDetailSurfaceKind.Unavailable)]
    [InlineData(TenantDetailSurfaceKind.Unknown)]
    public void Degraded_or_unavailable_detail_surface_blocks_as_stale_data_not_missing_permission(TenantDetailSurfaceKind surfaceKind)
    {
        TenantLifecycleAvailability availability = CurrentInput(
                TenantStatus.Active,
                surfaceKind,
                ReadModelFreshnessState.Current,
                TenantLifecycleGovernanceReadiness.Ready)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.StaleData);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.StaleFreshness");
    }

    [Fact]
    public void Unknown_tenant_status_blocks_as_missing_lifecycle_support()
    {
        TenantLifecycleAvailability availability = CurrentInput(
                TenantStatus.Unknown,
                governanceReadiness: TenantLifecycleGovernanceReadiness.Ready)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.UnknownStatus");
    }

    [Fact]
    public void Indeterminate_authorization_reflection_fails_closed()
    {
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Indeterminate,
                Lifecycle: ProjectionLifecycleState.Current)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingPermission);
    }

    [Fact]
    public void Missing_command_surface_blocks_lifecycle_support_even_when_authorized()
    {
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: false,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: ProjectionLifecycleState.Current)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.CommandSurface");
    }

    [Fact]
    public void Narrow_safety_context_fails_closed_before_command_availability()
    {
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                TenantStatus.Active,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                IsNarrowSafetyContext: true,
                Lifecycle: ProjectionLifecycleState.Current)
            .Evaluate(TenantLifecycleOperation.DisableTenant);

        availability.IsUnavailable.ShouldBeTrue();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.HighImpactFlowNotReady);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.Mobile");
    }

    [Theory]
    [InlineData(TenantStatus.Active, TenantLifecycleOperation.DisableTenant)]
    [InlineData(TenantStatus.Disabled, TenantLifecycleOperation.EnableTenant)]
    public void A_fully_satisfied_input_is_available(
        TenantStatus status,
        TenantLifecycleOperation operation)
    {
        // Every other assertion in this file is IsUnavailable.ShouldBeTrue(), and repo-wide
        // `IsUnavailable.ShouldBeFalse` and `UnavailableReasonCategory.None` had zero hits. Without a
        // positive control the success return of Evaluate could be replaced with Blocked(...) -- every
        // lifecycle control permanently dead in production -- with full coverage still reported.
        TenantLifecycleAvailability availability = new TenantLifecycleAvailabilityInput(
                "tenant.alpha",
                status,
                ReadModelFreshnessState.Current,
                TenantDetailSurfaceKind.Ready,
                IsCommandSurfaceConnected: true,
                ProjectionVersion: "tenant-sequence:41",
                TenantLifecycleGovernanceReadiness.Ready,
                TenantLifecycleAuthorizationReflectionState.Authorized,
                Lifecycle: ProjectionLifecycleState.Current)
            .Evaluate(operation);

        availability.IsUnavailable.ShouldBeFalse();
        availability.UnavailableReasonCategory.ShouldBe(TenantLifecycleUnavailableReasonCategory.None);
        availability.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Available");
        availability.ExpectedDomainOutcomeKey.ShouldBeNull();
        availability.FocusTarget.ShouldBe(TenantCommandFocusTarget.Submit);
        availability.Operation.ShouldBe(operation);
    }

    private static TenantLifecycleAvailabilityInput CurrentInput(
        TenantStatus status,
        TenantDetailSurfaceKind surfaceKind = TenantDetailSurfaceKind.Ready,
        ReadModelFreshnessState freshness = ReadModelFreshnessState.Current,
        TenantLifecycleGovernanceReadiness governanceReadiness = TenantLifecycleGovernanceReadiness.Unresolved)
        => new(
            "tenant.alpha",
            status,
            freshness,
            surfaceKind,
            IsCommandSurfaceConnected: true,
            ProjectionVersion: "tenant-sequence:41",
            governanceReadiness,
            TenantLifecycleAuthorizationReflectionState.Authorized,
            Lifecycle: ProjectionLifecycleState.Current);
}
