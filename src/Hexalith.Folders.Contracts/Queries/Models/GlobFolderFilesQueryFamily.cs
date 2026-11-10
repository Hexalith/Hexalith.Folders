using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Queries.Models;

/// <summary>The closed GlobFolderFilesQueryFamily vocabulary from the v2 Contract Spine.</summary>
[Description("The GlobFolderFilesQueryFamily vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<GlobFolderFilesQueryFamily>))]
public enum GlobFolderFilesQueryFamily
{
    /// <summary>The glob value.</summary>
    [JsonStringEnumMemberName("glob"), Description("The glob value.")]
    Glob,

}
