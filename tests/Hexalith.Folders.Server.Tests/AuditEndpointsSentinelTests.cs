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
/// Story 6.1 — AC #8. Metadata-only invariant is sentinel-swept across every audit-family
/// response channel (success + Problem Details + response headers) over the full
/// audit-leakage-corpus.json corpus. Also verifies the safety-channel inventory enrolls
/// every audit-family operation so no surface gets a free pass.
/// </summary>
public sealed class AuditEndpointsSentinelTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> AuditEndpointPaths()
    {
        TheoryData<string> data = new()
        {
            // Success paths
            "/api/v1/folders/folder-a/audit-trail",
            "/api/v1/folders/folder-a/audit-trail/opaque_audit_record_synthetic_001",
            "/api/v1/folders/folder-a/operation-timeline",
            "/api/v1/folders/folder-a/operation-timeline/opaque_timeline_entry_synthetic_001",
            // Validation-denial Problem Details surfaces
            "/api/v1/folders/folder-a/audit-trail?cursor=tampered",
            "/api/v1/folders/folder-a/audit-trail?filter=anything",
            "/api/v1/folders/folder-a/audit-trail?limit=0",
            "/api/v1/folders/folder-a/operation-timeline?cursor=tampered",
            "/api/v1/folders/folder-a/operation-timeline?filter=anything",
            "/api/v1/folders/folder-a/operation-timeline?limit=0",
            // Not-found Problem Details surfaces
            "/api/v1/folders/folder-a/audit-trail/opaque_unknown_record_001",
            "/api/v1/folders/folder-a/operation-timeline/opaque_unknown_entry_001",
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(AuditEndpointPaths))]
    public async Task EveryAuditEndpointResponseChannelMustBeSentinelClean(string path)
    {
        string[] sentinels = LoadSentinelSamples();
        sentinels.Length.ShouldBeGreaterThan(5, "leak corpus must seed multiple sentinel categories.");

        await using WebApplication app = BuildApp();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "opaque_correlation_sweep_001");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        foreach (string sentinel in sentinels)
        {
            body.ShouldNotContain(
                sentinel,
                Case.Sensitive,
                $"Sentinel '{Truncate(sentinel)}' from audit-leakage-corpus.json must never appear in body of {path}.");

            // Also sweep response headers (status line + every header value).
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers.Concat(response.Content.Headers))
            {
                foreach (string value in header.Value)
                {
                    value.ShouldNotContain(
                        sentinel,
                        Case.Sensitive,
                        $"Sentinel '{Truncate(sentinel)}' leaked in {header.Key} header on {path}.");
                }
            }
        }
    }

    [Fact]
    public void SafetyChannelInventoryEnrollsEveryAuditFamilyOperation()
    {
        // Concern #6 — no surface gets a free pass to skip the sentinel sweep. The inventory
        // must enumerate all four audit-family operations as covered channels.
        string inventoryPath = ResolveFixture("safety-channel-inventory.json");
        string json = File.ReadAllText(inventoryPath);
        using JsonDocument document = JsonDocument.Parse(json);

        string serialized = document.RootElement.GetRawText();
        serialized.ShouldContain("ListAuditTrail");
        serialized.ShouldContain("GetAuditRecord");
        serialized.ShouldContain("ListOperationTimeline");
        serialized.ShouldContain("GetOperationTimelineEntry");
    }

    private static string Truncate(string value)
        => value.Length <= 32 ? value : value[..32] + "…";

    private static string[] LoadSentinelSamples()
    {
        string corpusPath = ResolveFixture("audit-leakage-corpus.json");
        string json = File.ReadAllText(corpusPath);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(static element => element.GetProperty("value").GetString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value.Length >= 8)
            .ToArray();
    }

    private static string ResolveFixture(string fileName)
    {
        string candidate = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string maybe = Path.Combine(candidate, "tests", "fixtures", fileName);
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

        throw new FileNotFoundException($"{fileName} not found relative to test base directory.");
    }

    private static WebApplication BuildApp()
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

        RedactionMetadata visible = new(RedactionVisibility.MetadataOnly, "authorized");

        InMemoryAuditTrailReadModel trail = new(new FixedUtcClock(Now));
        trail.Save(new AuditTrailReadModelSnapshot(
            "tenant-a",
            "folder-a",
            [BuildAuditRecord("opaque_audit_record_synthetic_001", visible)],
            null,
            false,
            null,
            new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", false, null)));
        builder.Services.RemoveAll<IAuditTrailReadModel>();
        builder.Services.AddSingleton<IAuditTrailReadModel>(trail);

        InMemoryAuditRecordReadModel record = new(new FixedUtcClock(Now));
        record.Save(new AuditRecordReadModelSnapshot(
            "tenant-a",
            "folder-a",
            BuildAuditRecord("opaque_audit_record_synthetic_001", visible),
            new AuditFreshness("eventually_consistent", Now, "audit_watermark_v1", false, null)));
        builder.Services.RemoveAll<IAuditRecordReadModel>();
        builder.Services.AddSingleton<IAuditRecordReadModel>(record);

        InMemoryOperationTimelineReadModel tl = new(new FixedUtcClock(Now));
        tl.Save(new OperationTimelineReadModelSnapshot(
            "tenant-a",
            "folder-a",
            [BuildTimelineEntry("opaque_timeline_entry_synthetic_001", visible)],
            null,
            false,
            null,
            new AuditFreshness("eventually_consistent", Now, "timeline_watermark_v1", false, null)));
        builder.Services.RemoveAll<IOperationTimelineReadModel>();
        builder.Services.AddSingleton<IOperationTimelineReadModel>(tl);

        InMemoryOperationTimelineEntryReadModel tle = new(new FixedUtcClock(Now));
        tle.Save(new OperationTimelineEntryReadModelSnapshot(
            "tenant-a",
            "folder-a",
            BuildTimelineEntry("opaque_timeline_entry_synthetic_001", visible),
            new AuditFreshness("eventually_consistent", Now, "timeline_watermark_v1", false, null)));
        builder.Services.RemoveAll<IOperationTimelineEntryReadModel>();
        builder.Services.AddSingleton<IOperationTimelineEntryReadModel>(tle);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
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

    private static AuditRecord BuildAuditRecord(string id, RedactionMetadata redaction)
        => new(
            AuditRecordId: id,
            ActorReference: new RedactableAuditActorReference(DiagnosticFieldClassification.OperatorSanitized, redaction, "actorref_" + id),
            OperationId: new RedactableAuditOperationReference(DiagnosticFieldClassification.OperatorSanitized, redaction, "opaque_op_for_" + id),
            CorrelationId: "opaque_correlation_" + id,
            ResultStatus: "success",
            SanitizedErrorCategory: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: new RedactableAuditTimestamp(RedactableAuditTimestampPrecision.Exact, redaction, Now),
            Redaction: redaction,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "audit_watermark_v1", false, null),
            TaskId: "opaque_task_" + id,
            ChangedPathEvidence: null);

    private static OperationTimelineEntry BuildTimelineEntry(string id, RedactionMetadata redaction)
        => new(
            TimelineEntryId: id,
            OperationId: "opaque_op_for_" + id,
            TaskId: "opaque_task_for_" + id,
            CorrelationId: "opaque_correlation_" + id,
            WorkspaceReference: new RedactableDiagnosticIdentifier(DiagnosticFieldClassification.OperatorSanitized, redaction, "opaque_ws_for_" + id),
            StateTransition: new DiagnosticStateTransition("ready", "locked", "available"),
            SanitizedResult: "success",
            Retryable: false,
            DurationMilliseconds: 42,
            EvidenceTimestamp: Now,
            Freshness: new FreshnessMetadata("eventually_consistent", Now, "timeline_watermark_v1", false, null));

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
