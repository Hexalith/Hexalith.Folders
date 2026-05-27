using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<FolderOperatorDisposition>))]
public enum FolderOperatorDisposition
{
    [JsonStringEnumMemberName("auto_recovering")]
    AutoRecovering,

    [JsonStringEnumMemberName("available")]
    Available,

    [JsonStringEnumMemberName("degraded_but_serving")]
    DegradedButServing,

    [JsonStringEnumMemberName("awaiting_human")]
    AwaitingHuman,

    [JsonStringEnumMemberName("terminal_until_intervention")]
    TerminalUntilIntervention,
}
