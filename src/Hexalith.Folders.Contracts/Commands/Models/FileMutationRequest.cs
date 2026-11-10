using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The FileMutationRequest request metadata.</summary>
/// <param name="ExpectedFileVersion">Caller-observed immutable version; zero for a new incarnation. Required by durable execution.</param>
/// <param name="ExpectedLockFence">Caller-observed lock acquisition fence. Required by durable execution.</param>
/// <param name="FileOperationKind">The fileOperationKind value.</param>
/// <param name="TransportOperation">The transportOperation value.</param>
/// <param name="OperationId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="PathMetadata">File-policy v1.1.0 workspace-root-relative path metadata. Paths preserve the caller&#x27;s accepted spelling, use forward slashes, have no leading or trailing slash, are declared NFC, and are never local or absolute filesystem paths. The server does not lowercase, case-fold, separator-convert, Unicode-rewrite, alias-resolve, follow links, or otherwise retarget a path. Traversal, reserved names, a trailing space or dot in any segment, `.git` and its descendants, ordinal-ignore-case component aliases, and every touched symlink/reparse entry or ancestor are rejected before observation.</param>
/// <param name="OwnerFileId">Optional explicit owner incarnation; an exact accepted path is resolved by the owner when omitted.</param>
/// <param name="ContentHashReference">Non-secret opaque content hash reference. The `hashref_` prefix distinguishes content hash references from generic opaque identifiers (`opaque_*`) to prevent cross-domain key reuse, copy-paste collisions with operation IDs, and audit-pipeline misclassification. The reference is never a secret, token, credential, file content, or absolute path.</param>
/// <param name="ByteLength">Logical content length in bytes. Inline branch caps this at 262144; stream branch permits 262145 through the file-policy v1.1.0 per-file maximum of 1048576. Content is never silently truncated.</param>
/// <param name="InlineContent">The inlineContent value.</param>
/// <param name="StreamDescriptor">The streamDescriptor value.</param>
[Description("The FileMutationRequest request metadata.")]
public sealed record FileMutationRequest(
    [property: JsonPropertyName("expectedFileVersion"), Description("Caller-observed immutable version; zero for a new incarnation. Required by durable execution."), JsonRequired, Required, Range(typeof(long), "0", "9223372036854775807")]
    long ExpectedFileVersion,
    [property: JsonPropertyName("expectedLockFence"), Description("Caller-observed lock acquisition fence. Required by durable execution."), JsonRequired, Required, Range(typeof(long), "1", "9223372036854775807")]
    long ExpectedLockFence,
    [property: JsonPropertyName("fileOperationKind"), Description("The file operation kind."), JsonRequired, Required]
    FileMutationRequestFileOperationKind FileOperationKind,
    [property: JsonPropertyName("transportOperation"), Description("The transport operation."), JsonRequired, Required]
    FileMutationRequestTransportOperation TransportOperation,
    [property: HexalithIdentifier, JsonPropertyName("operationId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string OperationId,
    [property: JsonPropertyName("pathMetadata"), Description("File-policy v1.1.0 workspace-root-relative path metadata. Paths preserve the caller's accepted spelling, use forward slashes, have no leading or trailing slash, are declared NFC, and are never local or absolute filesystem paths. The server does not lowercase, case-fold, separator-convert, Unicode-rewrite, alias-resolve, follow links, or otherwise retarget a path. Traversal, reserved names, a trailing space or dot in any segment, `.git` and its descendants, ordinal-ignore-case component aliases, and every touched symlink/reparse entry or ancestor are rejected before observation."), JsonRequired, Required]
    PathMetadata PathMetadata,
    [property: HexalithIdentifier, JsonPropertyName("ownerFileId"), Description("Optional explicit owner incarnation; an exact accepted path is resolved by the owner when omitted."), StringLength(128)]
    string? OwnerFileId = null,
    [property: JsonPropertyName("contentHashReference"), Description("Non-secret opaque content hash reference. The `hashref_` prefix distinguishes content hash references from generic opaque identifiers (`opaque_*`) to prevent cross-domain key reuse, copy-paste collisions with operation IDs, and audit-pipeline misclassification. The reference is never a secret, token, credential, file content, or absolute path."), StringLength(104, MinimumLength = 40), RegularExpression("^hashref_[A-Za-z0-9]{32,96}$")]
    string? ContentHashReference = null,
    [property: JsonPropertyName("byteLength"), Description("Logical content length in bytes. Inline branch caps this at 262144; stream branch permits 262145 through the file-policy v1.1.0 per-file maximum of 1048576. Content is never silently truncated."), Range(0, 1048576)]
    int? ByteLength = null,
    [property: JsonPropertyName("inlineContent"), Description("The inline content.")]
    PutFileInline? InlineContent = null,
    [property: JsonPropertyName("streamDescriptor"), Description("The stream descriptor.")]
    PutFileStream? StreamDescriptor = null);
