using Hexalith.Folders.Projections.TenantAccess;
using System.Text.Json;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderFileIdentityProjectionTests
{
    [Fact]
    public async Task NativeMutationReplayAndRebuildPreserveIsolatedIdentityEndState()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new FixedUtcClock(FolderFileOwnerReadFixture.Now);
        var heads = new FolderOwnerReadHeads();
        var store = new FolderOwnerReadStore();
        var organization = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, heads);
        var folders = new EventStoreFolderEffectivePermissionsReadModel(store, clock, heads, organization);
        IFolderEvent[] history =
        [
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "private name", null, null, [],
                FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "actor", "corr", "task", "create", "create", clock.UtcNow),
            Mutation("add", "native-add") with { OwnerFileId = "owner-file", ProjectFileReferenceId = "référence-1" },
            Mutation("change", "native-change") with { OwnerFileId = "owner-file", ProjectFileReferenceId = "référence-1" },
            Mutation("remove", "native-remove") with { OwnerFileId = "owner-file", ProjectFileReferenceId = "référence-1" },
        ];
        string key = FolderFileIdentityReadModel.Key("tenant-a", "folder-a");
        (await store.GetAsync<FolderFileIdentityReadModel>("statestore", key, token)).Value.ShouldBeNull();
        for (int index = 0; index < history.Length; index++)
        {
            await folders.ApplyAsync("tenant-a", "folder-a", "message-" + index, index + 1, "payload-" + index, history[index], token);
            var row = (await store.GetAsync<FolderFileIdentityReadModel>("statestore", key, token)).Value!;
            row.LastSequence.ShouldBe(index + 1);
            if (index == 0) row.Files.ShouldBeEmpty();
            else row.Files.Single().Active.ShouldBe(index < 3);
            string json = JsonSerializer.Serialize(row);
            foreach (string sentinel in new[] { "workspace-private", "path-digest-private", "content-hash-private", "private name" })
                json.ShouldNotContain(sentinel);
        }
        var before = store.Snapshot();
        await folders.ApplyAsync("tenant-a", "folder-a", "message-3", 4, "payload-3", history[3], token);
        store.Snapshot().ShouldBe(before);
        var rebuilt = new FolderOwnerReadStore();
        var replay = new EventStoreFolderEffectivePermissionsReadModel(rebuilt, clock, heads,
            new EventStoreOrganizationEffectivePermissionsReadModel(rebuilt, clock, heads));
        for (int index = 0; index < history.Length; index++)
            await replay.ApplyAsync("tenant-a", "folder-a", "message-" + index, index + 1, "payload-" + index, history[index], token);
        var final = (await rebuilt.GetAsync<FolderFileIdentityReadModel>("statestore", key, token)).Value!;
        JsonSerializer.Serialize(final).ShouldBe(JsonSerializer.Serialize((await store.GetAsync<FolderFileIdentityReadModel>("statestore", key, token)).Value));
    }

    [Fact]
    public async Task AbsentIdentityGenerationCannotEstablishAnInitializedEmptyOwnerView()
    {
        var fixture = new FolderFileOwnerReadFixture();
        fixture.Store.IdentityReadsOnly = true; fixture.Store.ForbidWrites = true;
        var result = await fixture.DispatchAsync();
        result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        fixture.Row = fixture.Row with { Files = [] }; await fixture.SeedAsync();
        (await fixture.DispatchAsync()).ErrorMessage.ShouldBe("safe-denial");
    }

    private static WorkspaceFileMutationAccepted Mutation(string kind, string operation)
        => new("tenant-a", "tenant-a", "folder-a", "workspace-private", FolderWorkspaceLifecycleEvent.FileMutated,
            operation, kind, "native-operation", "policy", "path-digest-private", "content-hash-private", 12,
            "text/plain", "inline_decoded", 12, "actor", "corr", "task", operation, operation, FolderFileOwnerReadFixture.Now)
        { MutationTargetDigest = new string('c', 64) };
}
