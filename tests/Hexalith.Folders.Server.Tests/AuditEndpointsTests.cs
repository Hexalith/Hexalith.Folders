using System.Net;
using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Server.Authentication;

using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Story 6.1 — REST endpoint conformance for the audit-family operations
/// (ListAuditTrail, GetAuditRecord, ListOperationTimeline, GetOperationTimelineEntry).
/// Covers AC #1/#4/#5/#6/#8/#9 in a single hermetic in-process host suite.
/// </summary>
public sealed class AuditEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterAllFourAuditRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));
        WebApplication app = builder.Build();

        app.MapFoldersServerEndpoints();

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/audit-trail");
        routes.ShouldContain("/api/v1/folders/{folderId}/audit-trail/{auditRecordId}");
        routes.ShouldContain("/api/v1/folders/{folderId}/operation-timeline");
        routes.ShouldContain("/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}");
    }

    [Fact]
    public async Task ListAuditTrailHappyPathShouldReturnCanonicalPageShape()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/audit-trail");
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-a");
        response.Headers.GetValues("X-Hexalith-Freshness").Single().ShouldBe("eventually_consistent");

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        root.GetProperty("entries").GetArrayLength().ShouldBe(1);
        root.GetProperty("retentionClass").GetString().ShouldBe(AuditTrailQueryHandler.RetentionClassToken);
        root.GetProperty("page").GetProperty("limit").GetInt32().ShouldBe(AuditTrailQueryHandler.DefaultLimit);
        root.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
    }

    [Fact]
    public async Task ListAuditTrailShouldClampLimitAtConfiguredMaximum()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail?limit=500",
            TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement page = document.RootElement.GetProperty("page");
        page.GetProperty("limit").GetInt32().ShouldBe(AuditTrailQueryHandler.MaxLimit);
        page.GetProperty("requestedLimit").GetInt32().ShouldBe(500);
    }

    [Fact]
    public async Task ListAuditTrailShouldRejectIdempotencyKeyWithCanonicalProblem()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/audit-trail");
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "corr-a");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
        body.ShouldContain("\"category\":\"validation_error\"");
    }

    [Fact]
    public async Task ListAuditTrailShouldRejectUnsupportedFreshness()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/audit-trail");
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"code\":\"unsupported_read_consistency\"");
    }

    [Fact]
    public async Task ListAuditTrailShouldRejectAnyNonNullFilterWithFilterNotYetSupported()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail?filter=actorreference%3Dactor-a",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"code\":\"filter_not_yet_supported\"");
        body.ShouldContain("\"todoRef\":\"C4\"");
    }

    [Fact]
    public async Task ListAuditTrailShouldRejectInvalidLimit()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail?limit=0",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"code\":\"invalid_pagination\"");
    }

    [Fact]
    public async Task ListAuditTrailShouldRejectTamperedCursor()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail?cursor=not_a_valid_cursor_shape",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"code\":\"cursor_tampered\"");
    }

    [Fact]
    public async Task ListAuditTrailUnauthenticatedShouldEmit401()
    {
        await using WebApplication app = BuildApp(
            tenantId: null,
            principalId: null,
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"authentication_failure\"");
        // No freshness/correlation headers leaked on denial paths.
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
    }

    [Fact]
    public async Task ListAuditTrailWithTenantMismatchShouldEmitSafeAuthorizationDenial()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-secret-victim/audit-trail");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-secret-victim");
        request.Headers.Add("X-Correlation-Id", "corr-mismatch");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Tenant authority comes from authentication context only. Path/header tenant hints don't
        // change the answer. The mismatched payload tenant triggers a safe-denial envelope.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
        body.ShouldNotContain("folder-secret-victim", Case.Sensitive);
    }

    [Fact]
    public async Task GetAuditRecordHappyPathShouldReturnRecordShape()
    {
        AuditRecordReadModelSnapshot snapshot = SeededAuditRecord();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditRecord: snapshot);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/folders/folder-a/audit-trail/{snapshot.Record.AuditRecordId}");
        request.Headers.Add("X-Correlation-Id", "corr-r");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using JsonDocument document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("auditRecordId").GetString().ShouldBe(snapshot.Record.AuditRecordId);
        document.RootElement.GetProperty("resultStatus").GetString().ShouldBe("success");
    }

    [Fact]
    public async Task GetAuditRecordUnknownIdShouldEmit404Safe()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail/opaque_unknown_record_001",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"not_found\"");
    }

    [Fact]
    public async Task ListOperationTimelineHappyPathShouldReturnPageShape()
    {
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            timeline: SeededOperationTimeline());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/operation-timeline",
            TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using JsonDocument document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("entries").GetArrayLength().ShouldBe(1);
        document.RootElement.GetProperty("retentionClass").GetString().ShouldBe(OperationTimelineQueryHandler.RetentionClassToken);
    }

    [Fact]
    public async Task GetOperationTimelineEntryHappyPathShouldReturnEntryShape()
    {
        OperationTimelineEntryReadModelSnapshot snapshot = SeededOperationTimelineEntry();
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            timelineEntry: snapshot);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/folders/folder-a/operation-timeline/{snapshot.Entry.TimelineEntryId}",
            TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using JsonDocument document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("timelineEntryId").GetString().ShouldBe(snapshot.Entry.TimelineEntryId);
    }

    [Fact]
    public async Task ReadModelExceptionShouldEmit503WithReadModelUnavailable()
    {
        await using WebApplication app = BuildAppWithThrowingTrail(
            tenantId: "tenant-a",
            principalId: "user-a");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/audit-trail");
        request.Headers.Add("X-Correlation-Id", "corr-unavailable");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"category\":\"read_model_unavailable\"");
        body.ShouldContain("\"retryable\":true");
        // No internal diagnostic detail leak.
        body.ShouldNotContain("InvalidOperationException", Case.Sensitive);
        body.ShouldNotContain("never_leak_internal_diagnostic", Case.Sensitive);
    }

    [Fact]
    public async Task UnauthenticatedListAuditTrailDoesNotConsultReadModel()
    {
        InMemoryAuditTrailReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(SeededAuditTrail());
        await using WebApplication app = BuildAppWithCustomTrail(
            tenantId: null,
            principalId: null,
            readModel: readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        readModel.GetCount.ShouldBe(0, "authorization-before-observation must short-circuit before any read-model call.");
    }

    [Fact]
    public async Task SentinelCorpusValuesMustNeverAppearInAnyAuditEndpointResponse()
    {
        // AC #8 — sweep the entire audit-leakage-corpus across every audit endpoint's response
        // bodies (success + Problem Details) and assert no raw sentinel substring appears.
        AuditTrailReadModelSnapshot trail = SeededAuditTrail();
        AuditRecordReadModelSnapshot record = SeededAuditRecord();
        OperationTimelineReadModelSnapshot timeline = SeededOperationTimeline();
        OperationTimelineEntryReadModelSnapshot timelineEntry = SeededOperationTimelineEntry();

        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: trail,
            auditRecord: record,
            timeline: timeline,
            timelineEntry: timelineEntry);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        string[] paths =
        [
            "/api/v1/folders/folder-a/audit-trail",
            $"/api/v1/folders/folder-a/audit-trail/{record.Record.AuditRecordId}",
            "/api/v1/folders/folder-a/operation-timeline",
            $"/api/v1/folders/folder-a/operation-timeline/{timelineEntry.Entry.TimelineEntryId}",
            "/api/v1/folders/folder-a/audit-trail?cursor=tampered",  // denial path
            "/api/v1/folders/folder-a/audit-trail?filter=anything",  // denial path
        ];

        string[] sentinels = LoadAuditLeakageSentinelValues();
        sentinels.Length.ShouldBeGreaterThan(5, "leak corpus must seed multiple sentinel categories");

        foreach (string path in paths)
        {
            using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            foreach (string sentinel in sentinels)
            {
                body.ShouldNotContain(
                    sentinel,
                    Case.Sensitive,
                    $"Sentinel '{sentinel}' from audit-leakage-corpus.json must never appear in the response body of {path}.");
            }
        }
    }

    [Fact]
    public async Task SingleAndListResponsesMustCarryByteForByteIdenticalRedactionState()
    {
        // AC #7 — the same audit record's redaction metadata must be byte-for-byte identical
        // between the list endpoint (entry from page.entries[0]) and the single endpoint.
        AuditRecordReadModelSnapshot recordSnapshot = SeededAuditRecord();
        AuditTrailReadModelSnapshot trailSnapshot = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: [recordSnapshot.Record],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: SuccessfulFreshness());

        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: trailSnapshot,
            auditRecord: recordSnapshot);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage listResponse = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);
        using HttpResponseMessage singleResponse = await client.GetAsync(
            $"/api/v1/folders/folder-a/audit-trail/{recordSnapshot.Record.AuditRecordId}",
            TestContext.Current.CancellationToken);

        string listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string singleBody = await singleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using JsonDocument listDoc = JsonDocument.Parse(listBody);
        using JsonDocument singleDoc = JsonDocument.Parse(singleBody);

        string listRedaction = listDoc.RootElement.GetProperty("entries")[0].GetProperty("redaction").GetRawText();
        string singleRedaction = singleDoc.RootElement.GetProperty("redaction").GetRawText();
        listRedaction.ShouldBe(singleRedaction);

        string listActor = listDoc.RootElement.GetProperty("entries")[0].GetProperty("actorReference").GetRawText();
        string singleActor = singleDoc.RootElement.GetProperty("actorReference").GetRawText();
        listActor.ShouldBe(singleActor);
    }

    [Theory]
    [InlineData("/api/v1/folders/folder-a/audit-trail", "audit")]
    [InlineData("/api/v1/folders/folder-a/audit-trail/opaque_audit_record_x_001", "audit")]
    [InlineData("/api/v1/folders/folder-a/operation-timeline", "timeline")]
    [InlineData("/api/v1/folders/folder-a/operation-timeline/opaque_timeline_entry_x_001", "timeline")]
    public async Task SafeDenialProblemDetailsMustCarryEndpointSpecificEvidenceSource(string path, string expectedEvidenceSource)
    {
        // AC #4 / Dev Notes — audit endpoints emit details.evidenceSource: "audit"; the operation-timeline
        // endpoints emit "timeline". Idempotency-Key rejection short-circuits in the envelope validator
        // before any read-model call, so the per-endpoint evidenceSource selection is observable here.
        await using WebApplication app = BuildApp(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditTrail: SeededAuditTrail(),
            auditRecord: SeededAuditRecord(),
            timeline: SeededOperationTimeline(),
            timelineEntry: SeededOperationTimelineEntry());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("details").GetProperty("evidenceSource").GetString().ShouldBe(expectedEvidenceSource);
    }

    private static string[] LoadAuditLeakageSentinelValues()
    {
        string corpusPath = ResolveCorpusPath();
        string json = File.ReadAllText(corpusPath);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(static element => element.GetProperty("value").GetString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value.Length >= 8)
            .ToArray();
    }

    private static string ResolveCorpusPath()
    {
        string candidate = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string maybe = Path.Combine(candidate, "tests", "fixtures", "audit-leakage-corpus.json");
            if (File.Exists(maybe))
            {
                return maybe;
            }

            DirectoryInfo? parent = Directory.GetParent(candidate);
            if (parent is null)
            {
                break;
            }

            candidate = parent.FullName;
        }

        throw new FileNotFoundException("audit-leakage-corpus.json not found relative to test base directory.");
    }

    private static WebApplication BuildApp(
        string? tenantId,
        string? principalId,
        AuditTrailReadModelSnapshot? auditTrail = null,
        AuditRecordReadModelSnapshot? auditRecord = null,
        OperationTimelineReadModelSnapshot? timeline = null,
        OperationTimelineEntryReadModelSnapshot? timelineEntry = null)
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
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(BuildTenantStore(tenantId, principalId));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));

        InMemoryAuditTrailReadModel trail = new(new FixedUtcClock(Now));
        if (auditTrail is not null)
        {
            trail.Save(auditTrail);
        }

        InMemoryAuditRecordReadModel record = new(new FixedUtcClock(Now));
        if (auditRecord is not null)
        {
            record.Save(auditRecord);
        }

        InMemoryOperationTimelineReadModel tl = new(new FixedUtcClock(Now));
        if (timeline is not null)
        {
            tl.Save(timeline);
        }

        InMemoryOperationTimelineEntryReadModel tle = new(new FixedUtcClock(Now));
        if (timelineEntry is not null)
        {
            tle.Save(timelineEntry);
        }

        builder.Services.RemoveAll<IAuditTrailReadModel>();
        builder.Services.AddSingleton<IAuditTrailReadModel>(trail);
        builder.Services.RemoveAll<IAuditRecordReadModel>();
        builder.Services.AddSingleton<IAuditRecordReadModel>(record);
        builder.Services.RemoveAll<IOperationTimelineReadModel>();
        builder.Services.AddSingleton<IOperationTimelineReadModel>(tl);
        builder.Services.RemoveAll<IOperationTimelineEntryReadModel>();
        builder.Services.AddSingleton<IOperationTimelineEntryReadModel>(tle);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static WebApplication BuildAppWithCustomTrail(
        string? tenantId,
        string? principalId,
        IAuditTrailReadModel readModel)
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
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(BuildTenantStore(tenantId, principalId));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<IAuditTrailReadModel>();
        builder.Services.AddSingleton(readModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static WebApplication BuildAppWithThrowingTrail(string tenantId, string principalId)
        => BuildAppWithCustomTrail(tenantId, principalId, new ThrowingAuditTrailReadModel());

    private static IFolderTenantAccessProjectionStore BuildTenantStore(string? tenantId, string? principalId)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(principalId))
        {
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
        }

        return store;
    }

    private static AuditFreshness SuccessfulFreshness()
        => new("eventually_consistent", Now, "audit_watermark_v1", Stale: false, ReasonCode: null);

    private static AuditTrailReadModelSnapshot SeededAuditTrail()
    {
        AuditRecord entry = BuildSampleRecord();
        return new AuditTrailReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: [entry],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: SuccessfulFreshness());
    }

    private static AuditRecordReadModelSnapshot SeededAuditRecord()
        => new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Record: BuildSampleRecord(),
            Freshness: SuccessfulFreshness());

    private static OperationTimelineReadModelSnapshot SeededOperationTimeline()
    {
        OperationTimelineEntry entry = BuildSampleTimelineEntry();
        return new OperationTimelineReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: [entry],
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: SuccessfulFreshness());
    }

    private static OperationTimelineEntryReadModelSnapshot SeededOperationTimelineEntry()
        => new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entry: BuildSampleTimelineEntry(),
            Freshness: SuccessfulFreshness());

    private static AuditRecord BuildSampleRecord()
    {
        RedactionMetadata visible = new(RedactionVisibility.MetadataOnly, "authorized");
        return new AuditRecord(
            AuditRecordId: "opaque_audit_record_synthetic_001",
            ActorReference: new RedactableAuditActorReference(
                DiagnosticFieldClassification.OperatorSanitized,
                visible,
                "actorref_synthetic_safe_actor_001"),
            OperationId: new RedactableAuditOperationReference(
                DiagnosticFieldClassification.OperatorSanitized,
                visible,
                "opaque_operation_synthetic_op_001"),
            CorrelationId: "opaque_correlation_synthetic_001",
            ResultStatus: "success",
            SanitizedErrorCategory: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: new RedactableAuditTimestamp(
                RedactableAuditTimestampPrecision.Exact,
                visible,
                Now),
            Redaction: visible,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "audit_watermark_v1", false, null),
            TaskId: "opaque_task_synthetic_001",
            ChangedPathEvidence: null);
    }

    private static OperationTimelineEntry BuildSampleTimelineEntry()
    {
        RedactionMetadata visible = new(RedactionVisibility.MetadataOnly, "authorized");
        return new OperationTimelineEntry(
            TimelineEntryId: "opaque_timeline_entry_synthetic_001",
            OperationId: "opaque_operation_synthetic_op_001",
            TaskId: "opaque_task_synthetic_001",
            CorrelationId: "opaque_correlation_synthetic_001",
            WorkspaceReference: new RedactableDiagnosticIdentifier(
                DiagnosticFieldClassification.OperatorSanitized,
                visible,
                "opaque_workspace_synthetic_001"),
            StateTransition: new DiagnosticStateTransition("ready", "locked", "available"),
            SanitizedResult: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: Now,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "timeline_watermark_v1", false, null));
    }

    private sealed class ThrowingAuditTrailReadModel : IAuditTrailReadModel
    {
        public Task<AuditTrailReadModelResult> GetAsync(
            AuditTrailReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("never_leak_internal_diagnostic");
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

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1"));
    }

    private sealed class AllowingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
    }
}
