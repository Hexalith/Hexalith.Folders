namespace Hexalith.Folders.Server.Content;

/// <summary>Private metadata-only source erasure reservation.</summary>
/// <param name="WorkspaceId">Owning workspace.</param>
/// <param name="ReceiptId">Verified opaque body receipt.</param>
/// <param name="AssignedTaskId">Assigned source task, or null only for a durably abandoned never-task upload.</param>
/// <param name="ExpectedGeneration">Exact source revision for terminal/no-active/reference proof.</param>
internal sealed record WorkspaceContentErasureRequest(string WorkspaceId, string ReceiptId, string? AssignedTaskId, long ExpectedGeneration);
