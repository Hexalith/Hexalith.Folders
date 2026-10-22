namespace Hexalith.Folders.Server.Content;

/// <summary>A value-free file-policy, visibility or unsupported confidentiality outcome.</summary>
public sealed class WorkspaceContentPolicyException(string code) : InvalidOperationException("Workspace content policy rejected the operation.")
{
    /// <summary>Gets the safe canonical classification, never a path or policy rule.</summary>
    public string Code { get; } = code;
}
