namespace Hexalith.Folders.Server;

/// <summary>
/// Represents a single validated and translated file mutation item extracted from a decorated v2
/// <c>MutateFiles</c> <c>changes[]</c> batch before content-store receipt materialization.
/// </summary>
/// <param name="OperationId">The caller-supplied file operation identifier.</param>
/// <param name="FileOperationKind">The file operation kind (<c>add</c>, <c>change</c>, or <c>remove</c>).</param>
/// <param name="TransportOperation">The transport operation (<c>PutFileInline</c>, <c>PutFileStream</c>, or <c>metadataOnlyRemoval</c>).</param>
/// <param name="PathMetadata">The validated path metadata payload.</param>
/// <param name="ContentHashReference">The opaque content hash reference, or <see langword="null"/> for removals.</param>
/// <param name="ByteLength">The logical byte length, or <see langword="null"/> for removals.</param>
/// <param name="MediaType">The validated RFC 6838 media type, or <see langword="null"/> for removals.</param>
/// <param name="TransportEvidenceKind">The transport evidence kind (<c>inline_decoded</c> or <c>stream_observed</c>), or <see langword="null"/> for removals.</param>
/// <param name="ObservedByteLength">The observed byte length, or <see langword="null"/> for removals.</param>
/// <param name="ExpectedFileVersion">The caller-observed immutable file version.</param>
/// <param name="ExpectedLockFence">The caller-observed lock acquisition fence.</param>
/// <param name="OwnerFileId">Optional explicit owner incarnation identifier.</param>
/// <param name="DecodedInlineBytes">The decoded inline file bytes for <c>PutFileInline</c>, or <see langword="null"/> otherwise. Never persisted in events or logs.</param>
/// <param name="StagingReference">The staging reference for <c>PutFileStream</c>, or <see langword="null"/> otherwise.</param>
internal sealed record TranslatedFileMutationItem(
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    FolderDomainProcessor.PathMetadataPayload PathMetadata,
    string? ContentHashReference,
    long? ByteLength,
    string? MediaType,
    string? TransportEvidenceKind,
    long? ObservedByteLength,
    long ExpectedFileVersion,
    long ExpectedLockFence,
    string? OwnerFileId,
    byte[]? DecodedInlineBytes,
    string? StagingReference);
