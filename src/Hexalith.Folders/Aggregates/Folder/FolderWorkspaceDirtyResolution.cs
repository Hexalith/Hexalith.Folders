using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<FolderWorkspaceDirtyResolution>))]
public enum FolderWorkspaceDirtyResolution
{
    [JsonStringEnumMemberName("commit_confirmed")]
    CommitConfirmed,

    [JsonStringEnumMemberName("commit_rejected")]
    CommitRejected,
}
