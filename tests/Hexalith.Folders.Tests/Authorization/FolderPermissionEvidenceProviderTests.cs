using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class FolderPermissionEvidenceProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("tenant-a")]
    [InlineData("provider-binding-a")]
    public async Task ReservedNonFolderScopesNeverReadOrDiscloseProjectionEvidence(string scope)
    {
        RecordingEffectivePermissionsReadModel model = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        var result = await new EffectivePermissionsFolderPermissionEvidenceProvider(model, new FixedUtcClock(Now))
            .GetEvidenceAsync(Request(operationScope: scope), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(FolderPermissionEvidenceStatus.NotFoundSafe);
        result.FreshnessWatermark.ShouldBeNull();
        model.Requests.ShouldBe(0);
    }

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

    [Theory]
    [InlineData(-1, -4, FolderPermissionEvidenceStatus.Allowed, LayeredAuthorizationOutcomeCodes.Allowed)]
    [InlineData(0, -5, FolderPermissionEvidenceStatus.Allowed, LayeredAuthorizationOutcomeCodes.Allowed)]
    [InlineData(1, -5, FolderPermissionEvidenceStatus.Malformed, LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed)]
    public async Task ProviderShouldCompareObservationInstantsAcrossClockOffsets(
        int differenceInTicks,
        int observationOffsetHours,
        FolderPermissionEvidenceStatus expectedStatus,
        string expectedCode)
    {
        // The US Eastern clock repeats the 1 a.m. hour at 06:00 UTC on this date.
        DateTimeOffset now = new(2026, 11, 1, 6, 0, 0, TimeSpan.Zero);
        DateTimeOffset observedAt = now.AddTicks(differenceInTicks).ToOffset(TimeSpan.FromHours(observationOffsetHours));
        EffectivePermissionsReadModelSnapshot snapshot = Snapshot(true, false, EffectivePermissionsTestSupport.FolderGrant("read_metadata"));
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(snapshot with { Freshness = snapshot.Freshness with { ObservedAt = observedAt } }),
            new FixedUtcClock(now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expectedStatus);
        result.OutcomeCode.ShouldBe(expectedCode);
    }

    [Fact]
    public async Task ProviderShouldFailClosedWhenSnapshotObservedAtIsFutureDated()
    {
        EffectivePermissionsReadModelSnapshot snapshot = Snapshot(
            revocationFreshnessEstablished: true,
            stale: false,
            EffectivePermissionsTestSupport.FolderGrant("read_metadata")) with
        {
            Freshness = new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: Now.AddMinutes(5),
                ProjectionWatermark: "folder_watermark_future",
                Stale: false,
                ReasonCode: null),
        };
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(snapshot),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(
            Request(allowBoundedStale: true),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Malformed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed);
    }

    [Fact]
    public async Task ProviderShouldFailClosedForRevocationFreshnessUnprovenUnderMutationPolicy()
    {
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(Snapshot(
                revocationFreshnessEstablished: false,
                stale: false,
                EffectivePermissionsTestSupport.FolderGrant("mutate_files"))),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(
            Request(actionToken: "mutate_files", allowBoundedStale: false, policyClass: FolderOperationPolicyClass.Mutation),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.FolderAclStale);
    }

    [Fact]
    public async Task GrantAndRevokeAtIdenticalSequenceAndTimestampShouldPreferRevoke()
    {
        DateTimeOffset effectiveAt = Now.AddMinutes(-1);
        EffectivePermissionEvidenceRow grant = new(
            Principal: EffectivePermissionPrincipal.User("user-a"),
            Action: "read_metadata",
            Source: EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
            Sequence: 1,
            EffectiveAt: effectiveAt);
        EffectivePermissionEvidenceRow revoke = new(
            Principal: EffectivePermissionPrincipal.User("user-a"),
            Action: "read_metadata",
            Source: EffectivePermissionEvidenceSource.FolderOverrideRevoke,
            Sequence: 1,
            EffectiveAt: effectiveAt);
        EffectivePermissionsFolderPermissionEvidenceProvider provider = new(
            ReadModel(Snapshot(revocationFreshnessEstablished: true, stale: false, grant, revoke)),
            new FixedUtcClock(Now));

        FolderPermissionEvidenceResult result = await provider.GetEvidenceAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Denied);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.FolderAclDenied);
    }

    [Theory]
    [InlineData("tenant-b", "folder-a")]
    [InlineData("tenant-a", "folder-b")]
    [InlineData("TENANT-A", "folder-a")]
    [InlineData("tenant-a", "organization_baseline")]
    public async Task ProviderShouldFailClosedWhenTheReaderReturnsAForeignScopeSnapshot(
        string snapshotTenantId,
        string snapshotFolderId)
    {
        EffectivePermissionsReadModelSnapshot foreign = Snapshot(
            revocationFreshnessEstablished: true,
            stale: false,
            EffectivePermissionsTestSupport.FolderGrant("read_metadata")) with
        {
            ManagedTenantId = snapshotTenantId,
            FolderId = snapshotFolderId,
        };
        RecordingEffectivePermissionsReadModel model = new(EffectivePermissionsReadModelResult.Available(foreign));

        FolderPermissionEvidenceResult result = await new EffectivePermissionsFolderPermissionEvidenceProvider(
                model,
                new FixedUtcClock(Now))
            .GetEvidenceAsync(Request(), TestContext.Current.CancellationToken);

        model.Requests.ShouldBe(1);
        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Malformed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed);
        result.FreshnessWatermark.ShouldBeNull();
    }

    [Theory]
    [InlineData("tenant-b", "folder-a")]
    [InlineData("tenant-a", "folder-b")]
    public async Task ProviderShouldFailClosedWhenABoundedStaleSnapshotBelongsToAnotherScope(
        string snapshotTenantId,
        string snapshotFolderId)
    {
        EffectivePermissionsReadModelSnapshot foreign = Snapshot(
            revocationFreshnessEstablished: true,
            stale: true,
            EffectivePermissionsTestSupport.FolderGrant("read_metadata")) with
        {
            ManagedTenantId = snapshotTenantId,
            FolderId = snapshotFolderId,
        };
        RecordingEffectivePermissionsReadModel model = new(EffectivePermissionsReadModelResult.Stale(foreign));

        FolderPermissionEvidenceResult result = await new EffectivePermissionsFolderPermissionEvidenceProvider(
                model,
                new FixedUtcClock(Now))
            .GetEvidenceAsync(Request(allowBoundedStale: true), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Malformed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed);
    }

    [Fact]
    public async Task ProviderShouldAllowOrganizationScopeEvidenceForTheReservedOrganizationScope()
    {
        EffectivePermissionsReadModelSnapshot organization = Snapshot(
            revocationFreshnessEstablished: true,
            stale: false,
            EffectivePermissionsTestSupport.FolderGrant("create_folder")) with
        {
            FolderId = FolderStreamName.ReservedOrganizationPermissionScope,
        };
        RecordingEffectivePermissionsReadModel model = new(EffectivePermissionsReadModelResult.Available(organization));

        FolderPermissionEvidenceResult result = await new EffectivePermissionsFolderPermissionEvidenceProvider(
                model,
                new FixedUtcClock(Now))
            .GetEvidenceAsync(
                Request(operationScope: FolderStreamName.ReservedOrganizationPermissionScope, actionToken: "create_folder"),
                TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
    }

    private static FolderPermissionEvidenceRequest Request(
        string operationScope = "folder-a",
        bool allowBoundedStale = false,
        FolderOperationPolicyClass policyClass = FolderOperationPolicyClass.Mutation,
        string actionToken = "read_metadata")
        => new(
            ManagedTenantId: "tenant-a",
            PrincipalId: "user-a",
            ActorSafeIdentifier: "actor-user-a",
            ActionToken: actionToken,
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
