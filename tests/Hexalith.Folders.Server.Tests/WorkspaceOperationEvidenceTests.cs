using Xunit;
using Hexalith.Folders.Client.Native;
using Hexalith.Folders.NativeContent.Tests;
using Shouldly;

namespace Hexalith.Folders.Server.Tests;

public sealed partial class WorkspaceOperationEvidenceTests
{
    [Fact]
    public async Task AbsenceRequiresTheTasksLiveOwnerIssuedFence()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        Hexalith.Folders.Client.Native.NativeLease lease;
        try { lease = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", new() { Operation = "acquire", RequestedLeaseSeconds = 20 }, TestContext.Current.CancellationToken).ConfigureAwait(true); }
        catch (Exception error) { throw new InvalidOperationException(fixture.Gateway.Failure?.ToString() ?? error.ToString()); }
        lease.LockFence.ShouldBe(1); lease.OwnershipProof.ShouldNotBeNullOrWhiteSpace();
        lease.ExpiresAt.ShouldBe(fixture.Clock.Now.AddSeconds(20));
        var request = Request() ; request.LookupKind = "absence"; request.LockFence = lease.LockFence;
        var evidence = await client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        evidence.Status.ShouldBe("absent"); evidence.ExpectedFileVersion.ShouldBe(0);
        request.LockFence++;
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", request, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        fixture.Clock.Now = lease.ExpiresAt;
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "renew", RequestedLeaseSeconds = 20, LockId = lease.LockId, OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence }, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockFence.ShouldBe(1);
        (await fixture.BindingsAsync().ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("lock_workspace", "acquire")]
    [InlineData("mutate_files", "acquire")]
    [InlineData("lock_workspace", "renew")]
    [InlineData("mutate_files", "renew")]
    [InlineData("lock_workspace", "release")]
    [InlineData("mutate_files", "release")]
    [InlineData("lock_workspace", "validate")]
    [InlineData("mutate_files", "validate")]
    [InlineData("lock_workspace", "complete")]
    [InlineData("mutate_files", "complete")]
    public async Task LeaseCannotBypassEitherCurrentOwnerPermission(string deniedAction, string operation)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        NativeLease lease = new();
        if (operation != "acquire") lease = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        fixture.Authority.Denied.Add(deniedAction);
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = operation, RequestedLeaseSeconds = 300, LockId = lease.LockId, OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence },
            TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(404);
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockHolderTaskId.ShouldBe(operation == "acquire" ? null : "task-a");
        fixture.Gateway.Mutations.ShouldBe(0);
    }
    [Fact]
    public async Task MissingAcceptedLookupDoesNotRequireALeaseOrProveAbsence()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        (await client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", Request(), TestContext.Current.CancellationToken).ConfigureAwait(true)).Status.ShouldBe("missing");
        var absence = Request(); absence.LookupKind = "absence";
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", absence, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
    }
    [Fact]
    public async Task StaleUploadAndFingerprintConflictPreserveOnePublicationAndANewerHolder()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        var lease = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var path = new Hexalith.Folders.Client.Generated.PathMetadata { NormalizedPath = "docs/a.txt", DisplayName = "a.txt",
            PathPolicyClass = Hexalith.Folders.Client.Generated.PathMetadataPathPolicyClass.Metadata_only,
            UnicodeNormalization = Hexalith.Folders.Client.Generated.PathMetadataUnicodeNormalization.NFC };
        var mutation = Hexalith.Folders.Client.Convenience.FileUpload.BuildInlineFileMutation(new byte[] { 1, 2 }, "application/octet-stream", path,
            "operation_0123456789", expectedFileVersion: 0, expectedLockFence: lease.LockFence + 1);
        string key = Hexalith.Folders.Client.Convenience.FileUpload.ComputeIdempotencyKey(mutation, "workspace-a", "task-a");
        Newtonsoft.Json.Linq.JObject Wire()
        { var body = Newtonsoft.Json.Linq.JObject.FromObject(mutation); body["requestSchemaVersion"] = "v1"; return body; }
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.AddNativeFileAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            key, Wire(), TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        (await fixture.StateAsync().ConfigureAwait(true)).ContentPublications.ShouldBeEmpty();
        mutation.ExpectedLockFence = lease.LockFence;
        _ = await client.AddNativeFileAsync("folder-a", "workspace-a", "correlation-a", "task-a", key, Wire(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var query = Request(); query.OperationId = mutation.OperationId; query.AcceptedKey = key;
        query.ContentHashReference = mutation.ContentHashReference; query.ByteLength = 2; query.MediaType = "application/octet-stream";
        var publication = await client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", query,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        publication.Status.ShouldBe("accepted");
        var complete = new NativeLeaseRequest { Operation = "complete", RequestedLeaseSeconds = 300, LockId = lease.LockId,
            OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence, OperationId = mutation.OperationId, AcceptedKey = key, Fingerprint = "wrong" };
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", complete,
            TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        complete.Fingerprint = publication.Fingerprint;
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", complete, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-b", "task-b",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", complete, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var state = await fixture.StateAsync().ConfigureAwait(true);
        state.WorkspaceLockHolderTaskId.ShouldBe("task-b"); state.WorkspaceLockFence.ShouldBe(2); state.ContentPublications.Count.ShouldBe(1);
    }
    [Fact]
    public async Task NativeAcquireRenewAndReleaseReachTheSupportedLockHttpResponseAndGeneratedDecoder()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        var lease = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await fixture.ProjectLockAsync().ConfigureAwait(true);
        var acquired = await LockAsync().ConfigureAwait(true); acquired.Lease.LockFence.ShouldBe(lease.LockFence);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(10);
        lease = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "renew", RequestedLeaseSeconds = 300, LockId = lease.LockId, OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await fixture.ProjectLockAsync().ConfigureAwait(true);
        var renewed = await LockAsync().ConfigureAwait(true); renewed.Lease.LockFence.ShouldBe(lease.LockFence); renewed.Lease.EffectiveAt.ShouldBe(fixture.Clock.Now);
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "release", RequestedLeaseSeconds = 300, LockId = lease.LockId, OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await fixture.ProjectLockAsync().ConfigureAwait(true); (await LockAsync().ConfigureAwait(true)).LockState.ShouldBe(Hexalith.Folders.Client.Generated.LockState.Unlocked);
        async Task<Hexalith.Folders.Client.Generated.WorkspaceLockStatus> LockAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/lock");
            request.Headers.Add("X-Correlation-Id", "correlation-a"); request.Headers.Add("X-Hexalith-Task-Id", "task-a"); request.Headers.Add("X-Hexalith-Freshness", "read_your_writes");
            using var response = await fixture.Client().SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Hexalith.Folders.Client.Generated.WorkspaceLockStatus>(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))!;
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task DirectSdkUploadCannotAssertAStaleOrFutureOwnerFence(long assertedFence)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        int writes = fixture.Dapr.TrySaveByteOperations.Count;
        await Should.ThrowAsync<Hexalith.EventStore.DomainService.Content.ContentConflictException>(() => fixture.DirectUploadAsync("task-a", assertedFence)).ConfigureAwait(true);
        fixture.Dapr.TrySaveByteOperations.Count.ShouldBe(writes);
    }
    [Theory]
    [InlineData("operation-evidence")]
    [InlineData("task-lease")]
    [InlineData("files/add")]
    public async Task ForeignTenantSelectorsDenyBeforeEvidenceLeaseOrUpload(string operation)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/native/" + operation)
            { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Correlation-Id", "correlation-a"); request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Tenant-Id", "tenant-b");
        using var response = await fixture.Client().SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.ShouldNotContain("expectedFileVersion"); body.ShouldNotContain("lockFence"); body.ShouldNotContain("fileId");
        var state = await fixture.StateAsync().ConfigureAwait(true); state.WorkspaceLockFence.ShouldBe(0); state.ContentPublications.ShouldBeEmpty();
        fixture.Gateway.Mutations.ShouldBe(0);
    }
    [Theory]
    [InlineData("RenewalIntervalSeconds", 0)]
    [InlineData("AuthorizationIntervalSeconds", 16)]
    [InlineData("RevocationEffectSeconds", 5)]
    [InlineData("ExpiredToStaleSeconds", -1)]
    public async Task InvalidC7CeilingsCannotIssueALease(string setting, int value)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        foreach (var pair in new Dictionary<string, int> { ["RenewalIntervalSeconds"] = 30, ["AuthorizationIntervalSeconds"] = 15,
            ["RevocationEffectSeconds"] = 60, ["ExpiredToStaleSeconds"] = 60 }) fixture.App.Configuration["Folders:NativeLease:" + pair.Key] = pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fixture.App.Configuration["Folders:NativeLease:" + setting] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var client = new NativeClient(fixture.Client());
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(503);
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockFence.ShouldBe(0);
    }
    private static NativeEvidenceRequest Request() => new()
    {
        LookupKind = "accepted", OperationId = "operation-a", AcceptedKey = "sha256:" + new string('a', 64),
        PathMetadata = new() { NormalizedPath = "docs/a.txt", DisplayName = "a.txt", PathPolicyClass = "metadata_only", UnicodeNormalization = "NFC" },
        ContentHashReference = "hashref_" + new string('b', 64), ByteLength = 1, MediaType = "text/plain",
    };
}
