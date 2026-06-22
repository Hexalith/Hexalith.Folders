using System.Net;
using System.Net.Http.Json;
using System.Text;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
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

public sealed class CreateFolderEndpointTests
{
    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterCreateFolderRoute()
    {
        using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders");
    }

    [Fact]
    public async Task CreateFolderShouldSubmitCreateCommandWithServerDerivedFolderId()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.CreateFolderCommandType);
        submitted.MessageId.ShouldBe("idempotency-a");
        submitted.AggregateId.ShouldStartWith("fld-");
        submitted.Payload.GetProperty("folderId").GetString().ShouldBe(submitted.AggregateId);
        submitted.Payload.GetProperty("folderMetadata").GetProperty("displayName").GetString().ShouldBe("My Folder");
        submitted.Payload.GetProperty("parentFolderId").GetString().ShouldBe("parent-a");
    }

    [Fact]
    public async Task CreateFolderShouldDeriveSameFolderIdForSameIdempotencyKey()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage first = await client.SendAsync(CreateValidRequest(), TestContext.Current.CancellationToken);
        using HttpResponseMessage second = await client.SendAsync(CreateValidRequest(), TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        gateway.Requests.Count.ShouldBe(2);
        gateway.Requests[0].AggregateId.ShouldBe(gateway.Requests[1].AggregateId);
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task CreateFolderShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest();
        request.Headers.Remove(headerName);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateFolderShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest(requestSchemaVersion: "v2");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_request_schema_version\"");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateFolderShouldRejectMissingDisplayNameBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                parentFolderId = "parent-a",
                folderMetadata = new { metadataClass = "tenant_sensitive" },
            }),
        };
        AddHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateFolderShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders")
        {
            Content = new StringContent("{\"requestSchemaVersion\":\"v1\",", Encoding.UTF8, "application/json"),
        };
        AddHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateFolderShouldRejectUnauthenticatedCallerBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("parent-a", Case.Sensitive);
        json.ShouldNotContain("My Folder", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    private static HttpRequestMessage CreateValidRequest(string requestSchemaVersion = "v1")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                parentFolderId = "parent-a",
                folderMetadata = new
                {
                    displayName = "My Folder",
                    metadataClass = "tenant_sensitive",
                },
            }),
        };
        AddHeaders(request);
        return request;
    }

    private static void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
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
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private sealed class StaticTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
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
