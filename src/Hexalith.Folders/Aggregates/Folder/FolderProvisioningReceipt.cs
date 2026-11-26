namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>The durable scope and intent of a restricted Folder creation.</summary>
/// <param name="ReceiptId">The deterministic owner receipt identifier.</param>
/// <param name="TenantId">The original Tenant.</param>
/// <param name="FolderId">The exact derived owner stream identifier.</param>
/// <param name="OriginalActorId">The original actor bound by the admitted task.</param>
/// <param name="TaskId">The admitted task identifier.</param>
/// <param name="StepKey">The canonical numbered owner step key.</param>
/// <param name="AdmissionOperation">The versioned admitted operation.</param>
/// <param name="RequestSha256">The admitted normalized request digest.</param>
/// <param name="OwnerVersion">The owner stream version after the atomic create and two access grants.</param>
public sealed record FolderProvisioningReceipt(
    string ReceiptId,
    string TenantId,
    string FolderId,
    string OriginalActorId,
    string TaskId,
    string StepKey,
    string AdmissionOperation,
    string RequestSha256,
    long OwnerVersion);
