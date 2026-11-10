using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed FileMutationRequestTransportOperation vocabulary from the v2 Contract Spine.</summary>
[Description("The FileMutationRequestTransportOperation vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<FileMutationRequestTransportOperation>))]
public enum FileMutationRequestTransportOperation
{
    /// <summary>The PutFileInline value.</summary>
    [JsonStringEnumMemberName("PutFileInline"), Description("The PutFileInline value.")]
    PutFileInline,

    /// <summary>The PutFileStream value.</summary>
    [JsonStringEnumMemberName("PutFileStream"), Description("The PutFileStream value.")]
    PutFileStream,

    /// <summary>The metadataOnlyRemoval value.</summary>
    [JsonStringEnumMemberName("metadataOnlyRemoval"), Description("The metadataOnlyRemoval value.")]
    MetadataOnlyRemoval,

}
