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

public sealed class WorkspaceStatusEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterWorkspaceStatusRoute()
    {
        using WebApplication app = BuildApp(StatusReadModel());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/status");
    }

    [Fact]
    public async Task GetWorkspaceStatusShouldReturnContractShapedAuthorizedStatus()
    {
        await using WebApplication app = BuildApp(StatusReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();
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
        root.GetProperty("currentState").GetString().ShouldBe("committed");
        root.GetProperty("acceptedCommandState").GetProperty("state").GetString().ShouldBe("completed");
        root.GetProperty("projectedState").GetProperty("state").GetString().ShouldBe("committed");
        root.GetProperty("providerOutcome").GetProperty("state").GetString().ShouldBe("known_success");
        root.GetProperty("retryEligibility").GetProperty("reasonCode").GetString().ShouldBe("retry_not_required");
        root.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("read_your_writes");
        json.ShouldNotContain("commit message", Case.Insensitive);
        json.ShouldNotContain("refs/heads", Case.Sensitive);
        json.ShouldNotContain("https://", Case.Sensitive);
    }

    [Theory]
    [InlineData("locked", "accepted", "known_success", false, "workspace_locked", null)]
    [InlineData("dirty", "accepted", "known_success", true, "dirty_workspace", null)]
    [InlineData("changes_staged", "accepted", "known_success", false, "retry_not_required", null)]
    [InlineData("failed", "failed", "known_failure", true, "failed_operation", "failed_operation")]
    [InlineData("inaccessible", "failed", "known_failure", false, "tenant_access_denied", "tenant_access_denied")]
    [InlineData("unknown_provider_outcome", "accepted", "unknown_provider_outcome", true, "unknown_provider_outcome", "unknown_provider_outcome")]
    [InlineData("reconciliation_required", "accepted", "reconciliation_required", true, "reconciliation_required", "reconciliation_required")]
    public async Task GetWorkspaceStatusShouldReturnContractShapeForWorkspaceStates(
        string state,
        string acceptedState,
        string providerState,
        bool retryEligible,
        string retryReason,
        string? lastFailureCategory)
    {
        await using WebApplication app = BuildApp(StatusReadModel(state));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        JsonElement root = document.RootElement;
        root.GetProperty("folderId").GetString().ShouldBe("folder-a");
        root.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        root.GetProperty("currentState").GetString().ShouldBe(state);
        root.GetProperty("acceptedCommandState").GetProperty("state").GetString().ShouldBe(acceptedState);
        root.GetProperty("projectedState").GetProperty("state").GetString().ShouldBe(state);
        root.GetProperty("providerOutcome").GetProperty("state").GetString().ShouldBe(providerState);
        root.GetProperty("retryEligibility").GetProperty("eligible").GetBoolean().ShouldBe(retryEligible);
        root.GetProperty("retryEligibility").GetProperty("reasonCode").GetString().ShouldBe(retryReason);
        if (lastFailureCategory is null)
        {
            if (root.TryGetProperty("lastFailureCategory", out JsonElement lastFailure))
            {
                lastFailure.ValueKind.ShouldBe(JsonValueKind.Null);
            }
        }
        else
        {
            root.GetProperty("lastFailureCategory").GetString().ShouldBe(lastFailureCategory);
        }

        json.ShouldNotContain("commit message", Case.Insensitive);
        json.ShouldNotContain("refs/heads", Case.Sensitive);
        json.ShouldNotContain("https://", Case.Sensitive);
    }

    [Theory]
    [InlineData("not_found", HttpStatusCode.NotFound, "not_found", "not_found", false)]
    [InlineData("projection_stale", HttpStatusCode.ServiceUnavailable, "projection_stale", "projection_stale", true)]
    [InlineData("projection_unavailable", HttpStatusCode.ServiceUnavailable, "projection_unavailable", "projection_unavailable", true)]
    [InlineData("malformed", HttpStatusCode.ServiceUnavailable, "read_model_unavailable", "read_model_unavailable", true)]
    public async Task GetWorkspaceStatusShouldMapSafeReadModelOutcomesToProblemDetails(
        string outcome,
        HttpStatusCode expectedStatus,
        string expectedCategory,
        string expectedCode,
        bool retryable)
    {
        await using WebApplication app = BuildApp(new FixedWorkspaceStatusReadModel(ReadModelOutcome(outcome)));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();

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
    public async Task GetWorkspaceStatusShouldRejectIdempotencyKeyBeforeReadModelAccess()
    {
        CountingWorkspaceStatusReadModel readModel = new(StatusReadModel());
        await using WebApplication app = BuildApp(readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceStatusShouldRejectUnsupportedFreshnessBeforeReadModelAccess()
    {
        CountingWorkspaceStatusReadModel readModel = new(StatusReadModel());
        await using WebApplication app = BuildApp(readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/status", "bad correlation", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/status", "correlation-a", "bad task")]
    [InlineData("/api/v1/folders/folder a/workspaces/workspace-a/status", "correlation-a", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace a/status", "correlation-a", "task-a")]
    public async Task GetWorkspaceStatusShouldRejectMalformedIdentifiersBeforeReadModelAccess(
        string requestUri,
        string correlationId,
        string taskId)
    {
        CountingWorkspaceStatusReadModel readModel = new(StatusReadModel());
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
    public async Task GetWorkspaceStatusShouldUseSafeDenialForUnauthenticatedCallerBeforeReadModelAccess()
    {
        CountingWorkspaceStatusReadModel readModel = new(StatusReadModel());
        await using WebApplication app = BuildApp(readModel, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateStatusRequest();

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
    public async Task GetWorkspaceStatusProblemDetailsAndSuccessShouldNotEmitLeakageCorpusValues()
    {
        string[] sentinels = LeakageCorpusValues();
        await using WebApplication app = BuildApp(StatusReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage success = await client.SendAsync(CreateStatusRequest(), TestContext.Current.CancellationToken);
        using HttpRequestMessage deniedRequest = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/status");
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
        IWorkspaceStatusReadModel statusReadModel,
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
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
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(PermissionReadModel());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IWorkspaceStatusReadModel>();
        builder.Services.AddSingleton(statusReadModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage CreateStatusRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/status");
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

    private static InMemoryEffectivePermissionsReadModel PermissionReadModel()
    {
        InMemoryEffectivePermissionsReadModel readModel = new();
        readModel.Save(new EffectivePermissionsReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows:
            [
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"), "read_workspace_status", Sequence: 1, EffectiveAt: Now),
            ],
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

    private static InMemoryWorkspaceStatusReadModel StatusReadModel(string state = "committed")
    {
        InMemoryWorkspaceStatusReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(StatusSnapshot(state));
        return readModel;
    }

    private static WorkspaceStatusReadModelResult ReadModelOutcome(string outcome)
        => outcome switch
        {
            "not_found" => WorkspaceStatusReadModelResult.NotFound(Freshness()),
            "projection_stale" => new WorkspaceStatusReadModelResult(
                WorkspaceStatusReadModelStatus.Stale,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_stale")),
            "projection_unavailable" => WorkspaceStatusReadModelResult.Unavailable("projection_unavailable", Now),
            "malformed" => new WorkspaceStatusReadModelResult(
                WorkspaceStatusReadModelStatus.Malformed,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_malformed")),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown workspace status read-model outcome."),
        };

    private static WorkspaceStatusReadModelSnapshot StatusSnapshot(string state)
    {
        WorkspaceStatusRetryEligibility retry = state switch
        {
            "dirty" => new(true, "dirty_workspace"),
            "failed" => new(true, "failed_operation"),
            "unknown_provider_outcome" => new(true, "unknown_provider_outcome"),
            "reconciliation_required" => new(true, "reconciliation_required"),
            "locked" => new(false, "workspace_locked"),
            "inaccessible" => new(false, "tenant_access_denied"),
            _ => new(false, "retry_not_required"),
        };
        string providerState = state switch
        {
            "failed" or "inaccessible" => "known_failure",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => "known_success",
        };
        string? failureCategory = state switch
        {
            "failed" => "failed_operation",
            "inaccessible" => "tenant_access_denied",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => null,
        };
        FolderLifecycleFreshness freshness = Freshness();

        return new WorkspaceStatusReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            CurrentState: state,
            AcceptedCommandState: new WorkspaceAcceptedCommandState(
                "task-a",
                "workspace_status_operation",
                state == "committed" ? "completed" : state is "failed" or "inaccessible" ? "failed" : "accepted",
                Now),
            ProjectedState: new WorkspaceProjectedState(state, "projection", Now),
            ProviderOutcome: new WorkspaceProviderOutcome(
                "workspace_status_operation",
                providerState,
                failureCategory ?? "success",
                "provref_workspace_status",
                retry,
                RetryAfter: null,
                Freshness: freshness),
            RetryEligibility: retry,
            RetryAfter: null,
            Freshness: freshness,
            ProjectionLag: new WorkspaceProjectionLag(0, "projection"),
            LastFailureCategory: failureCategory,
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: "tenant-a",
                PrincipalId: "user-a",
                ActionToken: "read_workspace_status",
                TaskId: "task-a",
                CorrelationId: "correlation-a",
                AuthorizationWatermark: "permission_watermark_v1"));
    }

    private static FolderLifecycleFreshness Freshness(
        bool stale = false,
        string? reasonCode = null)
        => new("read_your_writes", Now, "workspace_status_watermark_v1", stale, reasonCode);

    private sealed class FixedWorkspaceStatusReadModel(WorkspaceStatusReadModelResult result) : IWorkspaceStatusReadModel
    {
        public Task<WorkspaceStatusReadModelResult> GetAsync(
            WorkspaceStatusReadModelRequest request,
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

    private sealed class CountingWorkspaceStatusReadModel(IWorkspaceStatusReadModel inner) : IWorkspaceStatusReadModel
    {
        public int Calls { get; private set; }

        public Task<WorkspaceStatusReadModelResult> GetAsync(
            WorkspaceStatusReadModelRequest request,
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
