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
            JsonSerializer.SerializeToUtf8Bytes(new { requestSchemaVersion = "v1", workspaceId = "workspace-a", taskId = "task-a", items = new[] { new {
                operationId = "operation-batch", fileOperationKind = "add", transportOperation = "PutFileInline", pathMetadata = WorkspaceContentProductionFixture.Path("docs/a.txt"),
                contentHashReference = "hashref_" + receipt.Sha256, byteLength = bytes.Length, mediaType = "text/plain", transportEvidenceKind = "inline_decoded", observedByteLength = bytes.Length,
                contentReceiptId = receipt.Id, expectedLockFence = 1, expectedFileVersion = 0 } } }), "correlation-a", null, "user-a", new Dictionary<string,string> { ["taskId"] = "task-a" });
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
    public async Task RangeCannotDisclose416AfterCurrentStateChangesDuringSensitivityAwait(string change)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var accepted = await fixture.PublishAsync("docs/a.txt", "original").ConfigureAwait(true);
        fixture.Sensitivity.After = async () =>
        {
            if (change == "revoke") fixture.Permissions.Allowed = false;
            else if (change == "remove") _ = await fixture.PublishAsync("docs/a.txt", "", accepted, remove: true).ConfigureAwait(true);
            else fixture.App.Configuration["Folders:WorkspaceFilePolicy:Excludes:0"] = "docs/**";
        };
        using var scope = fixture.Scope();
        var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>().QueryAsync(fixture.RangeRequest(100, 101), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldNotBe(WorkspaceFileContextSourceStatus.RangeUnsatisfiable);
        result.Status.ShouldNotBe(WorkspaceFileContextSourceStatus.Available);
        result.Range.ShouldBeNull(); result.Items.ShouldBeEmpty(); result.ContentBytes.ShouldBeNull();
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

