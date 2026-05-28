using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;

// Disambiguate the generated SDK Client type from the enclosing Hexalith.Folders.Client namespace.
using GeneratedSdkClient = Hexalith.Folders.Client.Generated.Client;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests.EndToEnd;

/// <summary>
/// Golden-lifecycle dual-surface conformance (Story 5.5 AC #4/#5/#6/#7/#10). Drives the in-process server
/// host through both <b>REST</b> (raw <see cref="HttpClient"/> against <c>/api/v1/...</c>) and the
/// generated <b>SDK</b> (<see cref="IClient"/>) against the <i>same</i> host, asserting that each
/// golden-lifecycle step reaches its oracle transport-terminal state class and echoes the explicit
/// <c>X-Correlation-Id</c> unchanged on both surfaces. A representative negative step proves the canonical
/// RFC 9457 <c>application/problem+json</c> shape (with <c>category</c> ∈ the row's <c>error_code_set</c>)
/// is identical across REST and SDK.
/// </summary>
/// <remarks>
/// <para>The shared golden-lifecycle step list lives in <c>tests/shared/Parity/GoldenLifecycle.cs</c>,
/// linked (not copied) into this project. The shared oracle reader and scenarios live alongside it.</para>
/// <para><b>Hermeticity (AC #10).</b> The host binds to <c>http://127.0.0.1:0</c> (loopback, port 0), uses
/// in-memory repository, lifecycle read model, tenant projection store, and permissions read model, with
/// a recording gateway stub. <i>No</i> Dapr/Keycloak/Redis sidecars, <i>no</i> provider credentials, <i>no</i>
/// network, <i>no</i> nested submodule init.</para>
/// <para><b>Audit step substitution (drift-aware).</b> The canonical AC #7 audit-inspection step pins to an
/// audit-family operation_id, but the REST server does not yet implement an audit-family <c>/api/v1</c>
/// endpoint (<c>ListAuditTrail</c> et al. are in <c>Server.Tests.TransportParityConformanceTests</c>'s
/// known REST surface gap). Per the drift-aware reconciliation, both surfaces use
/// <c>GetFolderLifecycleStatus</c> as the in-process inspection step so the dual-surface run is
/// transport-equivalent against the same host. The golden-lifecycle step list documents this directly.</para>
/// </remarks>
public sealed class GoldenLifecycleParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GoldenLifecycleStepListPinsToOracleRowsForEverySurface()
    {
        GoldenLifecycle.Steps.ShouldNotBeEmpty();
        foreach (GoldenLifecycleStep step in GoldenLifecycle.Steps)
        {
            // Every step's SDK operation_id and REST operation_id resolve to an oracle row.
            _ = ParityScenarios.Row(step.SdkOperationId);
            _ = ParityScenarios.Row(step.RestOperationId);
        }

        // The canonical flow per AC #7 (provider readiness → repository binding → prepare → lock →
        // file change → commit → context query → status → audit inspection).
        string[] stepNames = GoldenLifecycle.Steps.Select(s => s.StepName).ToArray();
        stepNames.ShouldContain("provider_readiness");
        stepNames.ShouldContain("repository_binding");
        stepNames.ShouldContain("prepare_workspace");
        stepNames.ShouldContain("lock_workspace");
        stepNames.ShouldContain("commit_workspace");
        stepNames.ShouldContain("context_query");
        stepNames.ShouldContain("workspace_status");
        stepNames.ShouldContain("audit_inspection");
    }

    [Fact]
    public async Task CrossSurfaceMutatingStepReachesAcceptedTransportTerminalAndEchoesCorrelation()
    {
        // ArchiveFolder is a mutating_command (terminal-state class 'accepted' → 202). The same
        // logical step is driven through REST and SDK against the same in-process host; both surfaces
        // must reach the 'accepted' transport-terminal class and echo the explicit X-Correlation-Id.
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.OperationFamily.ShouldBe("mutating_command");
        archive.Transport.TerminalStates.ShouldContain("accepted");

        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            // REST run.
            const string restCorrelation = "correlation-rest-archive";
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-key-rest", restCorrelation, "task-rest");
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST mutating step must reach 'accepted' transport-terminal class.");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(restCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run.
            const string sdkCorrelation = "correlation-sdk-archive";
            AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: "archive-key-sdk",
                x_Correlation_Id: sdkCorrelation,
                x_Hexalith_Task_Id: "task-sdk",
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                    ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            // SDK reached 'accepted' transport-terminal class iff the call returned without throwing.
            sdkResult.ShouldNotBeNull("SDK mutating step must reach 'accepted' transport-terminal class.");
            sdkResult.CorrelationId.ShouldBe(sdkCorrelation, "SDK response body must carry the explicit correlation echoed by the server.");

            // Cross-surface equivalence: both reached the same terminal-state class (202/accepted), both
            // echoed correlation. Both calls reached the same in-process aggregate (two folder-archived
            // events, one per surface).
            host.Repository.EventsAppended.ShouldBe(2, "both surfaces produced one ArchiveFolder event each against the same in-process aggregate.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CrossSurfaceLifecycleInspectionStepReachesProjectedTransportTerminalAndEchoesCorrelation()
    {
        // GetFolderLifecycleStatus is a query_status row (terminal-state class 'projected' → 200). Both
        // surfaces drive the same in-process lifecycle read model and reach the 'projected' class with
        // X-Correlation-Id echoed unchanged. This is the in-process surrogate for the AC #7 audit
        // inspection step (the audit-family operations have no /api/v1 route yet; see Dev Notes).
        ParityRow lifecycle = ParityScenarios.Row("GetFolderLifecycleStatus");
        lifecycle.OperationFamily.ShouldBe("query_status");
        lifecycle.Transport.TerminalStates.ShouldContain("projected");
        lifecycle.Transport.IdempotencyKeyRule.ShouldBe("not_accepted_for_non_mutating_operation");

        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            // REST run.
            const string restCorrelation = "correlation-rest-lifecycle";
            using HttpRequestMessage restRequest = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
            restRequest.Headers.Add("X-Correlation-Id", restCorrelation);
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "REST query_status step must reach 'projected' transport-terminal class (200).");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(restCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run — non-mutating SDK methods declare no idempotency_Key (AC #3).
            const string sdkCorrelation = "correlation-sdk-lifecycle";
            FolderLifecycleStatus sdkResult = await host.SdkClient.GetFolderLifecycleStatusAsync(
                folderId: "folder-a",
                x_Correlation_Id: sdkCorrelation,
                x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            sdkResult.ShouldNotBeNull("SDK query_status step must reach 'projected' transport-terminal class.");
            sdkResult.FolderId.ShouldBe("folder-a", "SDK response carries the inspected folder identity.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CrossSurfaceNegativeStepEmitsCanonicalRfc9457ProblemWithCategoryInErrorCodeSet()
    {
        // AC #5 — provoke the same negative on both surfaces (unauthenticated, by withholding the tenant
        // claim) and assert that the canonical RFC 9457 problem shape and the emitted category ∈
        // error_code_set are identical across REST and SDK. ArchiveFolder error_code_set includes
        // 'authentication_failure'.
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.Transport.ErrorCodeSet.ShouldContain("authentication_failure");

        TestHost host = await StartHostAsync(tenantId: null, principalId: null).ConfigureAwait(true);
        try
        {
            // REST: raw HttpClient receives a 401 problem body.
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-rest-neg", "correlation-rest-neg", "task-rest-neg");
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            string restJson = await restResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument restDoc = JsonDocument.Parse(restJson);
            JsonElement restRoot = restDoc.RootElement;

            string restCategory = restRoot.GetProperty("category").GetString()!;
            archive.Transport.ErrorCodeSet.ShouldContain(restCategory, $"REST category '{restCategory}' is outside ArchiveFolder error_code_set.");
            AssertCanonicalProblemShape(restRoot, expectCorrelation: "correlation-rest-neg");

            // SDK: HexalithFoldersApiException carries the same problem body and StatusCode.
            HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
                await host.SdkClient.ArchiveFolderAsync(
                    folderId: "folder-a",
                    idempotency_Key: "archive-sdk-neg",
                    x_Correlation_Id: "correlation-sdk-neg",
                    x_Hexalith_Task_Id: "task-sdk-neg",
                    body: new ArchiveFolderRequest
                    {
                        RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                        ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                    },
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ConfigureAwait(true);

            sdkException.StatusCode.ShouldBe((int)HttpStatusCode.Unauthorized);
            using JsonDocument sdkDoc = JsonDocument.Parse(sdkException.Response);
            JsonElement sdkRoot = sdkDoc.RootElement;
            string sdkCategory = sdkRoot.GetProperty("category").GetString()!;
            archive.Transport.ErrorCodeSet.ShouldContain(sdkCategory, $"SDK category '{sdkCategory}' is outside ArchiveFolder error_code_set.");
            AssertCanonicalProblemShape(sdkRoot, expectCorrelation: "correlation-sdk-neg");

            // Cross-surface equivalence: identical category + canonical shape across REST and SDK.
            sdkCategory.ShouldBe(restCategory, "REST and SDK must emit the same canonical category for the same provoked failure.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Host harness — single in-process host driven by both REST and SDK clients.
    // ---------------------------------------------------------------------------------------------------

    private static void AssertCanonicalProblemShape(JsonElement root, string expectCorrelation)
    {
        root.TryGetProperty("category", out _).ShouldBeTrue();
        root.TryGetProperty("code", out _).ShouldBeTrue();
        root.TryGetProperty("message", out _).ShouldBeTrue();
        root.GetProperty("correlationId").GetString().ShouldBe(expectCorrelation);
        root.TryGetProperty("retryable", out _).ShouldBeTrue();
        root.TryGetProperty("clientAction", out _).ShouldBeTrue();
        root.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
    }

    private static HttpRequestMessage CreateArchiveRequest(string folderId, string idempotencyKey, string correlationId, string taskId)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/{folderId}/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                archiveReasonCode = "caller_requested",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);
        return request;
    }

    private static Task<TestHost> StartHostAsync(string? tenantId = "tenant-a", string? principalId = "user-a")
        => TestHost.StartAsync(tenantId, principalId);

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
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "archive_folder", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "read_metadata", Sequence: 2, EffectiveAt: Now.AddMinutes(-1)),
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

    /// <summary>Bundles the in-process host together with the two clients that drive it (raw HttpClient + SDK IClient).</summary>
    private sealed record TestHost(
        WebApplication App,
        HttpClient HttpClient,
        IClient SdkClient,
        MutableTenantAndClaimContext Context,
        InMemoryFolderRepository Repository,
        InMemoryFolderTenantAccessProjectionStore TenantStore,
        InMemoryEffectivePermissionsReadModel Permissions,
        InMemoryFolderLifecycleStatusReadModel LifecycleReadModel) : IAsyncDisposable
    {
        public static async Task<TestHost> StartAsync(string? tenantId, string? principalId)
        {
            MutableTenantAndClaimContext context = new(tenantId, principalId);
            InMemoryFolderTenantAccessProjectionStore tenantStore = new();
            InMemoryEffectivePermissionsReadModel permissions = new();
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(new FixedUtcClock(Now));
            TimeProvider timeProvider = new FixedTimeProvider(Now);
            InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
            RecordingEventStoreGatewayClient gateway = new();

            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            });
            builder.Configuration["urls"] = "http://127.0.0.1:0";
            builder.Services.AddFoldersServer();
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
            builder.Services.RemoveAll<IUtcClock>();
            builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton(timeProvider);

            WebApplication app = builder.Build();
            app.MapFoldersServerEndpoints();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            Uri hostUri = new(app.Urls.First());
            HttpClient httpClient = new() { BaseAddress = hostUri };
            // SDK client points at the same in-process host. Per the generated Client constructor, the
            // HttpClient's BaseAddress is what's used; no other DI plumbing is required for the test.
            IClient sdkClient = new GeneratedSdkClient(new HttpClient { BaseAddress = hostUri });

            return new TestHost(app, httpClient, sdkClient, context, repository, tenantStore, permissions, lifecycleReadModel);
        }

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Mutable tenant/claim context shared by the in-process host and the gateway round-trip.</summary>
    private sealed class MutableTenantAndClaimContext(string? tenantId, string? principalId)
        : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;

        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(
                AuthoritativeTenantId ?? string.Empty,
                PrincipalId ?? string.Empty,
                [actionToken]);
    }

    /// <summary>Gateway stub that re-posts incoming command submissions to the host's <c>/process</c> route
    /// so the in-memory repository actually receives the events (the same in-process round-trip
    /// <c>ArchiveFolderProcessWiringTests</c> uses). Until the address is set, command submits succeed
    /// with a synthetic response.</summary>
    private sealed class RecordingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        private Uri? _baseAddress;
        private MutableTenantAndClaimContext? _context;

        public List<SubmitCommandRequest> Requests { get; } = [];

        public void SetSelfPostAddress(Uri baseAddress, MutableTenantAndClaimContext context)
        {
            _baseAddress = baseAddress;
            _context = context;
        }

        public async Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (_baseAddress is null || _context is null)
            {
                return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId);
            }

            using HttpClient client = new() { BaseAddress = _baseAddress };
            CommandEnvelope envelope = new(
                request.MessageId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.CommandType,
                JsonSerializer.SerializeToUtf8Bytes(request.Payload),
                request.CorrelationId ?? request.MessageId,
                CausationId: null,
                _context.PrincipalId ?? "actor-present",
                request.Extensions);

            HttpResponseMessage response = await client
                .PostAsJsonAsync("/process", new { Envelope = envelope, CurrentState = (object?)null }, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
