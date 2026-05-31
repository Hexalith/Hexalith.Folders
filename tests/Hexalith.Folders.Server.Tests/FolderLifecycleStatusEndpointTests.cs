using System.Net;
using System.Text.Json;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderLifecycleStatusEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore("tenant-a", "user-a"));
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(PermissionReadModel());
        builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(LifecycleReadModel());
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));
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

        routes.ShouldContain("/api/v1/folders/{folderId}/lifecycle-status");
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldReturnContractShapedActiveUnboundResponse()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? correlationHeaders).ShouldBeTrue();
        correlationHeaders.ShouldNotBeNull().ShouldContain("corr-a");
        document.RootElement.GetProperty("folderId").GetString().ShouldBe("folder-a");
        document.RootElement.GetProperty("lifecycleState").GetString().ShouldBe("ready");
        document.RootElement.GetProperty("archived").GetBoolean().ShouldBeFalse();
        document.RootElement.TryGetProperty("repositoryBindingId", out _).ShouldBeFalse();
        document.RootElement.TryGetProperty("providerBindingRef", out _).ShouldBeFalse();
        document.RootElement.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
        document.RootElement.GetProperty("freshness").GetProperty("stale").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldUseSafeDenialEnvelopeForTenantMismatch()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-secret-victim/lifecycle-status");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-secret-victim");
        request.Headers.Add("X-Correlation-Id", "corr-mismatch");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"clientAction\":\"no_action\"");
        json.ShouldContain("\"correlationId\":\"corr-mismatch\"");
        json.ShouldNotContain("folder-secret-victim", Case.Sensitive);
        json.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldEmit401ForUnauthenticatedCaller()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor(authoritativeTenantId: null, principalId: null),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        request.Headers.Add("X-Correlation-Id", "corr-anon");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldContain("\"code\":\"authentication_failure\"");
        json.ShouldContain("\"clientAction\":\"no_action\"");
        // Freshness/Correlation response headers must NOT leak on denial paths.
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldEmit404ForBlankFolderId()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        // ASP.NET route binding will reject a literal empty segment with 404 from routing.
        // Use a whitespace-only encoded segment to ensure our handler sees the blank id.
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/%20/lifecycle-status");
        request.Headers.Add("X-Correlation-Id", "corr-blank");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.ShouldContain("\"category\":\"not_found\"");
        json.ShouldContain("\"code\":\"not_found\"");
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldEmit400ForUnsupportedFreshnessClass()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        request.Headers.Add("X-Correlation-Id", "corr-freshness");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"category\":\"validation_error\"");
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldIgnorePrincipalQueryStringValue()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            LifecycleReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        // Attacker passes a different principal in the query string; endpoint must ignore it
        // and serve the legitimate principal from the authentication context.
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/api/v1/folders/folder-a/lifecycle-status?principalId=user-attacker");
        // Correlation must match the snapshot's EvidenceScope ("corr-a") to satisfy the
        // compatible-evidence-snapshot invariant; the focus of this test is the query-string
        // principal value, not correlation drift.
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        json.ShouldNotContain("user-attacker", Case.Sensitive);
    }

    [Fact]
    public async Task LifecycleStatusRouteShouldEmit503ForUnavailableReadModel()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            PermissionReadModel(),
            new ThrowingLifecycleStatusReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"),
            new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        request.Headers.Add("X-Correlation-Id", "corr-unavailable");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        json.ShouldContain("\"category\":\"read_model_unavailable\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldContain("\"clientAction\":\"retry\"");
        json.ShouldContain("\"correlationId\":\"corr-unavailable\"");
    }

    private static WebApplication BuildApp(
        IFolderTenantAccessProjectionStore tenantStore,
        IEffectivePermissionsReadModel permissionsReadModel,
        IFolderLifecycleStatusReadModel lifecycleReadModel,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddSingleton(tenantStore);
        builder.Services.AddSingleton(permissionsReadModel);
        builder.Services.AddSingleton(lifecycleReadModel);
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton(tenantContext);
        builder.Services.AddSingleton(claimTransformEvidence);
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static InMemoryFolderTenantAccessProjectionStore TenantStore(string tenantId, string principalId)
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
            ProjectionWatermark = $"{tenantId}:7",
            LastEventTimestamp = DateTimeOffset.UtcNow,
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

    private static InMemoryFolderLifecycleStatusReadModel LifecycleReadModel()
    {
        InMemoryFolderLifecycleStatusReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            LifecycleState: FolderLifecycleProjectionState.Active,
            BindingStatus: FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            Freshness: new FolderLifecycleFreshness(
                ReadConsistency: "eventually_consistent",
                ObservedAt: Now,
                ProjectionWatermark: "lifecycle_watermark_v1",
                Stale: false,
                ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: "tenant-a",
                PrincipalId: "user-a",
                ActionToken: "read_metadata",
                TaskId: null,
                CorrelationId: "corr-a",
                AuthorizationWatermark: "permission_watermark_v1"),
            DiagnosticSentinels: []));
        return readModel;
    }

    private sealed class ThrowingLifecycleStatusReadModel : IFolderLifecycleStatusReadModel
    {
        public Task<FolderLifecycleStatusReadModelResult> GetAsync(
            FolderLifecycleStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("raw unavailable diagnostic must not leak");
    }

    private sealed class StaticTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string tenantId, string principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
    }
}
