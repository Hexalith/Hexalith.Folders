using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
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

    [Fact]
    public async Task ArchiveFolderEndpointShouldRejectMissingIdempotencyKeyBeforeGatewaySubmit()
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
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new FixedTenantContextAccessor(tenantId, principalId));

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return app;
    }

    private sealed class FixedTenantContextAccessor(string? tenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
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
