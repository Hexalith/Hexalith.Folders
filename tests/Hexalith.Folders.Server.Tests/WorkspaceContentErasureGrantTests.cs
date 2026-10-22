using Microsoft.Extensions.DependencyInjection.Extensions;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Testing;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Content;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class WorkspaceContentErasureGrantTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedReceiptRemainsProtectedAfterNewStagedContentOrRemovalAndTerminalP7D(bool removal)
    {
        var fixture = await WorkspaceContentProductionFixture.CreateAsync().ConfigureAwait(true);
        await using var cleanup = fixture.ConfigureAwait(true);
        var accepted = await fixture.PublishAsync("docs/a.txt", "committed A").ConfigureAwait(true);
        fixture.Commit(); fixture.Resume();
        _ = await fixture.PublishAsync("docs/a.txt", removal ? "" : "staged B", accepted, remove: removal).ConfigureAwait(true);
        fixture.FailTask();
        var state = fixture.State;
        string receipt = accepted.ContentBinding!.ReceiptId!;
        state.CommittedContentReferences[accepted.OwnerFileId!].ShouldBe(receipt);
        state.FileIdentities[accepted.OwnerFileId!].Active.ShouldBe(!removal);
        WorkspaceContentErasure.HasActiveTask(state).ShouldBeFalse();
        WorkspaceContentErasure.HasLiveReference(state, receipt).ShouldBeTrue();
        using var scope = fixture.Scope();
        var authority = scope.ServiceProvider.GetRequiredService<IContentAuthority>();
        var disposition = await authority.GetDispositionAsync(fixture.ContentScope, new(receipt, accepted.ContentHashReference![8..], accepted.ByteLength!.Value, "text/plain", "task-a"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        disposition.Terminal.ShouldBeTrue(); disposition.HasLiveReference.ShouldBeTrue();
        var store = scope.ServiceProvider.GetRequiredService<IAuthoritativeContentStore>();
        (await store.ApplyDispositionAsync(fixture.Principal, fixture.ContentScope, receipt, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        (await store.ApplyDispositionAsync(fixture.Principal, fixture.ContentScope, receipt, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        string key = fixture.Dapr.TrySaveByteOperations.First(write => write.Key.EndsWith(receipt, StringComparison.Ordinal)).Key;
        fixture.ContentScope.ShouldBe(new ContentScope("tenant-a", "folders", "folder-a", "workspace-a"));
        string scopeKey = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fixture.ContentScope)));
        key.ShouldBe("authoritative-content:body:" + scopeKey + ":" + receipt);
        var (persisted, etag) = await fixture.Dapr.GetByteStateAndETagAsync("statestore", key, Dapr.Client.ConsistencyMode.Strong, null!, TestContext.Current.CancellationToken).ConfigureAwait(true);
        etag.ShouldNotBeNullOrWhiteSpace();
        persisted.IsEmpty.ShouldBeFalse();
        var body = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(persisted.Span);
        body.GetProperty("receipt").GetProperty("id").GetString().ShouldBe(receipt);
        body.GetProperty("erased").GetBoolean().ShouldBeFalse();
        System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("bytes").GetString()!)).ShouldBe("committed A");
        if (removal)
        {
            var result = await scope.ServiceProvider.GetRequiredService<IWorkspaceFileContextSource>().QueryAsync(fixture.RangeRequest(0, 1), TestContext.Current.CancellationToken).ConfigureAwait(true);
            result.Status.ShouldBe(WorkspaceFileContextSourceStatus.NotFoundSafe);
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{\"workspaceId\":\"workspace-a\",\"taskId\":\"task-a\",\"requestSchemaVersion\":1,\"items\":[{}]}")]
    [InlineData("{\"workspaceId\":\"workspace-a\",\"taskId\":\"task-a\",\"requestSchemaVersion\":\"v1\",\"items\":[null]}")]
    public async Task MutationProcessorRejectsNonObjectAndMalformedBatchShapes(string json)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions { EnvironmentName = "Test" });
        builder.Services.AddFoldersServerTestDefaults(); builder.Services.AddFoldersServer(); builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<IFolderRepository>(); builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();
        await using var app = builder.Build(); using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var processor = scope.ServiceProvider.GetServices<Hexalith.EventStore.Client.Handlers.IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
        var command = new Hexalith.EventStore.Contracts.Commands.CommandEnvelope("shape-message", "tenant-a", "folders", "folder-a",
            FoldersServerModule.MutateFilesCommandType, System.Text.Encoding.UTF8.GetBytes(json), "correlation-a", null, "user-a", new Dictionary<string, string> { ["taskId"] = "task-a" });
        var result = await processor.ProcessAsync(command, null);
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCommandRejected>().Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
    }

    [Fact]
    public void ReconciledGrantRequiresItsExactSourceTransitionAndNoResumedWork()
    {
        var scope = new ContentScope("tenant", "folders", "folder", "workspace");
        var receipt = new ContentReceipt("content_" + new string('a', 64), new string('b', 64), 1, "text/plain");
        var state = FolderState.Empty with
        {
            WorkspaceId = "workspace",
            ContentErasureGrants = new Dictionary<string, long> { [receipt.Id] = 10 },
        };
        var granted = new WorkspaceContentState(state, 11, [], false);
        WorkspaceContentErasure.HasCurrentGrant(granted, scope, receipt, 10).ShouldBeTrue();
        WorkspaceContentErasure.HasCurrentGrant(granted with { Head = 12 }, scope, receipt, 10).ShouldBeFalse();
        WorkspaceContentErasure.HasCurrentGrant(granted with { State = state with { WorkspaceTaskId = "resumed-task" } }, scope, receipt, 10).ShouldBeFalse();
        WorkspaceContentErasure.HasCurrentGrant(granted, scope, receipt with { TaskId = "unclosed-task" }, 10).ShouldBeFalse();
    }
    [Fact]
    public void ErasureShapeUsesTheOwningV1CanonicalIdentifierGrammar()
    {
        WorkspaceContentErasure.IsCanonicalRequest(new("workspace-a", "content_" + new string('a', 64), "task-a", 0)).ShouldBeTrue();
        WorkspaceContentErasure.IsCanonicalRequest(new("workspace-a", "content_" + new string('A', 64), "task-a", 0)).ShouldBeFalse();
        WorkspaceContentErasure.IsCanonicalRequest(new("workspace-a", "content_" + new string('a', 64), "unsafe/task", 0)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "workspace_0123456789", 0L)]
    [InlineData("content_ABCDEF", "workspace_0123456789", 0L)]
    [InlineData("content_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag", "workspace_0123456789", 0L)]
    [InlineData("content_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", null, 0L)]
    [InlineData("content_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "../unsafe", 0L)]
    [InlineData("content_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "workspace_0123456789", -1L)]
    public async Task ProcessorRejectsMalformedErasureShapeWithoutThrowing(string? receipt, string? workspace, long generation)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions { EnvironmentName = "Test" });
        builder.Services.AddFoldersServerTestDefaults(); builder.Services.AddFoldersServer(); builder.Services.AddInMemoryFolderRepository();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var processor = scope.ServiceProvider.GetServices<Hexalith.EventStore.Client.Handlers.IDomainProcessor>().OfType<FolderDomainProcessor>().Single();
        var command = new Hexalith.EventStore.Contracts.Commands.CommandEnvelope("erase-message", "tenant-a", "folders", "folder-a",
            FoldersServerModule.AuthorizeContentErasureCommandType,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { workspaceId = workspace, receiptId = receipt, expectedGeneration = generation }),
            "correlation-a", null, "user-a", new Dictionary<string, string> { ["taskId"] = "task-a" });
        var result = await processor.ProcessAsync(command, null);
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCommandRejected>().Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
    }
}
