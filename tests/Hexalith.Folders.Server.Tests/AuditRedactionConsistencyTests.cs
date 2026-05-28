using System.Net;
using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Story 6.1 — AC #7. Sensitive-metadata classification is applied consistently:
/// the same record/entry returned via the single endpoint and via the list endpoint
/// must carry byte-for-byte identical redaction state on every field. Seeds a
/// 3-record corpus mixing visible / redacted records and proves the invariant
/// for both (GetAuditRecord, ListAuditTrail) and (GetOperationTimelineEntry, ListOperationTimeline).
/// </summary>
public sealed class AuditRedactionConsistencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly RedactionMetadata Visible = new(RedactionVisibility.MetadataOnly, "authorized");
    private static readonly RedactionMetadata Redacted = new(RedactionVisibility.Redacted, "classification_upgraded");

    [Fact]
    public async Task EveryAuditRecordRedactionStateIsByteForByteIdenticalBetweenListAndSingleEndpoints()
    {
        AuditRecord[] records =
        [
            BuildAuditRecord("opaque_audit_record_synthetic_001", Visible),
            BuildAuditRecord("opaque_audit_record_synthetic_002", Redacted),
            BuildAuditRecord("opaque_audit_record_synthetic_003", Visible),
        ];

        AuditTrailReadModelSnapshot trail = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: records,
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", false, null));

        await using WebApplication app = BuildApp(trail, records);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // List once. Compare every record in entries[] against the corresponding single-record response.
        using HttpResponseMessage listResponse = await client.GetAsync(
            "/api/v1/folders/folder-a/audit-trail",
            TestContext.Current.CancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument listDoc = JsonDocument.Parse(listBody);
        JsonElement entries = listDoc.RootElement.GetProperty("entries");
        entries.GetArrayLength().ShouldBe(records.Length);

        foreach (AuditRecord record in records)
        {
            using HttpResponseMessage singleResponse = await client.GetAsync(
                $"/api/v1/folders/folder-a/audit-trail/{record.AuditRecordId}",
                TestContext.Current.CancellationToken);
            singleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, record.AuditRecordId);
            string singleBody = await singleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using JsonDocument singleDoc = JsonDocument.Parse(singleBody);

            JsonElement listEntry = FindEntryById(entries, "auditRecordId", record.AuditRecordId);

            // Byte-for-byte identical redaction object.
            listEntry.GetProperty("redaction").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("redaction").GetRawText(), record.AuditRecordId);

            // Byte-for-byte identical actor/operation/timestamp redactable references.
            listEntry.GetProperty("actorReference").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("actorReference").GetRawText(), record.AuditRecordId);
            listEntry.GetProperty("operationId").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("operationId").GetRawText(), record.AuditRecordId);
            listEntry.GetProperty("evidenceTimestamp").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("evidenceTimestamp").GetRawText(), record.AuditRecordId);
        }
    }

    [Fact]
    public async Task EveryOperationTimelineEntryRedactionStateIsByteForByteIdenticalBetweenListAndSingleEndpoints()
    {
        OperationTimelineEntry[] entries =
        [
            BuildTimelineEntry("opaque_timeline_entry_synthetic_001", Visible),
            BuildTimelineEntry("opaque_timeline_entry_synthetic_002", Redacted),
            BuildTimelineEntry("opaque_timeline_entry_synthetic_003", Visible),
        ];

        OperationTimelineReadModelSnapshot timeline = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            Entries: entries,
            NextCursor: null,
            IsTruncated: false,
            TruncatedReason: null,
            Freshness: new AuditFreshness("eventually_consistent", Now, "timeline_watermark_v1", false, null));

        await using WebApplication app = BuildApp(timeline, entries);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpResponseMessage listResponse = await client.GetAsync(
            "/api/v1/folders/folder-a/operation-timeline",
            TestContext.Current.CancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument listDoc = JsonDocument.Parse(listBody);
        JsonElement listEntries = listDoc.RootElement.GetProperty("entries");
        listEntries.GetArrayLength().ShouldBe(entries.Length);

        foreach (OperationTimelineEntry entry in entries)
        {
            using HttpResponseMessage singleResponse = await client.GetAsync(
                $"/api/v1/folders/folder-a/operation-timeline/{entry.TimelineEntryId}",
                TestContext.Current.CancellationToken);
            singleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, entry.TimelineEntryId);
            string singleBody = await singleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using JsonDocument singleDoc = JsonDocument.Parse(singleBody);

            JsonElement listEntry = FindEntryById(listEntries, "timelineEntryId", entry.TimelineEntryId);

            listEntry.GetProperty("workspaceReference").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("workspaceReference").GetRawText(), entry.TimelineEntryId);
            listEntry.GetProperty("stateTransition").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("stateTransition").GetRawText(), entry.TimelineEntryId);
            listEntry.GetProperty("sanitizedResult").GetRawText()
                .ShouldBe(singleDoc.RootElement.GetProperty("sanitizedResult").GetRawText(), entry.TimelineEntryId);
        }
    }

    private static JsonElement FindEntryById(JsonElement entries, string idProperty, string id)
    {
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.GetProperty(idProperty).GetString() == id)
            {
                return entry;
            }
        }

        throw new InvalidOperationException($"Did not find {idProperty}={id} in list entries.");
    }

    private static AuditRecord BuildAuditRecord(string id, RedactionMetadata redaction)
        => new(
            AuditRecordId: id,
            ActorReference: new RedactableAuditActorReference(
                DiagnosticFieldClassification.OperatorSanitized,
                redaction,
                "actorref_" + id),
            OperationId: new RedactableAuditOperationReference(
                DiagnosticFieldClassification.OperatorSanitized,
                redaction,
                "opaque_op_for_" + id),
            CorrelationId: "opaque_correlation_" + id,
            ResultStatus: "success",
            SanitizedErrorCategory: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: new RedactableAuditTimestamp(
                RedactableAuditTimestampPrecision.Exact,
                redaction,
                Now),
            Redaction: redaction,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "audit_watermark_v1", false, null),
            TaskId: "opaque_task_for_" + id,
            ChangedPathEvidence: null);

    private static OperationTimelineEntry BuildTimelineEntry(string id, RedactionMetadata redaction)
        => new(
            TimelineEntryId: id,
            OperationId: "opaque_op_for_" + id,
            TaskId: "opaque_task_for_" + id,
            CorrelationId: "opaque_correlation_" + id,
            WorkspaceReference: new RedactableDiagnosticIdentifier(
                DiagnosticFieldClassification.OperatorSanitized,
                redaction,
                "opaque_ws_for_" + id),
            StateTransition: new DiagnosticStateTransition("ready", "locked", "available"),
            SanitizedResult: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: Now,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "timeline_watermark_v1", false, null));

    private static WebApplication BuildApp(AuditTrailReadModelSnapshot trail, AuditRecord[] records)
    {
        WebApplicationBuilder builder = BuildBaseBuilder();

        InMemoryAuditTrailReadModel trailReadModel = new(new FixedUtcClock(Now));
        trailReadModel.Save(trail);

        InMemoryAuditRecordReadModel recordReadModel = new(new FixedUtcClock(Now));
        foreach (AuditRecord record in records)
        {
            recordReadModel.Save(new AuditRecordReadModelSnapshot(
                ManagedTenantId: "tenant-a",
                FolderId: "folder-a",
                Record: record,
                Freshness: new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", false, null)));
        }

        builder.Services.RemoveAll<IAuditTrailReadModel>();
        builder.Services.AddSingleton<IAuditTrailReadModel>(trailReadModel);
        builder.Services.RemoveAll<IAuditRecordReadModel>();
        builder.Services.AddSingleton<IAuditRecordReadModel>(recordReadModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static WebApplication BuildApp(OperationTimelineReadModelSnapshot timeline, OperationTimelineEntry[] entries)
    {
        WebApplicationBuilder builder = BuildBaseBuilder();

        InMemoryOperationTimelineReadModel timelineReadModel = new(new FixedUtcClock(Now));
        timelineReadModel.Save(timeline);

        InMemoryOperationTimelineEntryReadModel entryReadModel = new(new FixedUtcClock(Now));
        foreach (OperationTimelineEntry entry in entries)
        {
            entryReadModel.Save(new OperationTimelineEntryReadModelSnapshot(
                ManagedTenantId: "tenant-a",
                FolderId: "folder-a",
                Entry: entry,
                Freshness: new AuditFreshness("eventually_consistent", Now, "timeline_watermark_v1", false, null)));
        }

        builder.Services.RemoveAll<IOperationTimelineReadModel>();
        builder.Services.AddSingleton<IOperationTimelineReadModel>(timelineReadModel);
        builder.Services.RemoveAll<IOperationTimelineEntryReadModel>();
        builder.Services.AddSingleton<IOperationTimelineEntryReadModel>(entryReadModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static WebApplicationBuilder BuildBaseBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(BuildTenantStore("tenant-a", "user-a"));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator>(new AllowingEventStoreAuthorizationValidator());
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        return builder;
    }

    private static IFolderTenantAccessProjectionStore BuildTenantStore(string tenantId, string principalId)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
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

    private sealed class StaticTenantContextAccessor(string tenantId, string principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string tenantId, string principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);
            return EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
        }
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1"));
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
