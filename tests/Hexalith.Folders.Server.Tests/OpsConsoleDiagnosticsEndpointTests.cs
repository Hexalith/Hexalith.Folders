using System.Net;
using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.OpsConsole;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// REST conformance for the seven Story 8.2 ops-console diagnostics routes (Bucket B): route registration,
/// authorized metadata-only contract shape, read-op transport guardrails (reject <c>Idempotency-Key</c>,
/// validate <c>X-Hexalith-Freshness</c>), safe denial (401/404 with no resource echo), and the
/// diagnostics-specific <c>projection_stale</c> → 409 mapping.
/// </summary>
public sealed class OpsConsoleDiagnosticsEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    private const string ReadinessPath = "/api/v1/ops-console/readiness-diagnostics";
    private const string ProjectionFreshnessPath = "/api/v1/ops-console/projection-freshness";
    private const string LockPath = "/api/v1/folders/folder-a/workspaces/workspace-a/ops-console/lock-diagnostics";
    private const string DirtyStatePath = "/api/v1/folders/folder-a/workspaces/workspace-a/ops-console/dirty-state-diagnostics";
    private const string FailedOperationPath = "/api/v1/folders/folder-a/workspaces/workspace-a/ops-console/failed-operation-diagnostics";
    private const string SyncStatusPath = "/api/v1/folders/folder-a/workspaces/workspace-a/ops-console/sync-status-diagnostics";
    private const string ProviderStatusPath = "/api/v1/folders/folder-a/ops-console/provider-status-diagnostics";

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterAllSevenDiagnosticsRoutes()
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

        routes.ShouldContain(ReadinessPath);
        routes.ShouldContain(ProjectionFreshnessPath);
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics");
        routes.ShouldContain("/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics");
    }

    [Fact]
    public async Task GetReadinessDiagnosticsShouldReturnContractShape()
    {
        using JsonDocument document = await GetOkAsync(ReadinessPath);
        JsonElement root = document.RootElement;
        root.GetProperty("audience").GetString().ShouldBe("authorized_operator");
        root.GetProperty("status").GetString().ShouldBe("degraded");
        root.GetProperty("disposition").GetString().ShouldBe("degraded_but_serving");
        root.GetProperty("providerSummaryReference").GetProperty("value").GetString().ShouldBe("opaque_provider_005");
        root.GetProperty("trust").GetProperty("availability").GetString().ShouldBe("available");
        root.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
    }

    [Fact]
    public async Task GetLockDiagnosticsShouldReturnContractShape()
    {
        using JsonDocument document = await GetOkAsync(LockPath);
        JsonElement root = document.RootElement;
        root.GetProperty("status").GetString().ShouldBe("locked");
        root.GetProperty("lockReference").GetProperty("value").GetString().ShouldBe("opaque_lock_001");
        root.GetProperty("lockReference").GetProperty("redaction").GetProperty("visibility").GetString().ShouldBe("metadata_only");
    }

    [Fact]
    public async Task GetDirtyStateDiagnosticsShouldReturnDigestOnlyEvidence()
    {
        using JsonDocument document = await GetOkAsync(DirtyStatePath);
        JsonElement evidence = document.RootElement.GetProperty("changedPathEvidence");
        evidence.GetProperty("evidenceKind").GetString().ShouldBe("digest");
        evidence.GetProperty("digest").GetString().ShouldBe("digest_007");
        evidence.TryGetProperty("reference", out _).ShouldBeFalse("digest evidence must not also carry a reference.");
    }

    [Fact]
    public async Task GetFailedOperationDiagnosticsShouldReturnSanitizedRetryPosture()
    {
        using JsonDocument document = await GetOkAsync(FailedOperationPath);
        JsonElement root = document.RootElement;
        root.GetProperty("sanitizedErrorCategory").GetString().ShouldBe("provider_failure_known");
        root.GetProperty("retryEligibility").GetProperty("eligible").GetBoolean().ShouldBeFalse();
        root.GetProperty("retryEligibility").GetProperty("advisoryOnly").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetProviderStatusDiagnosticsShouldReturnRedactableBindingReference()
    {
        using JsonDocument document = await GetOkAsync(ProviderStatusPath);
        JsonElement root = document.RootElement;
        root.GetProperty("providerBindingReference").GetProperty("value").GetString().ShouldBe("opaque_binding_009");
        root.GetProperty("providerCorrelationReference").GetString().ShouldBe("provref_009");
    }

    [Fact]
    public async Task GetSyncStatusDiagnosticsShouldSeparateStates()
    {
        using JsonDocument document = await GetOkAsync(SyncStatusPath);
        JsonElement root = document.RootElement;
        root.GetProperty("acceptedCommandState").GetString().ShouldBe("accepted");
        root.GetProperty("projectedState").GetString().ShouldBe("unknown_provider_outcome");
        root.GetProperty("providerOutcomeState").GetString().ShouldBe("reconciliation_required");
    }

    [Fact]
    public async Task GetProjectionFreshnessShouldReturnContractShape()
    {
        using JsonDocument document = await GetOkAsync(ProjectionFreshnessPath);
        JsonElement root = document.RootElement;
        root.GetProperty("projectionName").GetString().ShouldBe("folders_workspace_status");
        root.GetProperty("availability").GetString().ShouldBe("stale");
        root.GetProperty("elapsedMilliseconds").GetInt32().ShouldBe(32);
        root.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
    }

    [Fact]
    public async Task DiagnosticsResponsesShouldNotLeakLookupKeys()
    {
        // The [JsonIgnore] tenant/folder/workspace lookup keys must never reach the wire.
        string lockJson = await GetStringAsync(LockPath);
        lockJson.ShouldNotContain("\"managedTenantId\"", Case.Sensitive);
        lockJson.ShouldNotContain("\"folderId\"", Case.Sensitive);
        lockJson.ShouldNotContain("\"workspaceId\"", Case.Sensitive);
        lockJson.ShouldNotContain("tenant-a", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(ProjectionFreshnessPath)]
    [InlineData(LockPath)]
    [InlineData(DirtyStatePath)]
    [InlineData(FailedOperationPath)]
    [InlineData(SyncStatusPath)]
    [InlineData(ProviderStatusPath)]
    public async Task DiagnosticsShouldRejectIdempotencyKey(string path)
    {
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(ProjectionFreshnessPath)]
    [InlineData(LockPath)]
    [InlineData(DirtyStatePath)]
    [InlineData(FailedOperationPath)]
    [InlineData(SyncStatusPath)]
    [InlineData(ProviderStatusPath)]
    public async Task DiagnosticsShouldRejectUnsupportedFreshness(string path)
    {
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
    }

    [Theory]
    [InlineData("/api/v1/folders/_invalid/workspaces/workspace-a/ops-console/lock-diagnostics")]
    [InlineData("/api/v1/folders/folder-a/workspaces/_invalid/ops-console/lock-diagnostics")]
    [InlineData("/api/v1/folders/_invalid/ops-console/provider-status-diagnostics")]
    public async Task DiagnosticsShouldRejectNonCanonicalPathId(string path)
    {
        // Canonical-id guardrail (anti-injection): a path segment that is not a canonical identifier
        // must fail closed at the transport boundary with a metadata-only validation problem that never
        // echoes the offending value.
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, json);
        json.ShouldContain("\"code\":\"validation_error\"");
        json.ShouldNotContain("_invalid", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(LockPath)]
    public async Task DiagnosticsShouldRejectSensitiveCorrelationId(string path)
    {
        // A correlation id that carries a secret-looking value (token/secret/credential/URL) must be
        // rejected as unsafe — never accepted, never reflected — to keep diagnostics free of secret leakage.
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "secret-token-aaaa");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, json);
        json.ShouldContain("\"code\":\"unsafe_correlation_id\"");
        json.ShouldNotContain("secret-token-aaaa", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(ProjectionFreshnessPath)]
    public async Task TenantScopedDiagnosticsShouldDenySafeWhenClientControlledTenantMismatch(string path)
    {
        // Authorization-before-observation: a client-supplied tenant override that disagrees with the
        // authoritative tenant fails closed with a safe 403 that never reflects the attempted tenant id.
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-impostor");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, json);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"denied_safe\"");
        json.ShouldNotContain("tenant-impostor", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(ProjectionFreshnessPath)]
    [InlineData(LockPath)]
    [InlineData(ProviderStatusPath)]
    public async Task DiagnosticsShouldReturnServiceUnavailableWhenReadModelThrows(string path)
    {
        // A failing backing read model surfaces as a retryable 503 read_model_unavailable — never a 200,
        // never a leaked exception.
        await using WebApplication app = BuildApp(new ThrowingOpsConsoleDiagnosticsReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, json);
        json.ShouldContain("\"code\":\"read_model_unavailable\"");
        json.ShouldContain("\"retryable\":true");
    }

    [Fact]
    public async Task GetLockDiagnosticsShouldReturnSafeNotFoundForUnknownWorkspace()
    {
        await using WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-unknown/ops-console/lock-diagnostics");
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        json.ShouldContain("\"category\":\"not_found\"");
        json.ShouldNotContain("workspace-unknown", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(LockPath)]
    public async Task DiagnosticsShouldUseSafeDenialForUnauthenticatedCaller(string path)
    {
        await using WebApplication app = BuildApp(SeededReadModel(), tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
    }

    [Theory]
    [InlineData(ReadinessPath)]
    [InlineData(ProjectionFreshnessPath)]
    [InlineData(LockPath)]
    [InlineData(ProviderStatusPath)]
    public async Task DiagnosticsShouldDenySafeWhenTenantAccessRevoked(string path)
    {
        // AC2 fail-closed-on-revocation: an authenticated caller whose tenant-access membership has been
        // revoked (principal no longer enrolled in the tenant-access projection) is denied with a safe 403,
        // distinct from the client-mismatch short-circuit and never echoing the folder/workspace id.
        await using WebApplication app = BuildApp(SeededReadModel(), principalEnrolled: false);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, json);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"denied_safe\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
    }

    [Fact]
    public async Task GetReadinessDiagnosticsShouldReturnConflictWhenTenantProjectionStale()
    {
        // Tenant projection aged beyond the 30-minute DiagnosticStalenessBudget → projection_stale → HTTP 409.
        await using WebApplication app = BuildApp(SeededReadModel(), tenantLastEvent: Now.AddHours(-1));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, ReadinessPath);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, json);
        json.ShouldContain("\"category\":\"projection_stale\"");
    }

    [Fact]
    public async Task GetLockDiagnosticsShouldReturnConflictWhenFolderProjectionStale()
    {
        // Folder/workspace-scoped counterpart of the tenant-scoped stale test: the layered folder
        // authorizer observes a stale tenant projection (aged past the staleness budget) and the
        // diagnostics-specific mapping surfaces it as projection_stale → HTTP 409 (not 503).
        await using WebApplication app = BuildApp(SeededReadModel(), tenantLastEvent: Now.AddHours(-1));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, LockPath);
        request.Headers.Add("X-Correlation-Id", "corr-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, json);
        json.ShouldContain("\"category\":\"projection_stale\"");
    }

    // -------------------------------------------------------------------------------------------------
    // Helpers.
    // -------------------------------------------------------------------------------------------------

    private static async Task<JsonDocument> GetOkAsync(string path)
        => JsonDocument.Parse(await GetStringAsync(path, assertOk: true).ConfigureAwait(false));

    private static async Task<string> GetStringAsync(string path, bool assertOk = false)
    {
        WebApplication app = BuildApp(SeededReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add("X-Correlation-Id", "corr-a");
            request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            if (assertOk)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
                response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("eventually_consistent");
            }

            return json;
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WebApplication BuildApp(
        IOpsConsoleDiagnosticsReadModel readModel,
        DateTimeOffset? tenantLastEvent = null,
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        bool principalEnrolled = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore(tenantLastEvent ?? Now.AddMinutes(-1), principalEnrolled));
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(PermissionReadModel());
        builder.Services.AddSingleton<IOpsConsoleDiagnosticsReadModel>(readModel);
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

    private static InMemoryOpsConsoleDiagnosticsReadModel SeededReadModel()
    {
        DiagnosticReadFreshness freshness = new("eventually_consistent", Now, "watermark_v1", Stale: false);
        DiagnosticTrustEvidenceView trust = new("available", 420, "not_stale", "none");
        RedactionMetadataView visible = new("metadata_only", "not_redacted");

        InMemoryOpsConsoleDiagnosticsReadModel readModel = new();

        readModel.Save(new ReadinessDiagnosticsView(
            "tenant-a", "authorized_operator", "degraded", "degraded_but_serving", trust,
            [new("readinessStatus", "consumer_safe")],
            new RedactableDiagnosticIdentifierView("opaque_provider_005", "operator_sanitized", visible),
            new RedactableDiagnosticIdentifierView("opaque_folder_005", "operator_sanitized", visible),
            new RedactableDiagnosticIdentifierView("opaque_workspace_005", "operator_sanitized", visible),
            freshness));

        readModel.Save(new LockDiagnosticsView(
            "tenant-a", "folder-a", "workspace-a", "authorized_operator", "locked", "degraded_but_serving", trust,
            [new("lockReference", "operator_sanitized")],
            new RedactableDiagnosticIdentifierView("opaque_lock_001", "operator_sanitized", visible),
            freshness));

        readModel.Save(new DirtyStateDiagnosticsView(
            "tenant-a", "folder-a", "workspace-a", "authorized_operator", "dirty", "awaiting_human", trust,
            [new("changedPathEvidence", "operator_sanitized")],
            new ChangedPathEvidenceView("digest", "digest_007", Reference: null, "operator_sanitized"),
            freshness));

        readModel.Save(new FailedOperationDiagnosticsView(
            "tenant-a", "folder-a", "workspace-a", "authorized_operator", "failed", "terminal_until_intervention", trust,
            [new("sanitizedErrorCategory", "consumer_safe")],
            "opaque_operation_008", "opaque_task_008", "provider_failure_known",
            new RetryEligibilityView(Eligible: false, "operator_review_required", AdvisoryOnly: true),
            freshness));

        readModel.Save(new ProviderStatusDiagnosticsView(
            "tenant-a", "folder-a", "authorized_operator", "degraded", "degraded_but_serving", trust,
            [new("providerBindingReference", "operator_sanitized")],
            new RedactableDiagnosticIdentifierView("opaque_binding_009", "operator_sanitized", visible),
            "provref_009",
            freshness));

        readModel.Save(new SyncStatusDiagnosticsView(
            "tenant-a", "folder-a", "workspace-a", "authorized_operator", "reconciliation_required", "awaiting_human", trust,
            [new("acceptedCommandState", "consumer_safe")],
            "accepted", "unknown_provider_outcome", "reconciliation_required",
            freshness));

        readModel.Save(new ProjectionFreshnessDiagnosticsView(
            "tenant-a", "authorized_operator", "folders_workspace_status", "stale", 1800, 32, "projection_lag", "none",
            "TODO(reference-pending): C5 projection freshness target",
            [new("projectionName", "consumer_safe")],
            freshness));

        return readModel;
    }

    private static InMemoryFolderTenantAccessProjectionStore TenantStore(DateTimeOffset lastEvent, bool principalEnrolled = true)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            // When the principal is not enrolled the tenant-access projection reflects a revoked
            // membership — fail-closed must surface as a safe 403, never as a served diagnostic.
            Principals = principalEnrolled
                ? new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    ["user-a"] = new("user-a", "Member"),
                }
                : new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal),
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = lastEvent,
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

    private sealed class ThrowingOpsConsoleDiagnosticsReadModel : IOpsConsoleDiagnosticsReadModel
    {
        public Task<ReadinessDiagnosticsView?> GetReadinessAsync(string managedTenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<LockDiagnosticsView?> GetLockAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<DirtyStateDiagnosticsView?> GetDirtyStateAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<FailedOperationDiagnosticsView?> GetFailedOperationAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<ProviderStatusDiagnosticsView?> GetProviderStatusAsync(string managedTenantId, string folderId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<SyncStatusDiagnosticsView?> GetSyncStatusAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");

        public Task<ProjectionFreshnessDiagnosticsView?> GetProjectionFreshnessAsync(string managedTenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("read model unavailable");
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
