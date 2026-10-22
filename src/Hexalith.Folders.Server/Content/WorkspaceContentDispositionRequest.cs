namespace Hexalith.Folders.Server.Content;

/// <summary>Content-only authenticated disposition transport; SDK owns every physical effect.</summary>
/// <param name="ReceiptId">Opaque tenant/workspace receipt.</param>
/// <param name="Operation">hold, release, abandon, or apply.</param>
public sealed record WorkspaceContentDispositionRequest(string ReceiptId, string Operation);
