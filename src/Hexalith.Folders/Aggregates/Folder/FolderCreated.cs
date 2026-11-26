using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCreated(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState LifecycleState,
    FolderRepositoryBindingState RepositoryBindingState,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent
{
    /// <summary>Gets the owner receipt committed on the restricted creation event.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FolderProvisioningReceipt? ProvisioningReceipt { get; init; }

    /// <summary>Gets the explicit inheritance choice for restricted provisioning. Legacy creation omits it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? InheritOrganizationPermissions { get; init; }
}
