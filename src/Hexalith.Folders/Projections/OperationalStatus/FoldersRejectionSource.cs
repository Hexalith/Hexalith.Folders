using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Validates retained rejection scope without requiring an unknown command to be implemented.</summary>
public static class FoldersRejectionSource
{
    private static readonly HashSet<string> OrganizationCommands = new(StringComparer.Ordinal)
    {
        "Hexalith.Folders.Commands.InitializeOrganizationAclBaseline",
        "Hexalith.Folders.Commands.GrantOrganizationAclPrincipal",
        "Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal",
    };

    private static readonly HashSet<string> FolderCommands = new(StringComparer.Ordinal)
    {
        "Hexalith.Folders.Commands.CreateFolder", "Hexalith.Folders.Commands.GrantFolderAccess",
        "Hexalith.Folders.Commands.RevokeFolderAccess", "Hexalith.Folders.Commands.ArchiveFolder",
        "Hexalith.Folders.Commands.CreateRepositoryBackedFolder", "Hexalith.Folders.Commands.BindRepository",
        "Hexalith.Folders.Commands.ConfigureBranchRefPolicy", "Hexalith.Folders.Commands.PrepareWorkspace",
        "Hexalith.Folders.Commands.LockWorkspace", "Hexalith.Folders.Commands.ReleaseWorkspaceLock",
        "Hexalith.Folders.Commands.MutateFiles", "Hexalith.Folders.Commands.ChangeNativeTask",
        "Hexalith.Folders.Commands.AuthorizeWorkspaceContentErasure", "Hexalith.Folders.Commands.CommitWorkspace",
    };

    /// <summary>Derives owner kind from the envelope scope and checks known commands and any retained scope hints.</summary>
    /// <remarks>Pre-validation rejections can legitimately have null scope hints; none is used as authority.</remarks>
    public static FoldersSourceOwnerKind ReadOwner(byte[] payload, string tenantId, string aggregateId, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!OrganizationStreamName.TryCreate(tenantId, tenantId, out _, out _))
            throw new InvalidOperationException("Invalid Folders rejection tenant.");
        FoldersSourceOwnerKind owner;
        if (aggregateId == tenantId) owner = FoldersSourceOwnerKind.Organization;
        else if (ProviderBindingActorId.IsReserved(aggregateId))
        {
            string suffix = aggregateId[ProviderBindingActorId.Prefix.Length..];
            if (suffix.Length != 64 || suffix.Any(character => !char.IsAsciiHexDigitLower(character)))
                throw new InvalidOperationException("Invalid provider-binding rejection owner.");
            owner = FoldersSourceOwnerKind.ProviderBinding;
        }
        else
        {
            _ = FolderStreamName.Create(tenantId, aggregateId);
            owner = FoldersSourceOwnerKind.Folder;
        }
        using JsonDocument json = JsonDocument.Parse(payload);
        JsonElement root = json.RootElement;
        string? command = Text(root, "CommandType", "commandType");
        string? code = Text(root, "Code", "code");
        if (command is null || command.Length is 0 or > 128
            || command.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            || !Enum.TryParse(code, out FolderResultCode result) || !Enum.IsDefined(result)
            || result == FolderResultCode.Accepted || code != result.ToString()
            || OrganizationCommands.Contains(command) && owner != FoldersSourceOwnerKind.Organization
            || FolderCommands.Contains(command) && owner != FoldersSourceOwnerKind.Folder
            || command == "Hexalith.Folders.Commands.ConfigureProviderBinding" && owner != FoldersSourceOwnerKind.ProviderBinding)
            throw new InvalidOperationException("Folders rejection command or owner is invalid.");
        RequireHint(root, "ManagedTenantId", "managedTenantId", tenantId);
        RequireHint(root, "OrganizationId", "organizationId", tenantId);
        RequireHint(root, "FolderId", "folderId", owner == FoldersSourceOwnerKind.Folder ? aggregateId : null);
        RequireHint(root, "CorrelationId", "correlationId", correlationId);
        return owner;
    }

    private static string? Text(JsonElement root, string name, string camelName)
        => root.ValueKind == JsonValueKind.Object && (root.TryGetProperty(name, out JsonElement value)
            || root.TryGetProperty(camelName, out value)) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void RequireHint(JsonElement root, string name, string camelName, string? expected)
    {
        if ((root.TryGetProperty(name, out JsonElement value) || root.TryGetProperty(camelName, out value))
            && value.ValueKind != JsonValueKind.Null
            && (value.ValueKind != JsonValueKind.String || value.GetString() != expected))
            throw new InvalidOperationException("Folders rejection scope differs from its source.");
    }
}
