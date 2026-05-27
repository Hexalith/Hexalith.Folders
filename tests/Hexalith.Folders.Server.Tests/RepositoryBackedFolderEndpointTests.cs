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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class RepositoryBackedFolderEndpointTests
{
    [Fact]
    public async Task ConfigureBranchRefPolicyEndpointShouldSubmitCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBranchRefPolicyRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe(FoldersServerModule.ConfigureBranchRefPolicyCommandType);
            submitted.Extensions.ShouldNotBeNull()["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            submitted.Payload.GetProperty("repositoryBindingId").GetString().ShouldBe("repository-binding-a");
            submitted.Payload.GetProperty("policyRef").GetString().ShouldBe("opaque-policy-a");
            submitted.Payload.GetProperty("defaultRef").GetString().ShouldBe("branch_ref_primary");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceEndpointShouldSubmitCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidPrepareWorkspaceRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            response.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");

            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe(FoldersServerModule.PrepareWorkspaceCommandType);
            submitted.Extensions.ShouldNotBeNull()["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
            submitted.Payload.GetProperty("repositoryBindingId").GetString().ShouldBe("repository-binding-a");
            submitted.Payload.GetProperty("branchRefPolicyRef").GetString().ShouldBe("branch-ref-policy-a");
            submitted.Payload.GetProperty("workspacePolicyRef").GetString().ShouldBe("workspace-policy-a");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceEndpointShouldUseRouteWorkspaceIdAsAuthoritative()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-route/preparation")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "repository-binding-a",
                    branchRefPolicyRef = "branch-ref-policy-a",
                    workspacePolicyRef = "workspace-policy-a",
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-route");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LockWorkspaceEndpointShouldSubmitCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidLockWorkspaceRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            response.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");

            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe(FoldersServerModule.LockWorkspaceCommandType);
            submitted.Extensions.ShouldNotBeNull()["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
            submitted.Payload.GetProperty("lockIntent").GetString().ShouldBe("exclusive_write");
            submitted.Payload.GetProperty("requestedLeaseSeconds").GetInt32().ShouldBe(3600);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LockWorkspaceEndpointShouldUseRouteWorkspaceIdAsAuthoritative()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-route/lock")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 3600,
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-route");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LockWorkspaceEndpointShouldUseAuthenticatedTenantDespiteClientTenantHeaders()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidLockWorkspaceRequest();
            request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-b");
            request.Headers.Add("X-Tenant-Id", "tenant-b");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
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
    public async Task LockWorkspaceEndpointShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidLockWorkspaceRequest();
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

    [Theory]
    [InlineData("/api/v1/folders/Folder-A/workspaces/workspace-a/lock")]
    [InlineData("/api/v1/folders/folder-a/workspaces/workspace%40secret/lock")]
    public async Task LockWorkspaceEndpointShouldRejectInvalidRouteIdentifiersBeforeGatewaySubmit(string route)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, route)
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 3600,
                }),
            };
            AddHeaders(request);

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
    [InlineData("v2", "exclusive_write", 3600, "unsupported_request_schema_version")]
    [InlineData("v1", "shared_read", 3600, "validation_error")]
    [InlineData("v1", "exclusive_write", 0, "validation_error")]
    [InlineData("v1", "exclusive_write", 86401, "validation_error")]
    public async Task LockWorkspaceEndpointShouldRejectInvalidBodyBeforeGatewaySubmit(
        string requestSchemaVersion,
        string lockIntent,
        int requestedLeaseSeconds,
        string expectedCode)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidLockWorkspaceRequest(
                requestSchemaVersion,
                lockIntent,
                requestedLeaseSeconds);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LockWorkspaceEndpointShouldRejectUnknownFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 3600,
                    repositoryUrl = "https://provider.example.test/owner/repository-secret",
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            json.ShouldNotContain("repository-secret", Case.Sensitive);
            json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LockWorkspaceEndpointShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

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
    public async Task LockWorkspaceEndpointShouldRejectReservedSystemTenantBeforeBodyParsing()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "system", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("lock_conflict", "lock_conflict", HttpStatusCode.Conflict)]
    [InlineData("duplicate-workspace-lock-rejected", "lock_conflict", HttpStatusCode.Conflict)]
    [InlineData("workspace_locked", "workspace_locked", HttpStatusCode.Conflict)]
    [InlineData("workspace-already-locked-rejected", "workspace_locked", HttpStatusCode.Conflict)]
    [InlineData("workspace_transition_invalid", "workspace_transition_invalid", HttpStatusCode.UnprocessableEntity)]
    [InlineData("workspace-transition-invalid-rejected", "workspace_transition_invalid", HttpStatusCode.UnprocessableEntity)]
    public async Task LockWorkspaceEndpointShouldMapGatewayFailuresToSafeProblems(
        string reasonCode,
        string expectedCategory,
        HttpStatusCode expectedStatusCode)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                expectedStatusCode == HttpStatusCode.UnprocessableEntity
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status409Conflict,
                "lock failure for https://provider.example.test/owner/repository-secret",
                correlationId: "correlation-gateway",
                reasonCode: reasonCode),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidLockWorkspaceRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(expectedStatusCode);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe(expectedCategory);
            document.RootElement.GetProperty("code").GetString().ShouldBe(expectedCategory);
            json.ShouldNotContain("repository-secret", Case.Sensitive);
            json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceEndpointShouldUseAuthenticatedTenantDespiteClientTenantHeaders()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidPrepareWorkspaceRequest();
            request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-b");
            request.Headers.Add("X-Tenant-Id", "tenant-b");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
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
    public async Task PrepareWorkspaceEndpointShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidPrepareWorkspaceRequest();
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
    public async Task PrepareWorkspaceEndpointShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidPrepareWorkspaceRequest(requestSchemaVersion: "v2");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceEndpointShouldRejectInvalidPayloadBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    branchRefPolicyRef = "branch-ref-policy-a",
                    workspacePolicyRef = "workspace-policy-a",
                }),
            };
            AddHeaders(request);

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
    public async Task PrepareWorkspaceEndpointShouldRejectUnknownFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "repository-binding-a",
                    branchRefPolicyRef = "branch-ref-policy-a",
                    workspacePolicyRef = "workspace-policy-a",
                    repositoryUrl = "https://provider.example.test/owner/repository-secret",
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            json.ShouldNotContain("repository-secret", Case.Sensitive);
            json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceEndpointShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

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
    public async Task PrepareWorkspaceEndpointShouldRejectReservedSystemTenantBeforeBodyParsing()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "system", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(StatusCodes.Status409Conflict, HttpStatusCode.Conflict, "idempotency_conflict", "idempotency_conflict")]
    [InlineData(StatusCodes.Status409Conflict, HttpStatusCode.Conflict, "reconciliation_required", "reconciliation_required")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, HttpStatusCode.UnprocessableEntity, "provider_readiness_failed", "provider_readiness_failed")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, HttpStatusCode.UnprocessableEntity, "workspace_transition_invalid", "workspace_transition_invalid")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, HttpStatusCode.UnprocessableEntity, "workspace_preparation_failed", "workspace_preparation_failed")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, HttpStatusCode.ServiceUnavailable, "provider_unavailable", "provider_unavailable")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, HttpStatusCode.ServiceUnavailable, "unknown_provider_outcome", "unknown_provider_outcome")]
    public async Task PrepareWorkspaceEndpointShouldMapGatewayFailuresToSafeProblems(
        int gatewayStatus,
        HttpStatusCode expectedStatus,
        string expectedCategory,
        string expectedCode)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                gatewayStatus,
                "workspace failure for https://provider.example.test/owner/repository-secret",
                correlationId: "correlation-gateway",
                reasonCode: expectedCode),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidPrepareWorkspaceRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(expectedStatus);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe(expectedCategory);
            document.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode);
            document.RootElement.GetProperty("retryable").GetBoolean().ShouldBe(expectedCategory == "provider_unavailable");
            document.RootElement.GetProperty("clientAction").GetString().ShouldBe(expectedCategory switch
            {
                "unknown_provider_outcome" or "reconciliation_required" => "wait_for_reconciliation",
                "provider_readiness_failed" => "contact_operator",
                "workspace_preparation_failed" or "workspace_transition_invalid" => "revise_request",
                "provider_unavailable" => "retry",
                _ => "no_action",
            });
            json.ShouldNotContain("repository-secret", Case.Sensitive);
            json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task GetBranchRefPolicyEndpointShouldRejectIdempotencyKeyBeforeReadModel()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/branch-ref-policy");
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("code").GetString().ShouldBe("idempotency_key_not_allowed");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task BindRepositoryEndpointShouldSubmitBindRepositoryCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBindRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            response.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");

            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe(FoldersServerModule.BindRepositoryCommandType);
            submitted.Extensions.ShouldNotBeNull();
            submitted.Extensions["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            submitted.Payload.GetProperty("providerBindingRef").GetString().ShouldBe("provider-binding-a");
            submitted.Payload.GetProperty("externalRepositoryRef").GetString().ShouldBe("external-repository-a");
            submitted.Payload.GetProperty("branchRefPolicy").GetProperty("policyRef").GetString().ShouldBe("branch_ref_policy_a");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task BindRepositoryEndpointShouldRejectUnknownFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/repository-bindings")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    providerBindingRef = "provider-binding-a",
                    externalRepositoryRef = "external-repository-a",
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v1",
                        repositoryBindingId = "binding-a",
                        policyRef = "branch_ref_policy_a",
                        defaultRef = "branch_ref_primary",
                        allowedRefPatterns = new[] { "branch_ref_feature" },
                    },
                    repositoryUrl = "https://provider.example.test/owner/repo-secret",
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            json.ShouldNotContain("repo-secret", Case.Sensitive);
            gateway.Requests.ShouldBeEmpty();
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
    public async Task BindRepositoryEndpointShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBindRequest();
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
    public async Task BindRepositoryEndpointShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBindRequest(requestSchemaVersion: "v2");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("missing_default_ref")]
    [InlineData("empty_allowed_ref_patterns")]
    [InlineData("invalid_branch_ref_pattern")]
    public async Task BindRepositoryEndpointShouldRejectInvalidBranchRefPolicyBeforeGatewaySubmit(string scenario)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            object branchRefPolicy = scenario switch
            {
                "missing_default_ref" => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
                "empty_allowed_ref_patterns" => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = Array.Empty<string>(),
                },
                _ => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "opaque-policy-a",
                    defaultRef = "feature/raw",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            };

            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBindRequest(branchRefPolicy: branchRefPolicy);

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
    public async Task BindRepositoryEndpointShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/repository-bindings")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

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
    public async Task BindRepositoryEndpointShouldRejectReservedSystemTenantBeforeBodyParsing()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "system", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/repository-bindings")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(StatusCodes.Status409Conflict, HttpStatusCode.Conflict, "idempotency_conflict", "idempotency_conflict")]
    [InlineData(StatusCodes.Status409Conflict, HttpStatusCode.Conflict, "duplicate_binding", "duplicate_binding")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, HttpStatusCode.UnprocessableEntity, "provider_readiness_failed", "provider_readiness_failed")]
    [InlineData(StatusCodes.Status429TooManyRequests, HttpStatusCode.TooManyRequests, "provider_rate_limited", "provider_rate_limited")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, HttpStatusCode.ServiceUnavailable, "provider_unavailable", "provider_unavailable")]
    public async Task BindRepositoryEndpointShouldMapGatewayFailuresToSafeProblems(
        int gatewayStatus,
        HttpStatusCode expectedStatus,
        string expectedCategory,
        string expectedCode)
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                gatewayStatus,
                "provider failure for https://provider.example.test/owner/repository-secret",
                correlationId: "correlation-gateway",
                reasonCode: expectedCode),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidBindRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(expectedStatus);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe(expectedCategory);
            document.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode);
            json.ShouldNotContain("repository-secret", Case.Sensitive);
            json.ShouldNotContain("https://provider.example.test", Case.Sensitive);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderEndpointShouldSubmitCreateRepositoryCommandAndReturnAcceptedShape()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation-a");
            response.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe("task-a");
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-a");
            document.RootElement.GetProperty("taskId").GetString().ShouldBe("task-a");
            document.RootElement.GetProperty("status").GetString().ShouldBe("accepted");

            SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
            submitted.MessageId.ShouldBe("idempotency-a");
            submitted.Tenant.ShouldBe("tenant-a");
            submitted.Domain.ShouldBe("folders");
            submitted.AggregateId.ShouldBe("folder-a");
            submitted.CommandType.ShouldBe(FoldersServerModule.CreateRepositoryBackedFolderCommandType);
            submitted.Extensions.ShouldNotBeNull();
            submitted.Extensions["taskId"].ShouldBe("task-a");
            submitted.Payload.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            submitted.Payload.GetProperty("folderId").GetString().ShouldBe("folder-a");
            submitted.Payload.GetProperty("providerBindingRef").GetString().ShouldBe("provider-binding-a");
            submitted.Payload.GetProperty("repositoryBindingId").GetString().ShouldBe("binding-a");
            submitted.Payload.GetProperty("branchRefPolicy").GetProperty("policyRef").GetString().ShouldBe("branch_ref_policy_a");
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
    public async Task RepositoryBackedFolderEndpointShouldRejectMissingRequiredHeadersBeforeGatewaySubmit(string headerName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest();
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
    public async Task RepositoryBackedFolderEndpointShouldRejectUnknownFieldsBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    folderId = "folder-a",
                    providerBindingRef = "provider-binding-a",
                    repositoryProfileRef = "profile-a",
                    folderMetadata = new
                    {
                        displayName = "Folder A",
                        metadataClass = "tenant_sensitive",
                    },
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v1",
                        repositoryBindingId = "binding-a",
                        policyRef = "policy-a",
                        defaultRef = "branch_ref_primary",
                        allowedRefPatterns = new[] { "branch_ref_feature" },
                    },
                    providerRepositoryUrl = "https://provider.example.test/owner/repo-secret",
                }),
            };
            AddHeaders(request);

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            json.ShouldNotContain("repo-secret", Case.Sensitive);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderEndpointShouldRejectUnsupportedSchemaVersionBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest(requestSchemaVersion: "v2");

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderEndpointShouldRejectMalformedJsonBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            AddHeaders(request);

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
    [InlineData("missing_default_ref")]
    [InlineData("empty_allowed_ref_patterns")]
    [InlineData("invalid_branch_ref_pattern")]
    public async Task RepositoryBackedFolderEndpointShouldRejectInvalidBranchRefPolicyBeforeGatewaySubmit(string scenario)
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            object branchRefPolicy = scenario switch
            {
                "missing_default_ref" => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
                "empty_allowed_ref_patterns" => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = Array.Empty<string>(),
                },
                _ => new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "opaque-policy-a",
                    defaultRef = "feature/raw",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            };

            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest(branchRefPolicy: branchRefPolicy);

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
    public async Task RepositoryBackedFolderEndpointShouldRejectReservedSystemTenantBeforeGatewaySubmit()
    {
        RecordingEventStoreGatewayClient gateway = new();
        WebApplication app = await StartAppAsync(gateway, "system", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            gateway.Requests.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderEndpointShouldMapGatewayIdempotencyConflictToSafeProblem()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                StatusCodes.Status409Conflict,
                "conflict for repository-secret",
                correlationId: "correlation-gateway"),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("idempotency_conflict");
            document.RootElement.GetProperty("code").GetString().ShouldBe("idempotency_conflict");
            json.ShouldNotContain("repository-secret", Case.Sensitive);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderEndpointShouldMapGatewayProviderUnavailableToSafeProblem()
    {
        RecordingEventStoreGatewayClient gateway = new()
        {
            Exception = new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "provider unavailable for repository-secret",
                correlationId: "correlation-gateway"),
        };
        WebApplication app = await StartAppAsync(gateway, "tenant-a", "principal-a").ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = CreateValidRequest();

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
            document.RootElement.GetProperty("code").GetString().ShouldBe("evidence_unavailable");
            json.ShouldNotContain("repository-secret", Case.Sensitive);
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
        builder.WebHost.UseTestServer();
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

    private static HttpRequestMessage CreateValidRequest(
        string requestSchemaVersion = "v1",
        object? branchRefPolicy = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                folderId = "folder-a",
                providerBindingRef = "provider-binding-a",
                repositoryProfileRef = "profile-a",
                folderMetadata = new
                {
                    displayName = "Folder A",
                    metadataClass = "tenant_sensitive",
                },
                branchRefPolicy = branchRefPolicy ?? new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            }),
        };
        AddHeaders(request);
        return request;
    }

    private static HttpRequestMessage CreateValidBindRequest(
        string requestSchemaVersion = "v1",
        object? branchRefPolicy = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/repository-bindings")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                providerBindingRef = "provider-binding-a",
                externalRepositoryRef = "external-repository-a",
                branchRefPolicy = branchRefPolicy ?? new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            }),
        };
        AddHeaders(request);
        return request;
    }

    private static HttpRequestMessage CreateValidBranchRefPolicyRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/folders/folder-a/branch-ref-policy")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                repositoryBindingId = "repository-binding-a",
                policyRef = "opaque-policy-a",
                defaultRef = "branch_ref_primary",
                allowedRefPatterns = new[] { "branch_ref_feature" },
                protectedRefPatterns = new[] { "branch_ref_release" },
            }),
        };
        AddHeaders(request);
        return request;
    }

    private static HttpRequestMessage CreateValidPrepareWorkspaceRequest(string requestSchemaVersion = "v1")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                repositoryBindingId = "repository-binding-a",
                branchRefPolicyRef = "branch-ref-policy-a",
                workspacePolicyRef = "workspace-policy-a",
            }),
        };
        AddHeaders(request);
        return request;
    }

    private static HttpRequestMessage CreateValidLockWorkspaceRequest(
        string requestSchemaVersion = "v1",
        string lockIntent = "exclusive_write",
        int requestedLeaseSeconds = 3600)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion,
                lockIntent,
                requestedLeaseSeconds,
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
