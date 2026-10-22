using System.Text.Json;
using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

/// <summary>Checks source binding invariants for a complete actor decision, including internal batches.</summary>
public sealed class WorkspaceContentBindingTests
{
    /// <summary>Retains every same-command batch binding without overwriting an earlier item.</summary>
    [Fact]
    public void BatchItemsShouldRetainSeparateImmutableBindings()
    {
        FolderState original = Locked();
        FolderState applied = original.Apply([Accepted("file-a", Key("file-a"), 'a'), Accepted("file-b", Key("file-b"), 'b')],
            FolderStreamName.Create("tenant-a", "folder-a"));
        applied.ContentPublications.Count.ShouldBe(2);
        applied.ContentPublications.Values.All(binding => binding.CommandId == "command-a").ShouldBeTrue();
        original.ContentPublications.ShouldBeEmpty();
        applied.FileIdentities.Values.All(file => file.Active).ShouldBeTrue();
        JsonSerializer.Serialize(applied.ContentPublications).ShouldNotContain("docs/private-body.txt");
    }

    /// <summary>A wrong fence or a skipped version cannot enter source truth, and the original state stays unchanged.</summary>
    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void InvalidBindingShouldRejectTheWholeFold(long fence, long version)
    {
        FolderState original = Locked();
        WorkspaceFileMutationAccepted invalid = Accepted("file-b", Key("file-b"), 'b');
        invalid = invalid with { ContentBinding = invalid.ContentBinding! with { LockFence = fence, Version = version } };
        Should.Throw<InvalidOperationException>(() => original.Apply([Accepted("file-a", Key("file-a"), 'a'), invalid],
            FolderStreamName.Create("tenant-a", "folder-a")));
        original.ContentPublications.ShouldBeEmpty(); original.FileIdentities.ShouldBeEmpty();
    }

    /// <summary>Historical accepted metadata without a content binding remains replayable and never manufactures bytes.</summary>
    [Fact]
    public void HistoricalMetadataShouldNotManufactureContentAuthority()
    {
        var historic = Accepted("file-a", "legacy-command", 'a') with { ContentBinding = null };
        FolderState applied = Locked().Apply([historic], FolderStreamName.Create("tenant-a", "folder-a"));
        applied.ContentPublications.ShouldBeEmpty();
        JsonSerializer.Serialize(historic).ShouldNotContain("ContentBinding");
    }

    [Fact]
    public void SerializedErasureReservationCannotAcquireANewReference()
    {
        WorkspaceFileMutationAccepted mutation = Accepted("file-a", Key("file-a"), 'a');
        FolderState reserved = Locked() with
        {
            ContentErasureGrants = new Dictionary<string, long> { [mutation.ContentBinding!.ReceiptId!] = 10 },
        };
        Should.Throw<InvalidOperationException>(() => reserved.Apply([mutation], FolderStreamName.Create("tenant-a", "folder-a")));
        reserved.ContentPublications.ShouldBeEmpty();
    }

    private static FolderState Locked() => FolderState.Empty with
    {
        IsCreated = true, ManagedTenantId = "tenant-a", OrganizationId = "tenant-a", FolderId = "folder-a",
        LifecycleState = FolderLifecycleState.Active, WorkspaceId = "workspace-a", WorkspaceTaskId = "task-a",
        WorkspaceLifecycleState = FolderWorkspaceLifecycleState.Locked, WorkspaceLockFence = 1, WorkspaceLockHolderTaskId = "task-a",
    };
    private static string Key(string file) => WorkspaceContentBinding.BatchItemKey("command-a", "operation-" + file);
    private static WorkspaceFileMutationAccepted Accepted(string file, string key, char marker)
    {
        string digest = new(marker, 64), operation = "operation-" + file, hash = "hashref_" + digest;
        return new("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.FileMutated,
            operation, "add", "PutFileInline", "content_allowed", digest, hash, 12, "text/plain", "inline_decoded", 12,
            "user-a", "correlation-a", "task-a", key, "fingerprint-" + key, DateTimeOffset.UnixEpoch)
        {
            OwnerFileId = file, MutationTargetDigest = digest,
            ContentBinding = new("tenant-a", "folder-a", "workspace-a", "task-a", "command-a", operation, file, 1, 11, 1,
                "content_" + digest, hash, 12, "text/plain", "1.1.0", new string('c', 64), "add", digest, true),
        };
    }
}
