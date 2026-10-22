using Hexalith.Folders.Queries.FileContext;
using Microsoft.Extensions.Configuration;

namespace Hexalith.Folders.Server.Content;

/// <summary>Captures complete configured policy on each evaluation; missing, invalid or drifting configuration fails closed.</summary>
public sealed class ConfigurationWorkspaceFilePolicySource(IConfiguration configuration) : IWorkspaceFilePolicySource
{
    /// <inheritdoc/>
    public WorkspaceFilePolicySnapshot Capture()
    {
        IConfigurationSection section = configuration.GetSection("Folders:WorkspaceFilePolicy");
        string[] Rules(string key) => section.GetSection(key).GetChildren().Select(child => child.Value ?? string.Empty).ToArray();
        try
        {
            return new(section["Version"] ?? string.Empty, Rules("Includes"), Rules("Excludes"), Rules("Restricted"),
                section["ProtectionMode"] ?? string.Empty, Rules("MetadataOnly"));
        }
        catch (InvalidOperationException) { throw new WorkspaceContentPolicyException("file_policy_unavailable"); }
    }
    /// <inheritdoc/>
    public bool IsCurrent(WorkspaceFilePolicySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try { var current = Capture(); return current.Version == snapshot.Version && current.Digest == snapshot.Digest; }
        catch (InvalidOperationException) { return false; }
    }
}
