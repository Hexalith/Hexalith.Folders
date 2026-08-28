using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

public sealed class ArchiveFolderProcessWiringTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 20, 11, 0, 0, TimeSpan.Zero);

    /// <summary>Upper bound on any server-side handshake signal, so a broken signal fails instead of hanging.</summary>
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Scoped identifiers seeded by the denial rows; none may appear in a safe-denial body.</summary>
    /// <remarks>
    /// The acting principal belongs here too: an actor identity interpolated into a denial message is a
    /// disclosure, not diagnostics. The idempotency key deliberately does not, because the safe denial
    /// legitimately carries the key-derived <c>correlationId</c> that
    /// <see cref="AssertMetadataOnlySafeDenial"/> pins.
    /// </remarks>
    private static readonly string[] ForbiddenDenialDisclosures =
    [
        "tenant-a",
        "tenant-b",
        "org-a",
        "folder-a",
        "user-a",
        "v1-test-denied",
    ];

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
    public async Task ArchiveRequestCancelledMidProcessorShouldUnwindAuthorizationWithoutAppending()
    {
        using CancellationTokenSource requestCancellation = new();
        CancelMidFlightFolderArchivePolicyEvidenceProvider policyProvider = new(requestCancellation);
        ScopedLayeredFolderAuthorizationResultAccessor authorizationAccessor = new();
        TestHost host = await StartHostAsync(
            policyProvider,
            authorizationAccessor: authorizationAccessor).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            Task<HttpResponseMessage> responseTask = host.Client.SendAsync(request, requestCancellation.Token);

            // Every handshake signal is bounded: if a future change stops the OperationCanceledException
            // from escaping /process, the middleware never signals and an unbounded wait would hang the
            // whole run instead of failing this row.
            await policyProvider.Entered
                .WaitAsync(SignalTimeout, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            await Should.ThrowAsync<OperationCanceledException>(
                async () => await responseTask.ConfigureAwait(true));
            await policyProvider.ServerCancellationObserved
                .WaitAsync(SignalTimeout, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            await policyProvider.Completion
                .WaitAsync(SignalTimeout, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            host.Gateway.ProcessCalls.ShouldBe(1);
            policyProvider.Calls.ShouldBe(1);
            authorizationAccessor.Current.ShouldBeNull();
            host.Repository.EventsAppended.ShouldBe(0);
            host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
                .LifecycleState.ShouldBe(FolderLifecycleState.Active);

            using HttpRequestMessage retryRequest = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using HttpResponseMessage retryResponse = await host.Client
                .SendAsync(retryRequest, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            string retryJson = await retryResponse.Content
                .ReadAsStringAsync(TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            retryResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, retryJson);
            using JsonDocument retryDocument = JsonDocument.Parse(retryJson);

            // A false replay flag is the caller-visible proof that the cancelled attempt left no
            // idempotency ledger entry behind for the same key.
            retryDocument.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeFalse();
            host.Gateway.ProcessCalls.ShouldBe(2);
            policyProvider.Calls.ShouldBe(2);
            host.Repository.EventsAppended.ShouldBe(1);
            host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
                .LifecycleState.ShouldBe(FolderLifecycleState.Archived);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage firstRequest = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using HttpResponseMessage first = await host.Client
                .SendAsync(firstRequest, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            using HttpRequestMessage replayRequest = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using HttpResponseMessage replay = await host.Client
                .SendAsync(replayRequest, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            string firstJson = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string replayJson = await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            first.StatusCode.ShouldBe(HttpStatusCode.Accepted, firstJson);
            replay.StatusCode.ShouldBe(HttpStatusCode.Accepted, replayJson);
            using JsonDocument firstDocument = JsonDocument.Parse(firstJson);
            using JsonDocument replayDocument = JsonDocument.Parse(replayJson);
            firstDocument.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
            firstDocument.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-archive-key-a");
            firstDocument.RootElement.GetProperty("taskId").GetString().ShouldBe("task-archive-key-a");
            firstDocument.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeFalse();
            replayDocument.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
            replayDocument.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-archive-key-a");
            replayDocument.RootElement.GetProperty("taskId").GetString().ShouldBe("task-archive-key-a");
            replayDocument.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();

            host.Gateway.ProcessCalls.ShouldBe(2);
            host.Repository.EventsAppended.ShouldBe(1);
            FolderState archived = host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"));
            archived.LifecycleState.ShouldBe(FolderLifecycleState.Archived);
            archived.ArchiveActorPrincipalId.ShouldBe("user-a");
            archived.ArchiveCorrelationId.ShouldBe("correlation-archive-key-a");
            archived.ArchiveTaskId.ShouldBe("task-archive-key-a");
            archived.ArchiveReasonCode.ShouldBe(FolderArchiveReasonCode.CallerRequested);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant()
    {
        DenyingFolderArchivePolicyEvidenceProvider policyProvider = new();
        ScopedLayeredFolderAuthorizationResultAccessor authorizationAccessor = new();
        TestHost host = await StartHostAsync(
            policyProvider,
            _ => "tenant-b",
            authorizationAccessor).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            string responseJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, responseJson);
            using JsonDocument document = JsonDocument.Parse(responseJson);
            AssertMetadataOnlySafeDenial(document.RootElement);
            host.Gateway.ProcessCalls.ShouldBe(1);
            policyProvider.Calls.ShouldBe(0);

            // This denial returns before BeginScope, so a null accessor is a leak guard on the
            // early-return path -- not evidence that a scope was begun and torn down. The
            // begin-then-clear chain is proven only on the policy-denial row, where `Calls == 1`
            // shows the processor ran inside the scope that `Current is null` shows was cleared.
            authorizationAccessor.Current.ShouldBeNull();
            host.Repository.EventsAppended.ShouldBe(0);
            host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
                .LifecycleState.ShouldBe(FolderLifecycleState.Active);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchivePolicyDenialShouldReturnSafeForbiddenWithoutAppending()
    {
        DenyingFolderArchivePolicyEvidenceProvider policyProvider = new();
        ScopedLayeredFolderAuthorizationResultAccessor authorizationAccessor = new();
        TestHost host = await StartHostAsync(
            policyProvider,
            authorizationAccessor: authorizationAccessor).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            using HttpRequestMessage request = CreateValidArchiveRequest("folder-a", "archive-key-a", "caller_requested");
            using HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            string responseJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, responseJson);
            using JsonDocument document = JsonDocument.Parse(responseJson);
            AssertMetadataOnlySafeDenial(document.RootElement);
            host.Gateway.ProcessCalls.ShouldBe(1);
            policyProvider.Calls.ShouldBe(1);
            policyProvider.LastManagedTenantId.ShouldBe("tenant-a");
            policyProvider.LastOrganizationId.ShouldBe("org-a");
            policyProvider.LastFolderId.ShouldBe("folder-a");
            authorizationAccessor.Current.ShouldBeNull();
            host.Repository.EventsAppended.ShouldBe(0);
            host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
                .LifecycleState.ShouldBe(FolderLifecycleState.Active);
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

    [Fact]
    public async Task CreateFolderRequestShouldRoundTripThroughProcessAndPersistFolderCreated()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedCreateFolderPermissions(host.Permissions, "tenant-a", "org-a", "user-a");

            using HttpRequestMessage request = CreateValidCreateFolderRequest("create-key-a", "My Folder");

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Gateway.ProcessCalls.ShouldBe(1);
            host.Repository.EventsAppended.ShouldBe(1);

            FolderState created = host.Repository.Load(
                FolderStreamName.Create("tenant-a", DeriveCreateFolderId("tenant-a", "create-key-a")));
            created.IsCreated.ShouldBeTrue();
            created.DisplayName.ShouldBe("My Folder");
            created.OrganizationId.ShouldBe("org-a");
            created.LifecycleState.ShouldBe(FolderLifecycleState.Active);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CreateFolderReplayWithSameKeyShouldNotPersistASecondFolder()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedCreateFolderPermissions(host.Permissions, "tenant-a", "org-a", "user-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(CreateValidCreateFolderRequest("create-key-a", "My Folder"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage replay = await host.Client
                .SendAsync(CreateValidCreateFolderRequest("create-key-a", "My Folder"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CreateFolderSameKeyDifferentPayloadShouldSurfaceIdempotencyConflict()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedCreateFolderPermissions(host.Permissions, "tenant-a", "org-a", "user-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(CreateValidCreateFolderRequest("create-key-a", "My Folder"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage conflict = await host.Client
                .SendAsync(CreateValidCreateFolderRequest("create-key-a", "A Different Name"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UpdateFolderAclEntryShouldPersistAccessOverrideAndRoundTripThroughList()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedAclPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            string aclEntryId = FolderAclContract.DeriveAclEntryId("user", "user-a", "read");
            using HttpRequestMessage grant = new(HttpMethod.Put, $"/api/v1/folders/folder-a/acl/{aclEntryId}")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    subjectRef = "user:user-a",
                    permissionLevel = "read",
                    effect = "grant",
                }),
            };
            grant.Headers.Add("Idempotency-Key", "acl-key-a");
            grant.Headers.Add("X-Correlation-Id", "correlation-acl-a");
            grant.Headers.Add("X-Hexalith-Task-Id", "task-acl-a");

            HttpResponseMessage grantResponse = await host.Client.SendAsync(grant, TestContext.Current.CancellationToken).ConfigureAwait(true);

            grantResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Gateway.ProcessCalls.ShouldBe(1);
            host.Repository.EventsAppended.ShouldBe(1);

            FolderState state = host.Repository.Load(FolderStreamName.Create("tenant-a", "folder-a"));
            state.HasFolderAccess(new FolderAccessEntryKey(
                "tenant-a",
                "folder-a",
                FolderAccessPrincipalKind.User,
                "user-a",
                FolderAclContract.ReadAction)).ShouldBeTrue();

            using HttpRequestMessage list = new(HttpMethod.Get, "/api/v1/folders/folder-a/acl");
            list.Headers.Add("X-Correlation-Id", "correlation-acl-list");

            HttpResponseMessage listResponse = await host.Client.SendAsync(list, TestContext.Current.CancellationToken).ConfigureAwait(true);
            string listJson = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listJson);
            using JsonDocument document = JsonDocument.Parse(listJson);
            JsonElement item = document.RootElement.GetProperty("items")[0];
            item.GetProperty("aclEntryId").GetString().ShouldBe(aclEntryId);
            item.GetProperty("subjectRef").GetString().ShouldBe("user:user-a");
            item.GetProperty("permissionLevel").GetString().ShouldBe("read");
            item.GetProperty("effect").GetString().ShouldBe("grant");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UpdateFolderAclEntryReplayWithSameKeyShouldNotPersistTwice()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedAclPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(CreateUpdateAclEntryRequest("acl-key-a", "grant"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage replay = await host.Client
                .SendAsync(CreateUpdateAclEntryRequest("acl-key-a", "grant"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UpdateFolderAclEntrySameKeyDifferentPayloadShouldSurfaceIdempotencyConflict()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedAclPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            HttpResponseMessage grant = await host.Client
                .SendAsync(CreateUpdateAclEntryRequest("acl-key-a", "grant"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            // Same idempotency key, different effect => different canonical fingerprint => conflict (AC5).
            HttpResponseMessage conflict = await host.Client
                .SendAsync(CreateUpdateAclEntryRequest("acl-key-a", "revoke"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            grant.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            host.Repository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ConfigureProviderBindingShouldRoundTripThroughProcessAndPersistBinding()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedConfigureProviderBindingPermissions(host.Permissions, "tenant-a", "org-a", "binding-a", "user-a");

            using HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/provider-bindings/binding-a")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    providerFamilyRef = "github",
                    capabilityProfileRef = "profile-a",
                    nonSecretCredentialReference = "credential-ref-a",
                }),
            };
            request.Headers.Add("Idempotency-Key", "binding-key-a");
            request.Headers.Add("X-Correlation-Id", "correlation-binding-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-binding-a");

            HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.Gateway.ProcessCalls.ShouldBe(1);
            host.OrganizationRepository.EventsAppended.ShouldBe(1);

            OrganizationState state = host.OrganizationRepository.Load(OrganizationStreamName.Create("tenant-a", "org-a"));
            state.ProviderBindings.ContainsKey("binding-a").ShouldBeTrue();
            OrganizationProviderBinding binding = state.ProviderBindings["binding-a"];
            binding.ProviderKind.ShouldBe("github");
            binding.CredentialReferenceId.ShouldBe("credential-ref-a");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ConfigureProviderBindingReplayWithSameKeyShouldNotPersistTwice()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedConfigureProviderBindingPermissions(host.Permissions, "tenant-a", "org-a", "binding-a", "user-a");

            HttpResponseMessage first = await host.Client.SendAsync(CreateConfigureProviderBindingRequest("binding-key-a", "credential-ref-a"), TestContext.Current.CancellationToken).ConfigureAwait(true);
            HttpResponseMessage replay = await host.Client.SendAsync(CreateConfigureProviderBindingRequest("binding-key-a", "credential-ref-a"), TestContext.Current.CancellationToken).ConfigureAwait(true);

            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            host.OrganizationRepository.EventsAppended.ShouldBe(1);

            // The shared gateway double now forwards the /process result payload, which is what makes
            // `idempotentReplay` observable through this harness at all. That flag is built by a
            // different payload writer than the folder path -- `OrganizationAcceptedNoOp`'s
            // `AlreadyApplied` branch -- so without these two assertions that branch could be inverted
            // with every test in the repository still green, and an existing caller of the double would
            // silently report a replay as a first-time accept.
            using JsonDocument firstDocument = JsonDocument.Parse(
                await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            using JsonDocument replayDocument = JsonDocument.Parse(
                await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            firstDocument.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeFalse();
            replayDocument.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ConfigureProviderBindingSameKeyDifferentPayloadShouldSurfaceIdempotencyConflict()
    {
        TestHost host = await StartHostAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedConfigureProviderBindingPermissions(host.Permissions, "tenant-a", "org-a", "binding-a", "user-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(CreateConfigureProviderBindingRequest("binding-key-a", "credential-ref-a"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            // Same idempotency key, different credential reference => different fingerprint => conflict (AC5).
            HttpResponseMessage conflict = await host.Client
                .SendAsync(CreateConfigureProviderBindingRequest("binding-key-a", "credential-ref-b"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            host.OrganizationRepository.EventsAppended.ShouldBe(1);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static HttpRequestMessage CreateUpdateAclEntryRequest(string key, string effect)
    {
        string aclEntryId = FolderAclContract.DeriveAclEntryId("user", "user-a", "read");
        HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/folders/folder-a/acl/{aclEntryId}")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                subjectRef = "user:user-a",
                permissionLevel = "read",
                effect,
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", $"correlation-{key}");
        request.Headers.Add("X-Hexalith-Task-Id", $"task-{key}");
        return request;
    }

    private static HttpRequestMessage CreateConfigureProviderBindingRequest(string key, string credentialReference)
    {
        HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/provider-bindings/binding-a")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                providerFamilyRef = "github",
                capabilityProfileRef = "profile-a",
                nonSecretCredentialReference = credentialReference,
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", $"correlation-{key}");
        request.Headers.Add("X-Hexalith-Task-Id", $"task-{key}");
        return request;
    }

    private static void SeedConfigureProviderBindingPermissions(
        InMemoryEffectivePermissionsReadModel readModel,
        string tenantId,
        string organizationId,
        string providerBindingRef,
        string principalId)
        => readModel.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            // configure_provider_binding authorizes against the provider-binding-ref scope; the owning
            // organization is carried by the snapshot and resolved into the command (story 8.1 DD4).
            providerBindingRef,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(
                    EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    "configure_provider_binding",
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static void SeedAclPermissions(
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
                    "manage_folder_access",
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    "read_metadata",
                    Sequence: 2,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static HttpRequestMessage CreateValidCreateFolderRequest(string key, string displayName)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                parentFolderId = "parent-a",
                folderMetadata = new
                {
                    displayName,
                    metadataClass = "tenant_sensitive",
                },
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", $"correlation-{key}");
        request.Headers.Add("X-Hexalith-Task-Id", $"task-{key}");
        return request;
    }

    private static void SeedCreateFolderPermissions(
        InMemoryEffectivePermissionsReadModel readModel,
        string tenantId,
        string organizationId,
        string principalId)
        => readModel.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            // CreateFolder authorizes against the synthetic organization-baseline scope, so the
            // effective-permissions snapshot is keyed by that scope, not a concrete folder id.
            "organization_baseline",
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(
                    EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    FolderCreationService.ActionToken,
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    // Mirrors FoldersDomainServiceEndpoints.DeriveCreateFolderId so the test can address the
    // server-assigned folder stream.
    private static string DeriveCreateFolderId(string tenantId, string idempotencyKey)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                $"{tenantId.Length}:{tenantId}|{idempotencyKey.Length}:{idempotencyKey}"));
        return "fld-" + Convert.ToHexString(hash)[..40].ToLowerInvariant();
    }

    private static void AssertMetadataOnlySafeDenial(JsonElement root)
    {
        root.GetProperty("category").GetString().ShouldBe("tenant_access_denied");
        root.GetProperty("code").GetString().ShouldBe("denied_safe");
        root.GetProperty("retryable").GetBoolean().ShouldBeFalse();
        root.GetProperty("clientAction").GetString().ShouldBe("no_action");
        root.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");

        // Metadata-only is only operable if the denial stays correlatable. Pinning the exact
        // correlation id also proves the two scans below run against a populated body rather than
        // passing vacuously over an empty or truncated one.
        root.GetProperty("correlationId").GetString().ShouldBe("correlation-archive-key-a");
        HasScopedMetadataProperty(root).ShouldBeFalse(
            "safe denials must not disclose tenant, organization, folder, policy, or existence fields");

        // A name-only scan cannot see the likeliest leak vector: a scoped identifier interpolated into
        // a free-text `message`, `title`, `detail`, or `type` value. Scan the values too.
        FindDisclosedValue(root).ShouldBeNull(
            "safe denials must not disclose seeded tenant, organization, folder, or policy-version values");
    }

    private static string? FindDisclosedValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string? disclosed = FindDisclosedValue(property.Value);
                    if (disclosed is not null)
                    {
                        return disclosed;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string? disclosed = FindDisclosedValue(item);
                    if (disclosed is not null)
                    {
                        return disclosed;
                    }
                }

                return null;

            case JsonValueKind.String:
                string value = element.GetString() ?? string.Empty;
                return ForbiddenDenialDisclosures
                    .FirstOrDefault(forbidden => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase));

            default:
                return null;
        }
    }

    private static bool HasScopedMetadataProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Contains("tenant", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("organization", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("folder", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("policy", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("exist", StringComparison.OrdinalIgnoreCase)
                    || HasScopedMetadataProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (HasScopedMetadataProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<TestHost> StartHostAsync(
        IFolderArchivePolicyEvidenceProvider? archivePolicyEvidenceProvider = null,
        Func<string, string>? envelopeTenantTransform = null,
        ScopedLayeredFolderAuthorizationResultAccessor? authorizationAccessor = null)
    {
        MutableTenantAndClaimContext context = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        InMemoryEffectivePermissionsReadModel permissions = new();
        InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(new FixedUtcClock(Now));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
        InMemoryOrganizationProviderBindingRepository organizationRepository = new();
        Uri? hostUri = null;
        InProcessRejectionPropagatingGatewayClient gateway = new(
            () => new HttpClient { BaseAddress = hostUri! },
            () => context.PrincipalId,
            envelopeTenantTransform);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(context);
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(context);
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddSingleton<IFolderRepository>(repository);
        builder.Services.RemoveAll<IOrganizationProviderBindingRepository>();
        builder.Services.AddSingleton<IOrganizationProviderBindingRepository>(organizationRepository);
        builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
        builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycleReadModel);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenantStore);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        if (authorizationAccessor is not null)
        {
            // Production registers this accessor TryAddScoped. The test override is deliberately a
            // singleton so the caller can observe one instance across requests: that is what makes
            // `Current is null` after an unwind, and a successful second BeginScope on the retry leg,
            // observable at all. It also means these assertions prove EndScope ran -- not the
            // per-request isolation that production gets from the scoped lifetime.
            builder.Services.RemoveAll<ILayeredFolderAuthorizationResultAccessor>();
            builder.Services.AddSingleton<ILayeredFolderAuthorizationResultAccessor>(authorizationAccessor);
        }

        if (archivePolicyEvidenceProvider is not null)
        {
            // Register against the interface explicitly: an inferred service type would silently stop
            // replacing the baseline allow-everything provider if this parameter were ever narrowed to
            // a concrete fake, and the denial rows would then pass for the wrong reason.
            builder.Services.RemoveAll<IFolderArchivePolicyEvidenceProvider>();
            builder.Services.AddSingleton<IFolderArchivePolicyEvidenceProvider>(archivePolicyEvidenceProvider);
        }

        builder.Services.RemoveAll<IRepositoryCreationReadinessValidator>();
        builder.Services.AddSingleton<IRepositoryCreationReadinessValidator>(new ReadyRepositoryCreationReadinessValidator());
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton(timeProvider);

        WebApplication app = builder.Build();
        if (archivePolicyEvidenceProvider is CancelMidFlightFolderArchivePolicyEvidenceProvider cancellationProvider)
        {
            app.Use(async (context, next) =>
            {
                bool isProcessRequest = context.Request.Path
                    .StartsWithSegments("/process", StringComparison.OrdinalIgnoreCase);
                try
                {
                    await next(context).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (isProcessRequest)
                {
                    cancellationProvider.ObserveServerCancellation();
                    throw;
                }
                finally
                {
                    if (isProcessRequest)
                    {
                        cancellationProvider.CompleteRequest();
                    }
                }
            });
        }

        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        hostUri = new Uri(app.Urls.First());
        return new TestHost(
            app,
            new HttpClient { BaseAddress = hostUri },
            gateway,
            context,
            repository,
            tenantStore,
            permissions,
            lifecycleReadModel,
            organizationRepository);
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
        InProcessRejectionPropagatingGatewayClient Gateway,
        MutableTenantAndClaimContext Context,
        InMemoryFolderRepository Repository,
        InMemoryFolderTenantAccessProjectionStore TenantStore,
        InMemoryEffectivePermissionsReadModel Permissions,
        InMemoryFolderLifecycleStatusReadModel LifecycleReadModel,
        InMemoryOrganizationProviderBindingRepository OrganizationRepository) : IAsyncDisposable
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
