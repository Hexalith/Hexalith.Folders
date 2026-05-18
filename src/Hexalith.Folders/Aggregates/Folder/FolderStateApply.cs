using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderStateApply
{
    public static FolderState Apply(FolderState state, IFolderEvent folderEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(folderEvent);

        if (state.IsCreated
            && (!string.Equals(state.ManagedTenantId, folderEvent.ManagedTenantId, StringComparison.Ordinal)
                || !string.Equals(state.FolderId, folderEvent.FolderId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Foreign folder event in Apply: expected {state.ManagedTenantId}:folders:{state.FolderId}, " +
                $"got {folderEvent.ManagedTenantId}:folders:{folderEvent.FolderId}.");
        }

        Dictionary<string, string> idempotency = new(state.IdempotencyFingerprints, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey))
        {
            idempotency[folderEvent.IdempotencyKey] = folderEvent.IdempotencyFingerprint;
        }

        return folderEvent switch
        {
            FolderCreated created => state with
            {
                IsCreated = true,
                ManagedTenantId = created.ManagedTenantId,
                OrganizationId = created.OrganizationId,
                FolderId = created.FolderId,
                DisplayName = created.DisplayName,
                Description = created.Description,
                PathLabel = created.PathLabel,
                Tags = created.Tags.ToArray(),
                LifecycleState = created.LifecycleState,
                RepositoryBindingState = created.RepositoryBindingState,
                IdempotencyFingerprints = idempotency.ToFrozenDictionary(StringComparer.Ordinal),
            },
            _ => state with { IdempotencyFingerprints = idempotency.ToFrozenDictionary(StringComparer.Ordinal) },
        };
    }
}
