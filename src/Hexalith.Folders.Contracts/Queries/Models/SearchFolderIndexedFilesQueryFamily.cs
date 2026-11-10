using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Queries.Models;

/// <summary>The closed SearchFolderIndexedFilesQueryFamily vocabulary from the v2 Contract Spine.</summary>
[Description("The SearchFolderIndexedFilesQueryFamily vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<SearchFolderIndexedFilesQueryFamily>))]
public enum SearchFolderIndexedFilesQueryFamily
{
    /// <summary>The semantic_reference_pending value.</summary>
    [JsonStringEnumMemberName("semantic_reference_pending"), Description("The semantic_reference_pending value.")]
    SemanticReferencePending,

}
