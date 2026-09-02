using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class FolderLifecycleStatusAuthorizationGateTests
{
    [Fact]
    public async Task MissingAuthenticationReturnsBeforeAuthorizationOrReadModelLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveUnbound()));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(tenantId: null),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.AuthenticationRequired);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.FolderId.ShouldBeNull();
        result.LifecycleState.ShouldBeNull();
        result.RepositoryBindingId.ShouldBeNull();
        result.ProviderBindingRef.ShouldBeNull();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("denied_safe");
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task RejectsBeforeFolderProjectionWhenTenantDenied()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-b"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveUnbound()));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.AuthorizationDenied);
        result.FolderId.ShouldBeNull();
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        tenantStore.Gets.ShouldBe(1);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task ClientTenantMismatchRejectsBeforeTenantProjectionLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveUnbound()));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(clientTenantValues: new Dictionary<string, string?>
            {
                ["header_hexalith_tenant_id"] = "tenant-b",
            }),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.AuthorizationDenied);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task RejectsBeforeBindingLookupWhenFolderAclDenied()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveBound()));
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(
            FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "acl_watermark_v1"));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(
            tenantStore,
            readModel,
            folderEvidence,
            validator,
            dapr);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.NotFoundSafe);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        readModel.Requests.ShouldBe(0);
        folderEvidence.Requests.Count.ShouldBe(1);
        validator.Requests.Count.ShouldBe(0);
        dapr.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AllowsReadModelOnlyAfterLayeredAuthorization()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveUnbound()));
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(
            FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(
            tenantStore,
            readModel,
            folderEvidence,
            validator,
            dapr);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.AuthorizationOutcome.ShouldBe("allowed");
        result.Freshness.ProjectionWatermark.ShouldBe(FolderLifecycleStatusTestSupport.LifecycleWatermark);
        tenantStore.Gets.ShouldBe(1);
        folderEvidence.Requests.Count.ShouldBe(1);
        validator.Requests.Count.ShouldBe(1);
        dapr.Requests.Count.ShouldBe(1);
        readModel.Requests.ShouldBe(1);
        readModel.LastRequest.ShouldNotBeNull();
        readModel.LastRequest.ManagedTenantId.ShouldBe("tenant-a");
        readModel.LastRequest.FolderId.ShouldBe("folder-a");
        readModel.LastRequest.PrincipalId.ShouldBe("user-a");
        readModel.LastRequest.ActionToken.ShouldBe("read_metadata");
        readModel.LastRequest.TaskId.ShouldBe("task-a");
        readModel.LastRequest.CorrelationId.ShouldBe("corr-a");
        readModel.LastRequest.AuthorizationWatermark.ShouldBe(FolderLifecycleStatusTestSupport.AuthorizationWatermark);
    }

    [Fact]
    public async Task ThrowingLifecycleReadModelReturnsMetadataOnlyUnavailableResult()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        ThrowingLifecycleStatusReadModel readModel = new();
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ReadModelUnavailable);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.FolderId.ShouldBeNull();
        result.LifecycleState.ShouldBeNull();
        result.RepositoryBindingId.ShouldBeNull();
        result.ProviderBindingRef.ShouldBeNull();
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("read_model_unavailable");
        tenantStore.Gets.ShouldBe(1);
        readModel.Requests.ShouldBe(1);
    }
}
