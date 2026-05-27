using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<WorkspacePathPolicyEvidenceDecision>))]
public enum WorkspacePathPolicyEvidenceDecision
{
    NoEscape,
    SymlinkEscape,
    CaseCollision,
    Unavailable,
}
