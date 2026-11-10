using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed LockWorkspaceLockIntent vocabulary from the v2 Contract Spine.</summary>
[Description("The LockWorkspaceLockIntent vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<LockWorkspaceLockIntent>))]
public enum LockWorkspaceLockIntent
{
    /// <summary>The exclusive_write value.</summary>
    [JsonStringEnumMemberName("exclusive_write"), Description("The exclusive_write value.")]
    ExclusiveWrite,

}
