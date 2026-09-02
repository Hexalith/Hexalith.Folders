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
        result.AuthorizationOutcome.ShouldBe("allowed");
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
    [InlineData("sdk")]
    [InlineData("cli")]
    [InlineData("mcp")]
    [InlineData("console")]
    public async Task ArchivedInaccessibleVocabularyShouldStayStableAcrossConsumerSurfaces(string consumerSurface)
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

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed, consumerSurface);
        result.LifecycleState.ShouldBe("inaccessible", consumerSurface);
        result.Archived.ShouldBeTrue(consumerSurface);

        string archiveParityRow = ArchiveFolderParityRow();
        archiveParityRow.ShouldContain("- 'sdk'");
        archiveParityRow.ShouldContain("- 'cli'");
        archiveParityRow.ShouldContain("- 'mcp'");
        archiveParityRow.ShouldContain("- 'rest'");
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
            FolderRepositoryBindingStatus.Unbound) with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_archive_reason"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ArchiveStateUnsupported);
        result.LifecycleState.ShouldBeNull();
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("projection_stale");
    }

    [Fact]
    public async Task AvailableSnapshotWithStaleFreshnessFailsClosedDuringCompatibilityValidation()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound() with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(
                stale: true,
                reasonCode: "source_snapshot_stale"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionStale);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("source_snapshot_stale");
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
            evidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(taskId: "different-task")) with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_task_scope_reason"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("task_mismatch");
    }

    [Fact]
    public async Task UnavailableProjectionStatusReturnsProjectionUnavailableResultCode()
    {
        FolderLifecycleStatusReadModelResult readModelResult = new(
            FolderLifecycleStatusReadModelStatus.Unavailable,
            Snapshot: null,
            FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_projection_unavailable"));
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(readModelResult).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("source_projection_unavailable");
    }

    [Fact]
    public async Task OuterUnavailableStatusPreservesReadContextWhileSuppressingWatermark()
    {
        DateTimeOffset sourceObservedAt = FolderLifecycleStatusTestSupport.Now.AddMinutes(-17);
        FolderLifecycleStatusReadModelResult readModelResult = new(
            FolderLifecycleStatusReadModelStatus.Unavailable,
            Snapshot: null,
            new FolderLifecycleFreshness(
                "snapshot_per_task",
                sourceObservedAt,
                "source_watermark_to_suppress",
                Stale: false,
                "source_outer_unavailable"));

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(readModelResult).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionUnavailable);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ReadConsistency.ShouldBe("snapshot_per_task");
        result.Freshness.ObservedAt.ShouldBe(sourceObservedAt);
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("source_outer_unavailable");
    }

    [Fact]
    public async Task MalformedReadModelStatusReturnsReadModelUnavailableResultCode()
    {
        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Malformed(FolderLifecycleStatusTestSupport.Freshness())).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.Freshness.ProjectionWatermark.ShouldBeNull();
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
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        readModel.Requests.ShouldBe(0);
        tenantStore.Gets.ShouldBe(0);
    }

    [Theory]
    [InlineData(FolderLifecycleStatusReadModelStatus.Available, FolderLifecycleStatusResultCode.ReadModelUnavailable, "projection_malformed")]
    [InlineData(FolderLifecycleStatusReadModelStatus.Stale, FolderLifecycleStatusResultCode.ProjectionStale, "source_specific_reason")]
    [InlineData(FolderLifecycleStatusReadModelStatus.Unavailable, FolderLifecycleStatusResultCode.ProjectionUnavailable, "source_specific_reason")]
    [InlineData(FolderLifecycleStatusReadModelStatus.Malformed, FolderLifecycleStatusResultCode.ReadModelUnavailable, "projection_malformed")]
    [InlineData(FolderLifecycleStatusReadModelStatus.NotFound, FolderLifecycleStatusResultCode.NotFoundSafe, "safe_not_found")]
    [InlineData((FolderLifecycleStatusReadModelStatus)int.MaxValue, FolderLifecycleStatusResultCode.ReadModelUnavailable, "read_model_status_unknown")]
    public async Task FailClosedReadModelStatusesSuppressWatermarkAndPreserveExactCode(
        FolderLifecycleStatusReadModelStatus status,
        FolderLifecycleStatusResultCode expectedCode,
        string expectedReason)
    {
        FolderLifecycleStatusReadModelResult readModelResult = new(
            status,
            Snapshot: null,
            FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_specific_reason"));

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(readModelResult).ConfigureAwait(true);

        result.Code.ShouldBe(expectedCode);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(FolderLifecycleProjectionState.Missing, FolderLifecycleStatusResultCode.NotFoundSafe, "safe_not_found")]
    [InlineData(FolderLifecycleProjectionState.Stale, FolderLifecycleStatusResultCode.ProjectionStale, "source_specific_reason")]
    [InlineData(FolderLifecycleProjectionState.Unavailable, FolderLifecycleStatusResultCode.ProjectionUnavailable, "source_specific_reason")]
    [InlineData(FolderLifecycleProjectionState.Malformed, FolderLifecycleStatusResultCode.ReadModelUnavailable, "lifecycle_malformed")]
    [InlineData(FolderLifecycleProjectionState.Unknown, FolderLifecycleStatusResultCode.ReadModelUnavailable, "lifecycle_state_unknown")]
    [InlineData((FolderLifecycleProjectionState)int.MaxValue, FolderLifecycleStatusResultCode.ReadModelUnavailable, "lifecycle_state_unknown")]
    public async Task FailClosedLifecycleStatesSuppressWatermarkAndPreserveExactCode(
        FolderLifecycleProjectionState state,
        FolderLifecycleStatusResultCode expectedCode,
        string expectedReason)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            state,
            FolderRepositoryBindingStatus.Unbound) with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_specific_reason"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(expectedCode);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData("repository_binding_opaque_safe", null)]
    [InlineData(null, "provider_binding_opaque_safe")]
    public async Task OneSidedUnboundBindingReferencesRemainMalformed(
        string? repositoryBindingId,
        string? providerBindingRef)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unbound,
            repositoryBindingId,
            providerBindingRef);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("unbound_binding_metadata_malformed");
    }

    [Theory]
    [InlineData(FolderLifecycleStatusReadModelStatus.Stale, FolderLifecycleStatusResultCode.ProjectionStale, "projection_stale")]
    [InlineData(FolderLifecycleStatusReadModelStatus.Unavailable, FolderLifecycleStatusResultCode.ProjectionUnavailable, "projection_unavailable")]
    public async Task GenericReadModelStatusesUseTheirOwnFallbackReasonWhenNoSourceReasonExists(
        FolderLifecycleStatusReadModelStatus status,
        FolderLifecycleStatusResultCode expectedCode,
        string expectedReason)
    {
        FolderLifecycleStatusReadModelResult readModelResult = new(
            status,
            Snapshot: null,
            FolderLifecycleStatusTestSupport.Freshness(reasonCode: null));

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(readModelResult).ConfigureAwait(true);

        result.Code.ShouldBe(expectedCode);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(FolderLifecycleProjectionState.Stale, FolderLifecycleStatusResultCode.ProjectionStale, "lifecycle_stale")]
    [InlineData(FolderLifecycleProjectionState.Unavailable, FolderLifecycleStatusResultCode.ProjectionUnavailable, "lifecycle_unavailable")]
    public async Task GenericLifecycleStatesUseTheirOwnFallbackReasonWhenNoSourceReasonExists(
        FolderLifecycleProjectionState state,
        FolderLifecycleStatusResultCode expectedCode,
        string expectedReason)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            state,
            FolderRepositoryBindingStatus.Unbound);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(expectedCode);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task StaleSnapshotWithoutSourceReasonUsesTheCompatibilityFallbackReason()
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound() with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: null),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionStale);
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_stale");
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData(null, "\t")]
    [InlineData(" ", "   ")]
    public async Task WhitespaceOnlyBindingReferencesCountAsAbsentForUnboundFolders(
        string? repositoryBindingId,
        string? providerBindingRef)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unbound,
            repositoryBindingId,
            providerBindingRef);

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.AuthorizationOutcome.ShouldBe("allowed");
        result.LifecycleState.ShouldBe("ready");
        result.RepositoryBindingId.ShouldBeNull();
        result.ProviderBindingRef.ShouldBeNull();
        result.Freshness.ProjectionWatermark.ShouldBe(FolderLifecycleStatusTestSupport.LifecycleWatermark);
    }

    [Theory]
    [InlineData(FolderLifecycleProjectionState.Active, FolderRepositoryBindingStatus.Bound, "repository_binding_opaque_001", null, "binding_metadata_malformed")]
    [InlineData(FolderLifecycleProjectionState.Active, FolderRepositoryBindingStatus.BindingRequested, null, "provider_binding_opaque_001", "binding_metadata_malformed")]
    [InlineData(FolderLifecycleProjectionState.Active, FolderRepositoryBindingStatus.Failed, "   ", "provider_binding_opaque_001", "binding_metadata_malformed")]
    [InlineData(FolderLifecycleProjectionState.Active, FolderRepositoryBindingStatus.Unsupported, null, null, "binding_state_unsupported")]
    [InlineData(FolderLifecycleProjectionState.Archived, FolderRepositoryBindingStatus.Bound, "repository_binding_opaque_001", " ", "binding_metadata_malformed")]
    [InlineData(FolderLifecycleProjectionState.Archived, FolderRepositoryBindingStatus.Failed, null, null, "archived_binding_state_unsupported")]
    public async Task BindingBranchFailuresOverrideSourceReasonAndSuppressWatermark(
        FolderLifecycleProjectionState lifecycleState,
        FolderRepositoryBindingStatus bindingStatus,
        string? repositoryBindingId,
        string? providerBindingRef,
        string expectedReason)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.Snapshot(
            "tenant-a",
            "folder-a",
            lifecycleState,
            bindingStatus,
            repositoryBindingId,
            providerBindingRef) with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(reasonCode: "source_specific_reason"),
        };

        FolderLifecycleStatusQueryResult result = await ExecuteAsync(
            FolderLifecycleStatusReadModelResult.Available(snapshot)).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.LifecycleState.ShouldBeNull();
        result.RepositoryBindingId.ShouldBeNull();
        result.ProviderBindingRef.ShouldBeNull();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
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

    private static string ArchiveFolderParityRow()
    {
        string path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "parity-contract.yaml");
        string content = File.ReadAllText(path);
        const string rowStart = "- operation_id: 'ArchiveFolder'";
        int start = content.IndexOf(rowStart, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        int next = content.IndexOf("\n- operation_id:", start + rowStart.Length, StringComparison.Ordinal);
        return next < 0 ? content[start..] : content[start..next];
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
