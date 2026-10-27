using System.Text.Json;

namespace Hexalith.Folders.Workers;

/// <summary>Classifies the frozen command type stored on a rejection event.</summary>
internal static class FoldersRejectedCommandClassifier
{
    private static readonly HashSet<string> OrganizationCommands = new(StringComparer.Ordinal)
    {
        "Hexalith.Folders.Commands.InitializeOrganizationAclBaseline",
        "Hexalith.Folders.Commands.GrantOrganizationAclPrincipal",
        "Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal",
        "Hexalith.Folders.Commands.ConfigureProviderBinding",
    };

    private static readonly HashSet<string> FolderCommands = new(StringComparer.Ordinal)
    {
        "Hexalith.Folders.Commands.CreateFolder",
        "Hexalith.Folders.Commands.GrantFolderAccess",
        "Hexalith.Folders.Commands.RevokeFolderAccess",
        "Hexalith.Folders.Commands.ArchiveFolder",
        "Hexalith.Folders.Commands.CreateRepositoryBackedFolder",
        "Hexalith.Folders.Commands.BindRepository",
        "Hexalith.Folders.Commands.ConfigureBranchRefPolicy",
        "Hexalith.Folders.Commands.PrepareWorkspace",
        "Hexalith.Folders.Commands.LockWorkspace",
        "Hexalith.Folders.Commands.ReleaseWorkspaceLock",
        "Hexalith.Folders.Commands.MutateFiles",
        "Hexalith.Folders.Commands.ChangeNativeTask",
        "Hexalith.Folders.Commands.AuthorizeWorkspaceContentErasure",
        "Hexalith.Folders.Commands.CommitWorkspace",
    };

    public static bool IsOrganization(byte[] payload)
    {
        using JsonDocument json = JsonDocument.Parse(payload);
        JsonElement root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("CommandType", out JsonElement commandType)
                && !root.TryGetProperty("commandType", out commandType)
            || commandType.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Folders rejection command type is unreadable.");
        }

        string? value = commandType.GetString();
        if (value is null || !OrganizationCommands.Contains(value) && !FolderCommands.Contains(value))
        {
            throw new InvalidOperationException("Folders rejection command type is unknown.");
        }

        return OrganizationCommands.Contains(value);
    }
}
