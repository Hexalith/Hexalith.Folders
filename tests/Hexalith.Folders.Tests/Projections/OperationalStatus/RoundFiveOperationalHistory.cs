using System.Globalization;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Tests.OperationalStatus;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Builds source-lawful native release and independently published File histories.</summary>
internal static class RoundFiveOperationalHistory
{
    internal static List<IFolderEvent> Create(int releases, int publications)
    {
        List<IFolderEvent> events = [.. OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now).Take(6)];
        for (int index = 1; index <= releases; index++)
        {
            events.Add(Native("acquire", index, "reusable-task"));
            events.Add(Native("release", index, "reusable-task") with { ExpiresAt = Native("release", index, "reusable-task").EffectiveAt });
        }
        for (int index = 0; index < publications; index++)
        {
            long fence = releases + index + 1;
            string task = "published-task-" + index;
            events.Add(Native("acquire", fence, task));
            events.Add(File(fence, task, index));
            events.Add(Native("complete", fence, task) with { PublicationCommandId = "publication-" + fence });
        }
        return events;
    }

    internal static WorkspaceNativeTaskChanged Native(string operation, long fence, string task)
    {
        DateTimeOffset time = OperationalStatusTestData.Now.AddSeconds(fence);
        return new("tenant-a", "tenant-a", "folder-a", "workspace-a", operation, "native-lock-" + fence,
            fence, 60, time, time.AddSeconds(60), null, "principal-a", "native-correlation-" + fence, task,
            operation + "-" + fence, "fingerprint", time);
    }

    internal static WorkspaceFileMutationAccepted File(long fence, string task, int index, long version = 1)
    {
        string command = "publication-" + fence;
        string operation = "file-operation-" + fence;
        string file = "file-" + index;
        string kind = version == 1 ? "add" : "change";
        return new("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.FileMutated,
            operation, kind, kind, "metadata_only", "metadata-" + index, "hashref_" + new string('a', 64), 1,
            "text/plain", null, null, "principal-a", "native-correlation-" + fence, task, command, "fingerprint",
            OperationalStatusTestData.Now.AddSeconds(fence))
        {
            OwnerFileId = file,
            MutationTargetDigest = index.ToString("x64", CultureInfo.InvariantCulture),
            ContentBinding = new("tenant-a", "folder-a", "workspace-a", task, command, operation, file, version,
                fence, fence, "receipt-" + fence, "hashref_" + new string('a', 64), 1, "text/plain", "1.1.0",
                "policy-digest", kind, "metadata-" + index) { AcceptedSdkKey = "sdk-key-" + fence },
        };
    }

    internal static FolderAccessGranted Grant(DateTimeOffset time)
        => new("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User, "principal-a", "read_metadata",
            "principal-a", "grant-correlation", "grant-task", "grant-key", "grant-fingerprint", 1, time);
}
