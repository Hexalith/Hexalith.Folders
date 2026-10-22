using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Shouldly;
using Xunit;
using PathMetadata = Hexalith.Folders.Aggregates.Folder.PathMetadata;

namespace Hexalith.Folders.Server.Tests;

internal sealed class WorkspaceContentProductionFixture : IAsyncDisposable
{
    internal readonly RecordingDaprClient Dapr = new() { PersistTypedState = true };
    internal readonly WorkspaceContentProductionGateway Gateway = new();
    internal readonly WorkspaceContentProductionClock Clock = new();
    internal readonly WorkspaceContentProductionSensitivityClassifier Sensitivity = new();
    internal readonly WorkspaceContentProductionPermissionProvider Permissions = new();
    internal readonly ClaimsPrincipal Principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-a")], "production-test"));
    internal ContentScope ContentScope { get; } = new("tenant-a", "folders", "folder-a", "workspace-a");
    internal WebApplication App { get; private set; } = null!;
    private DaprAuthoritativeContentStore _seed = null!;
    private string _task = "task-a";
    internal static async Task<WorkspaceContentProductionFixture> CreateAsync(bool expireAfterPreparation = false)
    {
        var fixture = new WorkspaceContentProductionFixture();
        fixture.App = FileContextEndpointTests.BuildProductionApp(builder =>
        {
            builder.Configuration["Folders:WorkspaceFilePolicy:Version"] = "1.1.0";
            builder.Configuration["Folders:WorkspaceFilePolicy:ProtectionMode"] = "non_confidential";
            builder.Configuration["Folders:WorkspaceFilePolicy:Includes:0"] = "**";
            Replace<DaprClient>(builder, fixture.Dapr); Replace<IEventStoreGatewayClient>(builder, fixture.Gateway);
            Replace<TimeProvider>(builder, fixture.Clock); Replace<IWorkspaceFileSensitivityClassifier>(builder, fixture.Sensitivity);
            Replace<IFolderPermissionEvidenceProvider>(builder, fixture.Permissions);
            if (expireAfterPreparation)
            {
                builder.Services.RemoveAll<IFolderRepository>(); builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();
                Replace<IContentAuthority>(builder, new WorkspaceContentProductionSeedAuthority(false));
                builder.Services.RemoveAll<IAuthoritativeContentStore>();
                builder.Services.AddScoped<IAuthoritativeContentStore>(services => new WorkspaceContentExpiringStore(
                    new DaprAuthoritativeContentStore(fixture.Dapr, services.GetRequiredService<IContentAuthority>(), Options.Create(new AuthoritativeContentOptions()), fixture.Clock), fixture.Clock));
            }
        });
        fixture.App.Use(async (http, next) => { http.User = fixture.Principal; await next(http).ConfigureAwait(true); });
        fixture.Gateway.Events.AddRange([
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "Content", null, null, [], FolderLifecycleState.Active,
                FolderRepositoryBindingState.Unbound, "user-a", "correlation-a", "task-a", "created", "fp-created", fixture.Clock.Now),
            new RepositoryBindingRequested("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "profile-a", "policy-a", "user-a", "correlation-a", "task-a", "requested", "fp-requested", fixture.Clock.Now),
            new RepositoryBound("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "correlation-a", "task-a", "bound", "fp-bound", fixture.Clock.Now),
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared, null, "prepare", "correlation-a", "task-a", "prepared", "fp-prepared", fixture.Clock.Now),
            fixture.Lock("task-a", 1),
        ]);
        fixture._seed = new(fixture.Dapr, new WorkspaceContentProductionSeedAuthority(), Options.Create(new AuthoritativeContentOptions()), fixture.Clock);
        await fixture.App.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return fixture;
    }
    private static void Replace<T>(WebApplicationBuilder builder, T service) where T : class
    { builder.Services.RemoveAll<T>(); builder.Services.AddSingleton(service); }
    internal FolderState State => FolderState.Empty.Apply(Gateway.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    internal IServiceScope Scope()
    {
        var scope = App.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = Principal, RequestServices = scope.ServiceProvider };
        return scope;
    }
    internal static PathMetadata Path(string path) => new(path, path.Split('/')[^1], "content_allowed", "NFC");
    internal WorkspaceFileContextSourceRequest RangeRequest(long start, long end) => new(WorkspaceFileContextQueryKind.Range, "tenant-a", "folder-a", "workspace-a", "user-a", "read_file_content", _task, "correlation-a", null,
        [new(Path("docs/a.txt"), "digest", "content_allowed", "tenant_sensitive", "not_redacted")], null, null, 1, null, start, end);
    internal Task<ContentReceipt> StageAsync(byte[] bytes) => _seed.UploadAsync(Principal,
        new(ContentScope, _task, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, "text/plain"), new MemoryStream(bytes), TestContext.Current.CancellationToken);
    internal async Task<WorkspaceFileMutationAccepted> PublishAsync(string path, string text, WorkspaceFileMutationAccepted? prior = null, bool remove = false)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text); string operation = "operation_" + Guid.NewGuid().ToString("N");
        string file = prior?.OwnerFileId ?? "file_" + Guid.NewGuid().ToString("N");
        string kind = remove ? "remove" : prior is null ? "add" : "change";
        ContentReceipt? receipt = remove ? null : await _seed.UploadAsync(Principal,
            new(ContentScope, _task, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, "text/plain", operation), new MemoryStream(bytes), TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var scope = Scope(); var policy = scope.ServiceProvider.GetRequiredService<IWorkspaceFilePolicySource>().Capture();
        string digest = WorkspacePathPolicyValidator.ComputePathMetadataDigest(Path(path));
        var metadata = new Dictionary<string, string> { ["path"] = path, ["displayName"] = path.Split('/')[^1], ["pathPolicyClass"] = "content_allowed", ["contentReadable"] = "true", ["policyVersion"] = policy.Version, ["policyDigest"] = policy.Digest, ["pathDigest"] = digest };
        var binding = new WorkspaceContentBinding("tenant-a", "folder-a", "workspace-a", _task, operation, operation, file!, (prior?.ContentBinding?.Version ?? 0) + 1, Gateway.Events.Count,
            State.WorkspaceLockFence, receipt?.Id, receipt is null ? null : "hashref_" + receipt.Sha256, receipt?.ByteLength, receipt?.MediaType, policy.Version, policy.Digest, kind, digest, !remove);
        await _seed.PrepareAsync(Principal, new(ContentScope, operation, _task, binding.LockFence, binding.ExpectedVersion, [new(file!, file!, binding.Version, receipt?.Id, metadata, receipt)]), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await _seed.RecoverAsync(Principal, ContentScope, operation, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var accepted = new WorkspaceFileMutationAccepted("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.FileMutated, operation, kind,
            remove ? "metadataOnlyRemoval" : "PutFileInline", "content_allowed", digest, binding.ContentHashReference, binding.ByteLength, binding.MediaType, remove ? null : "inline_decoded", binding.ByteLength,
            "user-a", "correlation-a", _task, operation, "fp-" + operation, Clock.Now) { ContentBinding = binding, OwnerFileId = file, MutationTargetDigest = WorkspacePathPolicyValidator.ComputeMutationTargetDigest("workspace-a", Path(path)) };
        Gateway.Events.Add(accepted); _ = State; return accepted;
    }
    internal void Commit() => Gateway.Events.Add(new WorkspaceCommitSucceeded("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.CommitSucceeded,
        "commit", "commitref_a", "succeeded", "author_a", "branch_a", "generated", "digest_a", "user-a", "correlation-a", _task, "commit", "fp-commit", Clock.Now));
    internal void Resume()
    {
        Gateway.Events.Add(new WorkspaceLockReleased("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspaceLockReleased,
            "lock-a", _task, "caller_completed", "lease_until_expiry", Clock.Now, Clock.Now, Clock.Now.AddHours(1), "user-a", "correlation-a", _task, "release", "fp-release", Clock.Now));
        _task = "task-b"; Gateway.Events.Add(Lock(_task, 2));
    }
    // Existing lifecycle completion closes the task without replacing the separately governed content commit.
    internal void FailTask() => Gateway.Events.Add(new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.CommitSucceeded,
        null, "terminal", "correlation-a", _task, "terminal", "fp-terminal", Clock.Now));
    private WorkspaceLockAcquired Lock(string task, long fence) => new("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspaceLocked,
        "lock-a", "exclusive_write", 3600, task, Clock.Now, Clock.Now, Clock.Now.AddHours(1), "lease_until_expiry", "user-a", "correlation-a", task, "locked-" + fence, "fp-locked-" + fence, Clock.Now) { LockFence = fence };
    internal HttpRequestMessage NewUploadRequest() => UploadRequest(new("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.FileMutated, "operation_0123456789", "add", "PutFileInline", "content_allowed", "digest", "hashref_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("original"))), 8, "text/plain", "inline_decoded", 8, "user-a", "correlation-a", "task-a", "key", "fp", Clock.Now), Encoding.UTF8.GetBytes("original"));
    internal static HttpRequestMessage UploadRequest(WorkspaceFileMutationAccepted accepted, byte[] bytes, bool multipart = false)
    {
        string path = "docs/a.txt";
        object body = new { requestSchemaVersion = "v1", fileOperationKind = "add", transportOperation = multipart ? "PutFileStream" : "PutFileInline", operationId = accepted.OperationId,
            pathMetadata = Path(path), contentHashReference = accepted.ContentHashReference, byteLength = accepted.ByteLength, expectedFileVersion = 0, expectedLockFence = accepted.ContentBinding?.LockFence ?? 1,
            inlineContent = multipart ? null : new { mediaType = "text/plain", contentBytes = Convert.ToBase64String(bytes) },
            streamDescriptor = multipart ? new { mediaType = "text/plain", declaredLength = accepted.ByteLength, observedLength = accepted.ByteLength, stagingReference = "staged_0123456789", observedContentHashReference = accepted.ContentHashReference, uploadMode = "request_body_stream" } : null };
        var json = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull })!;
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/files/add") { Content = multipart ? WorkspaceContentTransportTestServices.Multipart(JsonContent.Create(json), bytes) : JsonContent.Create(json) };
        Headers(request, accepted.TaskId); request.Headers.Add("Idempotency-Key", accepted.IdempotencyKey); return request;
    }
    internal HttpRequestMessage QueryRequest(string kind, object body)
    { var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/" + (kind == "range" ? "range-read" : kind)) { Content = JsonContent.Create(body) }; Headers(request, _task); request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task"); return request; }
    private static void Headers(HttpRequestMessage request, string task) { request.Headers.Add("X-Correlation-Id", "correlation_0123456789"); request.Headers.Add("X-Hexalith-Task-Id", task); }
    public async ValueTask DisposeAsync() { await App.DisposeAsync().ConfigureAwait(true); Dapr.Dispose(); }
}
