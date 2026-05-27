using System.Collections.Concurrent;
using System.Net;

using Hexalith.Folders.Observability;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderAuditEndpointFilterTests
{
    [Fact]
    public async Task QueryObservationShouldCarryScopedMetadataWithoutLeakingUnsafeHeaderValues()
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapGet(
                "/api/v1/folders/{folderId}/workspaces/{workspaceId}/status",
                static () => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable))
                .WithName("GetWorkspaceStatus")
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/status");
        request.Headers.Add("X-Correlation-Id", "synthetic-token-secret-value");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");

        HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        FolderAuditObservation observation = telemetry.Single();
        observation.OperationKind.ShouldBe(FolderAuditOperationKind.RestQuery);
        observation.Result.ShouldBe(FolderAuditResult.Unavailable);
        observation.TenantId.ShouldBe("tenant-a");
        observation.ActorReference.ShouldBe("principal-a");
        observation.TaskId.ShouldBe("task-a");
        observation.CorrelationId.ShouldBeNull();
        observation.FolderId.ShouldBe("folder-a");
        observation.WorkspaceId.ShouldBe("workspace-a");
        observation.SanitizedCategory.ShouldBe("projection_unavailable");
        observation.ToString().ShouldNotContain("synthetic-token-secret-value", Case.Sensitive);
    }

    [Theory]
    [InlineData("ValidateProviderReadiness", FolderAuditOperationKind.ProviderReadiness)]
    [InlineData("AddWorkspaceFile", FolderAuditOperationKind.FileOperation)]
    [InlineData("SearchFolderFiles", FolderAuditOperationKind.ContextQuery)]
    public async Task EndpointNamesShouldMapToStableOperationKinds(string endpointName, FolderAuditOperationKind expectedKind)
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapPost("/operation/{operationId}", static () => Results.Accepted())
                .WithName(endpointName)
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpRequestMessage request = new(HttpMethod.Post, "/operation/operation-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Retry", "true");

        HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        FolderAuditObservation observation = telemetry.Single();
        observation.OperationKind.ShouldBe(expectedKind);
        observation.Result.ShouldBe(FolderAuditResult.Success);
        observation.OperationId.ShouldBe("operation-a");
        observation.IsRetry.ShouldBeTrue();
        observation.Classifications["endpoint.name"].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRouteShouldLetDomainProcessorOwnSuccessfulCommandObservations()
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapPost(FoldersServerModule.ProcessRoute, () => Results.Ok(new { idempotentReplay = true }))
                .WithName("ProcessFolderDomainCommand")
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpResponseMessage response = await app.GetTestClient()
            .PostAsync(FoldersServerModule.ProcessRoute, null, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        telemetry.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, FolderAuditResult.Rejected, "validation_error")]
    [InlineData(StatusCodes.Status403Forbidden, FolderAuditResult.Denied, "authorization_denied")]
    public async Task ProcessRouteShouldEmitTransportRejectionObservationsWhenProcessorDoesNotRun(
        int statusCode,
        FolderAuditResult expectedResult,
        string expectedCategory)
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapPost(FoldersServerModule.ProcessRoute, () => Results.StatusCode(statusCode))
                .WithName("ProcessFolderDomainCommand")
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpRequestMessage request = new(HttpMethod.Post, FoldersServerModule.ProcessRoute);
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Operation-Id", "operation-a");

        HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ((int)response.StatusCode).ShouldBe(statusCode);
        FolderAuditObservation observation = telemetry.Single();
        observation.OperationKind.ShouldBe(FolderAuditOperationKind.ProcessCommand);
        observation.Result.ShouldBe(expectedResult);
        observation.SanitizedCategory.ShouldBe(expectedCategory);
        observation.OperationId.ShouldBe("operation-a");
        observation.CorrelationId.ShouldBe("correlation-a");
        observation.TaskId.ShouldBe("task-a");
    }

    [Fact]
    public async Task ProviderReadinessObservationShouldCarrySafeProviderReferenceFromResponse()
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapPost(
                "/api/v1/provider-readiness/validations",
                static () => Results.Json(new { providerReference = "provider-a" }, statusCode: StatusCodes.Status200OK))
                .WithName("ValidateProviderReadiness")
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpResponseMessage response = await app.GetTestClient()
            .PostAsync("/api/v1/provider-readiness/validations", null, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        FolderAuditObservation observation = telemetry.Single();
        observation.OperationKind.ShouldBe(FolderAuditOperationKind.ProviderReadiness);
        observation.ProviderReference.ShouldBe("provider-a");
    }

    [Fact]
    public async Task AcceptedReplayResponseShouldEmitIdempotentReplayObservation()
    {
        RecordingTelemetryEmitter telemetry = new();
        await using WebApplication app = BuildApp(telemetry, app =>
        {
            app.MapPost(
                "/api/v1/folders/{folderId}/archive",
                static () => Results.Json(new { idempotentReplay = true }, statusCode: StatusCodes.Status202Accepted))
                .WithName("ArchiveFolder")
                .AddEndpointFilter<FolderAuditEndpointFilter>();
        });

        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Operation-Id", "operation-a");

        HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        FolderAuditObservation observation = telemetry.Single();
        observation.OperationKind.ShouldBe(FolderAuditOperationKind.RestMutation);
        observation.Result.ShouldBe(FolderAuditResult.Replayed);
        observation.SanitizedCategory.ShouldBe("idempotent_replay");
        observation.IsIdempotentReplay.ShouldBeTrue();
        observation.IsDuplicate.ShouldBeFalse();
        observation.OperationId.ShouldBe("operation-a");
        observation.CorrelationId.ShouldBe("correlation-a");
        observation.TaskId.ShouldBe("task-a");
    }

    private static WebApplication BuildApp(
        IFolderTelemetryEmitter telemetry,
        Action<WebApplication> map)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(telemetry);
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "principal-a"));

        WebApplication app = builder.Build();
        map(app);
        app.StartAsync().GetAwaiter().GetResult();
        return app;
    }

    private sealed class RecordingTelemetryEmitter : IFolderTelemetryEmitter
    {
        private readonly ConcurrentQueue<FolderAuditObservation> _observations = new();

        public ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
        {
            _observations.Enqueue(observation);
            return ValueTask.CompletedTask;
        }

        public int Count => _observations.Count;

        public FolderAuditObservation Single()
            => _observations.Single();
    }

    private sealed class StaticTenantContextAccessor(string tenantId, string principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
    }
}
