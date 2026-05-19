using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Tests.Authorization;

internal static class EffectivePermissionsTestSupport
{
    internal static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    internal static EffectivePermissionsQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        IEffectivePermissionsReadModel readModel)
        => new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            readModel,
            new FixedUtcClock(Now));

    internal const string TenantWatermark = "tenant_a_watermark_v0007";

    internal const string FolderWatermark = "folder_a_watermark_v0011";

    internal static FolderTenantAccessProjection TenantProjection(
        string tenantId = "tenant-a",
        params string[] principals)
    {
        Dictionary<string, FolderTenantPrincipalEvidence> principalEvidence = principals
            .ToDictionary(
                static principal => principal,
                static principal => new FolderTenantPrincipalEvidence(principal, "Member"),
                StringComparer.Ordinal);

        return new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = true,
            Principals = principalEvidence,
            Watermark = 7,
            LastEventTimestamp = Now.AddMinutes(-1),
            ProjectionWatermark = TenantWatermark,
        };
    }

    internal static EffectivePermissionsReadModelSnapshot Snapshot(
        params EffectivePermissionEvidenceRow[] rows)
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows: rows,
            Freshness: new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: Now,
                ProjectionWatermark: FolderWatermark,
                Stale: false,
                ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null);

    internal static EffectivePermissionEvidenceRow OrganizationGrant(string action, string principalId = "user-a")
        => Row(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, action, principalId);

    internal static EffectivePermissionEvidenceRow FolderGrant(string action, string principalId = "user-a", long sequence = 1)
        => Row(EffectivePermissionEvidenceSource.FolderOverrideGrant, action, principalId, sequence);

    internal static EffectivePermissionEvidenceRow FolderRevoke(string action, string principalId = "user-a", long sequence = 2)
        => Row(EffectivePermissionEvidenceSource.FolderOverrideRevoke, action, principalId, sequence);

    private static EffectivePermissionEvidenceRow Row(
        EffectivePermissionEvidenceSource source,
        string action,
        string principalId,
        long sequence = 1)
        => new(
            Source: source,
            Principal: EffectivePermissionPrincipal.User(principalId),
            Action: action,
            Sequence: sequence,
            EffectiveAt: Now.AddSeconds(sequence));
}

internal sealed class CountingTenantAccessProjectionStore(FolderTenantAccessProjection? projection = null)
    : IFolderTenantAccessProjectionStore
{
    public int Gets { get; private set; }

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        Gets++;
        return Task.FromResult(projection);
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class RecordingEffectivePermissionsReadModel(EffectivePermissionsReadModelResult result)
    : IEffectivePermissionsReadModel
{
    public int Requests { get; private set; }

    public EffectivePermissionsReadModelRequest? LastRequest { get; private set; }

    public Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        LastRequest = request;
        return Task.FromResult(result);
    }
}
