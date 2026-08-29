using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsTaskScopeTests
{
    [Fact]
    public void ConstructionShouldSnapshotActionsWithOrdinalMembership()
    {
        HashSet<string> suppliedActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "read_metadata",
        };

        EffectivePermissionsTaskScope scope = new(
            Status: EffectivePermissionsTaskScopeStatus.Available,
            OpaqueTaskId: "task-a",
            OpaqueWorkspaceId: null,
            AllowedActions: suppliedActions);
        suppliedActions.Clear();
        suppliedActions.Add("mutate_files");

        scope.AllowedActions.Contains("read_metadata").ShouldBeTrue();
        scope.AllowedActions.Contains("READ_METADATA").ShouldBeFalse();
        scope.AllowedActions.Contains("mutate_files").ShouldBeFalse();
    }

    [Fact]
    public void InitAssignmentShouldSnapshotActionsWithOrdinalMembership()
    {
        EffectivePermissionsTaskScope scope = new(
            Status: EffectivePermissionsTaskScopeStatus.Available,
            OpaqueTaskId: "task-a",
            OpaqueWorkspaceId: null,
            AllowedActions: new HashSet<string>(StringComparer.Ordinal));
        HashSet<string> assignedActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "READ_METADATA",
        };

        scope = scope with { AllowedActions = assignedActions };
        assignedActions.Clear();
        assignedActions.Add("read_metadata");
        assignedActions.Add("mutate_files");

        scope.AllowedActions.Contains("READ_METADATA").ShouldBeTrue();
        scope.AllowedActions.Contains("read_metadata").ShouldBeFalse();
        scope.AllowedActions.Contains("mutate_files").ShouldBeFalse();
    }

    [Fact]
    public void ConstructionShouldRejectNullAllowedActions()
    {
        Should.Throw<ArgumentNullException>(() => new EffectivePermissionsTaskScope(
            Status: EffectivePermissionsTaskScopeStatus.Available,
            OpaqueTaskId: "task-a",
            OpaqueWorkspaceId: null,
            AllowedActions: null!));
    }

    [Fact]
    public async Task ValidTaskContextCanOnlyNarrowEffectivePermissions()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata"),
            EffectivePermissionsTestSupport.FolderGrant("mutate_files")) with
        {
            TaskScope = new EffectivePermissionsTaskScope(
                Status: EffectivePermissionsTaskScopeStatus.Available,
                OpaqueTaskId: "task-a",
                OpaqueWorkspaceId: "workspace-a",
                AllowedActions: new HashSet<string>(StringComparer.Ordinal)
                {
                    "read_metadata",
                }),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(
            snapshot,
            taskContextId: "task-a",
            workspaceContextId: "workspace-a").ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        result.Permissions.ShouldBe([EffectivePermissionLevel.Read]);
        result.TaskContextId.ShouldBe("task-a");
        result.WorkspaceContextId.ShouldBe("workspace-a");
    }

    [Fact]
    public async Task CaseVariantFromIncompatibleComparerShouldBeDeniedSafe()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            TaskScope = new EffectivePermissionsTaskScope(
                Status: EffectivePermissionsTaskScopeStatus.Available,
                OpaqueTaskId: "task-a",
                OpaqueWorkspaceId: null,
                AllowedActions: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "READ_METADATA",
                }),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(snapshot, taskContextId: "task-a").ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.DeniedSafe);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExactActionFromIncompatibleComparerShouldRemainAllowed()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            TaskScope = new EffectivePermissionsTaskScope(
                Status: EffectivePermissionsTaskScopeStatus.Available,
                OpaqueTaskId: "task-a",
                OpaqueWorkspaceId: null,
                AllowedActions: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "read_metadata",
                }),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(snapshot, taskContextId: "task-a").ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        result.AuthorizationOutcome.ShouldBe("allowed");
        result.Permissions.ShouldBe([EffectivePermissionLevel.Read]);
    }

    [Fact]
    public async Task TaskScopeWithWorkspaceMustReceiveWorkspaceContextIdToBeAllowed()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            TaskScope = new EffectivePermissionsTaskScope(
                Status: EffectivePermissionsTaskScopeStatus.Available,
                OpaqueTaskId: "task-a",
                OpaqueWorkspaceId: "workspace-a",
                AllowedActions: new HashSet<string>(StringComparer.Ordinal)
                {
                    "read_metadata",
                }),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(
            snapshot,
            taskContextId: "task-a",
            workspaceContextId: null).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.DeniedSafe);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EffectivePermissionsTaskScopeStatus.OutsideTenant)]
    [InlineData(EffectivePermissionsTaskScopeStatus.OutsideFolder)]
    [InlineData(EffectivePermissionsTaskScopeStatus.Unauthorized)]
    [InlineData(EffectivePermissionsTaskScopeStatus.Stale)]
    [InlineData(EffectivePermissionsTaskScopeStatus.Unavailable)]
    public async Task InvalidTaskContextReturnsSafeEvidence(EffectivePermissionsTaskScopeStatus status)
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            TaskScope = new EffectivePermissionsTaskScope(
                Status: status,
                OpaqueTaskId: "task-a",
                OpaqueWorkspaceId: "workspace-a",
                AllowedActions: new HashSet<string>(StringComparer.Ordinal)
                {
                    "read_metadata",
                }),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(snapshot, taskContextId: "task-a").ConfigureAwait(true);

        result.Code.ShouldBe(status == EffectivePermissionsTaskScopeStatus.Unavailable
            ? EffectivePermissionsResultCode.ReadModelUnavailable
            : EffectivePermissionsResultCode.DeniedSafe);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
    }

    private static async Task<EffectivePermissionsQueryResult> ExecuteAsync(
        EffectivePermissionsReadModelSnapshot snapshot,
        string? taskContextId,
        string? workspaceContextId = null)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(snapshot));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-a",
                AuthoritativeTenantId: "tenant-a",
                PrincipalId: "user-a",
                CorrelationId: "corr-a",
                TaskContextId: taskContextId,
                WorkspaceContextId: workspaceContextId),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
