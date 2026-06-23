using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingFileVersionIdentity
{
    public SemanticIndexingFileVersionIdentity(
        string managedTenantId,
        string organizationId,
        string folderId,
        string workspaceId,
        string operationId,
        string pathMetadataDigest,
        string fileVersionId,
        string? contentHashReference,
        string sourceUri)
    {
        ManagedTenantId = SemanticIndexingBridgeValidation.RequireSegment(managedTenantId, nameof(managedTenantId));
        OrganizationId = SemanticIndexingBridgeValidation.RequireSegment(organizationId, nameof(organizationId));
        FolderId = SemanticIndexingBridgeValidation.RequireSegment(folderId, nameof(folderId));
        WorkspaceId = SemanticIndexingBridgeValidation.RequireSegment(workspaceId, nameof(workspaceId));
        OperationId = SemanticIndexingBridgeValidation.RequireSegment(operationId, nameof(operationId));
        PathMetadataDigest = SemanticIndexingBridgeValidation.RequireMetadataReference(pathMetadataDigest, nameof(pathMetadataDigest));
        FileVersionId = SemanticIndexingBridgeValidation.RequireSegment(fileVersionId, nameof(fileVersionId));
        ContentHashReference = SemanticIndexingBridgeValidation.RequireOptionalValue(contentHashReference, nameof(contentHashReference));
        SourceUri = SemanticIndexingBridgeValidation.RequireSourceUri(sourceUri, nameof(sourceUri));
    }

    public string ManagedTenantId { get; }

    public string OrganizationId { get; }

    public string FolderId { get; }

    public string WorkspaceId { get; }

    public string OperationId { get; }

    public string PathMetadataDigest { get; }

    public string FileVersionId { get; }

    public string? ContentHashReference { get; }

    public string SourceUri { get; }

    public string ReadModelKey => SemanticIndexingBridgeKeys.FileVersion(ManagedTenantId, FolderId, FileVersionId);

    public static SemanticIndexingFileVersionIdentity From(WorkspaceFileMutationAccepted accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        string pathDigest = SemanticIndexingBridgeValidation.RequireMetadataReference(accepted.PathMetadataDigest, nameof(accepted.PathMetadataDigest));
        string contentHash = accepted.ContentHashReference ?? "no-content-hash";
        string fileVersionId = DeriveFileVersionId(
            accepted.ManagedTenantId,
            accepted.OrganizationId,
            accepted.FolderId,
            accepted.WorkspaceId,
            accepted.OperationId,
            contentHash,
            pathDigest);
        string sourceUri = string.Create(
            CultureInfo.InvariantCulture,
            $"folders://{accepted.ManagedTenantId}/organizations/{accepted.OrganizationId}/folders/{accepted.FolderId}/workspaces/{accepted.WorkspaceId}/file-versions/{fileVersionId}");

        return new SemanticIndexingFileVersionIdentity(
            accepted.ManagedTenantId,
            accepted.OrganizationId,
            accepted.FolderId,
            accepted.WorkspaceId,
            accepted.OperationId,
            pathDigest,
            fileVersionId,
            accepted.ContentHashReference,
            sourceUri);
    }

    private static string DeriveFileVersionId(
        string managedTenantId,
        string organizationId,
        string folderId,
        string workspaceId,
        string operationId,
        string contentHashReference,
        string pathMetadataDigest)
    {
        string material = string.Join(
            '\u001f',
            managedTenantId,
            organizationId,
            folderId,
            workspaceId,
            operationId,
            contentHashReference,
            pathMetadataDigest);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return "fv-" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
