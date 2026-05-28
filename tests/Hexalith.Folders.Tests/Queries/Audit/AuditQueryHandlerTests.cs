using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Tests.Queries.Folders;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.Audit;

/// <summary>
/// Story 6.1 — per-handler unit tests for the audit-family query stack. Covers AC #4 / #9:
/// authorization-before-observation, safe-denial mapping, and read-model exception handling.
/// Mirrors the FolderLifecycleStatus handler test pattern.
/// </summary>
public sealed class AuditQueryHandlerTests
{
    // Reuse the lifecycle test-support clock so the canned TenantProjection() helper
    // (LastEventTimestamp = supportNow.AddMinutes(-1)) is not considered stale by the
    // TenantAccessAuthorizer freshness check.
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Fact]
    public async Task AuditTrailHandlerRejectsBeforeReadModelWhenTenantDenied()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-b"]));
        CountingAuditTrailReadModel readModel = new(AuditTrailReadModelResult.Available(SeededTrail()));
        AuditTrailQueryHandler handler = TrailHandler(tenantStore, readModel);

        AuditTrailQueryResult result = await handler.HandleAsync(
            BuildTrailQuery(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBeOneOf(
            AuditQueryResultCode.TenantAccessDenied,
            AuditQueryResultCode.AuditAccessDenied,
            AuditQueryResultCode.FolderAclDenied,
            AuditQueryResultCode.NotFoundSafe);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task AuditTrailHandlerAllowsReadModelOnlyAfterLayeredAuthorization()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingAuditTrailReadModel readModel = new(AuditTrailReadModelResult.Available(SeededTrail()));
        AuditTrailQueryHandler handler = TrailHandler(tenantStore, readModel);

        AuditTrailQueryResult result = await handler.HandleAsync(
            BuildTrailQuery(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.Allowed);
        result.Page.ShouldNotBeNull();
        result.Page.RetentionClass.ShouldBe(AuditTrailQueryHandler.RetentionClassToken);
        readModel.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task AuditTrailHandlerEmitsReadModelUnavailableOnException()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        ThrowingAuditTrailReadModel readModel = new();
        AuditTrailQueryHandler handler = TrailHandler(tenantStore, readModel);

        AuditTrailQueryResult result = await handler.HandleAsync(
            BuildTrailQuery(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.ReadModelUnavailable);
    }

    [Fact]
    public async Task AuditTrailHandlerEmitsAuthenticationRequiredWithoutTenant()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingAuditTrailReadModel readModel = new(AuditTrailReadModelResult.Available(SeededTrail()));
        AuditTrailQueryHandler handler = TrailHandler(tenantStore, readModel);

        AuditTrailQueryResult result = await handler.HandleAsync(
            BuildTrailQuery(tenantId: null, principalId: null),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.AuthenticationRequired);
        readModel.Requests.ShouldBe(0);
        tenantStore.Gets.ShouldBe(0);
    }

    [Fact]
    public async Task AuditRecordHandlerEmitsNotFoundSafeForBlankRecordId()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingAuditRecordReadModel readModel = new(AuditRecordReadModelResult.NotFound(
            AuditFreshness.SafeUnavailable(Now, "missing")));
        AuditRecordQueryHandler handler = RecordHandler(tenantStore, readModel);

        AuditRecordQueryResult result = await handler.HandleAsync(
            new AuditRecordQuery(
                "folder-a",
                AuditRecordId: " ",
                "tenant-a",
                "user-a",
                EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_metadata"]),
                "corr-a",
                "task-a",
                ClientControlledTenantValues: null,
                ClientControlledPrincipalValues: null),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.NotFoundSafe);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task OperationTimelineHandlerEmitsProjectionStaleWhenReadModelStale()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        OperationTimelineReadModelSnapshot snapshot = SeededTimeline();
        CountingOperationTimelineReadModel readModel = new(
            OperationTimelineReadModelResult.Stale(snapshot));
        OperationTimelineQueryHandler handler = TimelineHandler(tenantStore, readModel);

        OperationTimelineQueryResult result = await handler.HandleAsync(
            BuildTimelineQuery(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.ProjectionStale);
        readModel.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task OperationTimelineEntryHandlerEmitsNotFoundSafeWhenProjectionMissing()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingOperationTimelineEntryReadModel readModel = new(
            OperationTimelineEntryReadModelResult.NotFound(AuditFreshness.SafeUnavailable(Now, "missing")));
        OperationTimelineEntryQueryHandler handler = TimelineEntryHandler(tenantStore, readModel);

        OperationTimelineEntryQueryResult result = await handler.HandleAsync(
            new OperationTimelineEntryQuery(
                "folder-a",
                "opaque_timeline_entry_synthetic_001",
                "tenant-a",
                "user-a",
                EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_metadata"]),
                "corr-a",
                "task-a",
                ClientControlledTenantValues: null,
                ClientControlledPrincipalValues: null),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(AuditQueryResultCode.NotFoundSafe);
        readModel.Requests.ShouldBe(1);
    }

    private static AuditTrailQueryHandler TrailHandler(
        CountingTenantAccessProjectionStore tenantStore,
        IAuditTrailReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = BuildAuthorization(tenantStore);
        return new AuditTrailQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static AuditRecordQueryHandler RecordHandler(
        CountingTenantAccessProjectionStore tenantStore,
        IAuditRecordReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = BuildAuthorization(tenantStore);
        return new AuditRecordQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static OperationTimelineQueryHandler TimelineHandler(
        CountingTenantAccessProjectionStore tenantStore,
        IOperationTimelineReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = BuildAuthorization(tenantStore);
        return new OperationTimelineQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static OperationTimelineEntryQueryHandler TimelineEntryHandler(
        CountingTenantAccessProjectionStore tenantStore,
        IOperationTimelineEntryReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = BuildAuthorization(tenantStore);
        return new OperationTimelineEntryQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static LayeredFolderAuthorizationService BuildAuthorization(CountingTenantAccessProjectionStore tenantStore)
        => new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed("auth_watermark_v1")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            new FixedUtcClock(Now));

    private static AuditTrailQuery BuildTrailQuery(
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
        => new(
            FolderId: "folder-a",
            AuthoritativeTenantId: tenantId,
            PrincipalId: principalId,
            ClaimTransformEvidence: EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, ["read_metadata"]),
            CorrelationId: "corr-a",
            TaskId: "task-a",
            Cursor: null,
            RequestedLimit: null,
            Filter: null,
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static OperationTimelineQuery BuildTimelineQuery()
        => new(
            FolderId: "folder-a",
            AuthoritativeTenantId: "tenant-a",
            PrincipalId: "user-a",
            ClaimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_metadata"]),
            CorrelationId: "corr-a",
            TaskId: "task-a",
            Cursor: null,
            RequestedLimit: null,
            Filter: null,
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static AuditTrailReadModelSnapshot SeededTrail()
    {
        RedactionMetadata visible = new(RedactionVisibility.MetadataOnly, "authorized");
        AuditRecord record = new(
            AuditRecordId: "opaque_audit_record_synthetic_001",
            ActorReference: new RedactableAuditActorReference(DiagnosticFieldClassification.OperatorSanitized, visible, "actorref_synthetic_001"),
            OperationId: new RedactableAuditOperationReference(DiagnosticFieldClassification.OperatorSanitized, visible, "opaque_op_001"),
            CorrelationId: "opaque_correlation_001",
            ResultStatus: "success",
            SanitizedErrorCategory: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: new RedactableAuditTimestamp(RedactableAuditTimestampPrecision.Exact, visible, Now),
            Redaction: visible,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "audit_watermark_v1", false, null),
            TaskId: "opaque_task_001",
            ChangedPathEvidence: null);
        return new AuditTrailReadModelSnapshot(
            "tenant-a",
            "folder-a",
            [record],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", false, null));
    }

    private static OperationTimelineReadModelSnapshot SeededTimeline()
    {
        RedactionMetadata visible = new(RedactionVisibility.MetadataOnly, "authorized");
        OperationTimelineEntry entry = new(
            TimelineEntryId: "opaque_timeline_entry_001",
            OperationId: "opaque_operation_001",
            TaskId: "opaque_task_001",
            CorrelationId: "opaque_correlation_001",
            WorkspaceReference: new RedactableDiagnosticIdentifier(DiagnosticFieldClassification.OperatorSanitized, visible, "opaque_ws_001"),
            StateTransition: new DiagnosticStateTransition("ready", "locked", "available"),
            SanitizedResult: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: Now,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "timeline_watermark_v1", false, null));
        return new OperationTimelineReadModelSnapshot(
            "tenant-a",
            "folder-a",
            [entry],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: new AuditFreshness("eventually_consistent", Now, "timeline_watermark_v1", false, null));
    }
}

internal sealed class CountingAuditTrailReadModel(AuditTrailReadModelResult result) : IAuditTrailReadModel
{
    public int Requests { get; private set; }

    public Task<AuditTrailReadModelResult> GetAsync(
        AuditTrailReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}

internal sealed class ThrowingAuditTrailReadModel : IAuditTrailReadModel
{
    public Task<AuditTrailReadModelResult> GetAsync(
        AuditTrailReadModelRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("synthetic test exception");
}

internal sealed class CountingAuditRecordReadModel(AuditRecordReadModelResult result) : IAuditRecordReadModel
{
    public int Requests { get; private set; }

    public Task<AuditRecordReadModelResult> GetAsync(
        AuditRecordReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}

internal sealed class CountingOperationTimelineReadModel(OperationTimelineReadModelResult result) : IOperationTimelineReadModel
{
    public int Requests { get; private set; }

    public Task<OperationTimelineReadModelResult> GetAsync(
        OperationTimelineReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}

internal sealed class CountingOperationTimelineEntryReadModel(OperationTimelineEntryReadModelResult result) : IOperationTimelineEntryReadModel
{
    public int Requests { get; private set; }

    public Task<OperationTimelineEntryReadModelResult> GetAsync(
        OperationTimelineEntryReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}
