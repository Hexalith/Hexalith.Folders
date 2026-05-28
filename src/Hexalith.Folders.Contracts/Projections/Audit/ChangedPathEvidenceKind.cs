using System.Runtime.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public enum ChangedPathEvidenceKind
{
    [EnumMember(Value = "digest")]
    Digest,

    [EnumMember(Value = "reference")]
    Reference,

    [EnumMember(Value = "redacted")]
    Redacted,

    [EnumMember(Value = "unavailable")]
    Unavailable,
}
