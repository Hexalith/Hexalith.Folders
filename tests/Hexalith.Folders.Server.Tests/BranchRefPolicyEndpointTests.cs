using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
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

public sealed class BranchRefPolicyEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterBranchRefPolicyRoutes()
    {
        RecordingEventStoreGatewayClient gateway = new();
        using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/branch-ref-policy");
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task ConfigureBranchRefPolicyShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidConfigureRequest();
        request.Headers.Remove(headerName);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureBranchRefPolicyShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidConfigureRequest(requestSchemaVersion: "v2");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureBranchRefPolicyShouldRejectUnknownFieldsWithoutLeakingUnsafeValues()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/folders/folder-a/branch-ref-policy")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                repositoryBindingId = "repository-binding-a",
                policyRef = "opaque-policy-a",
                defaultRef = "branch_ref_primary",
                allowedRefPatterns = new[] { "branch_ref_feature" },
                protectedRefPatterns = new[] { "branch_ref_release" },
                repositoryUrl = "https://provider.example.test/owner/repository-secret",
            }),
        };
        AddCommandHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldNotContain("repository-secret", Case.Sensitive);
        json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("feature/raw", "branch_ref_feature", "branch_ref_release")]
    [InlineData("branch_ref_primary", "feature/raw", "branch_ref_release")]
    [InlineData("branch_ref_primary", "branch_ref_feature", "branch_ref_feature")]
    public async Task ConfigureBranchRefPolicyShouldRejectUnsafeOrDuplicateRefPatternsBeforeGatewaySubmit(
        string defaultRef,
        string allowedRef,
        string protectedRef)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidConfigureRequest(
            defaultRef: defaultRef,
            allowedRefPatterns: [allowedRef],
            protectedRefPatterns: [protectedRef]);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureBranchRefPolicyShouldRejectPatternLimitOverflowBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidConfigureRequest(
            allowedRefPatterns: Enumerable.Range(0, 17)
                .Select(static index => $"branch_ref_feature_{index}")
                .ToArray());

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureBranchRefPolicyShouldMapReadinessFailureToSafeProblem()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                StatusCodes.Status422UnprocessableEntity,
                "readiness failed for https://provider.example.test/owner/repository-secret",
                correlationId: "correlation-gateway",
                reasonCode: "provider_readiness_failed"),
        };
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidConfigureRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        document.RootElement.GetProperty("category").GetString().ShouldBe("provider_readiness_failed");
        document.RootElement.GetProperty("code").GetString().ShouldBe("provider_readiness_failed");
        json.ShouldNotContain("repository-secret", Case.Sensitive);
        json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
    }

    [Fact]
    public async Task GetBranchRefPolicyShouldReturnContractShapedAuthorizedPolicy()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/branch-ref-policy");
        AddReadHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Correlation-Id").ShouldContain("correlation-a");
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("eventually_consistent");
        document.RootElement.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
        document.RootElement.GetProperty("repositoryBindingId").GetString().ShouldBe("repository-binding-a");
        document.RootElement.GetProperty("policyRef").GetString().ShouldBe("opaque-policy-a");
        document.RootElement.GetProperty("defaultRef").GetString().ShouldBe("branch_ref_primary");
        document.RootElement.GetProperty("allowedRefPatterns")[0].GetString().ShouldBe("branch_ref_feature");
        document.RootElement.GetProperty("protectedRefPatterns")[0].GetString().ShouldBe("branch_ref_release");
        document.RootElement.GetProperty("freshness").GetProperty("stale").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetBranchRefPolicyShouldAllowLaterAuthorizedReadsWithNewRequestScope()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/branch-ref-policy");
        request.Headers.Add("X-Correlation-Id", "correlation-later");
        request.Headers.Add("X-Hexalith-Task-Id", "task-later");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Correlation-Id").ShouldContain("correlation-later");
        json.ShouldContain("\"repositoryBindingId\":\"repository-binding-a\"");
    }

    [Fact]
    public async Task GetBranchRefPolicyShouldRejectUnsupportedFreshnessBeforeReadModel()
    {
        RecordingEventStoreGatewayClient gateway = new();
        CountingBranchRefPolicyReadModel readModel = new(BranchRefPolicyReadModel());
        await using WebApplication app = BuildApp(gateway, readModel);
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/branch-ref-policy");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetBranchRefPolicyShouldDenyClientTenantMismatchBeforeReadModelAccess()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, BranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-secret-victim/branch-ref-policy");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-secret-victim");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        json.ShouldContain("\"category\":\"authorization_denied\"");
        json.ShouldContain("\"code\":\"claim_transform_denied\"");
        json.ShouldContain("\"clientAction\":\"no_action\"");
        json.ShouldNotContain("folder-secret-victim", Case.Sensitive);
        json.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
    }

    [Fact]
    public async Task GetBranchRefPolicyShouldEmit503ForUnavailableReadModel()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, new ThrowingBranchRefPolicyReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/branch-ref-policy");
        AddReadHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        json.ShouldContain("\"category\":\"read_model_unavailable\"");
        json.ShouldContain("\"retryable\":true");
        json.ShouldNotContain("raw unavailable diagnostic", Case.Sensitive);
    }

    private static WebApplication BuildApp(
        RecordingEventStoreGatewayClient gateway,
        IBranchRefPolicyReadModel branchRefPolicyReadModel,
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
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
        builder.Services.RemoveAll<IBranchRefPolicyReadModel>();
        builder.Services.AddSingleton(branchRefPolicyReadModel);

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
                    "read_branch_ref_policy",
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

    private static InMemoryBranchRefPolicyReadModel BranchRefPolicyReadModel()
    {
        InMemoryBranchRefPolicyReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(new BranchRefPolicyReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            RepositoryBindingId: "repository-binding-a",
            PolicyRef: "opaque-policy-a",
            DefaultRef: "branch_ref_primary",
            AllowedRefPatterns: ["branch_ref_feature"],
            ProtectedRefPatterns: ["branch_ref_release"],
            Freshness: new FolderLifecycleFreshness(
                ReadConsistency: "eventually_consistent",
                ObservedAt: Now,
                ProjectionWatermark: "branch_ref_policy_watermark_v1",
                Stale: false,
                ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: "tenant-a",
                PrincipalId: "user-a",
                ActionToken: "read_branch_ref_policy",
                TaskId: "task-a",
                CorrelationId: "correlation-a",
                AuthorizationWatermark: "permission_watermark_v1")));
        return readModel;
    }

    private static HttpRequestMessage CreateValidConfigureRequest(
        string requestSchemaVersion = "v1",
        string defaultRef = "branch_ref_primary",
        IReadOnlyList<string>? allowedRefPatterns = null,
        IReadOnlyList<string>? protectedRefPatterns = null)
    {
        HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/folders/folder-a/branch-ref-policy")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                repositoryBindingId = "repository-binding-a",
                policyRef = "opaque-policy-a",
                defaultRef,
                allowedRefPatterns = allowedRefPatterns ?? ["branch_ref_feature"],
                protectedRefPatterns = protectedRefPatterns ?? ["branch_ref_release"],
            }),
        };
        AddCommandHeaders(request);
        return request;
    }

    private static void AddCommandHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
    }

    private static void AddReadHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");
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

    private sealed class CountingBranchRefPolicyReadModel(IBranchRefPolicyReadModel inner) : IBranchRefPolicyReadModel
    {
        public int Calls { get; private set; }

        public Task<BranchRefPolicyReadModelResult> GetAsync(
            BranchRefPolicyReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return inner.GetAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowingBranchRefPolicyReadModel : IBranchRefPolicyReadModel
    {
        public Task<BranchRefPolicyReadModelResult> GetAsync(
            BranchRefPolicyReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("raw unavailable diagnostic must not leak");
    }

    private sealed class RecordingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        public List<SubmitCommandRequest> Requests { get; } = [];

        public Exception? Exception { get; init; }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new SubmitCommandResponse(request.CorrelationId ?? request.MessageId));
        }

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
