using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The PutFileInline request metadata.</summary>
/// <param name="MediaType">RFC 6838 token grammar (type/subtype). Parameters such as `;charset=utf-8` are not accepted here — callers send them via the transport `Content-Type` header.</param>
/// <param name="ContentBytes">Authorized request body only; base64-encoded bytes up to the 262144-byte D-9 inline boundary (349528 base64 characters). Strict UTF-8 bytes with an optional leading UTF-8 BOM may be content-readable; binary and other encodings remain mutation-eligible but metadata_only. Exact bytes are preserved and never transcoded, repaired, or silently truncated. Never emitted in events, logs, audit, diagnostics, examples outside request bodies, or context-query metadata.</param>
/// <param name="ContentMediaType">Optional original media type when the request body media type differs (e.g., transport wrapper vs. authored content).</param>
[Description("The PutFileInline request metadata.")]
public sealed record PutFileInline(
    [property: JsonPropertyName("mediaType"), Description("RFC 6838 token grammar (type/subtype). Parameters such as `;charset=utf-8` are not accepted here — callers send them via the transport `Content-Type` header."), JsonRequired, Required, StringLength(128), RegularExpression("^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}$")]
    string MediaType,
    [property: JsonPropertyName("contentBytes"), Description("Authorized request body only; base64-encoded bytes up to the 262144-byte D-9 inline boundary (349528 base64 characters). Strict UTF-8 bytes with an optional leading UTF-8 BOM may be content-readable; binary and other encodings remain mutation-eligible but metadata_only. Exact bytes are preserved and never transcoded, repaired, or silently truncated. Never emitted in events, logs, audit, diagnostics, examples outside request bodies, or context-query metadata."), JsonRequired, Required, StringLength(349528)]
    string ContentBytes,
    [property: JsonPropertyName("contentMediaType"), Description("Optional original media type when the request body media type differs (e.g., transport wrapper vs. authored content)."), StringLength(128), RegularExpression("^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}$")]
    string? ContentMediaType = null);
