using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class MutationEnvelopeEndpointMatrixTests
{
    public static TheoryData<string> MutatingRoutes()
        => new()
        {
            "archive_folder",
            "create_repository_backed_folder",
            "bind_repository",
            "configure_branch_ref_policy",
            "prepare_workspace",
            "lock_workspace",
            "release_workspace_lock",
            "add_workspace_file",
            "change_workspace_file",
            "remove_workspace_file",
        };

    [Theory]
    [MemberData(nameof(MutatingRoutes))]
    public async Task MutatingEndpointShouldRejectMissingAndMalformedEnvelopeHeadersBeforeGatewaySubmit(string routeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        foreach (EnvelopeHeaderFault fault in EnvelopeHeaderFaults())
        {
            using HttpRequestMessage request = CreateValidRequest(routeName);
            fault.Apply(request);

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{routeName}:{fault.Name}:{json}");
            json.ShouldContain("\"category\":\"validation_error\"");
            if (fault.UnsafeValue is not null)
            {
                json.ShouldNotContain(fault.UnsafeValue, Case.Sensitive);
            }

            gateway.Requests.ShouldBeEmpty($"{routeName}:{fault.Name}");
        }
    }

    private static WebApplication BuildApp(RecordingEventStoreGatewayClient gateway)
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
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage CreateValidRequest(string routeName)
    {
        HttpRequestMessage request = routeName switch
        {
            "archive_folder" => new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    archiveReasonCode = "caller_requested",
                }),
            },
            "create_repository_backed_folder" => new(HttpMethod.Post, "/api/v1/folders/repository-backed")
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
                        policyRef = "branch_ref_policy_a",
                        defaultRef = "branch_ref_primary",
                        allowedRefPatterns = new[] { "branch_ref_feature" },
                    },
                }),
            },
            "bind_repository" => new(HttpMethod.Post, "/api/v1/folders/folder-a/repository-bindings")
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
                }),
            },
            "configure_branch_ref_policy" => new(HttpMethod.Put, "/api/v1/folders/folder-a/branch-ref-policy")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "repository-binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                }),
            },
            "prepare_workspace" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/preparation")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "repository-binding-a",
                    branchRefPolicyRef = "branch-ref-policy-a",
                    workspacePolicyRef = "workspace-policy-a",
                }),
            },
            "lock_workspace" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 3600,
                }),
            },
            "release_workspace_lock" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/lock/release")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockId = "workspace_lock_a",
                    lockOwnershipProof = "lock_proof_a",
                    releaseReasonCode = "caller_completed",
                }),
            },
            "add_workspace_file" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/files/add")
            {
                Content = JsonContent.Create(FileMutationBody("add", "PutFileInline")),
            },
            "change_workspace_file" => new(HttpMethod.Put, "/api/v1/folders/folder-a/workspaces/workspace-a/files/change")
            {
                Content = JsonContent.Create(FileMutationBody("change", "PutFileInline")),
            },
            "remove_workspace_file" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/files/remove")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    operationId = "operation-a",
                    fileOperationKind = "remove",
                    transportOperation = "metadataOnlyRemoval",
                    pathMetadata = PathMetadata(),
                }),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(routeName), routeName, "Unknown mutating route."),
        };

        AddEnvelopeHeaders(request);
        return request;
    }

    private static object FileMutationBody(string fileOperationKind, string transportOperation)
        => new
        {
            requestSchemaVersion = "v1",
            operationId = "operation-a",
            fileOperationKind,
            transportOperation,
            pathMetadata = PathMetadata(),
            contentHashReference = "hashref-a",
            byteLength = 12,
            inlineContent = new
            {
                mediaType = "text/plain",
                contentBytes = "aGVsbG8gd29ybGQh",
            },
        };

    private static object PathMetadata()
        => new
        {
            normalizedPath = "docs/readme.md",
            displayName = "readme.md",
            pathPolicyClass = "tenant_sensitive_document",
            unicodeNormalization = "NFC",
        };

    private static void AddEnvelopeHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Idempotency-Key", "idempotency-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
    }

    private static IEnumerable<EnvelopeHeaderFault> EnvelopeHeaderFaults()
    {
        yield return EnvelopeHeaderFault.Missing("Idempotency-Key");
        yield return EnvelopeHeaderFault.Missing("X-Correlation-Id");
        yield return EnvelopeHeaderFault.Missing("X-Hexalith-Task-Id");
        yield return EnvelopeHeaderFault.Malformed("Idempotency-Key", "unsafe idempotency");
        yield return EnvelopeHeaderFault.Malformed("X-Correlation-Id", "unsafe correlation");
        yield return EnvelopeHeaderFault.Malformed("X-Hexalith-Task-Id", "unsafe task");
    }

    private sealed record EnvelopeHeaderFault(string Name, string HeaderName, string? UnsafeValue)
    {
        public static EnvelopeHeaderFault Missing(string headerName) => new($"missing {headerName}", headerName, null);

        public static EnvelopeHeaderFault Malformed(string headerName, string unsafeValue) => new($"malformed {headerName}", headerName, unsafeValue);

        public void Apply(HttpRequestMessage request)
        {
            request.Headers.Remove(HeaderName);
            if (UnsafeValue is not null)
            {
                request.Headers.Add(HeaderName, UnsafeValue);
            }
        }
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
