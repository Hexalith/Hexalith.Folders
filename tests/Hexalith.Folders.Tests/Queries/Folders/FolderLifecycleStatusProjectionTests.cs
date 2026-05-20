using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class FolderLifecycleStatusProjectionTests
{
    [Fact]
    public async Task ActiveUnboundFolderReturnsReadyMetadataOnlyStatus()
    {
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveUnbound())).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.FolderId.ShouldBe("folder-a");
        result.LifecycleState.ShouldBe("ready");
        result.Archived.ShouldBeFalse();
        result.RepositoryBindingId.ShouldBeNull();
        result.ProviderBindingRef.ShouldBeNull();
        result.Freshness.ReadConsistency.ShouldBe("eventually_consistent");
        result.Freshness.ProjectionWatermark.ShouldBe(FolderLifecycleStatusTestSupport.LifecycleWatermark);
        result.Freshness.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task ActiveBoundFolderReturnsOnlyOpaqueBindingMetadata()
    {
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveBound(
                "repository_binding_opaque_safe",
                "provider_binding_opaque_safe"))).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.LifecycleState.ShouldBe("ready");
        result.Archived.ShouldBeFalse();
        result.RepositoryBindingId.ShouldBe("repository_binding_opaque_safe");
        result.ProviderBindingRef.ShouldBe("provider_binding_opaque_safe");
    }

    [Fact]
    public async Task ArchivedFolderReturnsInaccessibleArchivedStatusWithFreshnessEvidence()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Archived,
            FolderRepositoryBindingStatus.Unbound,
            evidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(),
            diagnosticSentinels: []);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.LifecycleState.ShouldBe("inaccessible");
        result.Archived.ShouldBeTrue();
        result.FolderId.ShouldBe("folder-a");
        result.Freshness.ProjectionWatermark.ShouldBe(FolderLifecycleStatusTestSupport.LifecycleWatermark);
        result.CorrelationId.ShouldBe("corr-a");
        result.TaskId.ShouldBe("task-a");
    }

    [Theory]
    [InlineData(FolderRepositoryBindingStatus.BindingRequested, "requested")]
    [InlineData(FolderRepositoryBindingStatus.Failed, "failed")]
    [InlineData(FolderRepositoryBindingStatus.UnknownProviderOutcome, "unknown_provider_outcome")]
    [InlineData(FolderRepositoryBindingStatus.ReconciliationRequired, "reconciliation_required")]
    public async Task RecognizedBindingStatesMapToContractLifecycleVocabulary(
        FolderRepositoryBindingStatus bindingStatus,
        string expectedLifecycleState)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Active,
            bindingStatus,
            "repository_binding_opaque_safe",
            "provider_binding_opaque_safe");

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.LifecycleState.ShouldBe(expectedLifecycleState);
        result.RepositoryBindingId.ShouldBe("repository_binding_opaque_safe");
        result.ProviderBindingRef.ShouldBe("provider_binding_opaque_safe");
    }

    [Fact]
    public async Task ArchiveUnsupportedFailsClosedInsteadOfDefaultingActive()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.ArchiveUnsupported,
            FolderRepositoryBindingStatus.Unbound);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ArchiveStateUnsupported);
        result.LifecycleState.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("archive_state_unsupported");
    }

    [Fact]
    public async Task UnknownBindingStateFailsClosed()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unknown);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("binding_state_unknown");
    }

    [Fact]
    public async Task IncompatibleAuthorizationWatermarkFailsClosed()
    {
        FolderLifecycleEvidenceScope evidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(
            authorizationWatermark: "different_authorization_watermark");
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound(
            evidenceScope: evidenceScope);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("incompatible_authorization_watermark");
    }

    [Fact]
    public async Task StaleLifecycleProjectionFailsClosedWithFreshnessEvidence()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound() with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: "projection_stale"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Stale(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionStale);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_stale");
    }

    [Fact]
    public async Task SameFolderIdAcrossTenantsUsesTenantScopedLifecycleProjection()
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-a", "user-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-b", "user-b"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        InMemoryFolderLifecycleStatusReadModel readModel = new(new FixedUtcClock(DateTimeOffset.UtcNow));
        readModel.Save(FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "shared-folder-id",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Bound,
            "repository_binding_tenant_a",
            "provider_binding_tenant_a",
            FolderLifecycleStatusTestSupport.EvidenceScope("tenant-a", "user-a", correlationId: "corr-a")));
        readModel.Save(FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-b",
            "shared-folder-id",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Bound,
            "repository_binding_tenant_b",
            "provider_binding_tenant_b",
            FolderLifecycleStatusTestSupport.EvidenceScope("tenant-b", "user-b", correlationId: "corr-b")));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult tenantA = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query("shared-folder-id", "tenant-a", "user-a", correlationId: "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        FolderLifecycleStatusQueryResult tenantB = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(
                "shared-folder-id",
                "tenant-b",
                "user-b",
                correlationId: "corr-b",
                claimTransformEvidence: Hexalith.Folders.Authorization.EventStoreClaimTransformEvidence.Allowed(
                    "tenant-b",
                    "user-b",
                    ["read_metadata"])),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        tenantA.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        tenantB.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        tenantA.RepositoryBindingId.ShouldBe("repository_binding_tenant_a");
        tenantB.RepositoryBindingId.ShouldBe("repository_binding_tenant_b");
        tenantA.ProviderBindingRef.ShouldBe("provider_binding_tenant_a");
        tenantB.ProviderBindingRef.ShouldBe("provider_binding_tenant_b");
    }

    [Fact]
    public async Task MismatchedTaskEvidenceFailsClosedInsteadOfReusingCachedStatus()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound(
            evidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(taskId: "different-task"));

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("task_mismatch");
    }

    [Fact]
    public async Task UnavailableProjectionStatusReturnsProjectionUnavailableResultCode()
    {
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Unavailable("projection_unavailable", FolderLifecycleStatusTestSupport.Now)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_unavailable");
    }

    [Fact]
    public async Task MalformedReadModelStatusReturnsReadModelUnavailableResultCode()
    {
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Malformed(FolderLifecycleStatusTestSupport.Freshness())).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_malformed");
    }

    [Fact]
    public async Task StaleBindingProjectionPreservesProjectionStaleResultCode()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveBound() with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: "binding_projection_stale"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Stale(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionStale);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("binding_projection_stale");
    }

    [Fact]
    public async Task UnknownLifecycleStateLabelFailsClosed()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Unknown,
            FolderRepositoryBindingStatus.Unbound);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("lifecycle_state_unknown");
    }

    [Fact]
    public async Task ConflictingLifecycleAndBindingWatermarksFailClosed()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveBound() with
        {
            EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(authorizationWatermark: "binding_watermark_drift"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("incompatible_authorization_watermark");
    }

    [Fact]
    public async Task EvidenceScopeWithoutPrincipalFailsClosedBeforeSurfacingSnapshot()
    {
        FolderLifecycleEvidenceScope scope = new(
            ManagedTenantId: "tenant-a",
            PrincipalId: null,
            ActionToken: "read_metadata",
            TaskId: "task-a",
            CorrelationId: "corr-a",
            AuthorizationWatermark: FolderLifecycleStatusTestSupport.AuthorizationWatermark);
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound(evidenceScope: scope);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("evidence_principal_missing");
    }

    [Fact]
    public async Task BlankFolderIdReturnsNotFoundSafeWithoutAuthorizationLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(
            FolderLifecycleStatusReadModelResult.Available(FolderLifecycleStatusTestSupport.ActiveUnbound()));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(folderId: " "),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.NotFoundSafe);
        readModel.Requests.ShouldBe(0);
        tenantStore.Gets.ShouldBe(0);
    }

    private static async Task<FolderLifecycleStatusQueryResult> ExecuteAsync(FolderLifecycleStatusReadModelResult readModelResult)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(readModelResult);
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
