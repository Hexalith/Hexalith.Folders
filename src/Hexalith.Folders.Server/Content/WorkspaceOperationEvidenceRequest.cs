using Hexalith.Folders.Aggregates.Folder;
namespace Hexalith.Folders.Server.Content;

/// <summary>Requests bounded authoritative absence or exact accepted-key recovery.</summary>
internal sealed record WorkspaceOperationEvidenceRequest(string LookupKind, string OperationId, string AcceptedKey,
    PathMetadata PathMetadata, string ContentHashReference, long ByteLength, string MediaType, long LockFence = 0, bool Legacy = false);
