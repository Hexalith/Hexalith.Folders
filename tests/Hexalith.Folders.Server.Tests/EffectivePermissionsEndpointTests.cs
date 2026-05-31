using System.Net;
using System.Text.Json;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Server.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class EffectivePermissionsEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterEffectivePermissionsRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore("tenant-a", "user-a"));
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(ReadModel());
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));
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

        routes.ShouldContain("/api/v1/folders/{folderId}/effective-permissions");
    }

    [Fact]
    public async Task EffectivePermissionsRouteShouldReturnContractShapedResponse()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            ReadModel(EffectivePermissionEvidenceRow(
                EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                "read_metadata")),
            new StaticTenantContextAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/effective-permissions");
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.RootElement.GetProperty("folderId").GetString().ShouldBe("folder-a");
        document.RootElement.GetProperty("authorizationOutcome").GetString().ShouldBe("allowed");
        document.RootElement.GetProperty("permissions")[0].GetString().ShouldBe("read");
        document.RootElement.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("read_your_writes");
        document.RootElement.GetProperty("freshness").GetProperty("stale").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task EffectivePermissionsRouteShouldUseSafeDenialEnvelopeForTenantMismatch()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            ReadModel(EffectivePermissionEvidenceRow(
                EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                "read_metadata")),
            new StaticTenantContextAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-secret-victim/effective-permissions");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-secret-victim");
        request.Headers.Add("X-Correlation-Id", "corr-mismatch");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"tenant_access_denied\"");
        json.ShouldContain("\"code\":\"denied_safe\"");
        json.ShouldContain("\"clientAction\":\"no_action\"");
        json.ShouldContain("\"correlationId\":\"corr-mismatch\"");
        json.ShouldContain("\"message\":");
        json.ShouldContain("\"details\":");
        json.ShouldNotContain("folder-secret-victim", Case.Sensitive);
        json.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
    }

    [Fact]
    public async Task EffectivePermissionsRouteShouldFoldMissingFolderIntoDeniedSafeBody()
    {
        InMemoryEffectivePermissionsReadModel emptyReadModel = new();
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            emptyReadModel,
            new StaticTenantContextAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-missing/effective-permissions");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.RootElement.GetProperty("authorizationOutcome").GetString().ShouldBe("denied_safe");
        document.RootElement.GetProperty("permissions").GetArrayLength().ShouldBe(0);
        json.ShouldNotContain("folder-missing", Case.Sensitive);
    }

    [Fact]
    public async Task EffectivePermissionsRouteShouldEmit503ProblemDetailsForUnavailableReadModel()
    {
        await using WebApplication app = BuildApp(
            TenantStore("tenant-a", "user-a"),
            new ThrowingReadModel(),
            new StaticTenantContextAccessor("tenant-a", "user-a"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/effective-permissions");
        request.Headers.Add("X-Correlation-Id", "corr-unavail");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        json.ShouldContain("\"category\":\"read_model_unavailable\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldContain("\"clientAction\":\"retry\"");
        json.ShouldContain("\"correlationId\":\"corr-unavail\"");
    }

    private static WebApplication BuildApp(
        IFolderTenantAccessProjectionStore tenantStore,
        IEffectivePermissionsReadModel readModel,
        ITenantContextAccessor tenantContext)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddSingleton(tenantStore);
        builder.Services.AddSingleton(readModel);
        builder.Services.AddSingleton(tenantContext);
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

    private static InMemoryEffectivePermissionsReadModel ReadModel(params EffectivePermissionEvidenceRow[] rows)
    {
        InMemoryEffectivePermissionsReadModel readModel = new();
        readModel.Save(new EffectivePermissionsReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows: rows,
            Freshness: new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: Now,
                ProjectionWatermark: "folder_a_watermark_v0011",
                Stale: false,
                ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));
        return readModel;
    }

    private sealed class ThrowingReadModel : IEffectivePermissionsReadModel
    {
        public Task<EffectivePermissionsReadModelResult> GetAsync(
            EffectivePermissionsReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");
    }

    private static EffectivePermissionEvidenceRow EffectivePermissionEvidenceRow(
        EffectivePermissionEvidenceSource source,
        string action)
        => new(
            Source: source,
            Principal: EffectivePermissionPrincipal.User("user-a"),
            Action: action,
            Sequence: 1,
            EffectiveAt: Now);

    private sealed class StaticTenantContextAccessor(string tenantId, string principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => tenantId;

        public string? PrincipalId => principalId;
    }
}
