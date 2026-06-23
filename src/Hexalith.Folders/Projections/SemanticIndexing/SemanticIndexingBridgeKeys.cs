namespace Hexalith.Folders.Projections.SemanticIndexing;

public static class SemanticIndexingBridgeKeys
{
    public static string FileVersion(string managedTenantId, string folderId, string fileVersionId)
    {
        SemanticIndexingBridgeValidation.RequireSegment(managedTenantId, nameof(managedTenantId));
        SemanticIndexingBridgeValidation.RequireSegment(folderId, nameof(folderId));
        SemanticIndexingBridgeValidation.RequireSegment(fileVersionId, nameof(fileVersionId));
        return $"{managedTenantId}:semantic-indexing:file-version:{folderId}:{fileVersionId}";
    }
}
