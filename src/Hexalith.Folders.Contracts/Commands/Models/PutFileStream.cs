using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The PutFileStream request metadata.</summary>
/// <param name="MediaType">RFC 6838 token grammar (type/subtype). Parameters such as `;charset=utf-8` are not accepted here.</param>
/// <param name="DeclaredLength">The declaredLength value.</param>
/// <param name="ObservedLength">Observed byte length from the transient content staging boundary. It must equal declaredLength and the top-level byteLength; descriptor-only stream requests are not valid evidence. Exact bytes are preserved without truncation or transcoding.</param>
/// <param name="StagingReference">Tenant-scoped transient content staging reference. It is metadata only and never a caller-supplied local path.</param>
/// <param name="ObservedContentHashReference">Content hash reference observed by the transient staging boundary; it must match the top-level contentHashReference.</param>
/// <param name="UploadMode">The uploadMode value.</param>
[Description("The PutFileStream request metadata.")]
public sealed record PutFileStream(
    [property: JsonPropertyName("mediaType"), Description("RFC 6838 token grammar (type/subtype). Parameters such as `;charset=utf-8` are not accepted here."), JsonRequired, Required, StringLength(128), RegularExpression("^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}$")]
    string MediaType,
    [property: JsonPropertyName("declaredLength"), Description("The declared length."), JsonRequired, Required, Range(262145, 1048576)]
    int DeclaredLength,
    [property: JsonPropertyName("observedLength"), Description("Observed byte length from the transient content staging boundary. It must equal declaredLength and the top-level byteLength; descriptor-only stream requests are not valid evidence. Exact bytes are preserved without truncation or transcoding."), JsonRequired, Required, Range(262145, 1048576)]
    int ObservedLength,
    [property: HexalithIdentifier, JsonPropertyName("stagingReference"), Description("Tenant-scoped transient content staging reference. It is metadata only and never a caller-supplied local path."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string StagingReference,
    [property: JsonPropertyName("observedContentHashReference"), Description("Content hash reference observed by the transient staging boundary; it must match the top-level contentHashReference."), JsonRequired, Required, StringLength(104, MinimumLength = 40), RegularExpression("^hashref_[A-Za-z0-9]{32,96}$")]
    string ObservedContentHashReference,
    [property: JsonPropertyName("uploadMode"), Description("The upload mode."), JsonRequired, Required]
    PutFileStreamUploadMode UploadMode);
