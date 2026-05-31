using System.Net;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Server.Authentication;

using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Story 6.1 — AC #9. Authorization-before-observation is verified, not assumed.
/// For every authorization-class denial, the read-model must NOT be consulted
/// (GetCount stays 0) and the response must surface the canonical safe-denial
/// category for that layer. Then we verify projection-stale / projection-unavailable
/// surface their canonical 409 / 503 responses once authorization passes.
/// </summary>
public sealed class AuditEndpointsAuthorizationOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAuditTrailWithTenantAccessDeniedMustNotConsultReadModel()
    {
        CountingAuditTrailReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: false,
            folderAclAllows: true,
            claimTransformPresent: true,
            auditTrailReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":");
        readModel.GetCount.ShouldBe(0, "read-model must not be observed before authorization succeeds (tenant_access_denied).");
    }

    [Fact]
    public async Task ListAuditTrailWithFolderAclDeniedMustNotConsultReadModel()
    {
        CountingAuditTrailReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: false,
            claimTransformPresent: true,
            auditTrailReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        readModel.GetCount.ShouldBe(0, "read-model must not be observed before folder ACL authorization succeeds.");
    }

    [Fact]
    public async Task ListAuditTrailWithMissingClaimTransformEvidenceMustNotConsultReadModel()
    {
        CountingAuditTrailReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: true,
            claimTransformPresent: false,
            auditTrailReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        // Missing claim transform short-circuits via the auth pipeline; the read-model is never reached.
        ((int)response.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
        readModel.GetCount.ShouldBe(0, "missing claim transform evidence must short-circuit before any read-model call.");
    }

    [Fact]
    public async Task GetAuditRecordWithFolderAclDeniedMustNotConsultReadModel()
    {
        CountingAuditRecordReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: false,
            claimTransformPresent: true,
            auditRecordReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail/opaque_audit_record_x_001",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        readModel.GetCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListOperationTimelineWithTenantAccessDeniedMustNotConsultReadModel()
    {
        CountingOperationTimelineReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: false,
            folderAclAllows: true,
            claimTransformPresent: true,
            timelineReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/operation-timeline",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        readModel.GetCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetOperationTimelineEntryWithFolderAclDeniedMustNotConsultReadModel()
    {
        CountingOperationTimelineEntryReadModel readModel = new();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: false,
            claimTransformPresent: true,
            timelineEntryReadModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/operation-timeline/opaque_timeline_entry_x_001",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        readModel.GetCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListAuditTrailWithStaleProjectionSurfacesProjectionStale409()
    {
        AuditTrailReadModelSnapshot snapshot = BuildEmptyTrailSnapshot();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: true,
            claimTransformPresent: true,
            auditTrailReadModel: new ScriptedAuditTrailReadModel(AuditTrailReadModelResult.Stale(snapshot)));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"projection_stale\"");
        body.ShouldContain("\"code\":\"projection_stale\"");
        body.ShouldContain("\"retryable\":true");
    }

    [Fact]
    public async Task ListAuditTrailWithUnavailableProjectionSurfacesProjectionUnavailable503()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: true,
            claimTransformPresent: true,
            auditTrailReadModel: new ScriptedAuditTrailReadModel(
                AuditTrailReadModelResult.Unavailable("projection_unavailable", Now)));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"projection_unavailable\"");
        body.ShouldContain("\"retryable\":true");
    }

    [Fact]
    public async Task ListAuditTrailReadModelExceptionLogsMetadataOnly()
    {
        // Read-model throws an exception carrying a sentinel string in its message.
        // The HTTP body must NOT leak the sentinel; the response must be the canonical
        // read_model_unavailable safe-denial.
        const string Sentinel = "never_leak_internal_diagnostic_path_C_credentials";
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            tenantAccessAllows: true,
            folderAclAllows: true,
            claimTransformPresent: true,
            auditTrailReadModel: new ThrowingTrailReadModel(Sentinel));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"read_model_unavailable\"");
        body.ShouldNotContain(Sentinel, Case.Sensitive, "exception message must never leak into the response body.");
        body.ShouldNotContain("InvalidOperationException", Case.Sensitive);
    }

    private static AuditTrailReadModelSnapshot BuildEmptyTrailSnapshot()
        => new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: [],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", Stale: true, ReasonCode: "projection_stale"));

    private static WebApplication BuildApp(
        string? tenantId,
        string? principalId,
        bool tenantAccessAllows,
        bool folderAclAllows,
        bool claimTransformPresent,
        IAuditTrailReadModel? auditTrailReadModel = null,
        IAuditRecordReadModel? auditRecordReadModel = null,
        IOperationTimelineReadModel? timelineReadModel = null,
        IOperationTimelineEntryReadModel? timelineEntryReadModel = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(
            claimTransformPresent
                ? new StaticClaimTransformEvidenceAccessor(tenantId, principalId)
                : new MissingClaimTransformEvidenceAccessor());
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(
            BuildTenantStore(tenantId, principalId, tenantAccessAllows));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(
            folderAclAllows
                ? new AllowingFolderPermissionEvidenceProvider()
                : new DenyingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator>(new AllowingEventStoreAuthorizationValidator());
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));

        if (auditTrailReadModel is not null)
        {
            builder.Services.RemoveAll<IAuditTrailReadModel>();
            builder.Services.AddSingleton(auditTrailReadModel);
        }

        if (auditRecordReadModel is not null)
        {
            builder.Services.RemoveAll<IAuditRecordReadModel>();
            builder.Services.AddSingleton(auditRecordReadModel);
        }

        if (timelineReadModel is not null)
        {
            builder.Services.RemoveAll<IOperationTimelineReadModel>();
            builder.Services.AddSingleton(timelineReadModel);
        }

        if (timelineEntryReadModel is not null)
        {
            builder.Services.RemoveAll<IOperationTimelineEntryReadModel>();
            builder.Services.AddSingleton(timelineEntryReadModel);
        }

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static IFolderTenantAccessProjectionStore BuildTenantStore(string? tenantId, string? principalId, bool allows)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        if (!allows || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId))
        {
            // Empty projection — TenantAccessAuthorizer denies on the absence of the tenant entry.
            return store;
        }

        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [principalId] = new(principalId, "Member"),
            },
            Watermark = 7,
            LastEventTimestamp = Now.AddMinutes(-1),
            ProjectionWatermark = $"{tenantId}:7",
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class CountingAuditTrailReadModel : IAuditTrailReadModel
    {
        private int _getCount;

        public int GetCount => _getCount;

        public Task<AuditTrailReadModelResult> GetAsync(
            AuditTrailReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCount);
            return Task.FromResult(AuditTrailReadModelResult.NotFound(
                AuditFreshness.SafeUnavailable(Now, "synthetic")));
        }
    }

    private sealed class CountingAuditRecordReadModel : IAuditRecordReadModel
    {
        private int _getCount;

        public int GetCount => _getCount;

        public Task<AuditRecordReadModelResult> GetAsync(
            AuditRecordReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCount);
            return Task.FromResult(AuditRecordReadModelResult.NotFound(
                AuditFreshness.SafeUnavailable(Now, "synthetic")));
        }
    }

    private sealed class CountingOperationTimelineReadModel : IOperationTimelineReadModel
    {
        private int _getCount;

        public int GetCount => _getCount;

        public Task<OperationTimelineReadModelResult> GetAsync(
            OperationTimelineReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCount);
            return Task.FromResult(OperationTimelineReadModelResult.NotFound(
                AuditFreshness.SafeUnavailable(Now, "synthetic")));
        }
    }

    private sealed class CountingOperationTimelineEntryReadModel : IOperationTimelineEntryReadModel
    {
        private int _getCount;

        public int GetCount => _getCount;

        public Task<OperationTimelineEntryReadModelResult> GetAsync(
            OperationTimelineEntryReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCount);
            return Task.FromResult(OperationTimelineEntryReadModelResult.NotFound(
                AuditFreshness.SafeUnavailable(Now, "synthetic")));
        }
    }

    private sealed class ScriptedAuditTrailReadModel(AuditTrailReadModelResult result) : IAuditTrailReadModel
    {
        public Task<AuditTrailReadModelResult> GetAsync(
            AuditTrailReadModelRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class ThrowingTrailReadModel(string sentinel) : IAuditTrailReadModel
    {
        public Task<AuditTrailReadModelResult> GetAsync(
            AuditTrailReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(sentinel);
    }

    private sealed class StaticTenantContextAccessor(string? tenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string? tenantId, string? principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);
            return string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
        }
    }

    private sealed class MissingClaimTransformEvidenceAccessor : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Missing();
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1"));
    }

    private sealed class DenyingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.FromStatus(
                FolderPermissionEvidenceStatus.Denied,
                "permission_watermark_v1"));
    }

    private sealed class AllowingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
    }

    private sealed class AllowingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
    }
}
