using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Server.Authentication;

using Hexalith.Folders.Testing;

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
            "create_folder",
            "archive_folder",
            "update_folder_acl_entry",
            "configure_provider_binding",
            "create_repository_backed_folder",
            "bind_repository",
            "configure_branch_ref_policy",
            "prepare_workspace",
            "lock_workspace",
            "release_workspace_lock",
            "add_workspace_file",
            "change_workspace_file",
            "remove_workspace_file",
            "commit_workspace",
        };

    [Fact]
    public void MutationFixturesShouldCoverTheGeneratedMutationInventory()
    {
        List<string> operationIds = [];
        foreach (TheoryDataRow<string> row in MutatingRoutes())
        {
            using HttpRequestMessage request = CreateValidRequest(row.Data);
            Pd10ProtectedOperationCatalog.TryResolveHistorical(
                request.Method.Method,
                request.RequestUri!.OriginalString,
                out Pd10ProtectedOperationDescriptor? descriptor,
                out _).ShouldBeTrue();
            operationIds.Add(descriptor!.OperationId);
        }

        operationIds.Order(StringComparer.Ordinal).ShouldBe(
            ParityOracle.Rows
                .Where(static row => row.IsMutating)
                .Select(static row => row.OperationId)
                .Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(MutatingRoutes))]
    public async Task MutatingEndpointShouldRejectDuplicateJsonPropertiesBeforeGatewayAdmission(string routeName)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage valid = CreateValidRequest(routeName);
        string body = await valid.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage accepted = await client.SendAsync(valid, TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted, routeName);
        gateway.Requests.ShouldHaveSingleItem();

        using JsonDocument document = JsonDocument.Parse(body);
        foreach (JsonProperty property in ObjectProperties(document.RootElement).DistinctBy(static property => property.Name))
        {
            string propertyName = property.Name;
            foreach (string duplicateName in new[] { propertyName, propertyName.ToUpperInvariant() })
            {
                using HttpRequestMessage ambiguous = CreateValidRequest(routeName);
                string propertyToken = JsonSerializer.Serialize(propertyName) + ":";
                // Keep both values valid so rejection proves duplicate detection rather than a type error.
                string duplicateToken = JsonSerializer.Serialize(duplicateName) + ":" + property.Value.GetRawText() + "," + propertyToken;
                ambiguous.Content = new StringContent(
                    body.Replace(propertyToken, duplicateToken, StringComparison.Ordinal),
                    Encoding.UTF8,
                    "application/json");
                using HttpResponseMessage response = await client.SendAsync(ambiguous, TestContext.Current.CancellationToken);
                string problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{routeName}:{duplicateName}:{problem}");
                problem.ShouldContain("\"category\":\"validation_error\"");
                problem.ShouldContain("\"code\":\"validation_error\"");
                gateway.Requests.Count.ShouldBe(1, $"{routeName}:{duplicateName}");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileMutationShouldRejectDuplicatePropertiesInsideUntypedTransportArrays(bool streamTransport)
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage valid = CreateValidRequest("add_workspace_file");
        JsonObject body = JsonNode.Parse(
            await valid.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        string transportProperty = streamTransport ? "streamDescriptor" : "inlineContent";
        if (streamTransport)
        {
            body["transportOperation"] = "PutFileStream";
            body["byteLength"] = 262145;
            body.Remove("inlineContent");
            body[transportProperty] = JsonSerializer.SerializeToNode(new
            {
                mediaType = "text/plain",
                declaredLength = 262145,
                observedLength = 262145,
                stagingReference = "staging-a",
                observedContentHashReference = "hashref-a",
                uploadMode = "request_body_stream",
            });
        }

        // Transport objects remain untyped; duplicate detection must inspect even
        // additional metadata that domain-semantic validation does not consume.
        body[transportProperty]!["additionalEvidence"] = new JsonArray(new JsonObject
        {
            ["classification"] = "metadata_only",
        });
        string unambiguousBody = body.ToJsonString();
        valid.Content = new StringContent(unambiguousBody, Encoding.UTF8, "application/json");
        using HttpResponseMessage accepted = await client.SendAsync(valid, TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        gateway.Requests.ShouldHaveSingleItem();

        foreach (string duplicateName in new[] { "classification", "CLASSIFICATION" })
        {
            using HttpRequestMessage ambiguous = CreateValidRequest("add_workspace_file");
            ambiguous.Content = new StringContent(
                unambiguousBody.Replace(
                    "\"classification\":",
                    $"\"{duplicateName}\":\"metadata_only\",\"classification\":",
                    StringComparison.Ordinal),
                Encoding.UTF8,
                "application/json");
            using HttpResponseMessage response = await client.SendAsync(ambiguous, TestContext.Current.CancellationToken);
            string problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, problem);
            problem.ShouldContain("\"code\":\"validation_error\"");
            problem.ShouldNotContain("additionalEvidence");
            problem.ShouldNotContain(duplicateName);
            gateway.Requests.Count.ShouldBe(1);
        }
    }

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

    [Fact]
    public async Task CommitEndpointShouldRejectMalformedBodyBeforeGatewaySubmitWithoutLeakingRawMetadata()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("commit_workspace");
        request.Content = JsonContent.Create(new
        {
            requestSchemaVersion = "v1",
            operationId = "operation-a",
            taskId = "task-a",
            branchRefTarget = "branchref_primary",
            changedPathMetadataDigest = "digest_workspace_a",
            authorMetadataReference = "authorref_service",
            commitMessageClassification = "generated_summary",
            auditMetadataKeys = new[] { "operation_id" },
            rawCommitMessage = "FILE_CONTENT_SYNTHETIC_NEVER_ECHO",
        });

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, json);
        json.ShouldContain("\"category\":\"validation_error\"");
        json.ShouldContain("\"code\":\"validation_error\"");
        json.ShouldNotContain("FILE_CONTENT_SYNTHETIC_NEVER_ECHO", Case.Sensitive);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitEndpointShouldSubmitRouteAuthoritativeWorkspacePayload()
    {
        RecordingEventStoreGatewayClient gateway = new();
        await using WebApplication app = BuildApp(gateway);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = CreateValidRequest("commit_workspace");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandRequest submitted = gateway.Requests.ShouldHaveSingleItem();
        submitted.CommandType.ShouldBe(FoldersServerModule.CommitWorkspaceCommandType);
        submitted.AggregateId.ShouldBe("folder-a");
        submitted.ShouldBeKeyedUlidEnvelope("idempotency-a");
        submitted.CorrelationId.ShouldBe("correlation-a");
        submitted.Extensions.ShouldNotBeNull();
        submitted.Extensions["taskId"].ShouldBe("task-a");
        submitted.Payload.GetProperty("workspaceId").GetString().ShouldBe("workspace-a");
        submitted.Payload.GetProperty("operationId").GetString().ShouldBe("operation-a");
        submitted.Payload.GetProperty("taskId").GetString().ShouldBe("task-a");
        submitted.Payload.GetProperty("branchRefTarget").GetString().ShouldBe("branchref_primary");
        submitted.Payload.GetProperty("changedPathMetadataDigest").GetString().ShouldBe("digest_workspace_a");
        submitted.Payload.GetProperty("authorMetadataReference").GetString().ShouldBe("authorref_service");
        submitted.Payload.GetProperty("commitMessageClassification").GetString().ShouldBe("generated_summary");
    }

    private static WebApplication BuildApp(RecordingEventStoreGatewayClient gateway)
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
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static HttpRequestMessage CreateValidRequest(string routeName)
    {
        HttpRequestMessage request = routeName switch
        {
            "create_folder" => new(HttpMethod.Post, "/api/v1/folders")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    parentFolderId = "parent-a",
                    folderMetadata = new { displayName = "My Folder", metadataClass = "tenant_sensitive" },
                }),
            },
            "archive_folder" => new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    archiveReasonCode = "caller_requested",
                }),
            },
            "update_folder_acl_entry" => new(HttpMethod.Put,
                $"/api/v1/folders/folder-a/acl/{FolderAclContract.DeriveAclEntryId("user", "user-a", "read")}")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    subjectRef = "user:user-a",
                    permissionLevel = "read",
                    effect = "grant",
                }),
            },
            "configure_provider_binding" => new(HttpMethod.Put, "/api/v1/provider-bindings/provider-binding-a")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    providerFamilyRef = "github",
                    capabilityProfileRef = "profile-a",
                    nonSecretCredentialReference = "credential-ref-a",
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
            "commit_workspace" => new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/commits")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    operationId = "operation-a",
                    taskId = "task-a",
                    branchRefTarget = "branchref_primary",
                    changedPathMetadataDigest = "digest_workspace_a",
                    authorMetadataReference = "authorref_service",
                    commitMessageClassification = "generated_summary",
                    auditMetadataKeys = new[] { "operation_id" },
                }),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(routeName), routeName, "Unknown mutating route."),
        };

        AddEnvelopeHeaders(request);
        return request;
    }

    private static IEnumerable<JsonProperty> ObjectProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                yield return property;
                foreach (JsonProperty nested in ObjectProperties(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (JsonProperty nested in ObjectProperties(item))
                {
                    yield return nested;
                }
            }
        }
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
