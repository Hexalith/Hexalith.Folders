using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderAclEndpointTests
{
    private static readonly string ReadAclEntryId = FolderAclContract.DeriveAclEntryId("user", "user-a", "read");

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterAclRoutes()
    {
        using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/acl");
        routes.ShouldContain("/api/v1/folders/{folderId}/acl/{aclEntryId}");
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldSubmitGrantCommandForGrantEffect()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("grant", "read", ReadAclEntryId);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.GrantFolderAccessCommandType);
        submitted.AggregateId.ShouldBe("folder-a");
        submitted.MessageId.ShouldBe("idempotency-a");
        System.Text.Json.JsonElement operation = submitted.Payload.GetProperty("operations")[0];
        operation.GetProperty("principalKind").GetString().ShouldBe("user");
        operation.GetProperty("principalId").GetString().ShouldBe("user-a");
        operation.GetProperty("action").GetString().ShouldBe("read_metadata");
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldSubmitRevokeCommandForRevokeEffect()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("revoke", "read", ReadAclEntryId);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.RevokeFolderAccessCommandType);
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldRejectAclEntryIdMismatchBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        // aclEntryId derived for "read" but the path uses the "administer" id => mismatch.
        using HttpRequestMessage request = CreateValidRequest("grant", "read", FolderAclContract.DeriveAclEntryId("user", "user-a", "administer"));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"acl_entry_id_mismatch\"");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("grant", "read", ReadAclEntryId, requestSchemaVersion: "v2");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_request_schema_version\"");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldRejectUnauthenticatedCaller()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("grant", "read", ReadAclEntryId);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("user-a", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListFolderAclEntriesShouldRejectIdempotencyKey()
    {
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/acl");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
    }

    [Fact]
    public async Task ListFolderAclEntriesShouldRejectUnsupportedFreshness()
    {
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/acl");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
    }

    [Fact]
    public async Task ListFolderAclEntriesShouldUseSafeDenialForUnauthenticatedCaller()
    {
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/acl");
        request.Headers.Add("X-Correlation-Id", "correlation-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
    }

    private static HttpRequestMessage CreateValidRequest(
        string effect,
        string permissionLevel,
        string aclEntryId,
        string requestSchemaVersion = "v1")
    {
        HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/folders/folder-a/acl/{aclEntryId}")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                subjectRef = "user:user-a",
                permissionLevel,
                effect,
            }),
        };
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        return request;
    }

    private static WebApplication BuildApp(
        RecordingEventStoreGatewayClient gateway,
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
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
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

    private sealed class RecordingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        public List<SubmitCommandRequest> Requests { get; } = [];

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
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
