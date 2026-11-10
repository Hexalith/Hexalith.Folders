using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed PutFileStreamUploadMode vocabulary from the v2 Contract Spine.</summary>
[Description("The PutFileStreamUploadMode vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<PutFileStreamUploadMode>))]
public enum PutFileStreamUploadMode
{
    /// <summary>The request_body_stream value.</summary>
    [JsonStringEnumMemberName("request_body_stream"), Description("The request_body_stream value.")]
    RequestBodyStream,

}
