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
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantIds = null,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalIds = null) : IFolderCommand
{
    public const string CommandTypeName = "Hexalith.Folders.Commands.MutateFiles";

    public string CommandType => CommandTypeName;

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
