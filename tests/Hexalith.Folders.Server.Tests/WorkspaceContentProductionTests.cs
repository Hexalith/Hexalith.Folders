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

public sealed class WorkspaceContentProductionTests
{
    [Fact]
    public async Task BatchLeaseExpiryAfterPreparationRejectsTheFinalMetadataDecision()
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync(expireAfterPreparation: true).ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        byte[] bytes = Encoding.UTF8.GetBytes("batch bytes");
        var receipt = await fixture.StageAsync(bytes).ConfigureAwait(true);
        using var scope = fixture.Scope();
        var services = scope.ServiceProvider;
        var authorization = await services.GetRequiredService<LayeredFolderAuthorizationService>().AuthorizeAsync(new("tenant-a", "user-a", "user-a", "mutate_files",
            LayeredFolderOperationPolicy.Mutation(), EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["mutate_files"]), "folder-a", "correlation-a", "task-a", null, null), TestContext.Current.CancellationToken).ConfigureAwait(true);
        services.GetRequiredService<Hexalith.Folders.Server.Authorization.ILayeredFolderAuthorizationResultAccessor>().BeginScope(authorization);
        var command = new CommandEnvelope("batch-message", "tenant-a", "folders", "folder-a", FoldersServerModule.MutateFilesCommandType,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                requestSchemaVersion = "v1",
                workspaceId = "workspace-a",
                taskId = "task-a",
                items = new[] { new {
                operationId = "operation-batch", fileOperationKind = "add", transportOperation = "PutFileInline", pathMetadata = WorkspaceContentProductionFixture.Path("docs/a.txt"),
                contentHashReference = "hashref_" + receipt.Sha256, byteLength = bytes.Length, mediaType = "text/plain", transportEvidenceKind = "inline_decoded", observedByteLength = bytes.Length,
                contentReceiptId = receipt.Id, expectedLockFence = 1, expectedFileVersion = 0 } }
            }), "correlation-a", null, "user-a", new Dictionary<string, string> { ["taskId"] = "task-a" });
        var current = new DomainServiceCurrentState(JsonSerializer.SerializeToElement(fixture.State), [], 5, 5);
        var processor = services.GetServices<Hexalith.EventStore.Client.Handlers.IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
        var result = await processor.ProcessAsync(command, current).ConfigureAwait(true);
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCommandRejected>().Code.ShouldBe(nameof(FolderResultCode.LockExpired));
        var store = (WorkspaceContentExpiringStore)services.GetRequiredService<IAuthoritativeContentStore>();
        store.Prepared.ShouldBeTrue();
        (await store.ListAsync(fixture.Principal, fixture.ContentScope, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EquivalentReplayUsesAcceptedEvidenceAfterLeaseExpiryOrTaskClosure(bool closed)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var accepted = await fixture.PublishAsync("docs/a.txt", "original").ConfigureAwait(true);
        if (closed) fixture.Commit(); else fixture.Clock.Now = fixture.Clock.Now.AddHours(2);
        using var client = fixture.App.GetTestClient();
        using var response = await client.SendAsync(WorkspaceContentProductionFixture.UploadRequest(accepted, Encoding.UTF8.GetBytes("original")), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        fixture.Gateway.Submissions.ShouldBe(1);
        // Removal/physical disposal never lets equivalent replay recreate a readable body.
        if (closed) fixture.Resume();
        var removed = await fixture.PublishAsync("docs/a.txt", "", accepted, remove: true).ConfigureAwait(true);
        using var scope = fixture.Scope();
        var store = scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>();
        var receipt = accepted.ContentBinding!.ReceiptId!;
        fixture.FailTask();
        (await store.ApplyDispositionAsync(fixture.Principal, fixture.ContentScope, receipt, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        // A committed receipt remains protected even after staged removal.
        if (!closed) (await store.ApplyDispositionAsync(fixture.Principal, fixture.ContentScope, receipt, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        using var replay = await client.SendAsync(WorkspaceContentProductionFixture.UploadRequest(accepted, Encoding.UTF8.GetBytes("original")), TestContext.Current.CancellationToken).ConfigureAwait(true);
        replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>().QueryAsync(fixture.RangeRequest(0, 1), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(WorkspaceFileContextSourceStatus.NotFoundSafe);
    }

    [Fact]
    public async Task MultipartReplayVerifiesActualBytesBeforeAcceptedSourceShortcut()
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        string content = new('a', 262145);
        var accepted = await fixture.PublishAsync("docs/a.txt", content).ConfigureAwait(true);
        using var client = fixture.App.GetTestClient();
        byte[] changed = Encoding.UTF8.GetBytes(content); changed[^1] = (byte)'b';
        using var response = await client.SendAsync(WorkspaceContentProductionFixture.UploadRequest(accepted, changed, multipart: true), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.Gateway.Submissions.ShouldBe(0);
    }

    [Theory]
    [InlineData(WorkspaceFileSensitivityDecision.Redacted, HttpStatusCode.NotFound)]
    [InlineData(WorkspaceFileSensitivityDecision.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task MutationSensitivityFailsBeforeBodyWritesOrAdmission(WorkspaceFileSensitivityDecision decision, HttpStatusCode status)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        fixture.Sensitivity.Decision = decision;
        using var client = fixture.App.GetTestClient();
        using var response = await client.SendAsync(fixture.NewUploadRequest(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        fixture.Dapr.TrySaveByteOperations.ShouldBeEmpty(); fixture.Gateway.Submissions.ShouldBe(0);
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("remove")]
    [InlineData("policy")]
    [InlineData("watermark")]
    public async Task RangeCannotDisclose416AfterCurrentStateChangesDuringSensitivityAwait(string change)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var accepted = await fixture.PublishAsync("docs/a.txt", "original").ConfigureAwait(true);
        fixture.Sensitivity.After = async () =>
        {
            if (change == "revoke") fixture.Permissions.Allowed = false;
            else if (change == "watermark") fixture.Permissions.Watermark = null;
            else if (change == "remove") _ = await fixture.PublishAsync("docs/a.txt", "", accepted, remove: true).ConfigureAwait(true);
            else fixture.App.Configuration["Folders:WorkspaceFilePolicy:Excludes:0"] = "docs/**";
        };
        using var scope = fixture.Scope();
        var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>().QueryAsync(fixture.RangeRequest(100, 101), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldNotBe(WorkspaceFileContextSourceStatus.RangeUnsatisfiable);
        result.Status.ShouldNotBe(WorkspaceFileContextSourceStatus.Available);
        result.Range.ShouldBeNull(); result.Items.ShouldBeEmpty(); result.ContentBytes.ShouldBeNull();
        if (change == "watermark")
        {
            result.Status.ShouldBe(WorkspaceFileContextSourceStatus.Unavailable);
            fixture.State.ContentPublications.ShouldContain(pair => pair.Value.ReceiptId == accepted.ContentBinding!.ReceiptId);
            fixture.State.FileIdentities[accepted.OwnerFileId!].Active.ShouldBeTrue();
        }
    }

    /// <summary>Fences every awaited namespace classification against current complete authorization.</summary>
    /// <param name="incompleteEvidence">Whether the current permission watermark is lost instead of access being revoked.</param>
    /// <param name="sensitivity">The classification returned after authority changes.</param>
    [Theory]
    [InlineData(false, WorkspaceFileSensitivityDecision.Allowed)]
    [InlineData(false, WorkspaceFileSensitivityDecision.Redacted)]
    [InlineData(false, WorkspaceFileSensitivityDecision.Unavailable)]
    [InlineData(true, WorkspaceFileSensitivityDecision.Allowed)]
    [InlineData(true, WorkspaceFileSensitivityDecision.Redacted)]
    [InlineData(true, WorkspaceFileSensitivityDecision.Unavailable)]
    public async Task NamespaceClassificationRechecksAuthorityBeforeConsumingEachAwaitedResult(
        bool incompleteEvidence, WorkspaceFileSensitivityDecision sensitivity)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var first = await fixture.PublishAsync("docs/a.txt", "alpha").ConfigureAwait(true);
        var second = await fixture.PublishAsync("docs/b.txt", "beta").ConfigureAwait(true);
        string persistedBefore = SnapshotPersistedContent(fixture);
        fixture.Sensitivity.Decision = sensitivity;
        fixture.Sensitivity.After = () =>
        {
            if (incompleteEvidence) fixture.Permissions.Watermark = null;
            else fixture.Permissions.Allowed = false;
            return Task.CompletedTask;
        };
        using var scope = fixture.Scope();
        var request = fixture.RangeRequest(0, 1) with
        {
            Kind = WorkspaceFileContextQueryKind.Tree,
            Paths = [],
            Limit = 10,
            StartOffset = null,
            EndOffset = null,
        };
        var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>()
            .QueryAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(incompleteEvidence
            ? WorkspaceFileContextSourceStatus.Unavailable : WorkspaceFileContextSourceStatus.NotFoundSafe);
        fixture.Sensitivity.Calls.ShouldBe(1);
        result.Items.ShouldBeEmpty(); result.ContentBytes.ShouldBeNull(); result.Range.ShouldBeNull();
        fixture.State.ContentPublications.ShouldContain(pair => pair.Value.ReceiptId == first.ContentBinding!.ReceiptId);
        fixture.State.ContentPublications.ShouldContain(pair => pair.Value.ReceiptId == second.ContentBinding!.ReceiptId);
        fixture.State.FileIdentities[first.OwnerFileId!].Active.ShouldBeTrue();
        fixture.State.FileIdentities[second.OwnerFileId!].Active.ShouldBeTrue();
        SnapshotPersistedContent(fixture).ShouldBe(persistedBefore);
    }

    /// <summary>Discards accumulated namespace output when authority changes during a later pending classification.</summary>
    /// <param name="incompleteEvidence">Whether the permission watermark is removed instead of access being revoked.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSecondClassificationRechecksAuthorityBeforeObservingThirdEntry(bool incompleteEvidence)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var first = await fixture.PublishAsync("docs/a.txt", "alpha").ConfigureAwait(true);
        var second = await fixture.PublishAsync("docs/b.txt", "beta").ConfigureAwait(true);
        var third = await fixture.PublishAsync("docs/c.txt", "gamma").ConfigureAwait(true);
        string persistedBefore = SnapshotPersistedContent(fixture);
        TaskCompletionSource secondPending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sensitivity.BeforeResult = (call, cancellationToken) =>
        {
            if (call != 2) return Task.CompletedTask;
            secondPending.SetResult();
            return releaseSecond.Task.WaitAsync(cancellationToken);
        };
        using var scope = fixture.Scope();
        var request = fixture.RangeRequest(0, 1) with
        {
            Kind = WorkspaceFileContextQueryKind.Tree,
            Paths = [],
            Limit = 10,
            StartOffset = null,
            EndOffset = null,
        };
        Task<WorkspaceFileContextSourceResult> pending = scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>()
            .QueryAsync(request, TestContext.Current.CancellationToken);
        try
        {
            await secondPending.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            pending.IsCompleted.ShouldBeFalse();
            fixture.Sensitivity.Calls.ShouldBe(2);
            if (incompleteEvidence) fixture.Permissions.Watermark = null;
            else fixture.Permissions.Allowed = false;
        }
        finally
        {
            releaseSecond.TrySetResult();
        }

        var result = await pending.ConfigureAwait(true);
        result.Status.ShouldBe(incompleteEvidence
            ? WorkspaceFileContextSourceStatus.Unavailable : WorkspaceFileContextSourceStatus.NotFoundSafe);
        fixture.Sensitivity.Calls.ShouldBe(2);
        result.Items.ShouldBeEmpty(); result.ContentBytes.ShouldBeNull(); result.Range.ShouldBeNull();
        foreach (var accepted in new[] { first, second, third })
        {
            fixture.State.ContentPublications.ShouldContain(pair => pair.Value.ReceiptId == accepted.ContentBinding!.ReceiptId);
            fixture.State.FileIdentities[accepted.OwnerFileId!].Active.ShouldBeTrue();
        }
        SnapshotPersistedContent(fixture).ShouldBe(persistedBefore);
    }

    private static string SnapshotPersistedContent(WorkspaceContentProductionFixture fixture)
        => JsonSerializer.Serialize(new
        {
            Folder = fixture.State,
            Content = fixture.Dapr.TrySaveByteOperations.Select(operation => operation.Key)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .ToDictionary(key => key, key => fixture.Dapr.ByteStoreValue(key), StringComparer.Ordinal),
        });

    [Fact]
    public async Task DirectLegacyContentReauthorizationPreservesTheRequestTaskInValidatorContexts()
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var accepted = await fixture.PublishAsync("docs/a.txt", "original").ConfigureAwait(true);
        using var scope = fixture.Scope();
        fixture.Validator.Requests.Clear();
        var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>()
            .QueryAsync(fixture.RangeRequest(0, 1), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(WorkspaceFileContextSourceStatus.Available);
        fixture.Validator.Requests.ShouldNotBeEmpty();
        foreach (var request in fixture.Validator.Requests)
        {
            request.SafeContext.TaskId.ShouldBe(accepted.TaskId);
            request.SafeContext.OperationScope.ShouldBe("folder-a");
            request.SafeContext.ActionToken.ShouldBe("mutate_files");
        }
        fixture.State.ContentPublications.ShouldContain(pair => pair.Value.ReceiptId == accepted.ContentBinding!.ReceiptId);
    }

    [Fact]
    public async Task ProducedRangeSearchAndGlobWireShapesDeserializeWithCanonicalWatermarksAndTruncation()
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        await fixture.PublishAsync("docs/a.txt", "needle alpha").ConfigureAwait(true);
        await fixture.PublishAsync("docs/b.txt", "needle beta").ConfigureAwait(true);
        await fixture.PublishAsync("other/c.txt", "unrelated").ConfigureAwait(true);
        using var client = fixture.App.GetTestClient();
        using var search = fixture.QueryRequest("search", new { requestSchemaVersion = "v1", queryFamily = "search", queryText = "needle", limit = 1 });
        using var response = await client.SendAsync(search, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        var wire = JsonConvert.DeserializeObject<FileSearchResult>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
        wire.Items.ShouldHaveSingleItem().Path.NormalizedPath.ShouldBe("docs/a.txt");
        wire.Page.IsTruncated.ShouldBeTrue();
        using var glob = fixture.QueryRequest("glob", new { requestSchemaVersion = "v1", queryFamily = "glob", globPattern = "docs/*.txt", limit = 10 });
        using var globResponse = await client.SendAsync(glob, TestContext.Current.CancellationToken).ConfigureAwait(true);
        globResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tree = JsonConvert.DeserializeObject<FileTreeResult>(await globResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
        tree.Items.Select(value => value.Path.NormalizedPath).ShouldBe(["docs/a.txt", "docs/b.txt"]);
        using var range = fixture.QueryRequest("range", new { requestSchemaVersion = "v1", path = WorkspaceContentProductionFixture.Path("docs/a.txt"), startOffset = 0, endOffset = 1 });
        using var rangeResponse = await client.SendAsync(range, TestContext.Current.CancellationToken).ConfigureAwait(true);
        rangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rangeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        JsonConvert.DeserializeObject<FileRangeReadCompleteResult>(await rangeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
    }
}

