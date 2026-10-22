namespace Hexalith.Folders.Queries.FileContext;

/// <summary>Captures and reconfirms current immutable server policy; no permissive fallback exists.</summary>
public interface IWorkspaceFilePolicySource
{
    /// <summary>Captures the current authoritative server configuration.</summary>
    WorkspaceFilePolicySnapshot Capture();
    /// <summary>Confirms that no policy change occurred before publication or disclosure.</summary>
    bool IsCurrent(WorkspaceFilePolicySnapshot snapshot);
}
