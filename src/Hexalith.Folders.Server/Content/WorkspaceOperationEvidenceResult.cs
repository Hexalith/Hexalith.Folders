namespace Hexalith.Folders.Server.Content;

/// <summary>Returns original source preconditions only for an exact confirmed publication.</summary>
internal sealed record WorkspaceOperationEvidenceResult(string Status, long ExpectedFileVersion, long LockFence, bool Completed,
    string? FileId = null, string? IncarnationId = null, string? Fingerprint = null, string? AcceptedKey = null,
    string? OperationId = null, string? ContentHashReference = null, string? MediaType = null, string? TaskId = null,
    string? CommandId = null, long FileVersion = 0, long ByteLength = 0, string? LockId = null, string? OwnershipProof = null);
