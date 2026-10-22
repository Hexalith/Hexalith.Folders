namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileContentStoreRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string TaskId,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    string ContentHashReference,
    long ByteLength,
    string MediaType,
    string TransportEvidenceKind,
    long ObservedByteLength)
{
    /// <summary>Gets the caller-observed lock fence; absence cannot authorize durable execution.</summary>
    public long? RequestedLockFence { get; init; }
    /// <summary>Gets the caller-observed file version; absence cannot authorize durable execution.</summary>
    public long? RequestedFileVersion { get; init; }
    /// <summary>Gets the authenticated principal, distinct from diagnostic actor aliases.</summary>
    public string? PrincipalId { get; init; }
    /// <summary>Gets the owner-allocated file incarnation.</summary>
    public string? FileId { get; init; }
    /// <summary>Gets the immutable next file version.</summary>
    public long FileVersion { get; init; }
    /// <summary>Gets the pre-append authoritative actor head.</summary>
    public long ExpectedVersion { get; init; }
    /// <summary>Gets the current monotonic lock fence.</summary>
    public long LockFence { get; init; }
    /// <summary>Gets the verified opaque upload receipt, absent for removal.</summary>
    public string? ReceiptId { get; init; }
    /// <summary>Gets the exact accepted path metadata for the private content ledger.</summary>
    public PathMetadata? PathMetadata { get; init; }
    /// <summary>Gets the pinned server-owned policy version.</summary>
    public string? PolicyVersion { get; init; }
    /// <summary>Gets the pinned immutable policy digest.</summary>
    public string? PolicyDigest { get; init; }
}
