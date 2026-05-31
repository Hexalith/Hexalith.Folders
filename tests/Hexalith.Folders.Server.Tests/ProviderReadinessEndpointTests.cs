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

using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
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

        routes.ShouldContain("/api/v1/provider-readiness/validations");
        routes.ShouldContain("/api/v1/provider-readiness/support-evidence");
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
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request("corr-ready");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? correlationHeaders).ShouldBeTrue();
        correlationHeaders.ShouldNotBeNull().ShouldContain("corr-ready");
        response.Headers.TryGetValues("X-Hexalith-Freshness", out IEnumerable<string>? freshnessHeaders).ShouldBeTrue();
        freshnessHeaders.ShouldNotBeNull().ShouldContain("snapshot_per_task");
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
        using HttpClient client = app.GetTestClient();
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
    public async Task ProviderReadinessRouteShouldSanitizeUnsafeCorrelationOnPreServiceValidationFailure()
    {
        const string unsafeCorrelation = "https://user:pass@example.invalid/repo.git";
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), bindingReader: bindingReader);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request(correlationId: null);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", unsafeCorrelation).ShouldBeTrue();
        request.Headers.Add("Idempotency-Key", "idempotency-should-not-be-accepted");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_accepted\"");
        json.ShouldNotContain(unsafeCorrelation, Case.Sensitive);
        document.RootElement.GetProperty("correlationId").GetString().ShouldStartWith("correlation_");
        bindingReader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldRejectUnsupportedFreshnessSafely()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), bindingReader: bindingReader);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request("corr-freshness");
        request.Headers.Remove("X-Hexalith-Freshness");
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
        using HttpClient client = app.GetTestClient();
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
    public async Task ProviderReadinessRouteShouldMapProviderUnavailableToCanonicalProblemDetails()
    {
        await using WebApplication app = BuildApp(FakeGitProvider.Failing(ProviderFailureCategory.ProviderUnavailable));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request("corr-unavailable");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        json.ShouldContain("\"category\":\"provider_unavailable\"");
        json.ShouldContain("\"code\":\"provider_unavailable\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldContain("\"correlationId\":\"corr-unavailable\"");
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldGenerateSafeCorrelationWhenHeaderIsMissingOrUnsafe()
    {
        const string unsafeCorrelation = "ghp_abcdefghijklmnopqrstuvwxyz123456";
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
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request(correlationId: null);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", unsafeCorrelation).ShouldBeTrue();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        json.ShouldNotContain(unsafeCorrelation, Case.Sensitive);
        document.RootElement.GetProperty("correlationId").GetString().ShouldStartWith("correlation_");
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(document.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task ProviderReadinessRouteShouldDenyClientTenantMismatchBeforeObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), bindingReader: bindingReader);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request("corr-tenant-mismatch");
        request.RequestUri = new Uri("/api/v1/provider-readiness/validations?tenantId=tenant-other", UriKind.Relative);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"provider_readiness_read_denied\"");
        json.ShouldNotContain("tenant-other", Case.Sensitive);
        bindingReader.Calls.ShouldBe(0);
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
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = Request("corr-denied", providerBindingRef: "binding-secret-victim");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"provider_readiness_read_denied\"");
        json.ShouldNotContain("binding-secret-victim", Case.Sensitive);
        bindingReader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldReturnAuthorizedEvidenceList()
    {
        InMemoryProviderReadinessEvidenceStore evidenceStore = new(new FixedUtcClock(Now));
        await evidenceStore.StoreAsync(
            SupportRecord("tenant-a", "profile_aaaaaaaaaaaaaaaa", """{"evidence":{"repositoryCreation":"supported","existingRepositoryBinding":"supported","branchRefPolicy":"supported","fileOperations":"supported","commitStatus":"supported","providerErrors":"temporarily_unavailable","failureBehavior":"documented"}}"""),
            TestContext.Current.CancellationToken);
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), evidenceStore: evidenceStore);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence?limit=10");
        request.Headers.Add("X-Correlation-Id", "corr-support");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Correlation-Id").ShouldContain("corr-support");
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("eventually_consistent");
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(7);
        document.RootElement.GetProperty("items")[0].GetProperty("capabilityProfileRef").GetString().ShouldBe("profile_aaaaaaaaaaaaaaaa");
        document.RootElement.GetProperty("items")[0].GetProperty("capability").GetString().ShouldBe("repository_creation");
        document.RootElement.GetProperty("items")[0].GetProperty("supportState").GetString().ShouldBe("supported");
        document.RootElement.GetProperty("page").GetProperty("limit").GetInt32().ShouldBe(10);
        document.RootElement.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
        json.ShouldNotContain("diagnostic", Case.Insensitive);
        json.ShouldNotContain("binding-a", Case.Sensitive);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldReturnSafeEmptyListForAuthorizedTenantWithoutEvidence()
    {
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike());

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence");
        request.Headers.Add("X-Correlation-Id", "corr-empty");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("page").GetProperty("limit").GetInt32().ShouldBe(50);
        document.RootElement.GetProperty("page").GetProperty("isTruncated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldClampContractAllowedLimitToEffectiveServerMaximum()
    {
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike());

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence?limit=500");
        request.Headers.Add("X-Correlation-Id", "corr-limit-clamp");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        document.RootElement.GetProperty("page").GetProperty("limit").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldRejectPreAuthorizationHeadersBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), supportReadModel: readModel);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence?limit=10");
        request.Headers.Add("X-Correlation-Id", "corr-support-idempotency");
        request.Headers.Add("Idempotency-Key", "idempotency-not-accepted");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_accepted\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldRejectUnsupportedFreshnessBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), supportReadModel: readModel);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence");
        request.Headers.Add("X-Correlation-Id", "corr-support-freshness");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"category\":\"validation_error\"");
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("/api/v1/provider-readiness/support-evidence?limit=0", "invalid_pagination")]
    [InlineData("/api/v1/provider-readiness/support-evidence?limit=1001", "invalid_pagination")]
    [InlineData("/api/v1/provider-readiness/support-evidence?cursor=tenant-a:secret", "invalid_pagination")]
    public async Task ProviderSupportEvidenceRouteShouldRejectInvalidPaginationBeforeReadModelObservation(
        string uri,
        string expectedCode)
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), supportReadModel: readModel);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.Add("X-Correlation-Id", "corr-support-pagination");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain($"\"code\":\"{expectedCode}\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldRejectUnsafeCorrelationBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        await using WebApplication app = BuildApp(FakeGitProvider.GitHubLike(), supportReadModel: readModel);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "https://provider.example.test/owner/repository-secret").ShouldBeTrue();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsafe_correlation_id\"");
        json.ShouldNotContain("repository-secret", Case.Sensitive);
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderSupportEvidenceRouteShouldDenyMissingProviderSupportAuthorityBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        await using WebApplication app = BuildApp(
            FakeGitProvider.GitHubLike(),
            supportReadModel: readModel,
            claimTransform: new StaticClaimTransformEvidenceAccessor(
                "tenant-a",
                "user-a",
                [ProviderReadinessValidationService.ReadActionToken]));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence");
        request.Headers.Add("X-Correlation-Id", "corr-support-denied");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"provider_support_read_denied\"");
        json.ShouldNotContain("binding-a", Case.Sensitive);
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderSupportEvidenceReadModelStatus.Stale, HttpStatusCode.ServiceUnavailable, "projection_stale", "projection_stale")]
    [InlineData(ProviderSupportEvidenceReadModelStatus.Unavailable, HttpStatusCode.ServiceUnavailable, "provider_unavailable", "provider_unavailable")]
    [InlineData(ProviderSupportEvidenceReadModelStatus.Malformed, HttpStatusCode.ServiceUnavailable, "read_model_unavailable", "projection_malformed")]
    public async Task ProviderSupportEvidenceRouteShouldMapReadModelFailuresToSafeProblemDetails(
        ProviderSupportEvidenceReadModelStatus status,
        HttpStatusCode expectedStatus,
        string expectedCategory,
        string expectedCode)
    {
        await using WebApplication app = BuildApp(
            FakeGitProvider.GitHubLike(),
            supportReadModel: new StatusProviderSupportEvidenceReadModel(status));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/provider-readiness/support-evidence");
        request.Headers.Add("X-Correlation-Id", "corr-support-readmodel");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        json.ShouldContain($"\"category\":\"{expectedCategory}\"");
        json.ShouldContain($"\"code\":\"{expectedCode}\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldContain("\"correlationId\":\"corr-support-readmodel\"");
        json.ShouldNotContain("binding-a", Case.Sensitive);
        json.ShouldNotContain("diagnostic", Case.Insensitive);
    }

    private static WebApplication BuildApp(
        IGitProvider provider,
        IProviderReadinessBindingReader? bindingReader = null,
        ITenantContextAccessor? tenantContext = null,
        IEventStoreClaimTransformEvidenceAccessor? claimTransform = null,
        InMemoryProviderReadinessEvidenceStore? evidenceStore = null,
        IProviderSupportEvidenceReadModel? supportReadModel = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore());
        RecordingProviderReadinessBindingReader reader = bindingReader as RecordingProviderReadinessBindingReader
            ?? new RecordingProviderReadinessBindingReader(Binding());
        builder.Services.AddSingleton(reader);
        builder.Services.AddSingleton<IProviderReadinessBindingReader>(reader);
        builder.Services.AddSingleton<IProviderCapabilityAuthorizer>(RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"));
        builder.Services.AddSingleton<IProviderCapabilityResolver>(new RecordingProviderCapabilityResolver(provider));
        builder.Services.AddSingleton<IProviderCapabilityEvidenceStore, RecordingProviderCapabilityEvidenceStore>();
        InMemoryProviderReadinessEvidenceStore readinessEvidenceStore = evidenceStore ?? new InMemoryProviderReadinessEvidenceStore(new FixedUtcClock(Now));
        builder.Services.AddSingleton(readinessEvidenceStore);
        builder.Services.AddSingleton<IProviderReadinessEvidenceStore>(readinessEvidenceStore);
        builder.Services.AddSingleton<IProviderSupportEvidenceReadModel>(supportReadModel ?? readinessEvidenceStore);
        builder.Services.AddSingleton(tenantContext ?? new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.AddSingleton(claimTransform ?? new StaticClaimTransformEvidenceAccessor(
            "tenant-a",
            "user-a",
            [ProviderReadinessValidationService.ReadActionToken, ProviderSupportEvidenceQueryHandler.ReadActionToken]));
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage Request(string? correlationId, string providerBindingRef = "binding-a")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/provider-readiness/validations");
        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlationId);
        }

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

    private static ProviderReadinessEvidenceRecord SupportRecord(
        string tenantId,
        string capabilityProfileRef,
        string diagnosticJson)
        => new(
            tenantId,
            "organization-a",
            "binding-a",
            "github",
            "github",
            capabilityProfileRef,
            "ready",
            "success",
            Retryable: false,
            "none",
            Now.AddMinutes(-1),
            "tenant-a:7",
            "corr-support-evidence",
            diagnosticJson);

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

    private sealed class CountingProviderSupportEvidenceReadModel : IProviderSupportEvidenceReadModel
    {
        public int Calls { get; private set; }

        public Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
            ProviderSupportEvidenceReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ProviderSupportEvidenceReadModelResult.Available([], request.EmptyFreshness(), null));
        }
    }

    private sealed class StatusProviderSupportEvidenceReadModel(ProviderSupportEvidenceReadModelStatus status) : IProviderSupportEvidenceReadModel
    {
        public Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
            ProviderSupportEvidenceReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            ProviderSupportEvidenceReadModelResult result = status switch
            {
                ProviderSupportEvidenceReadModelStatus.Stale => ProviderSupportEvidenceReadModelResult.Stale(request.EmptyFreshness()),
                ProviderSupportEvidenceReadModelStatus.Unavailable => ProviderSupportEvidenceReadModelResult.Unavailable(request.EmptyFreshness()),
                ProviderSupportEvidenceReadModelStatus.Malformed => ProviderSupportEvidenceReadModelResult.Malformed(request.EmptyFreshness()),
                _ => ProviderSupportEvidenceReadModelResult.Available([], request.EmptyFreshness(), null),
            };

            return Task.FromResult(result);
        }
    }
}
