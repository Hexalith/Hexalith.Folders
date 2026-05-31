using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

public sealed class ArchiveFolderProcessWiringTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 20, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ArchiveRequestShouldRoundTripThroughProcessAndPersistOneArchiveEvent()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Gateway.ProcessCalls.ShouldBe(1);
            host.Gateway.LastWireEventCount.ShouldBe(0, "Option B keeps folder event persistence inside the gate and returns no framework events.");
            host.Repository.EventsAppended.ShouldBe(1);

            // AC2: the persisted FolderArchived event must carry the correct evidence fields
            // sourced end-to-end through REST -> gateway -> /process -> gate. Actor comes from
            // the verified layered-auth context (not the raw envelope); correlation and task id
            // come from the request headers; reason code maps from the request body.
            FolderState archived = host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"));
            archived.LifecycleState.ShouldBe(FolderLifecycleState.Archived);
            archived.ArchiveActorPrincipalId.ShouldBe("user-a");
            archived.ArchiveCorrelationId.ShouldBe("correlation-archive-key-a");
            archived.ArchiveTaskId.ShouldBe("task-archive-key-a");
            archived.ArchiveReasonCode.ShouldBe(FolderArchiveReasonCode.CallerRequested);
            FolderLifecycleStatusReadModelResult lifecycle = await host.LifecycleReadModel
                .GetAsync(
                    new FolderLifecycleStatusReadModelRequest(
                        "tenant-a",
                        "folder-a",
                        "user-a",
                        "read_metadata",
                        TaskId: null,
                        CorrelationId: null,
                        AuthorizationWatermark: null,
                        "eventually_consistent"),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            lifecycle.Snapshot.ShouldNotBeNull();
            lifecycle.Snapshot.LifecycleState.ShouldBe(FolderLifecycleProjectionState.Archived);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RepositoryBackedFolderRequestShouldRoundTripThroughProcessAndPersistRequestEvent()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage request = CreateValidRepositoryBackedRequest();

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Gateway.ProcessCalls.ShouldBe(1);
            host.Gateway.LastWireEventCount.ShouldBe(0, "Folder event persistence stays inside the repository-backed gate.");
            host.Repository.EventsAppended.ShouldBe(1);

            FolderState state = host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"));
            state.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.BindingRequested);
            state.RepositoryBindingId.ShouldBe("repository-binding-a");
            state.ProviderBindingRef.ShouldBe("provider-binding-a");

            FolderLifecycleStatusReadModelResult lifecycle = await host.LifecycleReadModel
                .GetAsync(
                    new FolderLifecycleStatusReadModelRequest(
                        "tenant-a",
                        "folder-a",
                        "user-a",
                        "read_metadata",
                        TaskId: null,
                        CorrelationId: null,
                        AuthorizationWatermark: null,
                        "eventually_consistent"),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            lifecycle.Snapshot.ShouldNotBeNull();
            lifecycle.Snapshot.BindingStatus.ShouldBe(FolderRepositoryBindingStatus.BindingRequested);
            lifecycle.Snapshot.RepositoryBindingId.ShouldBe("repository-binding-a");
            lifecycle.Snapshot.ProviderBindingRef.ShouldBe("provider-binding-a");

            using HttpRequestMessage lifecycleRequest = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
            lifecycleRequest.Headers.Add("X-Correlation-Id", "correlation-binding-a");
            lifecycleRequest.Headers.Add("X-Hexalith-Task-Id", "task-binding-a");

            HttpResponseMessage lifecycleResponse = await host.Client
                .SendAsync(lifecycleRequest, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            string lifecycleJson = await lifecycleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            lifecycleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, lifecycleJson);
            using JsonDocument lifecycleDocument = JsonDocument.Parse(
                lifecycleJson);
            lifecycleDocument.RootElement.GetProperty("lifecycleState").GetString().ShouldBe("requested");
            lifecycleDocument.RootElement.GetProperty("repositoryBindingId").GetString().ShouldBe("repository-binding-a");
            lifecycleDocument.RootElement.GetProperty("providerBindingRef").GetString().ShouldBe("provider-binding-a");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            HttpResponseMessage allowed = await host.Client
                .SendAsync(CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            host.Context.Set("tenant-a", "user-b");
            HttpResponseMessage denied = await host.Client
                .SendAsync(CreateValidArchiveRequest("folder-a", "archive-key-b", "operator_review"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveRequestShouldSurfaceIdempotencyConflictThroughGatewayAndProcess()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            HttpResponseMessage conflict = await host.Client
                .SendAsync(CreateValidArchiveRequest("folder-a", "archive-key-a", "operator_review"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveRequestShouldSurfaceAlreadyArchivedAsSafeDenialThroughProcess()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedArchivedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            HttpResponseMessage response = await host.Client
                .SendAsync(CreateValidArchiveRequest("folder-a", "archive-key-b", "caller_requested"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            host.Repository.EventsAppended.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task MissingIdempotencyKeyShouldStopBeforeGatewayRoundTrip()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            request.Headers.Remove("Idempotency-Key");

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task MalformedBodyShouldStopBeforeGatewayRoundTrip()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
            {
                Content = new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Idempotency-Key", "archive-key-a");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CancelledRequestShouldStopBeforeGatewayRoundTrip()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using CancellationTokenSource cts = new();
            await cts.CancelAsync().ConfigureAwait(true);

            await Should.ThrowAsync<TaskCanceledException>(
                async () => await host.Client.SendAsync(request, cts.Token).ConfigureAwait(true));

            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<TestHost> StartHostAsync()
    {
        MutableTenantAndClaimContext context = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        InMemoryEffectivePermissionsReadModel permissions = new();
        InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(new FixedUtcClock(Now));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
        Uri? hostUri = null;
        InProcessEventStoreGatewayClient gateway = new(() => hostUri!, context);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServer();
        // AddFoldersServer registers FoldersAuthSchemeValidator (needs IAuthenticationSchemeProvider)
        // and MapFoldersServerEndpoints maps the ServiceDefaults health endpoints (need
        // HealthCheckService). The slim test host doesn't pull in AddServiceDefaults, so register
        // the two composition primitives the server surface depends on. Matches the pattern in
        // GoldenLifecycleParityTests and MixedSurfaceHandoffTests.
        builder.Services.AddAuthentication();
        builder.Services.AddHealthChecks();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(context);
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(context);
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddSingleton<IFolderRepository>(repository);
        builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
        builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycleReadModel);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenantStore);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IRepositoryCreationReadinessValidator>();
        builder.Services.AddSingleton<IRepositoryCreationReadinessValidator>(new ReadyRepositoryCreationReadinessValidator());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton(timeProvider);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        hostUri = new Uri(app.Urls.First());
        return new TestHost(app, new HttpClient { BaseAddress = hostUri }, gateway, context, repository, tenantStore, permissions, lifecycleReadModel);
    }

    private static HttpRequestMessage CreateValidArchiveRequest(string folderId, string key, string reasonCode)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/{folderId}/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                archiveReasonCode = reasonCode,
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", $"correlation-{key}");
        request.Headers.Add("X-Hexalith-Task-Id", $"task-{key}");
        return request;
    }

    private static HttpRequestMessage CreateValidRepositoryBackedRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                folderId = "folder-a",
                providerBindingRef = "provider-binding-a",
                repositoryProfileRef = "repository-profile-a",
                folderMetadata = new
                {
                    displayName = "Folder",
                    metadataClass = "tenant_sensitive",
                },
                branchRefPolicy = new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "repository-binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            }),
        };
        request.Headers.Add("Idempotency-Key", "idempotency-binding-a");
        request.Headers.Add("X-Correlation-Id", "correlation-binding-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-binding-a");
        return request;
    }

    private static void SeedTenant(InMemoryFolderTenantAccessProjectionStore store, string tenantId, string principalId)
        => store.SaveAsync(
            new FolderTenantAccessProjection
            {
                TenantId = tenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    [principalId] = new(principalId, "Owner"),
                },
                Watermark = 1,
                LastEventTimestamp = Now.AddMinutes(-1),
                ProjectionWatermark = $"{tenantId}:1",
            },
            TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private static void SeedPermissions(
        InMemoryEffectivePermissionsReadModel readModel,
        string tenantId,
        string organizationId,
        string folderId,
        string principalId)
        => readModel.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            folderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    "archive_folder",
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    RepositoryBackedFolderCreationService.ActionToken,
                    Sequence: 2,
                    EffectiveAt: Now.AddMinutes(-1)),
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    "read_metadata",
                    Sequence: 3,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static void SeedFolder(InMemoryFolderRepository repository, string tenantId, string organizationId, string folderId)
        => repository.Seed(
            FolderStreamName.Create(tenantId, folderId),
            [
                new FolderCreated(
                    tenantId,
                    organizationId,
                    folderId,
                    "Folder",
                    null,
                    null,
                    [],
                    FolderLifecycleState.Active,
                    FolderRepositoryBindingState.Unbound,
                    "user-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key",
                    "seed-fingerprint",
                    Now.AddMinutes(-2)),
            ]);

    private static void SeedArchivedFolder(InMemoryFolderRepository repository, string tenantId, string organizationId, string folderId)
    {
        // Seed both lifecycle events in a single call so FolderState is recomputed from
        // empty with the full transition history. Each event carries a distinct
        // idempotency key so the seed ledger guard (which now rejects duplicate keys) sees
        // two independent entries.
        repository.Seed(
            FolderStreamName.Create(tenantId, folderId),
            [
                new FolderCreated(
                    tenantId,
                    organizationId,
                    folderId,
                    "Folder",
                    null,
                    null,
                    [],
                    FolderLifecycleState.Active,
                    FolderRepositoryBindingState.Unbound,
                    "user-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key",
                    "seed-fingerprint",
                    Now.AddMinutes(-2)),
                new FolderArchived(
                    tenantId,
                    organizationId,
                    folderId,
                    FolderArchiveReasonCode.CallerRequested,
                    "user-a",
                    "seed-archive-correlation",
                    "seed-archive-task",
                    "seed-archive-key",
                    "seed-archive-fingerprint",
                    Now.AddMinutes(-1)),
            ]);
        repository.ResetAppendCounters();
    }

    private sealed record TestHost(
        WebApplication App,
        HttpClient Client,
        InProcessEventStoreGatewayClient Gateway,
        MutableTenantAndClaimContext Context,
        InMemoryFolderRepository Repository,
        InMemoryFolderTenantAccessProjectionStore TenantStore,
        InMemoryEffectivePermissionsReadModel Permissions,
        InMemoryFolderLifecycleStatusReadModel LifecycleReadModel) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class MutableTenantAndClaimContext(string tenantId, string principalId)
        : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
    {
        public string? AuthoritativeTenantId { get; private set; } = tenantId;

        public string? PrincipalId { get; private set; } = principalId;

        public void Set(string tenantId, string principalId)
        {
            AuthoritativeTenantId = tenantId;
            PrincipalId = principalId;
        }

        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(
                AuthoritativeTenantId ?? string.Empty,
                PrincipalId ?? string.Empty,
                [actionToken]);
    }

    private sealed class InProcessEventStoreGatewayClient(
        Func<Uri> baseAddress,
        MutableTenantAndClaimContext context) : IEventStoreGatewayClient
    {
        public int LastWireEventCount { get; private set; }

        public int ProcessCalls { get; private set; }

        public async Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            using HttpClient client = new() { BaseAddress = baseAddress() };
            CommandEnvelope envelope = new(
                request.MessageId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.CommandType,
                JsonSerializer.SerializeToUtf8Bytes(request.Payload),
                request.CorrelationId ?? request.MessageId,
                CausationId: null,
                context.PrincipalId ?? "actor-present",
                request.Extensions);

            HttpResponseMessage response = await client
                .PostAsJsonAsync("/process", new DomainServiceRequest(envelope, CurrentState: null), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new EventStoreGatewayException((int)response.StatusCode, response.ReasonPhrase ?? "Process failed", correlationId: request.CorrelationId);
            }

            DomainServiceWireResult result = (await response.Content
                .ReadFromJsonAsync<DomainServiceWireResult>(cancellationToken)
                .ConfigureAwait(false))!;
            LastWireEventCount = result.Events.Count;

            if (result.IsRejection)
            {
                throw ToGatewayException(result, request.CorrelationId ?? request.MessageId);
            }

            return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId);
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

        private static EventStoreGatewayException ToGatewayException(DomainServiceWireResult result, string correlationId)
        {
            DomainServiceWireEvent rejection = result.Events.Single();
            using JsonDocument document = JsonDocument.Parse(rejection.Payload);
            string code = document.RootElement.TryGetProperty("code", out JsonElement camelCode)
                ? camelCode.GetString() ?? "MalformedEvidence"
                : document.RootElement.GetProperty("Code").GetString() ?? "MalformedEvidence";
            int status = code switch
            {
                nameof(FolderResultCode.IdempotencyConflict) => 409,
                nameof(FolderResultCode.FolderNotFound) => 404,
                nameof(FolderResultCode.ProviderRateLimited) => 429,
                nameof(FolderResultCode.ValidationFailed)
                    or nameof(FolderResultCode.MalformedJsonPayload)
                    or nameof(FolderResultCode.InvalidFolderId)
                    or nameof(FolderResultCode.InvalidTenant)
                    or nameof(FolderResultCode.ReservedTenant) => 400,
                nameof(FolderResultCode.StaleProjection)
                    or nameof(FolderResultCode.UnavailableProjection)
                    or nameof(FolderResultCode.PolicyEvidenceUnavailable)
                    or nameof(FolderResultCode.PolicyEvidenceStale)
                    or nameof(FolderResultCode.AclEvidenceUnavailable) => 503,
                _ => 403,
            };

            return new EventStoreGatewayException(status, "Rejected", correlationId: correlationId);
        }
    }

    private sealed class ReadyRepositoryCreationReadinessValidator : IRepositoryCreationReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderReadinessValidationResult(
                ProviderReadinessResultCode.Allowed,
                "ready",
                "success",
                "none",
                Retryable: false,
                RetryAfter: null,
                RemediationCategory: "none",
                CorrelationId: request.CorrelationId ?? "correlation-a",
                ProviderReference: request.ProviderBindingRef,
                ProviderBindingRef: request.ProviderBindingRef,
                CapabilityProfileRef: "repository-profile-a",
                Evidence: null,
                new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
                ProviderFailureCategory.None,
                "none"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
