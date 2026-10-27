namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Source-folded metadata binding; authoritative bodies remain exclusively in EventStore's content namespace.</summary>
/// <param name="ManagedTenantId">Owning tenant.</param>
/// <param name="FolderId">Owning folder.</param>
/// <param name="WorkspaceId">Owning workspace.</param>
/// <param name="TaskId">Authorized task.</param>
/// <param name="CommandId">Exact authoritative command.</param>
/// <param name="OperationId">Logical file operation.</param>
/// <param name="FileId">Never-reused owner incarnation.</param>
/// <param name="Version">Immutable incarnation version.</param>
/// <param name="ExpectedVersion">Pre-append aggregate head.</param>
/// <param name="LockFence">Monotonic lock acquisition fence.</param>
/// <param name="ReceiptId">Verified opaque body receipt, or null for removal.</param>
/// <param name="ContentHashReference">Verified content hash reference.</param>
/// <param name="ByteLength">Verified exact length.</param>
/// <param name="MediaType">Verified media metadata.</param>
/// <param name="PolicyVersion">Pinned server policy version.</param>
/// <param name="PolicyDigest">Pinned server policy digest.</param>
/// <param name="FileOperationKind">Add, change or removal.</param>
/// <param name="PathMetadataDigest">Exact accepted descriptive metadata digest.</param>
/// <param name="ContentReadable">Server-observed exact-byte UTF-8 eligibility.</param>
public sealed record WorkspaceContentBinding(string ManagedTenantId, string FolderId, string WorkspaceId,
    string TaskId, string CommandId, string OperationId, string FileId, long Version, long ExpectedVersion,
    long LockFence, string? ReceiptId, string? ContentHashReference, long? ByteLength, string? MediaType,
    string PolicyVersion, string PolicyDigest, string FileOperationKind, string PathMetadataDigest, bool ContentReadable = false)
{
    /// <summary>Gets the exact caller SDK key retained independently of EventStore execution identifiers.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AcceptedSdkKey { get; init; }

    /// <summary>Derives a bounded, unambiguous per-item source key without extending caller identifiers.</summary>
    public static string BatchItemKey(string commandId, string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId); ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        string value = commandId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + commandId
            + operationId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + operationId;
        return "batch_" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }
}
