using System.Net;
using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class GetWorkspaceTransitionEvidenceEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterTransitionEvidenceRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();

        app.MapFoldersServerEndpoints();

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/transition-evidence");
    }

    [Fact]
    public async Task GetWorkspaceTransitionEvidenceShouldReturnContractShapedEvidence()
    {
        await using WebApplication app = BuildApp(EvidenceReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/transition-evidence");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("snapshot_per_task");
        document.RootElement.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        document.RootElement.GetProperty("currentState").GetString().ShouldBe("ready");
        JsonElement attempt = document.RootElement.GetProperty("attemptedTransition");
        attempt.GetProperty("fromState").GetString().ShouldBe("preparing");
        attempt.GetProperty("eventName").GetString().ShouldBe("WorkspacePrepared");
        attempt.GetProperty("toState").GetString().ShouldBe("ready");
        document.RootElement.GetProperty("result").GetString().ShouldBe("accepted");
        document.RootElement.GetProperty("reasonCode").GetString().ShouldBe("transition_allowed");
        document.RootElement.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("snapshot_per_task");
    }

    [Fact]
    public async Task GetWorkspaceTransitionEvidenceShouldRejectIdempotencyKey()
    {
        await using WebApplication app = BuildApp(EvidenceReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/transition-evidence");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
    }

    [Fact]
    public async Task GetWorkspaceTransitionEvidenceShouldRejectUnsupportedFreshness()
    {
        await using WebApplication app = BuildApp(EvidenceReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/transition-evidence");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
    }

    [Fact]
    public async Task GetWorkspaceTransitionEvidenceShouldReturnSafeNotFoundForUnknownWorkspace()
    {
        await using WebApplication app = BuildApp(EvidenceReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-unknown/transition-evidence");
        request.Headers.Add("X-Correlation-Id", "correlation-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        json.ShouldContain("\"category\":\"not_found\"");
    }

    [Fact]
    public async Task GetWorkspaceTransitionEvidenceShouldUseSafeDenialForUnauthenticatedCaller()
    {
        await using WebApplication app = BuildApp(EvidenceReadModel(), tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/transition-evidence");
        request.Headers.Add("X-Correlation-Id", "correlation-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("workspace-a", Case.Sensitive);
    }

    private static WebApplication BuildApp(
        InMemoryWorkspaceTransitionEvidenceReadModel readModel,
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore());
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(PermissionReadModel());
        builder.Services.AddSingleton<IWorkspaceTransitionEvidenceReadModel>(readModel);
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor(tenantId, principalId));
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
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
                new(
                    EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"),
                    "read_metadata",
                    Sequence: 1,
                    EffectiveAt: Now),
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

    private static InMemoryWorkspaceTransitionEvidenceReadModel EvidenceReadModel()
    {
        InMemoryWorkspaceTransitionEvidenceReadModel readModel = new();
        readModel.Save(new WorkspaceTransitionEvidenceSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            CurrentState: "ready",
            FromState: "preparing",
            EventName: "WorkspacePrepared",
            Result: "accepted",
            ReasonCode: "transition_allowed",
            EvidenceAt: Now,
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            LockEvidence: null,
            AuditMetadata: new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
            {
                ["workspace_lifecycle_updated_at"] = Now,
            },
            Freshness: new FolderLifecycleFreshness("snapshot_per_task", Now, "transition_watermark_v1", Stale: false, ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: "tenant-a",
                PrincipalId: "user-a",
                ActionToken: "read_metadata",
                TaskId: "task-a",
                CorrelationId: "correlation-a",
                AuthorizationWatermark: "permission_watermark_v1")));
        return readModel;
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
            => string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
    }

    private sealed class AllowingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
    }
}
