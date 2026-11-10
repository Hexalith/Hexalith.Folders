using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The FolderMetadata request metadata.</summary>
/// <param name="DisplayName">Projected display metadata only; it is not aggregate identity.</param>
/// <param name="MetadataClass">Paths, repository names, branch names, and commit messages default to tenant_sensitive unless stricter tenant policy applies.</param>
[Description("The FolderMetadata request metadata.")]
public sealed record FolderMetadata(
    [property: JsonPropertyName("displayName"), Description("Projected display metadata only; it is not aggregate identity."), JsonRequired, Required, StringLength(160, MinimumLength = 1)]
    string DisplayName,
    [property: JsonPropertyName("metadataClass"), Description("Paths, repository names, branch names, and commit messages default to tenant_sensitive unless stricter tenant policy applies."), JsonRequired, Required]
    SensitiveMetadataTier MetadataClass);
