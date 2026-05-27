using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Metadata describing where and how a file mutation is applied, independent of the transport shape
/// (inline vs streamed) that the upload convenience selects. Carries no raw content and no header values.
/// </summary>
public sealed record FileUploadDescriptor
{
    /// <summary>Gets the opaque tenant-scoped folder identifier (resource reference, not tenant authority).</summary>
    public required string FolderId { get; init; }

    /// <summary>Gets the opaque workspace identifier for the locked workspace receiving the mutation.</summary>
    public required string WorkspaceId { get; init; }

    /// <summary>Gets the opaque per-file operation identifier (an <c>OpaqueIdentifier</c>, caller-supplied).</summary>
    public required string OperationId { get; init; }

    /// <summary>Gets the workspace-root-relative path metadata for the file (never a local/absolute path).</summary>
    public required PathMetadata PathMetadata { get; init; }

    /// <summary>Gets the RFC 6838 media type (type/subtype only) for the content.</summary>
    public required string MediaType { get; init; }

    /// <summary>
    /// Gets the optional original media type when the request body media type differs from the authored
    /// content type. Applies to the inline transport only.
    /// </summary>
    public string? ContentMediaType { get; init; }

    /// <summary>
    /// Gets the file operation kind. Only <see cref="FileMutationRequestFileOperationKind.Add"/> and
    /// <see cref="FileMutationRequestFileOperationKind.Change"/> are valid for uploads; removals use the
    /// generated <c>RemoveFileAsync</c> operation directly.
    /// </summary>
    public FileMutationRequestFileOperationKind FileOperationKind { get; init; } = FileMutationRequestFileOperationKind.Add;

    /// <summary>
    /// Gets an optional explicit content-hash reference (<c>hashref_</c> shape). When null, the inline
    /// builder derives a deterministic reference from the content bytes; the streamed builder uses the
    /// observed reference from <see cref="FileStreamStagingEvidence"/>.
    /// </summary>
    public string? ContentHashReference { get; init; }
}
