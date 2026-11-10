using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed ArchiveFolderArchiveReasonCode vocabulary from the v2 Contract Spine.</summary>
[Description("The ArchiveFolderArchiveReasonCode vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<ArchiveFolderArchiveReasonCode>))]
public enum ArchiveFolderArchiveReasonCode
{
    /// <summary>The caller_requested value.</summary>
    [JsonStringEnumMemberName("caller_requested"), Description("The caller_requested value.")]
    CallerRequested,

    /// <summary>The policy_retention value.</summary>
    [JsonStringEnumMemberName("policy_retention"), Description("The policy_retention value.")]
    PolicyRetention,

    /// <summary>The operator_review value.</summary>
    [JsonStringEnumMemberName("operator_review"), Description("The operator_review value.")]
    OperatorReview,

}
