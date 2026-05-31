using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server.Authentication;

using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class CommitWorkspaceProcessEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 23, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommitProcessShouldRejectMalformedPayloadBeforeDomainMutation()
    {
        RecordingCommitExecutor executor = new();
        InMemoryFolderRepository repository = new();
        await using WebApplication app = BuildApp(repository, executor);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(
                Envelope(
                    JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        requestSchemaVersion = "v1",
                        workspaceId = "workspace-a",
                        operationId = "operation-a",
                        taskId = "task-a",
                        branchRefTarget = "branchref_primary",
                        changedPathMetadataDigest = "digest_workspace_a",
                        authorMetadataReference = "authorref_service",
                        commitMessageClassification = "generated_summary",
                        auditMetadataKeys = new[] { "operation_id" },
                        rawCommitMessage = "must-not-enter-domain",
                    })),
                CurrentState: null),
            TestContext.Current.CancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
        DomainServiceWireResult wireResult = (await response.Content
            .ReadFromJsonAsync<DomainServiceWireResult>(TestContext.Current.CancellationToken))!;
        wireResult.IsRejection.ShouldBeTrue();
        RejectionCode(wireResult).ShouldBe(nameof(FolderResultCode.MalformedJsonPayload));
        responseBody.ShouldNotContain("must-not-enter-domain", Case.Sensitive);
        executor.Requests.ShouldBeEmpty();
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public async Task CommitProcessShouldRejectMalformedEnvelopeBeforeDomainMutation()
    {
        RecordingCommitExecutor executor = new();
        InMemoryFolderRepository repository = new();
        await using WebApplication app = BuildApp(repository, executor);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(
                Envelope(ValidCommitPayload(), taskId: "task secret"),
                CurrentState: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        problem.ShouldContain("\"code\":\"validation_error\"");
        problem.ShouldNotContain("task secret", Case.Sensitive);
        executor.Requests.ShouldBeEmpty();
        repository.EventsAppended.ShouldBe(0);
    }

    private static WebApplication BuildApp(
        InMemoryFolderRepository repository,
        RecordingCommitExecutor executor)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddSingleton<IFolderRepository>(repository);
        builder.Services.RemoveAll<IWorkspaceCommitExecutor>();
        builder.Services.AddSingleton<IWorkspaceCommitExecutor>(executor);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(SeededTenantStore());
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor("tenant-a", "user-a"));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor("tenant-a", "user-a"));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator>(new AllowingEventStoreAuthorizationValidator());
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static InMemoryFolderTenantAccessProjectionStore SeededTenantStore()
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
            LastEventTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private static CommandEnvelope Envelope(byte[] payload, string taskId = "task-a")
        => new(
            MessageId: "idempotency-commit-a",
            TenantId: "tenant-a",
            Domain: FoldersServerModule.DomainName,
            AggregateId: "folder-a",
            CommandType: FoldersServerModule.CommitWorkspaceCommandType,
            Payload: payload,
            CorrelationId: "correlation-commit-a",
            CausationId: null,
            UserId: "user-a",
            Extensions: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["taskId"] = taskId,
            });

    private static byte[] ValidCommitPayload()
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            requestSchemaVersion = "v1",
            workspaceId = "workspace-a",
            operationId = "operation-a",
            taskId = "task-a",
            branchRefTarget = "branchref_primary",
            changedPathMetadataDigest = "digest_workspace_a",
            authorMetadataReference = "authorref_service",
            commitMessageClassification = "generated_summary",
            auditMetadataKeys = new[] { "operation_id" },
        });

    private static string RejectionCode(DomainServiceWireResult wireResult)
    {
        byte[] payload = wireResult.Events.ShouldHaveSingleItem().Payload;
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("code", out JsonElement code)
            ? code.GetString()!
            : root.GetProperty("Code").GetString()!;
    }

    private sealed class RecordingCommitExecutor : IWorkspaceCommitExecutor
    {
        public List<WorkspaceCommitExecutionRequest> Requests { get; } = [];

        public Task<WorkspaceCommitExecutionResult> CommitAsync(
            WorkspaceCommitExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"));
        }
    }

    private sealed class StaticTenantContextAccessor(string authoritativeTenantId, string principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string tenantId, string principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("folder-a:7", organizationId: "organization-a"));
    }

    private sealed class AllowingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator-a"));
    }

    private sealed class AllowingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
    }
}
