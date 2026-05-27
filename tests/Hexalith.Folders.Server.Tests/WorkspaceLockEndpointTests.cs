using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
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

public sealed class WorkspaceLockEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterWorkspaceLockRoutes()
    {
        using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), LockReadModel());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove");
    }

    [Fact]
    public async Task GetWorkspaceLockShouldReturnContractShapedAuthorizedLockStatus()
    {
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("read_your_writes");
        document.RootElement.GetProperty("workspaceReference").GetProperty("value").GetString().ShouldBe("workspace-a");
        document.RootElement.GetProperty("lockState").GetString().ShouldBe("locked");
        document.RootElement.GetProperty("lease").GetProperty("lockId").GetString().ShouldBe("workspace_lock_a");
        document.RootElement.GetProperty("retryEligibility").GetProperty("reasonCode").GetString().ShouldBe("lock_active");
        json.ShouldNotContain("lockOwnershipProof", Case.Sensitive);
    }

    [Fact]
    public async Task GetWorkspaceLockShouldRejectIdempotencyKeyBeforeReadModelAccess()
    {
        CountingWorkspaceLockStatusReadModel readModel = new(LockReadModel());
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceLockShouldRejectUnsupportedFreshnessBeforeReadModelAccess()
    {
        CountingWorkspaceLockStatusReadModel readModel = new(LockReadModel());
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_read_consistency\"");
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/lock", "bad correlation", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/lock", "correlation-a", "bad task")]
    [InlineData("/api/v1/folders/folder a/workspaces/workspace-a/lock", "correlation-a", "task-a")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace a/lock", "correlation-a", "task-a")]
    public async Task GetWorkspaceLockShouldRejectMalformedIdentifiersBeforeReadModelAccess(
        string requestUri,
        string correlationId,
        string taskId)
    {
        CountingWorkspaceLockStatusReadModel readModel = new(LockReadModel());
        await using WebApplication app = BuildApp(new RecordingEventStoreGatewayClient(), readModel);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"validation_error\"");
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceLockShouldUseSafeDenialForUnauthenticatedCallerBeforeReadModelAccess()
    {
        CountingWorkspaceLockStatusReadModel readModel = new(LockReadModel());
        await using WebApplication app = BuildApp(
            new RecordingEventStoreGatewayClient(),
            readModel,
            tenantId: null,
            principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        json.ShouldNotContain("workspace_lock_a", Case.Sensitive);
        response.Headers.Contains("X-Hexalith-Freshness").ShouldBeFalse();
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkspaceLockShouldReturnExpiredLeaseWithoutMutatingState()
    {
        await using WebApplication app = BuildApp(
            new RecordingEventStoreGatewayClient(),
            LockReadModel(expiresAt: Now.AddSeconds(-1)));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        document.RootElement.GetProperty("lockState").GetString().ShouldBe("expired");
        document.RootElement.GetProperty("lease").GetProperty("leaseStatus").GetString().ShouldBe("expired");
        document.RootElement.GetProperty("retryEligibility").GetProperty("retryable").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("retryEligibility").GetProperty("reasonCode").GetString().ShouldBe("lock_conflict_retry");
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldSubmitRouteAuthoritativeWorkspacePayload()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.ReleaseWorkspaceLockCommandType);
        submitted.AggregateId.ShouldBe("folder-a");
        submitted.MessageId.ShouldBe("idempotency-release-a");
        submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        submitted.Payload.GetProperty("lockId").GetString().ShouldBe("workspace_lock_a");
        submitted.Payload.GetProperty("releaseReasonCode").GetString().ShouldBe("caller_completed");
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task ReleaseWorkspaceLockShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest();
        request.Headers.Remove(headerName);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest(requestSchemaVersion: "v2");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"unsupported_request_schema_version\"");
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock/release")
        {
            Content = new StringContent("{\"requestSchemaVersion\":\"v1\",", Encoding.UTF8, "application/json"),
        };
        AddReleaseHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "lock_proof_a", "caller_completed")]
    [InlineData("workspace_lock_a", "lock proof unsafe", "caller_completed")]
    [InlineData("workspace_lock_a", "lock_proof_a", "unsafe_reason")]
    public async Task ReleaseWorkspaceLockShouldRejectInvalidBodyBeforeGatewaySubmit(
        string lockId,
        string lockOwnershipProof,
        string releaseReasonCode)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest(
            lockId: lockId,
            lockOwnershipProof: lockOwnershipProof,
            releaseReasonCode: releaseReasonCode);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldRejectUnknownFieldsWithoutLeakingProof()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock/release")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                lockId = "workspace_lock_a",
                lockOwnershipProof = "lock_proof_do_not_echo",
                releaseReasonCode = "caller_completed",
                repositoryUrl = "https://provider.example.test/owner/repository-secret",
            }),
        };
        AddReleaseHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldNotContain("lock_proof_do_not_echo", Case.Sensitive);
        json.ShouldNotContain("repository-secret", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldRejectMissingAuthenticationBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel(), tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("workspace_lock_a", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseWorkspaceLockShouldNormalizeLockNotOwnedGatewayReason()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                StatusCodes.Status409Conflict,
                "proof mismatch lock_proof_do_not_echo",
                correlationId: "correlation-gateway",
                reasonCode: "lock-not-owned"),
        };
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest(lockOwnershipProof: "lock_proof_do_not_echo");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        json.ShouldContain("\"category\":\"lock_not_owned\"");
        json.ShouldNotContain("lock_proof_do_not_echo", Case.Sensitive);
    }

    [Theory]
    [InlineData(StatusCodes.Status409Conflict, null, "idempotency_conflict", HttpStatusCode.Conflict)]
    [InlineData(StatusCodes.Status410Gone, "lock-expired", "lock_expired", HttpStatusCode.Gone)]
    [InlineData(StatusCodes.Status422UnprocessableEntity, "workspace-transition-invalid", "workspace_transition_invalid", (HttpStatusCode)422)]
    [InlineData(StatusCodes.Status409Conflict, "reconciliation-required", "reconciliation_required", HttpStatusCode.Conflict)]
    public async Task ReleaseWorkspaceLockShouldNormalizeGatewayReasonCodes(
        int statusCode,
        string? gatewayReason,
        string expectedCategory,
        HttpStatusCode expectedStatus)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                statusCode,
                "safe gateway rejection",
                correlationId: "correlation-gateway",
                reasonCode: gatewayReason),
        };
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidReleaseRequest();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        json.ShouldContain($"\"category\":\"{expectedCategory}\"");
    }

    [Fact]
    public async Task AddWorkspaceFileShouldSubmitRouteAuthoritativeWorkspacePayload()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.MutateFilesCommandType);
        submitted.AggregateId.ShouldBe("folder-a");
        submitted.MessageId.ShouldBe("idempotency-file-a");
        submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        submitted.Payload.GetProperty("fileOperationKind").GetString().ShouldBe("add");
        submitted.Payload.GetProperty("pathMetadata").GetProperty("normalizedPath").GetString().ShouldBe("docs/readme.md");
    }

    [Theory]
    [InlineData("POST", "/api/v1/folders/folder-a/workspaces/workspace-a/files/add", "add", "PutFileInline", "hashref-a", 12L)]
    [InlineData("PUT", "/api/v1/folders/folder-a/workspaces/workspace-a/files/change", "change", "PutFileStream", "hashref-change-a", 262145L)]
    [InlineData("POST", "/api/v1/folders/folder-a/workspaces/workspace-a/files/remove", "remove", "metadataOnlyRemoval", null, null)]
    public async Task FileMutationShouldSubmitSupportedRoutePayloads(
        string method,
        string uri,
        string fileOperationKind,
        string transportOperation,
        string? contentHashReference,
        long? byteLength)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(fileOperationKind);
        ArgumentNullException.ThrowIfNull(transportOperation);

        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            new HttpMethod(method),
            uri,
            fileOperationKind,
            transportOperation: transportOperation,
            contentHashReference: contentHashReference,
            byteLength: byteLength);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.MutateFilesCommandType);
        submitted.AggregateId.ShouldBe("folder-a");
        submitted.MessageId.ShouldBe("idempotency-file-a");
        submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        submitted.Payload.GetProperty("fileOperationKind").GetString().ShouldBe(fileOperationKind);
        submitted.Payload.GetProperty("transportOperation").GetString().ShouldBe(transportOperation);
        submitted.Payload.GetProperty("pathMetadata").GetProperty("normalizedPath").GetString().ShouldBe("docs/readme.md");

        if (contentHashReference is null)
        {
            submitted.Payload.TryGetProperty("contentHashReference", out _).ShouldBeFalse();
        }
        else
        {
            JsonElement contentHash = submitted.Payload.GetProperty("contentHashReference");
            contentHash.GetString().ShouldBe(contentHashReference);
        }

        if (byteLength is null)
        {
            submitted.Payload.TryGetProperty("byteLength", out _).ShouldBeFalse();
        }
        else
        {
            JsonElement submittedByteLength = submitted.Payload.GetProperty("byteLength");
            submittedByteLength.GetInt64().ShouldBe(byteLength.Value);
        }
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-Id")]
    [InlineData("X-Hexalith-Task-Id")]
    public async Task FileMutationShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add");
        request.Headers.Remove(headerName);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileMutationShouldRejectUnauthenticatedCallerBeforeGatewaySubmitWithoutPathEcho()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(
            gateway,
            LockReadModel(),
            tenantId: null,
            principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add",
            normalizedPath: "../secret.txt");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"category\":\"authentication_failure\"");
        json.ShouldNotContain("../secret.txt", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileMutationShouldRejectPathPolicyDenialBeforeGatewaySubmitWithoutPathEcho()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add",
            normalizedPath: "../secret.txt");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"category\":\"path_validation_failed\"");
        json.ShouldNotContain("../secret.txt", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileMutationShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/files/add")
        {
            Content = new StringContent("{\"requestSchemaVersion\":\"v1\",", Encoding.UTF8, "application/json"),
        };
        AddFileMutationHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileMutationShouldRejectUnknownFieldsWithoutUnsafePathEcho()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add");
        request.Content = JsonContent.Create(new
        {
            requestSchemaVersion = "v1",
            operationId = "operation-a",
            fileOperationKind = "add",
            transportOperation = "PutFileInline",
            pathMetadata = new
            {
                normalizedPath = "docs/readme.md",
                displayName = "readme.md",
                pathPolicyClass = "tenant_sensitive_document",
                unicodeNormalization = "NFC",
            },
            contentHashReference = "hashref-a",
            byteLength = 12,
            providerPayload = "secret-provider-payload",
        });

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldNotContain("secret-provider-payload", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/files/add", "change")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/files/change", "remove")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace-a/files/remove", "add")]
    public async Task FileMutationShouldRejectOperationKindRouteMismatchBeforeGatewaySubmit(
        string uri,
        string fileOperationKind)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(fileOperationKind);

        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            uri.Contains("/change", StringComparison.Ordinal) ? HttpMethod.Put : HttpMethod.Post,
            uri,
            fileOperationKind);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileMutationShouldNormalizePathPolicyGatewayReasonWithoutUnsafePathEcho()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                StatusCodes.Status422UnprocessableEntity,
                "unsafe path ../secret.txt",
                correlationId: "correlation-gateway",
                reasonCode: "PathPolicyDenied"),
        };
        await using WebApplication app = BuildApp(gateway, LockReadModel());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidFileMutationRequest(
            HttpMethod.Post,
            "/api/v1/folders/folder-a/workspaces/workspace-a/files/add",
            "add");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        json.ShouldContain("\"category\":\"path_policy_denied\"");
        json.ShouldNotContain("../secret.txt", Case.Sensitive);
    }

    private static WebApplication BuildApp(
        RecordingEventStoreGatewayClient gateway,
        IWorkspaceLockStatusReadModel lockReadModel,
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
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
        builder.Services.RemoveAll<IWorkspaceLockStatusReadModel>();
        builder.Services.AddSingleton(lockReadModel);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage CreateValidReleaseRequest(
        string requestSchemaVersion = "v1",
        string lockId = "workspace_lock_a",
        string lockOwnershipProof = "lock_proof_a",
        string releaseReasonCode = "caller_completed")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock/release")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                lockId,
                lockOwnershipProof,
                releaseReasonCode,
            }),
        };
        AddReleaseHeaders(request);
        return request;
    }

    private static void AddReleaseHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Idempotency-Key", "idempotency-release-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
    }

    private static HttpRequestMessage CreateValidFileMutationRequest(
        HttpMethod method,
        string uri,
        string fileOperationKind,
        string normalizedPath = "docs/readme.md",
        string? transportOperation = null,
        string? contentHashReference = null,
        long? byteLength = null)
    {
        transportOperation ??= fileOperationKind == "remove" ? "metadataOnlyRemoval" : "PutFileInline";
        contentHashReference ??= fileOperationKind == "remove" ? null : "hashref-a";
        byteLength ??= fileOperationKind == "remove" ? null : 12;
        HttpRequestMessage request = new(method, uri)
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                operationId = "operation-a",
                fileOperationKind,
                transportOperation,
                pathMetadata = new
                {
                    normalizedPath,
                    displayName = "readme.md",
                    pathPolicyClass = "tenant_sensitive_document",
                    unicodeNormalization = "NFC",
                },
                contentHashReference,
                byteLength,
                inlineContent = fileOperationKind == "remove"
                    ? null
                    : new
                    {
                        mediaType = "text/plain",
                        contentBytes = "aGVsbG8=",
                    },
            }),
        };
        request.Headers.Add("Idempotency-Key", "idempotency-file-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        return request;
    }

    private static void AddFileMutationHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Idempotency-Key", "idempotency-file-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
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
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"), "read_workspace_lock", Sequence: 1, EffectiveAt: Now),
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"), "lock_workspace", Sequence: 2, EffectiveAt: Now),
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"), "mutate_files", Sequence: 3, EffectiveAt: Now),
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

    private static InMemoryWorkspaceLockStatusReadModel LockReadModel(DateTimeOffset? expiresAt = null)
    {
        InMemoryWorkspaceLockStatusReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(new WorkspaceLockStatusReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            WorkspaceState: "locked",
            LockState: "locked",
            LockId: "workspace_lock_a",
            HolderTaskId: "task-a",
            AcquiredAt: Now.AddMinutes(-5),
            EffectiveAt: Now.AddMinutes(-5),
            ExpiresAt: expiresAt ?? Now.AddMinutes(55),
            RetryEligibilityBasis: "lease_until_expiry",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            Freshness: new FolderLifecycleFreshness("read_your_writes", Now, "lock_watermark_v1", Stale: false, ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: "tenant-a",
                PrincipalId: "user-a",
                ActionToken: "read_workspace_lock",
                TaskId: "task-a",
                CorrelationId: "correlation-a",
                AuthorizationWatermark: "permission_watermark_v1")));
        return readModel;
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

    private sealed class CountingWorkspaceLockStatusReadModel(IWorkspaceLockStatusReadModel inner) : IWorkspaceLockStatusReadModel
    {
        public int Calls { get; private set; }

        public Task<WorkspaceLockStatusReadModelResult> GetAsync(
            WorkspaceLockStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return inner.GetAsync(request, cancellationToken);
        }
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
