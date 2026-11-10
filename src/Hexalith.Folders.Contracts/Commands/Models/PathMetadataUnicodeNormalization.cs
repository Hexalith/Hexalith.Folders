using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed PathMetadataUnicodeNormalization vocabulary from the v2 Contract Spine.</summary>
[Description("The PathMetadataUnicodeNormalization vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<PathMetadataUnicodeNormalization>))]
public enum PathMetadataUnicodeNormalization
{
    /// <summary>The NFC value.</summary>
    [JsonStringEnumMemberName("NFC"), Description("The NFC value.")]
    NFC,

}
