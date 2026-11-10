using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Queries.Models;

/// <summary>The closed SearchFolderFilesQueryFamily vocabulary from the v2 Contract Spine.</summary>
[Description("The SearchFolderFilesQueryFamily vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<SearchFolderFilesQueryFamily>))]
public enum SearchFolderFilesQueryFamily
{
    /// <summary>The search value.</summary>
    [JsonStringEnumMemberName("search"), Description("The search value.")]
    Search,

}
