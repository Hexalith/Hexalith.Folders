using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ArchiveFolderEndpointTests
{
    [Fact]
    public async Task ArchiveFolderEndpointShouldSubmitExistingArchiveCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    archiveReasonCode = "caller_requested",
                }),
            };
            request.Headers.Add("Idempotency-Key", "idempotency-a");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            response.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-a");
            document.RootElement.GetProperty("taskId").GetString().ShouldBe("task-a");
            document.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
            document.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeFalse();

            gateway.Requests.Count.ShouldBe(1);
            SubmitCommandRequest submitted = gateway.Requests.Single();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe("Hexalith.Folders.Commands.ArchiveFolder");
            submitted.CorrelationId.ShouldBe("correlation-a");
            submitted.Extensions.ShouldNotBeNull();
            submitted.Extensions["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            submitted.Payload.GetProperty("archiveReasonCode").GetString().ShouldBe("caller_requested");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task ArchiveFolderEndpointShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();
            request.Headers.Remove(headerName);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldSurfaceIdempotentReplayFromGatewayPayload()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Response = new SubmitCommandResponse(
                "correlation-a",
                JsonSerializer.SerializeToElement(new { idempotentReplay = true })),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
            // Ensure the endpoint actually reached the gateway with the canonical archive
            // command; a future short-circuit returning replay without invoking the gateway
            // would otherwise pass this test silently.
            gateway.Requests.Count.ShouldBe(1);
            gateway.Requests.Single().CommandType.ShouldBe("Hexalith.Folders.Commands.ArchiveFolder");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(403, "tenant_access_denied", "denied_safe")]
    [InlineData(404, "not_found", "not_found")]
    [InlineData(409, "idempotency_conflict", "idempotency_conflict")]
    public async Task ArchiveFolderEndpointShouldMapGatewayRejectionsToContractSafeShapes(
        int gatewayStatus,
        string expectedCategory,
        string expectedCode)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                gatewayStatus,
                "Gateway rejection",
                type: "https://hexalith.dev/errors/internal-detail",
                detail: "folder folder-a rejected by internal gateway",
                correlationId: "correlation-gateway"),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe((HttpStatusCode)gatewayStatus);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe(expectedCategory);
            document.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode);
            document.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-gateway");
            json.ShouldNotContain("folder folder-a rejected");
            gateway.Requests.Count.ShouldBe(1);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldReturnSchemaVersionHintWithoutGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest(requestSchemaVersion: "V1");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("validation_error");
            document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            document.RootElement.GetProperty("message").GetString().ShouldBe("requestSchemaVersion must be exactly v1.");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldRejectMalformedIdempotencyKeyBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();
            request.Headers.Remove("Idempotency-Key");
            request.Headers.Add("Idempotency-Key", "idempotency key");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldReturnUnauthenticatedWhenTenantContextIsMissing()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, tenantId: null, principalId: null).ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("authentication_failure");
            document.RootElement.GetProperty("code").GetString().ShouldBe("authentication_failure");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task ArchiveFolderEndpointShouldRejectWhenRequiredEnvelopeHeaderIsMissing(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();
            request.Headers.Remove(headerName);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldRejectMalformedJsonBodyBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = new StringContent("{ this is not valid json", System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Idempotency-Key", "idempotency-a");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("validation_error");
            document.RootElement.GetProperty("code").GetString().ShouldBe("validation_error");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldRejectBodyWithUnknownFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    archiveReasonCode = "caller_requested",
                    smuggledTenantId = "tenant-b",
                }),
            };
            request.Headers.Add("Idempotency-Key", "idempotency-a");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveFolderEndpointShouldRejectBodyWithNullRequiredFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = (string?)null,
                    archiveReasonCode = (string?)null,
                }),
            };
            request.Headers.Add("Idempotency-Key", "idempotency-a");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(504)]
    public async Task ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable(int gatewayStatus)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                gatewayStatus,
                "Gateway upstream failure",
                type: "https://hexalith.dev/errors/internal-detail",
                detail: "internal gateway exception text",
                correlationId: "correlation-gateway"),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using HttpRequestMessage request = CreateValidArchiveRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            // Gateway 5xx must surface as safe 503 evidence_unavailable, NOT as 403
            // tenant_access_denied — the latter would actively mislead operators investigating
            // a backend incident.
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
            document.RootElement.GetProperty("code").GetString().ShouldBe("evidence_unavailable");
            document.RootElement.GetProperty("retryable").GetBoolean().ShouldBeTrue();
            json.ShouldNotContain("internal gateway exception text");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<WebApplication> StartAppAsync(
        RecordingEventStoreGatewayClient gateway,
        string? tenantId,
        string? principalId)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new FixedTenantContextAccessor(tenantId, principalId));

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return app;
    }

    private static HttpRequestMessage CreateValidArchiveRequest(string requestSchemaVersion = "v1")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                archiveReasonCode = "caller_requested",
            }),
        };
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        return request;
    }

    private sealed class FixedTenantContextAccessor(string? tenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
    }

    private sealed class RecordingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        public List<SubmitCommandRequest> Requests { get; } = [];

        public SubmitCommandResponse? Response { get; init; }

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

            return Task.FromResult(Response ?? new SubmitCommandResponse(request.CorrelationId ?? request.MessageId));
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
