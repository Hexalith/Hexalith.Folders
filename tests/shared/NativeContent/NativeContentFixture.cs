#if CHATBOT_NATIVE_FOLDERS_OWNER_ALIAS
extern alias FoldersOwner;
using FoldersOwner::Hexalith.Folders.Server;
using FoldersOwner::Hexalith.Folders.Server.Authentication;
using FoldersOwner::Hexalith.Folders.Server.Authorization;
#else
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
#endif

using System.Security.Claims;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentFixture : IAsyncDisposable
{
    internal const string ReceiptKey = "TU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU0=";
    internal readonly RecordingDaprClient Dapr = new() { PersistTypedState = true };
    internal readonly NativeContentClock Clock = new();
    internal WebApplication App = null!;
    internal NativeContentGateway Gateway = null!;
    internal NativeContentAuthority Authority = null!;
    internal string Folder = "folder-a", Workspace = "workspace-a";
    internal List<string> Requests { get; } = [];
    internal string? ContentFailure { get; set; }
    internal ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity([new Claim("sub", "user-a"), new Claim("eventstore:tenant", "tenant-a"), new Claim("eventstore:permission", "mutate_files"),
        new Claim("eventstore:permission", "lock_workspace"), new Claim("eventstore:permission", "read_metadata"), new Claim("eventstore:permission", "read_workspace_lock")], "native-test"));
    internal static async Task<NativeContentFixture> CreateAsync(string folder = "folder-a", string workspace = "workspace-a")
    {
        var fixture = new NativeContentFixture { Folder = folder.Replace(':', '_'), Workspace = workspace.Replace(':', '_') };
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer(); builder.Services.AddAuthentication(); builder.Services.AddAuthorization(); builder.Services.AddHealthChecks();
        builder.Configuration["Folders:WorkspaceFilePolicy:Version"] = "1.1.0";
        builder.Configuration["Folders:WorkspaceFilePolicy:ProtectionMode"] = "non_confidential";
        builder.Configuration["Folders:WorkspaceFilePolicy:Includes:0"] = "**";
        builder.Configuration["Authentication:HumanAuthorityReceipt:SigningKey"] = ReceiptKey;
        builder.Services.AddFoldersServer();
        builder.Services.AddSingleton<HumanAuthorityReceiptVerifier>();
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddScoped(typeof(IFolderRepository), typeof(FolderDomainProcessor).Assembly.GetType("Hexalith.Folders.Server.ActorFoldedFolderRepository")!);
        builder.Services.RemoveAll<DaprClient>(); builder.Services.AddSingleton<DaprClient>(fixture.Dapr);
        builder.Services.RemoveAll<IAuthoritativeContentStore>();
        builder.Services.AddScoped<IAuthoritativeContentStore>(sp => new NativeContentDiagnosticStore(new DaprAuthoritativeContentStore(fixture.Dapr,
            sp.GetRequiredService<IContentAuthority>(), Microsoft.Extensions.Options.Options.Create(new AuthoritativeContentOptions()), fixture.Clock), fixture));
        builder.Services.RemoveAll<TimeProvider>(); builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.RemoveAll<IUtcClock>(); builder.Services.AddSingleton<IUtcClock>(fixture.Clock);
        builder.Services.AddSingleton<NativeContentAuthority>();
        Replace<ITenantContextAccessor>(); Replace<IEventStoreClaimTransformEvidenceAccessor>();
        Replace<IFolderPermissionEvidenceProvider>(); Replace<IDaprPolicyEvidenceProvider>();
        var tenants = new InMemoryFolderTenantAccessProjectionStore();
        await tenants.SaveAsync(new FolderTenantAccessProjection { TenantId = "tenant-a", Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence> { ["user-a"] = new("user-a", "Member") },
            Watermark = 1, LastEventTimestamp = fixture.Clock.Now.AddMinutes(-1), ProjectionWatermark = "tenant_watermark_v1" }).ConfigureAwait(true);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>(); builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenants);
        fixture.Gateway = new NativeContentGateway(fixture);
        builder.Services.RemoveAll<IEventStoreGatewayClient>(); builder.Services.AddSingleton<IEventStoreGatewayClient>(fixture.Gateway);
        fixture.App = builder.Build(); fixture.Authority = fixture.App.Services.GetRequiredService<NativeContentAuthority>(); fixture.Authority.Folder = fixture.Folder;
        fixture.App.Use(async (http, next) => { http.User = fixture.Principal; if (string.IsNullOrEmpty(http.Request.Headers.Authorization)) http.Request.Headers.Authorization = "Bearer native-test-human"; await next(http).ConfigureAwait(true); lock (fixture.Requests) fixture.Requests.Add(http.Request.Path + " " + http.Response.StatusCode); });
        fixture.App.UseAuthorization();
        fixture.App.MapFoldersServerEndpoints();
        await fixture.App.StartAsync().ConfigureAwait(true);
        DateTimeOffset now = fixture.Clock.Now;
        await fixture.Gateway.SeedAsync([
            new FolderCreated("tenant-a", "tenant-a", fixture.Folder, "Native", null, null, [], FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "seed", "seed", "created", "fp-created", now),
            new RepositoryBindingRequested("tenant-a", "tenant-a", fixture.Folder, "binding-a", "provider-a", "profile-a", "policy-a", "user-a", "seed", "seed", "requested", "fp-requested", now),
            new RepositoryBound("tenant-a", "tenant-a", fixture.Folder, "binding-a", "provider-a", "seed", "seed", "bound", "fp-bound", now),
            new WorkspacePreparationRequested("tenant-a", "tenant-a", fixture.Folder, fixture.Workspace, "binding-a", "policy-a", "workspace_policy_a",
                "user-a", "seed", "seed", "workspace_requested", "fp-workspace_requested", now),
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", fixture.Folder, fixture.Workspace, FolderWorkspaceLifecycleEvent.WorkspacePrepared, null, "prepare", "seed", "seed", "prepared", "fp-prepared", now),
        ]).ConfigureAwait(true);
        return fixture;
        void Replace<T>() where T : class { builder.Services.RemoveAll<T>(); builder.Services.AddSingleton<T>(sp => (T)(object)sp.GetRequiredService<NativeContentAuthority>()); }
    }
    // This retained historical seed is separate from the real processor used for every fresh Add.
    internal async Task<string> SeedHistoricalAsync(string task, string operation, string key, string normalizedPath, string displayName, byte[] bytes, bool retainKey, bool closeTask = true)
    {
        var contentScope = new ContentScope("tenant-a", "folders", Folder, Workspace);
        var store = new DaprAuthoritativeContentStore(Dapr, new NativeContentSeedAuthority(),
            Microsoft.Extensions.Options.Options.Create(new AuthoritativeContentOptions()), Clock);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var receipt = await store.UploadAsync(Principal, new(contentScope, task, hash, bytes.Length, "application/octet-stream", operation),
            new MemoryStream(bytes)).ConfigureAwait(true);
        await Gateway.AppendSeedAsync(new WorkspaceNativeTaskChanged("tenant-a", "tenant-a", Folder, Workspace, "acquire", "legacy_lock", 1, 300,
            Clock.Now, Clock.Now.AddSeconds(300), null, "user-a", "legacy_correlation", task, "legacy_lock_command", "legacy_lock_fp", Clock.Now)).ConfigureAwait(true);
        using var services = App.Services.CreateScope();
        var policy = services.ServiceProvider.GetRequiredService<Hexalith.Folders.Queries.FileContext.IWorkspaceFilePolicySource>().Capture();
        var path = new PathMetadata(normalizedPath, displayName, "metadata_only", "NFC");
        string digest = (string)typeof(WorkspacePathPolicyValidator).GetMethod("ComputePathMetadataDigest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [path])!;
        string target = (string)typeof(WorkspacePathPolicyValidator).GetMethod("ComputeMutationTargetDigest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [Workspace, path])!;
        string file = "file_" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        long head = (await Gateway.EventsAsync().ConfigureAwait(true)).Count;
        var binding = new WorkspaceContentBinding("tenant-a", Folder, Workspace, task, "legacy_command", operation, file, 1, head, 1,
            receipt.Id, "hashref_" + hash, bytes.Length, "application/octet-stream", policy.Version, policy.Digest, "add", digest)
            { AcceptedSdkKey = retainKey ? key : null };
        var metadata = new Dictionary<string, string> { ["path"] = normalizedPath, ["displayName"] = displayName, ["pathPolicyClass"] = "metadata_only",
            ["pathDigest"] = digest, ["policyVersion"] = policy.Version, ["policyDigest"] = policy.Digest, ["contentReadable"] = "false" };
        await store.PrepareAsync(Principal, new(contentScope, "legacy_command", task, 1, head, [new(file, file, 1, receipt.Id, metadata, receipt)])).ConfigureAwait(true);
        await store.RecoverAsync(Principal, contentScope, "legacy_command").ConfigureAwait(true);
        await Gateway.AppendSeedAsync(new WorkspaceFileMutationAccepted("tenant-a", "tenant-a", Folder, Workspace, FolderWorkspaceLifecycleEvent.FileMutated,
            operation, "add", "PutFileInline", "metadata_only", digest, "hashref_" + hash, bytes.Length, "application/octet-stream", "inline_decoded", bytes.Length,
            "user-a", "legacy_correlation", task, "legacy_command", "legacy_original_fingerprint", Clock.Now)
            { ContentBinding = binding, OwnerFileId = file, MutationTargetDigest = target }).ConfigureAwait(true);
        if (retainKey && closeTask) await Gateway.AppendSeedAsync(new WorkspaceNativeTaskChanged("tenant-a", "tenant-a", Folder, Workspace, "complete", "legacy_lock", 1, 300,
            Clock.Now, Clock.Now.AddSeconds(300), "legacy_command", "user-a", "legacy_correlation", task, "legacy_completion", "legacy_completion_fp", Clock.Now)).ConfigureAwait(true);
        return file;
    }
    internal HttpClient Client() => App.GetTestClient();
    internal async Task<ContentReceipt> DirectUploadAsync(string task, long fence, byte[]? content = null)
    {
        using var scope = App.Services.CreateScope();
        var context = new DefaultHttpContext { User = Principal, RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST"; context.Request.Path = "/api/v1/folders/" + Folder + "/workspaces/" + Workspace + "/native/files/add";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        byte[] bytes = content ?? [1];
        return await scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>().UploadAsync(Principal,
            new ContentUploadRequest(new("tenant-a", "folders", Folder, Workspace), task,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.Length, "application/octet-stream", "direct_upload") { LeaseFence = fence },
            new MemoryStream(bytes)).ConfigureAwait(true);
    }
    internal async Task<bool> DirectSealAsync(string task, long fence, bool completion, bool readOnly = false)
    {
        using var scope = App.Services.CreateScope();
        var principal = readOnly ? new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-a"), new Claim("eventstore:tenant", "tenant-a"),
            new Claim("eventstore:permission", "read_metadata"), new Claim("eventstore:permission", "read_workspace_lock")], "read-only")) : Principal;
        var context = new DefaultHttpContext { User = principal, RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST"; context.Request.Path = "/api/v1/folders/" + Folder + "/workspaces/" + Workspace + "/native/task-lease";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var store = scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>();
        var contentScope = new ContentScope("tenant-a", "folders", Folder, Workspace);
        return completion ? await store.TrySealTaskForCompletionAsync(principal, contentScope, task, fence).ConfigureAwait(true)
            : await store.TryQuiesceTaskLeaseAsync(principal, contentScope, task, fence).ConfigureAwait(true);
    }
    internal async Task RecordExpiryAsync(string task = "task-a")
    {
        await Gateway.AppendSeedAsync(new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", Folder, Workspace,
            FolderWorkspaceLifecycleEvent.LockLeaseExpired, null, "native-expiry", "correlation-a", task,
            "native-expiry-" + (await Gateway.EventsAsync().ConfigureAwait(true)).Count, "native-expiry-fp", Clock.Now)).ConfigureAwait(true);
    }
    internal async Task ProjectLockAsync()
    {
        var events = await Gateway.EventsAsync().ConfigureAwait(true);
        var source = events.Select(item => new Hexalith.Folders.Projections.OperationalStatus.OperationalStatusEvent(item.SequenceNumber,
            item.MessageId, item.Timestamp, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(item.Payload)),
            typeof(IFolderEvent).Assembly.GetType(item.EventTypeName) is { } type ? (IFolderEvent)JsonSerializer.Deserialize(item.Payload, type)! : null));
        var folded = Hexalith.Folders.Projections.OperationalStatus.OperationalStatusFold.Apply("tenant-a", Folder, null, source);
        var readModel = (Hexalith.Folders.Queries.Folders.InMemoryWorkspaceLockStatusReadModel)App.Services.GetRequiredService<Hexalith.Folders.Queries.Folders.IWorkspaceLockStatusReadModel>();
        foreach (var workspace in folded.Workspaces.Values) readModel.Save(workspace.Lock);
    }
    internal async Task<FolderState> StateAsync()
    {
        var events = await Gateway.EventsAsync().ConfigureAwait(true);
        return FolderState.Empty.Apply(events.Where(item => typeof(IFolderEvent).Assembly.GetType(item.EventTypeName) is not null)
            .Select(item => (IFolderEvent)JsonSerializer.Deserialize(item.Payload, typeof(IFolderEvent).Assembly.GetType(item.EventTypeName)!)!), FolderStreamName.Create("tenant-a", Folder));
    }
    internal async Task<IReadOnlyList<ContentBinding>> BindingsAsync()
    {
        using var scope = App.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = Principal, RequestServices = scope.ServiceProvider };
        return await scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>().ListAsync(Principal, new("tenant-a", "folders", Folder, Workspace)).ConfigureAwait(true);
    }
    internal async Task<byte[]> ReadAsync(ContentBinding binding)
    {
        using var scope = App.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = Principal, RequestServices = scope.ServiceProvider };
        return (await scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>().ReadAsync(Principal,
            new(new("tenant-a", "folders", Folder, Workspace), binding.Change.FileId, binding.Change.IncarnationId, binding.Change.Version, 262144)).ConfigureAwait(true)).Bytes.ToArray();
    }
    public async ValueTask DisposeAsync() { await App.DisposeAsync().ConfigureAwait(true); Dapr.Dispose(); }
}
