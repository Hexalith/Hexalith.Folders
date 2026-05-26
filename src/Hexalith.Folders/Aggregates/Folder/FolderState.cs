using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderState(
    bool IsCreated,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState? LifecycleState,
    FolderRepositoryBindingState? RepositoryBindingState,
    string? RepositoryBindingId,
    string? ProviderBindingRef,
    string? RepositoryProfileRef,
    string? BranchRefPolicyRef,
    string? RepositoryBindingFailureCategory,
    string? RepositoryBindingOutcomeCategory,
    DateTimeOffset? RepositoryBindingUpdatedAt,
    string? RepositoryBindingActorPrincipalId,
    string? RepositoryBindingCorrelationId,
    string? RepositoryBindingTaskId,
    string? RepositoryBindingIdempotencyKey,
    string? RepositoryBindingIdempotencyFingerprint,
    FolderArchiveReasonCode? ArchiveReasonCode,
    string? ArchiveActorPrincipalId,
    string? ArchiveCorrelationId,
    string? ArchiveTaskId,
    string? ArchiveIdempotencyKey,
    DateTimeOffset? ArchivedAt,
    IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> AccessOverrides,
    long AccessSequence,
    IReadOnlyDictionary<string, string> IdempotencyFingerprints)
{
    public static FolderState Empty { get; } = new(
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        FrozenDictionary<FolderAccessEntryKey, FolderAccessOverride>.Empty,
        0,
        FrozenDictionary<string, string>.Empty);

    public FolderState Apply(IEnumerable<IFolderEvent> events, FolderStreamName expectedStreamName)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(expectedStreamName);

        FolderState state = this;
        foreach (IFolderEvent folderEvent in events)
        {
            state = FolderStateApply.Apply(state, folderEvent, expectedStreamName);
        }

        return state;
    }

    public bool HasFolderAccess(FolderAccessEntryKey key)
        => AccessOverrides.TryGetValue(key, out FolderAccessOverride? access)
            && access.IsGranted;
}
