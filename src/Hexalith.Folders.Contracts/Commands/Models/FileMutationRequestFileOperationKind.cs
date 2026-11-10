using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed FileMutationRequestFileOperationKind vocabulary from the v2 Contract Spine.</summary>
[Description("The FileMutationRequestFileOperationKind vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<FileMutationRequestFileOperationKind>))]
public enum FileMutationRequestFileOperationKind
{
    /// <summary>The add value.</summary>
    [JsonStringEnumMemberName("add"), Description("The add value.")]
    Add,

    /// <summary>The change value.</summary>
    [JsonStringEnumMemberName("change"), Description("The change value.")]
    Change,

    /// <summary>The remove value.</summary>
    [JsonStringEnumMemberName("remove"), Description("The remove value.")]
    Remove,

}
