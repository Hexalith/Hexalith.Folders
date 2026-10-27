using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Client.Native;
using Hexalith.Folders.NativeContent.Tests;
using Hexalith.Folders.Projections.TenantAccess;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;
public sealed partial class WorkspaceOperationEvidenceTests
{
    [Theory]
    [InlineData("operation-evidence", "{}") ]
    [InlineData("operation-evidence", "")]
    [InlineData("operation-evidence", "null")]
    [InlineData("operation-evidence", "{\"operationId\":null}")]
    [InlineData("operation-evidence", "{\"pathMetadata\":{\"normalizedPath\":null}}")]
    [InlineData("task-lease", "{}") ]
    [InlineData("task-lease", "")]
    [InlineData("task-lease", "null")]
    [InlineData("task-lease", "{\"operation\":\"renew\",\"requestedLeaseSeconds\":300}")]
    public async Task MalformedNativeInputIsBoundedValidationAfterAuthorization(string operation, string body)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/native/" + operation)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Correlation-Id", "correlation-a"); request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        using var response = await fixture.Client().SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        ((int)response.StatusCode).ShouldBeInRange(400, 499);
        response.StatusCode.ShouldNotBe(HttpStatusCode.ServiceUnavailable);
        (await fixture.StateAsync().ConfigureAwait(true)).ContentPublications.ShouldBeEmpty();
        fixture.Gateway.Submissions.ShouldBeEmpty();
    }
    [Theory]
    [InlineData(false, "read-only")]
    [InlineData(true, "read-only")]
    [InlineData(false, "task")]
    [InlineData(true, "task")]
    [InlineData(false, "future-fence")]
    [InlineData(true, "future-fence")]
    public async Task DirectSealCannotUseReadAuthorityOrForeignTaskOrFutureFence(bool completion, string denial)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        var lease = await AcquireAsync(client).ConfigureAwait(true);
        int writes = fixture.Dapr.TrySaveByteOperations.Count;
        var error = await Record.ExceptionAsync(() => fixture.DirectSealAsync(denial == "task" ? "task-other" : "task-a",
            denial == "future-fence" ? lease.LockFence + 1 : lease.LockFence, completion, denial == "read-only")).ConfigureAwait(true);
        (error is UnauthorizedAccessException or ContentConflictException).ShouldBeTrue(error?.ToString());
        fixture.Dapr.TrySaveByteOperations.Count.ShouldBe(writes);
    }
    [Theory]
    [InlineData("validate")]
    [InlineData("acquire")]
    public async Task LeaseObservationRejectsAHeadChangeDuringFinalAuthorization(string operation)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client()); var lease = await AcquireAsync(client).ConfigureAwait(true);
        int reads = fixture.Gateway.StreamReads;
        fixture.Authority.BeforePermission = async permission =>
        {
            if (fixture.Gateway.StreamReads <= reads) return;
            fixture.Authority.BeforePermission = null;
            _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", Owned("release", lease), TestContext.Current.CancellationToken).ConfigureAwait(true);
        };
        var error = await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            operation == "acquire" ? new() { Operation = operation, RequestedLeaseSeconds = 300 } : Owned(operation, lease), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        error.StatusCode.ShouldBe(409);
        fixture.Authority.BeforePermission.ShouldBeNull();
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockHolderTaskId.ShouldBeNull();
    }
    [Fact]
    public async Task NewlyPolledAcquireRejectsReleaseDuringFinalAuthorization()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client());
        fixture.Gateway.AfterAppend = async command =>
        {
            fixture.Gateway.AfterAppend = null;
            var state = await fixture.StateAsync().ConfigureAwait(true);
            var lease = new NativeLease { LockId = state.WorkspaceLockId!, LockFence = state.WorkspaceLockFence,
                OwnershipProof = WorkspaceNativeTaskService.OwnershipProof("tenant-a", "folder-a", "workspace-a", "task-a", state.WorkspaceLockId!, state.WorkspaceLockFence) };
            int reads = fixture.Gateway.StreamReads;
            fixture.Authority.BeforePermission = async permission =>
            {
                if (fixture.Gateway.StreamReads <= reads) return;
                fixture.Authority.BeforePermission = null;
                _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", Owned("release", lease), TestContext.Current.CancellationToken).ConfigureAwait(true);
            };
        };
        (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => AcquireAsync(client)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        fixture.Authority.BeforePermission.ShouldBeNull();
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockHolderTaskId.ShouldBeNull();
    }
    [Fact]
    public async Task QueuedRenewCannotBeAcknowledgedFromThePriorLease()
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client()); var lease = await AcquireAsync(client).ConfigureAwait(true);
        fixture.Gateway.QueueOnly = _ => Task.FromResult(true);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var canceled = await Record.ExceptionAsync(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", Owned("renew", lease), cancel.Token)).ConfigureAwait(true);
        (canceled is OperationCanceledException or HttpRequestException).ShouldBeTrue(canceled?.ToString());
        cancel.IsCancellationRequested.ShouldBeTrue();
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockFence.ShouldBe(lease.LockFence);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordedNativeExpiryAllowsOnlyExactCleanCleanupOrConfirmedCompletion(bool publish)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client()); var lease = await AcquireAsync(client).ConfigureAwait(true);
        NativeEvidence? publication = publish ? await PublishAsync(client, lease).ConfigureAwait(true) : null;
        fixture.Clock.Now = lease.ExpiresAt.AddSeconds(1); await fixture.RecordExpiryAsync().ConfigureAwait(true);
        var tenants = fixture.App.Services.GetRequiredService<IFolderTenantAccessProjectionStore>();
        var tenant = (await tenants.GetAsync("tenant-a", TestContext.Current.CancellationToken).ConfigureAwait(true))!;
        tenant.LastEventTimestamp = fixture.Clock.Now; await tenants.SaveAsync(tenant, TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Dirty);
        var change = Owned(publish ? "complete" : "release", lease);
        if (publication is not null) { change.OperationId = publication.OperationId; change.AcceptedKey = publication.AcceptedKey; change.Fingerprint = publication.Fingerprint; }
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", change, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-b", "task-b", new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var state = await fixture.StateAsync().ConfigureAwait(true); state.WorkspaceLockFence.ShouldBe(2); state.WorkspaceLockHolderTaskId.ShouldBe("task-b");
        if (publication is not null)
        {
            // Even a queued gateway must not acknowledge a conflicting historical closure.
            fixture.Gateway.QueueOnly = _ => Task.FromResult(true);
            int submissions = fixture.Gateway.Submissions.Count;
            change.Fingerprint = "wrong";
            (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", change, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
            fixture.Gateway.Submissions.Count.ShouldBe(submissions);
            change.Fingerprint = publication.Fingerprint;
            _ = await client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a", change, TestContext.Current.CancellationToken).ConfigureAwait(true);
            (await fixture.StateAsync().ConfigureAwait(true)).WorkspaceLockHolderTaskId.ShouldBe("task-b");
            (await fixture.BindingsAsync().ConfigureAwait(true)).Count.ShouldBe(1);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredAcquireRecoveryCannotClearStagedWorkOrTakeAnotherTask(bool recordExpiry)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client()); var lease = await AcquireAsync(client).ConfigureAwait(true);
        _ = await fixture.DirectUploadAsync("task-a", lease.LockFence).ConfigureAwait(true);
        fixture.Clock.Now = lease.ExpiresAt.AddSeconds(1);
        if (recordExpiry) await fixture.RecordExpiryAsync().ConfigureAwait(true);
        var tenants = fixture.App.Services.GetRequiredService<IFolderTenantAccessProjectionStore>();
        var tenant = (await tenants.GetAsync("tenant-a", TestContext.Current.CancellationToken).ConfigureAwait(true))!;
        tenant.LastEventTimestamp = fixture.Clock.Now; await tenants.SaveAsync(tenant, TestContext.Current.CancellationToken).ConfigureAwait(true);
        foreach (string task in new[] { "task-a", "task-other" })
            (await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", task,
                new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken)).ConfigureAwait(true)).StatusCode.ShouldBe(409);
        var state = await fixture.StateAsync().ConfigureAwait(true); state.WorkspaceLockFence.ShouldBe(1); state.WorkspaceLockHolderTaskId.ShouldBe("task-a");
        state.IsTaskClosed("workspace-a", "task-a").ShouldBeFalse(); state.ContentPublications.ShouldBeEmpty();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeAddRejectsStreamedOrOversizeBeforeBodyPersistence(bool streamed)
    {
        var fixture = await NativeContentFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var client = new NativeClient(fixture.Client()); var lease = await AcquireAsync(client).ConfigureAwait(true);
        var mutation = FileUpload.BuildInlineFileMutation(new byte[] { 1, 2 }, "application/octet-stream", Path(), "operation-correction",
            expectedFileVersion: 0, expectedLockFence: lease.LockFence);
        byte[] bytes = new byte[262145];
        string hash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(bytes));
        JObject body;
        if (streamed)
        {
            var receipt = await fixture.DirectUploadAsync("task-a", lease.LockFence, bytes).ConfigureAwait(true);
            mutation = FileUpload.BuildStreamedFileMutation(new FileStreamStagingEvidence { StagingReference = receipt.Id,
                ObservedContentHashReference = hash, ObservedLength = bytes.Length }, "application/octet-stream", Path(), "operation-correction",
                expectedFileVersion: 0, expectedLockFence: lease.LockFence);
            body = JObject.FromObject(mutation);
        }
        else
        {
            body = JObject.FromObject(mutation);
            mutation.ContentHashReference = hash; body["contentHashReference"] = hash; body["byteLength"] = bytes.Length;
            body["inlineContent"]!["contentBytes"] = Convert.ToBase64String(bytes);
        }
        body["requestSchemaVersion"] = "v1";
        int writes = fixture.Dapr.TrySaveByteOperations.Count;
        var failure = await Should.ThrowAsync<HexalithFoldersNativeApiException>(() => client.AddNativeFileAsync("folder-a", "workspace-a", "correlation-a", "task-a",
            FileUpload.ComputeIdempotencyKey(mutation, "workspace-a", "task-a"), body, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        failure.StatusCode.ShouldBeInRange(400, 499); fixture.Dapr.TrySaveByteOperations.Count.ShouldBe(writes);
        (await fixture.StateAsync().ConfigureAwait(true)).ContentPublications.ShouldBeEmpty();
    }
    private static Task<NativeLease> AcquireAsync(NativeClient client) => client.ChangeNativeTaskLeaseAsync("folder-a", "workspace-a", "correlation-a", "task-a",
        new() { Operation = "acquire", RequestedLeaseSeconds = 300 }, TestContext.Current.CancellationToken);
    private static NativeLeaseRequest Owned(string operation, NativeLease lease) => new() { Operation = operation, RequestedLeaseSeconds = 300,
        LockId = lease.LockId, OwnershipProof = lease.OwnershipProof, LockFence = lease.LockFence };
    private static Hexalith.Folders.Client.Generated.PathMetadata Path() => new() { NormalizedPath = "docs/a.txt", DisplayName = "a.txt",
        PathPolicyClass = PathMetadataPathPolicyClass.Metadata_only, UnicodeNormalization = PathMetadataUnicodeNormalization.NFC };
    private static async Task<NativeEvidence> PublishAsync(NativeClient client, NativeLease lease)
    {
        var mutation = FileUpload.BuildInlineFileMutation(new byte[] { 1, 2 }, "application/octet-stream", Path(), "operation-correction",
            expectedFileVersion: 0, expectedLockFence: lease.LockFence);
        string key = FileUpload.ComputeIdempotencyKey(mutation, "workspace-a", "task-a");
        var wire = JObject.FromObject(mutation); wire["requestSchemaVersion"] = "v1";
        _ = await client.AddNativeFileAsync("folder-a", "workspace-a", "correlation-a", "task-a", key, wire, TestContext.Current.CancellationToken).ConfigureAwait(true);
        return await client.GetNativeOperationEvidenceAsync("folder-a", "workspace-a", "correlation-a", "task-a", new() { LookupKind = "accepted", OperationId = mutation.OperationId,
            AcceptedKey = key, PathMetadata = new() { NormalizedPath = "docs/a.txt", DisplayName = "a.txt", PathPolicyClass = "metadata_only", UnicodeNormalization = "NFC" },
            ContentHashReference = mutation.ContentHashReference, ByteLength = 2, MediaType = "application/octet-stream" }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
