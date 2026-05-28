using System.Runtime.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public enum RedactableAuditTimestampPrecision
{
    [EnumMember(Value = "exact")]
    Exact,

    [EnumMember(Value = "bucketed")]
    Bucketed,

    [EnumMember(Value = "redacted")]
    Redacted,
}
