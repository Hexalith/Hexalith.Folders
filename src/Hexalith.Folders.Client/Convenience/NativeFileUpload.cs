using Hexalith.Folders.Client.Generated;
namespace Hexalith.Folders.Client.Convenience;

/// <summary>Supports native recovery using the existing generated canonical Add key.</summary>
public static class NativeFileUpload
{
    /// <summary>Hashes historical metadata through the unchanged canonical helper without serializing an invalid mutation.</summary>
    public static string ComputeLegacyAddIdempotencyKey(string operationId, PathMetadata path, string contentHashReference, string workspaceId, string taskId)
    {
        ArgumentNullException.ThrowIfNull(path);
        var legacy = new LegacyIdempotencyPathMetadata
        {
            NormalizedPath = path.NormalizedPath,
            DisplayName = path.DisplayName,
            PathPolicyClass = path.PathPolicyClass,
            UnicodeNormalization = path.UnicodeNormalization
        };
        return FileUpload.ComputeIdempotencyKey(new FileMutationRequest
        {
            OperationId = operationId,
            PathMetadata = legacy,
            FileOperationKind = FileMutationRequestFileOperationKind.Add,
            ContentHashReference = contentHashReference
        }, workspaceId, taskId);
    }
}
