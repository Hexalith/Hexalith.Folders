using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<WorkspacePathPolicyDecision>))]
public enum WorkspacePathPolicyDecision
{
    Accepted,
    MissingPathMetadata,
    Traversal,
    AbsolutePath,
    EmptySegment,
    DotSegment,
    MixedSeparators,
    ReservedPlatformName,
    ControlCharacter,
    InvisibleCharacter,
    UnicodeNormalizationAmbiguity,
    PercentDotSegmentSmuggling,
    WorkspaceRootEscape,
    SymlinkEscape,
    CaseCollision,
    TrailingSpaceOrDotAmbiguity,
    OverLength,
    InvalidPathPolicyClass,
    InvalidDisplayName,
    InvalidUnicodeNormalization,
    EvidenceUnavailable,
}
