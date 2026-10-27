namespace Hexalith.Folders.Server.Content;

/// <summary>Carries exact source generation and owner lease identity into the serialized actor decision.</summary>
internal sealed record WorkspaceNativeTaskCommand(string WorkspaceId, string TaskId, long ExpectedHead, WorkspaceNativeLeaseRequest Request);
