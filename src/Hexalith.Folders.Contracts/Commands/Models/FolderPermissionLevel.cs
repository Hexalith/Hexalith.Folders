using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed FolderPermissionLevel vocabulary from the v2 Contract Spine.</summary>
[Description("The FolderPermissionLevel vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<FolderPermissionLevel>))]
public enum FolderPermissionLevel
{
    /// <summary>The read value.</summary>
    [JsonStringEnumMemberName("read"), Description("The read value.")]
    Read,

    /// <summary>The write value.</summary>
    [JsonStringEnumMemberName("write"), Description("The write value.")]
    Write,

    /// <summary>The administer value.</summary>
    [JsonStringEnumMemberName("administer"), Description("The administer value.")]
    Administer,

}
