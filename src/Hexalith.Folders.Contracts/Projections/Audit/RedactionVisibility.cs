using System.Runtime.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public enum RedactionVisibility
{
    [EnumMember(Value = "metadata_only")]
    MetadataOnly,

    [EnumMember(Value = "redacted")]
    Redacted,
}
