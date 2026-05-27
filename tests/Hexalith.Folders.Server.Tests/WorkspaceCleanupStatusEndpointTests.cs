using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class WorkspaceCleanupStatusEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterWorkspaceCleanupStatusRoute()
    {
        using WebApplication app = BuildApp(CleanupReadModel());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/cleanup/status");
    }

    [Theory]
    [InlineData("pending", "workspace_lifecycle_in_progress", false)]
    [InlineData("succeeded", "workspace_committed", false)]
    [InlineData("failed", "failed_operation", true)]
    [InlineData("status_only", "dirty_workspace", true)]
    public async Task GetWorkspaceCleanupStatusShouldReturnContractShapedAuthorizedStatus(
        string status,
        string reasonCode,
        bool retryEligible)
    {
        await using WebApplication app = BuildApp(CleanupReadModel(status, reasonCode, retryEligible));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Correlation-Id").ShouldContain("correlation-a");
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("read_your_writes");
        JsonElement root = document.RootElement;
        root.GetProperty("folderId").GetString().ShouldBe("folder-a");
        root.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        root.GetProperty("taskId").GetString().ShouldBe("task-a");
        root.GetProperty("status").GetString().ShouldBe(status);
        root.GetProperty("reasonCode").GetString().ShouldBe(reasonCode);
        root.GetProperty("retryEligibility").GetProperty("eligible").GetBoolean().ShouldBe(retryEligible);
        root.GetProperty("retryEligibility").GetProperty("advisoryOnly").GetBoolean().ShouldBeTrue();
        root.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("read_your_writes");
        root.GetProperty("correlationId").GetString().ShouldBe("correlation-a");
        json.ShouldNotContain("commit message", Case.Insensitive);
        json.ShouldNotContain("refs/heads", Case.Sensitive);
        json.ShouldNotContain("https://", Case.Sensitive);
    }

    [Theory]
    [InlineData("not_found", HttpStatusCode.NotFound, "not_found", "not_found", false)]
    [InlineData("projection_stale", HttpStatusCode.ServiceUnavailable, "projection_stale", "projection_stale", true)]
    [InlineData("projection_unavailable", HttpStatusCode.ServiceUnavailable, "projection_unavailable", "projection_unavailable", true)]
    [InlineData("malformed", HttpStatusCode.ServiceUnavailable, "read_model_unavailable", "read_model_unavailable", true)]
    public async Task GetWorkspaceCleanupStatusShouldMapSafeReadModelOutcomesToProblemDetails(
        string outcome,
        HttpStatusCode expectedStatus,
        string expectedCategory,
        string expectedCode,
        bool retryable)
    {
        await using WebApplication app = BuildApp(new FixedWorkspaceCleanupStatusReadModel(ReadModelOutcome(outcome)));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(expectedStatus, json);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
        JsonElement root = document.RootElement;
        root.GetProperty("category").GetString().ShouldBe(expectedCategory);
        root.GetProperty("code").GetString().ShouldBe(expectedCode);
        root.GetProperty("retryable").GetBoolean().ShouldBe(retryable);
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        json.ShouldNotContain("tenant-a", Case.Sensitive);
    }

    [Fact]
    public async Task GetWorkspaceCleanupStatusShouldRejectIdempotencyKeyBeforeReadModelAccess()
    {
        CountingWorkspaceCleanupStatusEndpointReadModel readModel = new(CleanupReadModel());
        await using WebApplication app = BuildApp(readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceCleanupStatusShouldRejectUnsupportedFreshnessBeforeReadModelAccess()
    {
        CountingWorkspaceCleanupStatusEndpointReadModel readModel = new(CleanupReadModel());
        await using WebApplication app = BuildApp(readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/cleanup/status", "bad correlation", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/cleanup/status", "correlation-a", "bad task")]
    [InlineData("/api/v1/folders/folder a/workspaces/workspace-a/cleanup/status", "correlation-a", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace a/cleanup/status", "correlation-a", "task-a")]
    public async Task GetWorkspaceCleanupStatusShouldRejectMalformedIdentifiersBeforeReadModelAccess(
        string requestUri,
        string correlationId,
        string taskId)
    {
        CountingWorkspaceCleanupStatusEndpointReadModel readModel = new(CleanupReadModel());
        await using WebApplication app = BuildApp(readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"validation_error\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceCleanupStatusShouldUseSafeDenialForUnauthenticatedCallerBeforeReadModelAccess()
    {
        CountingWorkspaceCleanupStatusEndpointReadModel readModel = new(CleanupReadModel());
        await using WebApplication app = BuildApp(readModel, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceCleanupStatusShouldUseSafeDenialForFolderAclDeniedBeforeReadModelAccess()
    {
        CountingWorkspaceCleanupStatusEndpointReadModel readModel = new(CleanupReadModel());
        await using WebApplication app = BuildApp(readModel, grantCleanupPermission: false);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        json.ShouldContain("\"category\":\"not_found_to_caller\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("wrong-workspace")]
    [InlineData("wrong-task")]
    [InlineData("wrong-correlation")]
    public async Task GetWorkspaceCleanupStatusShouldFailClosedForScopeMismatchedSnapshots(string mismatch)
    {
        WorkspaceCleanupStatusReadModelSnapshot snapshot = mismatch switch
        {
            "wrong-workspace" => CleanupSnapshot("status_only", "dirty_workspace", retryEligible: true, workspaceId: "workspace-b"),
            "wrong-task" => CleanupSnapshot("status_only", "dirty_workspace", retryEligible: true, taskId: "task-b"),
            "wrong-correlation" => CleanupSnapshot("status_only", "dirty_workspace", retryEligible: true, correlationId: "correlation-b"),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, "Unknown mismatch."),
        };
        await using WebApplication app = BuildApp(new FixedWorkspaceCleanupStatusReadModel(
            WorkspaceCleanupStatusReadModelResult.Available(snapshot)));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateCleanupStatusRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, json);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
        document.RootElement.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
        document.RootElement.GetProperty("code").GetString().ShouldBe("read_model_unavailable");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        json.ShouldNotContain("workspace-b", Case.Sensitive);
        json.ShouldNotContain("task-b", Case.Sensitive);
        json.ShouldNotContain("correlation-b", Case.Sensitive);
    }

    [Fact]
    public async Task GetWorkspaceCleanupStatusProblemDetailsAndSuccessShouldNotEmitLeakageCorpusValues()
    {
        string[] sentinels = LeakageCorpusValues();
        await using WebApplication app = BuildApp(CleanupReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage success = await client.SendAsync(CreateCleanupStatusRequest(), TestContext.Current.CancellationToken);
        using HttpRequestMessage deniedRequest = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/cleanup/status");
        deniedRequest.Headers.Add("Idempotency-Key", "idempotency-a");
        using HttpResponseMessage denied = await client.SendAsync(deniedRequest, TestContext.Current.CancellationToken);

        string combined = await success.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            + await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (string sentinel in sentinels)
        {
            combined.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    private static WebApplication BuildApp(
        IWorkspaceCleanupStatusReadModel cleanupReadModel,
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        bool grantCleanupPermission = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient, UnsupportedGatewayClient>();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(
            new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore());
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(PermissionReadModel(grantCleanupPermission));
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IWorkspaceCleanupStatusReadModel>();
        builder.Services.AddSingleton(cleanupReadModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage CreateCleanupStatusRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/cleanup/status");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        return request;
    }

    private static InMemoryFolderTenantAccessProjectionStore TenantStore()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private static InMemoryEffectivePermissionsReadModel PermissionReadModel(bool grantCleanupPermission = true)
    {
        InMemoryEffectivePermissionsReadModel readModel = new();
        readModel.Save(new EffectivePermissionsReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows: grantCleanupPermission
                ? [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"), "read_workspace_cleanup_status", Sequence: 1, EffectiveAt: Now)]
                : [],
            Freshness: new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: Now,
                ProjectionWatermark: "permission_watermark_v1",
                Stale: false,
                ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));
        return readModel;
    }

    private static InMemoryWorkspaceCleanupStatusReadModel CleanupReadModel(
        string status = "status_only",
        string reasonCode = "dirty_workspace",
        bool retryEligible = true)
    {
        InMemoryWorkspaceCleanupStatusReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(CleanupSnapshot(status, reasonCode, retryEligible));
        return readModel;
    }

    private static WorkspaceCleanupStatusReadModelResult ReadModelOutcome(string outcome)
        => outcome switch
        {
            "not_found" => WorkspaceCleanupStatusReadModelResult.NotFound(Freshness()),
            "projection_stale" => new WorkspaceCleanupStatusReadModelResult(
                WorkspaceCleanupStatusReadModelStatus.Stale,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_stale")),
            "projection_unavailable" => WorkspaceCleanupStatusReadModelResult.Unavailable("projection_unavailable", Now),
            "malformed" => new WorkspaceCleanupStatusReadModelResult(
                WorkspaceCleanupStatusReadModelStatus.Malformed,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_malformed")),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown cleanup status read-model outcome."),
        };

    private static WorkspaceCleanupStatusReadModelSnapshot CleanupSnapshot(
        string status,
        string reasonCode,
        bool retryEligible,
        string managedTenantId = "tenant-a",
        string folderId = "folder-a",
        string workspaceId = "workspace-a",
        string? taskId = "task-a",
        string? correlationId = "correlation-a")
        => new(
            ManagedTenantId: managedTenantId,
            FolderId: folderId,
            WorkspaceId: workspaceId,
            TaskId: taskId,
            Status: status,
            ReasonCode: reasonCode,
            RetryEligibility: new WorkspaceStatusRetryEligibility(retryEligible, reasonCode),
            Freshness: Freshness(),
            CorrelationId: correlationId,
            ObservedAt: Now,
            LastAttemptedAt: Now,
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: managedTenantId,
                PrincipalId: "user-a",
                ActionToken: "read_workspace_cleanup_status",
                TaskId: taskId,
                CorrelationId: correlationId,
                AuthorizationWatermark: "permission_watermark_v1"));

    private static FolderLifecycleFreshness Freshness(
        bool stale = false,
        string? reasonCode = null)
        => new("read_your_writes", Now, "cleanup_status_watermark_v1", stale, reasonCode);

    private sealed class FixedWorkspaceCleanupStatusReadModel(WorkspaceCleanupStatusReadModelResult result) : IWorkspaceCleanupStatusReadModel
    {
        public Task<WorkspaceCleanupStatusReadModelResult> GetAsync(
            WorkspaceCleanupStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private static string[] LeakageCorpusValues()
    {
        using FileStream stream = File.OpenRead(Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json"));
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(sample => sample.GetProperty("value").GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class StaticTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
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

    private sealed class CountingWorkspaceCleanupStatusEndpointReadModel(IWorkspaceCleanupStatusReadModel inner) : IWorkspaceCleanupStatusReadModel
    {
        public int Calls { get; private set; }

        public Task<WorkspaceCleanupStatusReadModelResult> GetAsync(
            WorkspaceCleanupStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return inner.GetAsync(request, cancellationToken);
        }
    }

    private sealed class UnsupportedGatewayClient : IEventStoreGatewayClient
    {
        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult> SubmitQueryAsync(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamReadPage> ReadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
