using System.Net;
using System.Net.Http.Json;

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

public sealed class ConfigureProviderBindingEndpointTests
{
    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterConfigureProviderBindingRoute()
    {
        using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/provider-bindings/{providerBindingRef}");
    }

    [Fact]
    public async Task ConfigureProviderBindingShouldSubmitCommandWithBindingRefAggregateId()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.ConfigureProviderBindingCommandType);
        submitted.AggregateId.ShouldBe("binding-a");
        submitted.MessageId.ShouldBe("idempotency-a");
        submitted.Payload.GetProperty("providerBindingRef").GetString().ShouldBe("binding-a");
        submitted.Payload.GetProperty("providerFamilyRef").GetString().ShouldBe("github");
        submitted.Payload.GetProperty("capabilityProfileRef").GetString().ShouldBe("profile-a");
        submitted.Payload.GetProperty("nonSecretCredentialReference").GetString().ShouldBe("credential-ref-a");
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task ConfigureProviderBindingShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
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
    public async Task ConfigureProviderBindingShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
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
    public async Task ConfigureProviderBindingShouldRejectMissingProviderFamilyBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/provider-bindings/binding-a")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                capabilityProfileRef = "profile-a",
                nonSecretCredentialReference = "credential-ref-a",
            }),
        };
        AddHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureProviderBindingShouldRejectUnauthenticatedCallerBeforeGatewaySubmit()
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
        json.ShouldNotContain("binding-a", Case.Sensitive);
        json.ShouldNotContain("github", Case.Sensitive);
        json.ShouldNotContain("credential-ref-a", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfigureProviderBindingShouldMapUnexpectedGatewayFailureToProviderUnavailable()
    {
        await using WebApplication app = BuildApp(new ThrowingEventStoreGatewayClient());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        json.ShouldContain("\"category\":\"provider_unavailable\"");
        json.ShouldContain("\"code\":\"provider_unavailable\"");
        json.ShouldNotContain("credential-ref-a", Case.Sensitive);
    }

    private static HttpRequestMessage CreateValidRequest(string requestSchemaVersion = "v1")
    {
        HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/provider-bindings/binding-a")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                providerFamilyRef = "github",
                capabilityProfileRef = "profile-a",
                nonSecretCredentialReference = "credential-ref-a",
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
        IEventStoreGatewayClient gateway,
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

    // Gateway double whose command submit fails with an unexpected (non-gateway, non-cancellation)
    // exception, exercising the route's generic catch → 503 provider_unavailable mapping (AC1 op4 503 leg).
    private sealed class ThrowingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated unexpected gateway failure");

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
