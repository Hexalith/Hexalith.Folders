using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class FolderPermissionEvidenceProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProviderShouldAllowSupportedActionFromEffectivePermissionEvidence()
    {
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(Snapshot(
                revocationFreshnessEstablished: true,
                stale: false,
                EffectivePermissionsTestSupport.FolderGrant("read_metadata"))),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.Allowed);
        result.FreshnessWatermark.ShouldBe("folder_watermark_v1");
        result.FreshnessClass.ShouldBe("fresh");
    }

    [Fact]
    public async Task ProviderShouldFailClosedWhenRevocationFreshnessIsUnprovenForStrictOperation()
    {
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(Snapshot(
                revocationFreshnessEstablished: false,
                stale: false,
                EffectivePermissionsTestSupport.FolderGrant("read_metadata"))),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.FolderAclStale);
    }

    [Fact]
    public async Task ProviderShouldAllowBoundedStaleOnlyWhenPolicyExplicitlyAllowsIt()
    {
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(Snapshot(
                revocationFreshnessEstablished: false,
                stale: true,
                EffectivePermissionsTestSupport.FolderGrant("read_metadata"))),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(
            Request(allowBoundedStale: true, policyClass: FolderOperationPolicyClass.BoundedDiagnosticRead),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
        result.FreshnessClass.ShouldBe("bounded_stale");
    }

    [Fact]
    public async Task ProviderShouldReturnSafeNotFoundWithoutEchoingMissingFolderScope()
    {
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            new InMemoryEffectivePermissionsReadModel(),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(
            Request(operationScope: "folder-secret-victim"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.NotFoundSafe);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.SafeNotFound);
        result.FreshnessWatermark.ShouldBeNull();
    }

    private static FolderPermissionEvidenceRequest Request(
        string operationScope = "folder-a",
        bool allowBoundedStale = false,
        FolderOperationPolicyClass policyClass = FolderOperationPolicyClass.Mutation)
        => new(
            ManagedTenantId: "tenant-a",
            PrincipalId: "user-a",
            ActorSafeIdentifier: "actor-user-a",
            ActionToken: "read_metadata",
            OperationScope: operationScope,
            CorrelationId: "corr-a",
            TaskId: "task-a",
            OperationPolicyClass: policyClass,
            AllowBoundedStale: allowBoundedStale);

    private static InMemoryEffectivePermissionsReadModel ReadModel(EffectivePermissionsReadModelSnapshot snapshot)
    {
        InMemoryEffectivePermissionsReadModel readModel = new();
        readModel.Save(snapshot);
        return readModel;
    }

    private static EffectivePermissionsReadModelSnapshot Snapshot(
        bool revocationFreshnessEstablished,
        bool stale,
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
                ProjectionWatermark: "folder_watermark_v1",
                Stale: stale,
                ReasonCode: stale ? "bounded_stale" : null),
            RevocationFreshnessEstablished: revocationFreshnessEstablished,
            TaskScope: null);
}
