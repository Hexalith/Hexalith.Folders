namespace Hexalith.Folders.Aggregates.Folder;

public sealed record MutateWorkspaceFile(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string WorkspaceId,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    PathMetadata PathMetadata,
    string? ContentHashReference,
    long? ByteLength,
    string? MediaType,
    string? TransportEvidenceKind,
    long? ObservedByteLength,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantIds = null,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalIds = null) : IFolderCommand
{
    /// <summary>Gets the server-verified opaque upload receipt.</summary>
    public string? ContentReceiptId { get; init; }

    /// <summary>Gets the caller's expected incarnation version.</summary>
    public long? ExpectedFileVersion { get; init; }

    /// <summary>Gets the caller's expected lock acquisition fence.</summary>
    public long? ExpectedLockFence { get; init; }

    /// <summary>Gets the explicit owner File identity; add operations allocate it at the owner boundary.</summary>
    public string? OwnerFileId { get; init; }

    /// <summary>Gets the explicit Projects-owned reference bound by this owner operation.</summary>
    public string? ProjectFileReferenceId { get; init; }

    public const string CommandTypeName = "Hexalith.Folders.Commands.MutateFiles";

    public string CommandType => CommandTypeName;

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
