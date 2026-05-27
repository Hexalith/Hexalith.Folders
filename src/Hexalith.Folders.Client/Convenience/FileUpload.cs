using System;
using System.Security.Cryptography;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Builds the polymorphic <see cref="FileMutationRequest"/> body for the generated file-mutation operations,
/// selecting the inline vs streamed transport shape so callers never hand-assemble the union.
/// </summary>
/// <remarks>
/// <para>This helper is intentionally thin: every value it sets is an existing field of the Contract Spine
/// <c>FileMutationRequest</c> schema (<c>requestSchemaVersion</c>, <c>operationId</c>, <c>pathMetadata</c>,
/// <c>contentHashReference</c>, <c>byteLength</c>, <c>transportOperation</c>, <c>fileOperationKind</c>, and
/// the <c>inlineContent</c>/<c>streamDescriptor</c> branch). It introduces no new field, state, operation,
/// error category, or retry semantics.</para>
/// <para>The inline (<c>PutFileInline</c>) and streamed (<c>PutFileStream</c>) descriptors are attached
/// through the generated request's JSON extension data because the generated <see cref="FileMutationRequest"/>
/// surfaces the merged <c>oneOf</c> branch members there rather than as typed properties.</para>
/// </remarks>
public static class FileUpload
{
    /// <summary>
    /// The inclusive inline-transport boundary in bytes (the D-9 boundary). Content at or below this size
    /// uses <c>PutFileInline</c>; larger content must be staged and submitted via <c>PutFileStream</c>.
    /// </summary>
    public const int InlineTransportBoundaryBytes = 262144;

    private const string RequestSchemaVersionV1 = "v1";
    private const string InlineTransportOperation = "PutFileInline";
    private const string StreamTransportOperation = "PutFileStream";
    private const string InlineContentField = "inlineContent";
    private const string StreamDescriptorField = "streamDescriptor";

    /// <summary>
    /// Builds an inline (<c>PutFileInline</c>) file-mutation request from in-memory content.
    /// </summary>
    /// <param name="content">The decoded file content; must be at or below <see cref="InlineTransportBoundaryBytes"/>.</param>
    /// <param name="mediaType">The RFC 6838 media type (type/subtype only).</param>
    /// <param name="pathMetadata">The workspace-root-relative path metadata.</param>
    /// <param name="operationId">The opaque per-file operation identifier.</param>
    /// <param name="fileOperationKind">Add or Change.</param>
    /// <param name="contentMediaType">Optional original media type when it differs from <paramref name="mediaType"/>.</param>
    /// <param name="contentHashReference">Optional explicit content-hash reference; derived from content when null.</param>
    /// <returns>A <see cref="FileMutationRequest"/> carrying the inline transport branch.</returns>
    /// <exception cref="FileUploadStreamingRequiredException">Thrown when content exceeds the inline boundary.</exception>
    public static FileMutationRequest BuildInlineFileMutation(
        ReadOnlyMemory<byte> content,
        string mediaType,
        PathMetadata pathMetadata,
        string operationId,
        FileMutationRequestFileOperationKind fileOperationKind = FileMutationRequestFileOperationKind.Add,
        string? contentMediaType = null,
        string? contentHashReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(pathMetadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        EnsureUploadOperationKind(fileOperationKind);

        if (content.Length > InlineTransportBoundaryBytes)
        {
            throw new FileUploadStreamingRequiredException();
        }

        PutFileInline inline = new()
        {
            MediaType = mediaType,
            ContentBytes = Convert.ToBase64String(content.Span),
        };
        if (!string.IsNullOrWhiteSpace(contentMediaType))
        {
            inline.ContentMediaType = contentMediaType;
        }

        FileMutationRequest request = new()
        {
            RequestSchemaVersion = RequestSchemaVersionV1,
            OperationId = operationId,
            PathMetadata = pathMetadata,
            FileOperationKind = fileOperationKind,
            TransportOperation = InlineTransportOperation,
            ByteLength = content.Length,
            ContentHashReference = string.IsNullOrWhiteSpace(contentHashReference)
                ? ComputeContentHashReference(content.Span)
                : contentHashReference,
        };
        request.AdditionalProperties[InlineContentField] = inline;
        return request;
    }

    /// <summary>
    /// Builds a streamed (<c>PutFileStream</c>) file-mutation request from caller-supplied staging evidence.
    /// </summary>
    /// <param name="stagingEvidence">Evidence describing content already staged out-of-band.</param>
    /// <param name="mediaType">The RFC 6838 media type (type/subtype only).</param>
    /// <param name="pathMetadata">The workspace-root-relative path metadata.</param>
    /// <param name="operationId">The opaque per-file operation identifier.</param>
    /// <param name="fileOperationKind">Add or Change.</param>
    /// <returns>A <see cref="FileMutationRequest"/> carrying the streamed transport branch.</returns>
    public static FileMutationRequest BuildStreamedFileMutation(
        FileStreamStagingEvidence stagingEvidence,
        string mediaType,
        PathMetadata pathMetadata,
        string operationId,
        FileMutationRequestFileOperationKind fileOperationKind = FileMutationRequestFileOperationKind.Add)
    {
        ArgumentNullException.ThrowIfNull(stagingEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(pathMetadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingEvidence.StagingReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingEvidence.ObservedContentHashReference);
        EnsureUploadOperationKind(fileOperationKind);

        if (stagingEvidence.ObservedLength <= InlineTransportBoundaryBytes)
        {
            // The streamed branch is only valid above the inline boundary; smaller content belongs inline.
            throw new ArgumentOutOfRangeException(
                nameof(stagingEvidence),
                "Streamed staging evidence must observe content larger than the inline transport boundary.");
        }

        PutFileStream descriptor = new()
        {
            MediaType = mediaType,
            DeclaredLength = stagingEvidence.ObservedLength,
            ObservedLength = stagingEvidence.ObservedLength,
            StagingReference = stagingEvidence.StagingReference,
            ObservedContentHashReference = stagingEvidence.ObservedContentHashReference,
            UploadMode = PutFileStreamUploadMode.Request_body_stream,
        };

        FileMutationRequest request = new()
        {
            RequestSchemaVersion = RequestSchemaVersionV1,
            OperationId = operationId,
            PathMetadata = pathMetadata,
            FileOperationKind = fileOperationKind,
            TransportOperation = StreamTransportOperation,
            ByteLength = stagingEvidence.ObservedLength,
            ContentHashReference = stagingEvidence.ObservedContentHashReference,
        };
        request.AdditionalProperties[StreamDescriptorField] = descriptor;
        return request;
    }

    /// <summary>
    /// Computes the idempotency key for a file mutation via the generated canonical hasher. This is the
    /// supported path for callers to source an idempotency key — the SDK never auto-generates one.
    /// </summary>
    /// <param name="request">The built file-mutation request.</param>
    /// <param name="workspaceId">The workspace identifier (a canonical idempotency-equivalence input).</param>
    /// <param name="taskId">The task identifier (a canonical idempotency-equivalence input).</param>
    /// <returns>The canonical <c>sha256:&lt;hex&gt;</c> idempotency key.</returns>
    public static string ComputeIdempotencyKey(FileMutationRequest request, string workspaceId, string taskId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        // Delegates to the generated ComputeIdempotencyHash so canonicalization is never reimplemented.
        return request.ComputeIdempotencyHash(workspaceId, taskId);
    }

    private static void EnsureUploadOperationKind(FileMutationRequestFileOperationKind fileOperationKind)
    {
        if (fileOperationKind is not (FileMutationRequestFileOperationKind.Add or FileMutationRequestFileOperationKind.Change))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileOperationKind),
                "File uploads support only Add and Change; use RemoveFileAsync for metadata-only removals.");
        }
    }

    private static string ComputeContentHashReference(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[32];
        _ = SHA256.HashData(content, digest);

        // hashref_ + 64 lowercase hex matches the ContentHashReference spine pattern.
        return string.Concat("hashref_", Convert.ToHexString(digest).ToLowerInvariant());
    }
}
