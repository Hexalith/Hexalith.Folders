using System.Net.Http.Json;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Folders.Server.Content;
using Hexalith.Folders.Queries.FileContext;
using System.Net;
using System.Security.Claims;
using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>Exercises REST, the shared gateway round-trip, /process, mutation gates and SDK persistence.</summary>
/// <remarks>The SDK's CAS double isolates the in-process lane; the AppHost class independently qualifies real Redis/Dapr and production source authority.</remarks>
public sealed class WorkspaceContentProcessWiringTests
{
    /// <summary>Requires actual byte persistence and accepted metadata, then rejects stale and hidden mutations without publication.</summary>
    [Fact]
    public async Task ActualBytesShouldTraverseTheGatewayAndOwningProcess()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedTimeProvider(now);
        var repository = new InMemoryFolderRepository(timeProvider: clock);
        Seed(repository, now);
        var context = new TenantContext();
        var tenants = new InMemoryFolderTenantAccessProjectionStore();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a", Enabled = true, Watermark = 1, LastEventTimestamp = now, ProjectionWatermark = "tenant-a:1",
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal) { ["user-a"] = new("user-a", "Owner") },
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var permissions = new InMemoryEffectivePermissionsReadModel();
        permissions.Save(new("tenant-a", "tenant-a", "folder-a", EffectivePermissionsFolderLifecycleState.Active,
            [new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("user-a"), "mutate_files", 1, now),
             new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("user-a"), "read_metadata", 1, now),
             new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("user-a"), "read_file_content", 1, now)],
            new("read_your_writes", now, "permission-watermark-a", false, null), true, null));
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        Func<HttpClient>? factory = null;
        var gateway = new InProcessRejectionPropagatingGatewayClient(() => factory!(), () => context.PrincipalId);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Folders:WorkspaceFilePolicy:Version"] = "1.1.0";
        builder.Configuration["Folders:WorkspaceFilePolicy:ProtectionMode"] = "non_confidential";
        builder.Configuration["Folders:WorkspaceFilePolicy:Includes:0"] = "**";
        builder.Configuration["Folders:WorkspaceFilePolicy:Restricted:0"] = "private/**";
        builder.Services.AddFoldersServerTestDefaults(); builder.Services.AddFoldersServer();
        Replace<IEventStoreGatewayClient>(builder, gateway); Replace<IFolderRepository>(builder, repository);
        Replace<ITenantContextAccessor>(builder, context); Replace<IEventStoreClaimTransformEvidenceAccessor>(builder, context);
        Replace<IFolderTenantAccessProjectionStore>(builder, tenants); Replace<IEffectivePermissionsReadModel>(builder, permissions);
        Replace<IEventStoreAuthorizationValidator>(builder, new AllowingEventStoreAuthorizationValidator());
        Replace<TimeProvider>(builder, clock); Replace<IUtcClock>(builder, new FixedUtcClock(now));
        Replace<DaprClient>(builder, dapr); Replace<IContentAuthority>(builder, new SourceAuthority(repository));
        builder.Services.RemoveAll<WorkspaceContentStateReader>();
        builder.Services.AddScoped<WorkspaceContentStateReader>(services =>
        {
            var replay = services.GetRequiredService<FolderEventReplay>();
            var actor = new ActorFoldedFolderRepository(replay);
            actor.Begin("tenant-a", "folder-a", new DomainServiceCurrentState(JsonSerializer.SerializeToElement(repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))), [], 15, 15));
            return new(actor, gateway, replay, clock);
        });
        await using WebApplication app = builder.Build();
        app.Use(async (http, next) =>
        {
            http.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-a")], "integration"));
            await next(http).ConfigureAwait(false);
        });
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        factory = app.GetTestClient;
        using HttpClient client = app.GetTestClient();
        byte[] bytes = Encoding.UTF8.GetBytes("Actual bytes cross the process gate.\r\n");
        var descriptor = new FileUploadDescriptor
        {
            FolderId = "folder-a", WorkspaceId = "workspace-a", OperationId = "operation-a", MediaType = "text/plain",
            ExpectedFileVersion = 0, ExpectedLockFence = 1,
            PathMetadata = new() { NormalizedPath = "docs/a.txt", DisplayName = "a.txt", PathPolicyClass = PathMetadataPathPolicyClass.Content_allowed },
        };
        using (var content = new MemoryStream(bytes))
        using (HttpResponseMessage response = await client.UploadWorkspaceFileAsync(descriptor, content, "key-a", "correlation-a", "task-a", TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("accepted");
        }
        gateway.ProcessCalls.ShouldBe(1); gateway.LastWireEventCount.ShouldBe(1);
        FolderState state = repository.Load(FolderStreamName.Create("tenant-a", "folder-a"));
        WorkspaceContentBinding binding = state.ContentPublications.Values.ShouldHaveSingleItem();
        using IServiceScope scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>();
        ContentReadResult body = await store.ReadAsync(new(new ClaimsIdentity([], "integration")),
            new(new("tenant-a", "folders", "folder-a", "workspace-a"), binding.FileId, binding.FileId, 1, 1048576), TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.Bytes.AsSpan().SequenceEqual(bytes).ShouldBeTrue();
        foreach (var write in dapr.TrySaveByteOperations.Where(write => write.Key.Contains(":metadata:", StringComparison.Ordinal)))
            Encoding.UTF8.GetString(write.Value).ShouldNotContain(Convert.ToBase64String(bytes));
        int before = repository.EventsAppended;
        using (var content = new MemoryStream(bytes))
        using (HttpResponseMessage stale = await client.UploadWorkspaceFileAsync(descriptor with { OperationId = "stale", ExpectedLockFence = 2 },
            content, "stale-key", "correlation-stale", "task-a", TestContext.Current.CancellationToken).ConfigureAwait(true))
            stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        repository.EventsAppended.ShouldBe(before);
        using (var content = new MemoryStream(bytes))
        using (HttpResponseMessage hidden = await client.UploadWorkspaceFileAsync(descriptor with { OperationId = "hidden", PathMetadata = new()
            { NormalizedPath = "private/a.txt", DisplayName = "a.txt", PathPolicyClass = PathMetadataPathPolicyClass.Content_allowed } },
            content, "hidden-key", "correlation-hidden", "task-a", TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await hidden.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotContain("private/a.txt");
        }
        repository.EventsAppended.ShouldBe(before);
        foreach (var file in new[] { ("docs/b.txt", "needle in docs"), ("other/c.txt", "needle elsewhere") })
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(file.Item2));
            using var uploaded = await client.UploadWorkspaceFileAsync(descriptor with { OperationId = "operation-" + Guid.NewGuid().ToString("N"), PathMetadata = new()
                { NormalizedPath = file.Item1, DisplayName = file.Item1.Split('/')[^1], PathPolicyClass = PathMetadataPathPolicyClass.Content_allowed } }, content,
                Guid.NewGuid().ToString("N"), "correlation-a", "task-a", TestContext.Current.CancellationToken).ConfigureAwait(true);
            uploaded.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }
        async Task<string> QueryAsync(string family, string term, int limit)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/" + family)
            { Content = family == "search" ? JsonContent.Create(new { requestSchemaVersion = "v1", queryFamily = family, queryText = term, limit })
                : JsonContent.Create(new { requestSchemaVersion = "v1", queryFamily = family, globPattern = term, limit }) };
            request.Headers.Add("X-Correlation-Id", "correlation_0123456789"); request.Headers.Add("X-Hexalith-Task-Id", "task-a"); request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            string wire = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, wire); return wire;
        }
        var search = Newtonsoft.Json.JsonConvert.DeserializeObject<FileSearchResult>(await QueryAsync("search", "needle", 10).ConfigureAwait(true)).ShouldNotBeNull();
        search.Items.Select(item => item.Path.NormalizedPath).ShouldBe(["docs/b.txt", "other/c.txt"]);
        Newtonsoft.Json.JsonConvert.DeserializeObject<FileSearchResult>(await QueryAsync("search", "absent_phrase", 10).ConfigureAwait(true))!.Items.ShouldBeEmpty();
        var truncated = Newtonsoft.Json.JsonConvert.DeserializeObject<FileSearchResult>(await QueryAsync("search", "needle", 1).ConfigureAwait(true)).ShouldNotBeNull();
        truncated.Page.IsTruncated.ShouldBeTrue(); truncated.Items.ShouldHaveSingleItem().Path.NormalizedPath.ShouldBe("docs/b.txt");
        var glob = Newtonsoft.Json.JsonConvert.DeserializeObject<FileTreeResult>(await QueryAsync("glob", "docs/*.txt", 10).ConfigureAwait(true)).ShouldNotBeNull();
        glob.Items.Select(item => item.Path.NormalizedPath).ShouldBe(["docs/a.txt", "docs/b.txt"]);
        Newtonsoft.Json.JsonConvert.DeserializeObject<FileTreeResult>(await QueryAsync("glob", "absent/*.txt", 10).ConfigureAwait(true))!.Items.ShouldBeEmpty();
    }

    private static void Replace<T>(WebApplicationBuilder builder, T service) where T : class
    { builder.Services.RemoveAll<T>(); builder.Services.AddSingleton(service); }
    private static void Seed(InMemoryFolderRepository repository, DateTimeOffset now)
        => repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"),
        [
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "Content", null, null, [], FolderLifecycleState.Active,
                FolderRepositoryBindingState.Unbound, "user-a", "seed-correlation", "task-a", "created", "created-fp", now.AddMinutes(-5)),
            new RepositoryBindingRequested("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "profile-a", "policy-a",
                "user-a", "seed-correlation", "task-a", "requested", "requested-fp", now.AddMinutes(-4)),
            new RepositoryBound("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "seed-correlation", "task-a", "bound", "bound-fp", now.AddMinutes(-3)),
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                null, "prepare-a", "seed-correlation", "task-a", "prepared", "prepared-fp", now.AddMinutes(-2)),
            new WorkspaceLockAcquired("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspaceLocked,
                "workspace_lock_a", "exclusive_write", 3600, "task-a", now, now, now.AddHours(1), "lease_until_expiry",
                "user-a", "seed-correlation", "task-a", "locked", "locked-fp", now) { LockFence = 1 },
        ]);

    private sealed class TenantContext : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
    {
        public string? AuthoritativeTenantId => "tenant-a";
        public string? PrincipalId => "user-a";
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken) => EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [actionToken]);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed class SourceAuthority(InMemoryFolderRepository repository) : IContentAuthority
    {
        public Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken token)
        { if (scope.TenantId != "tenant-a" || principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException(); return Task.CompletedTask; }
        public Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken token) => Task.CompletedTask;
        public Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken token) => Task.CompletedTask;
        public Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken token)
            => Task.FromResult(repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).ContentPublications.Values.Any(item => item.CommandId == request.CommandId)
                ? ContentPublicationOutcome.Accepted : ContentPublicationOutcome.Pending);
        public Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken token) => Task.FromResult(new ContentDisposition(1, false, true, true, false));
        public Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken token) => Task.FromResult(false);
    }
}
