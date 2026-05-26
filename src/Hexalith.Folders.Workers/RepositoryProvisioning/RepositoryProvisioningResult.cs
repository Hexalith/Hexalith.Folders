using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Workers.RepositoryProvisioning;

public sealed record RepositoryProvisioningResult(
    RepositoryProvisioningResultCode Code,
    string ReasonCode,
    string ManagedTenantId,
    string FolderId,
    string RepositoryBindingId,
    string ProviderBindingRef,
    FolderAppendOutcome? AppendOutcome);
