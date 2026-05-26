using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing.Providers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ProviderReadinessEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterProviderReadinessRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
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

        routes.ShouldContain("/api/v1/provider-readiness/validations");
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldReturnOperatorReadinessWithSafeDiagnostics()
    {
        await using WebApplication app = BuildApp(FakeGitProvider.WithOperationRows(
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence)));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = Request("corr-ready");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? correlationHeaders).ShouldBeTrue();
        correlationHeaders.ShouldNotBeNull().ShouldContain("corr-ready");
        document.RootElement.GetProperty("audience").GetString().ShouldBe("authorized_operator");
        document.RootElement.GetProperty("status").GetString().ShouldBe("ready");
        document.RootElement.GetProperty("safeReasonCode").GetString().ShouldBe("success");
        document.RootElement.GetProperty("retryable").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("remediationCategory").GetString().ShouldBe("none");
        document.RootElement.GetProperty("providerReference").GetString().ShouldBe("binding-a");
        document.RootElement.GetProperty("correlationId").GetString().ShouldBe("corr-ready");
        document.RootElement.GetProperty("evidence").GetProperty("repositoryCreation").GetString().ShouldBe("supported");
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldRejectIdempotencyKeyBeforeObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), bindingReader: bindingReader);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = Request("corr-idempotency");
        request.Headers.Add("Idempotency-Key", "idempotency-should-not-be-accepted");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"category\":\"validation_error\"");
        json.ShouldContain("\"code\":\"idempotency_key_not_accepted\"");
        bindingReader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldRejectUnsupportedFreshnessSafely()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), bindingReader: bindingReader);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = Request("corr-freshness");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"category\":\"validation_error\"");
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        bindingReader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldMapProviderRateLimitedToCanonicalProblemDetails()
    {
        await using WebApplication app = BuildApp(FakeGitProvider.Failing(ProviderFailureCategory.ProviderRateLimited));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = Request("corr-rate");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)429);
        json.ShouldContain("\"category\":\"provider_rate_limited\"");
        json.ShouldContain("\"code\":\"provider_rate_limited\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldContain("\"correlationId\":\"corr-rate\"");
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldNotLeakProviderIdentifiersOnSafeDenial()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(
            FakeGitProvider.GitHubLike(),
            bindingReader,
            tenantContext: new StaticTenantContextAccessor("tenant-a", "user-a"),
            claimTransform: new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a", ["read_metadata"]));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpRequestMessage request = Request("corr-denied", providerBindingRef: "binding-secret-victim");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"provider_readiness_read_denied\"");
        json.ShouldNotContain("binding-secret-victim", Case.Sensitive);
        bindingReader.Calls.ShouldBe(0);
    }

    private static WebApplication BuildApp(
        IGitProvider provider,
        IProviderReadinessBindingReader? bindingReader = null,
        ITenantContextAccessor? tenantContext = null,
        IEventStoreClaimTransformEvidenceAccessor? claimTransform = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore());
        RecordingProviderReadinessBindingReader reader = bindingReader as RecordingProviderReadinessBindingReader
            ?? new RecordingProviderReadinessBindingReader(Binding());
        builder.Services.AddSingleton(reader);
        builder.Services.AddSingleton<IProviderReadinessBindingReader>(reader);
        builder.Services.AddSingleton<IProviderCapabilityAuthorizer>(RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"));
        builder.Services.AddSingleton<IProviderCapabilityResolver>(new RecordingProviderCapabilityResolver(provider));
        builder.Services.AddSingleton<IProviderCapabilityEvidenceStore, RecordingProviderCapabilityEvidenceStore>();
        builder.Services.AddSingleton<IProviderReadinessEvidenceStore, InMemoryProviderReadinessEvidenceStore>();
        builder.Services.AddSingleton(tenantContext ?? new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.AddSingleton(claimTransform ?? new StaticClaimTransformEvidenceAccessor(
            "tenant-a",
            "user-a",
            [ProviderReadinessValidationService.ReadActionToken]));
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage Request(string correlationId, string providerBindingRef = "binding-a")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/provider-readiness/validations");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
        request.Content = new StringContent(
            $$"""{"providerBindingRef":"{{providerBindingRef}}","requestedCapability":"repository_creation"}""",
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static OrganizationProviderBinding Binding()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            ProviderKind: "github",
            CredentialReferenceId: "credential-ref-a",
            NamingPolicy: new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            BranchPolicy: new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            CorrelationId: "binding-corr-a",
            TaskId: "binding-task-a",
            IdempotencyKey: "binding-idempotency-a",
            IdempotencyFingerprint: "binding-fingerprint-a",
            ConfiguredStatus: "configured",
            OccurredAt: Now);

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
            LastEventTimestamp = DateTimeOffset.UtcNow,
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class RecordingProviderReadinessBindingReader(OrganizationProviderBinding? binding) : IProviderReadinessBindingReader
    {
        public int Calls { get; private set; }

        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(binding);
        }
    }

    private sealed class StaticTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(
        string tenantId,
        string principalId,
        IReadOnlyList<string> permissions) : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, permissions);
    }
}
