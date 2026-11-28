namespace Hexalith.Folders.Server.Content;

/// <summary>Requests one task-owned native lease operation.</summary>
internal sealed record WorkspaceNativeLeaseRequest(string Operation, int RequestedLeaseSeconds, string? LockId = null,
    string? OwnershipProof = null, long LockFence = 0, string? OperationId = null, string? AcceptedKey = null, string? Fingerprint = null);
