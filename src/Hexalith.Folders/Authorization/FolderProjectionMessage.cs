namespace Hexalith.Folders.Authorization;

/// <summary>Bounded replay identity retained for a recently projected event.</summary>
public sealed record FolderProjectionMessage(long Sequence, string MessageId, string PayloadDigest);
