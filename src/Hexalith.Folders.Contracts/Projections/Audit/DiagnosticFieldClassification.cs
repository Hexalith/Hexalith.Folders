using System.Runtime.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public enum DiagnosticFieldClassification
{
    [EnumMember(Value = "consumer_safe")]
    ConsumerSafe,

    [EnumMember(Value = "operator_sanitized")]
    OperatorSanitized,

    [EnumMember(Value = "forbidden")]
    Forbidden,
}
