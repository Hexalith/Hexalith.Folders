using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<FolderWorkspaceLifecycleState>))]
public enum FolderWorkspaceLifecycleState
{
    [JsonStringEnumMemberName("requested")]
    Requested,

    [JsonStringEnumMemberName("preparing")]
    Preparing,

    [JsonStringEnumMemberName("ready")]
    Ready,

    [JsonStringEnumMemberName("locked")]
    Locked,

    [JsonStringEnumMemberName("changes_staged")]
    ChangesStaged,

    [JsonStringEnumMemberName("dirty")]
    Dirty,

    [JsonStringEnumMemberName("committed")]
    Committed,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("inaccessible")]
    Inaccessible,

    [JsonStringEnumMemberName("unknown_provider_outcome")]
    UnknownProviderOutcome,

    [JsonStringEnumMemberName("reconciliation_required")]
    ReconciliationRequired,
}
