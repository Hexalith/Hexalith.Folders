using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed ReleaseWorkspaceLockReleaseReasonCode vocabulary from the v2 Contract Spine.</summary>
[Description("The ReleaseWorkspaceLockReleaseReasonCode vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<ReleaseWorkspaceLockReleaseReasonCode>))]
public enum ReleaseWorkspaceLockReleaseReasonCode
{
    /// <summary>The caller_completed value.</summary>
    [JsonStringEnumMemberName("caller_completed"), Description("The caller_completed value.")]
    CallerCompleted,

    /// <summary>The caller_abandoned value.</summary>
    [JsonStringEnumMemberName("caller_abandoned"), Description("The caller_abandoned value.")]
    CallerAbandoned,

    /// <summary>The operator_requested value.</summary>
    [JsonStringEnumMemberName("operator_requested"), Description("The operator_requested value.")]
    OperatorRequested,

    /// <summary>The authorization_revoked value.</summary>
    [JsonStringEnumMemberName("authorization_revoked"), Description("The authorization_revoked value.")]
    AuthorizationRevoked,

    /// <summary>The task_cancelled value.</summary>
    [JsonStringEnumMemberName("task_cancelled"), Description("The task_cancelled value.")]
    TaskCancelled,

    /// <summary>The lock_revoked value.</summary>
    [JsonStringEnumMemberName("lock_revoked"), Description("The lock_revoked value.")]
    LockRevoked,

}
