using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

/// <summary>
/// Negative-control tests proving the lifecycle-status handler does not fall back to
/// aggregate scans, provider calls, repository reads, filesystem reads, or audit queries
/// when its read model returns Unavailable, Stale, or Malformed. Story 2.7 spec
/// "Regression Traps" forbids such fallbacks; these tests pin that contract.
/// </summary>
public sealed class FolderLifecycleStatusNoFallbackTests
{
    [Fact]
    public async Task DoesNotFallbackToAggregateWhenLifecycleProjectionUnavailable()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(
            FolderLifecycleStatusReadModelResult.Unavailable("lifecycle_projection_unavailable", FolderLifecycleStatusTestSupport.Now));
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(
            FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark));
        RecordingEventStoreAuthorizationValidator validator = new(
            EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(
            DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));

        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(
            tenantStore,
            readModel,
            folderEvidence,
            validator,
            dapr);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        // Result must be a safe stale/unavailable outcome, not a permissive active default.
        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionUnavailable);
        result.LifecycleState.ShouldBeNull();
        result.FolderId.ShouldBeNull();

        // Handler must call the read model exactly once and never escalate to a second
        // probe (no aggregate scan, no follow-up read).
        readModel.Requests.ShouldBe(1);

        // Authorization seams should be touched exactly once for the layered decision —
        // not re-probed when the read model is unavailable.
        tenantStore.Gets.ShouldBe(1);
        folderEvidence.Requests.Count.ShouldBe(1);
        validator.Requests.Count.ShouldBe(1);
        dapr.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotCallProviderForBindingStatus()
    {
        // The handler must never reach out to provider, repository, filesystem, or audit
        // seams to compute binding status. This is asserted structurally: the handler's
        // constructor does not accept any such collaborator, so a query with a Bound
        // snapshot can be served without provider plumbing.
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(
            FolderLifecycleStatusReadModelResult.Available(FolderLifecycleStatusTestSupport.ActiveBound()));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);

        // Static structural assertion: the handler's constructor accepts only authorization,
        // read model, clock, and (optional) logger — no provider, repository, audit, or
        // filesystem seams that could be a fallback path.
        System.Reflection.ConstructorInfo[] constructors = typeof(FolderLifecycleStatusQueryHandler).GetConstructors();
        constructors.Length.ShouldBe(1);
        System.Type[] parameterTypes = constructors[0]
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();
        parameterTypes.ShouldContain(typeof(LayeredFolderAuthorizationService));
        parameterTypes.ShouldContain(typeof(IFolderLifecycleStatusReadModel));
        parameterTypes.ShouldContain(typeof(IUtcClock));
        parameterTypes
            .Select(t => t.FullName ?? string.Empty)
            .ShouldNotContain(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("EvidenceProvider", StringComparison.OrdinalIgnoreCase));
        parameterTypes
            .Select(t => t.FullName ?? string.Empty)
            .ShouldNotContain(name => name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        parameterTypes
            .Select(t => t.FullName ?? string.Empty)
            .ShouldNotContain(name => name.Contains("FileSystem", StringComparison.OrdinalIgnoreCase));
        parameterTypes
            .Select(t => t.FullName ?? string.Empty)
            .ShouldNotContain(name => name.Contains("Audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DoesNotCallReadModelTwiceWhenStaleProjectionReturned()
    {
        // Stale projections must not trigger a retry, projection repair, or compensating
        // read against the aggregate stream.
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveUnbound() with
        {
            Freshness = FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: "projection_stale"),
        };
        CountingLifecycleStatusReadModel readModel = new(
            FolderLifecycleStatusReadModelResult.Stale(snapshot));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.ProjectionStale);
        readModel.Requests.ShouldBe(1);
    }
}
