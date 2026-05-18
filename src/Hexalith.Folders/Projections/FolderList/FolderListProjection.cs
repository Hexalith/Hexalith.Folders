using System.Collections.Frozen;

namespace Hexalith.Folders.Projections.FolderList;

public sealed record FolderListProjection(IReadOnlyDictionary<string, FolderListItem> Folders)
{
    public static FolderListProjection Empty { get; } = new(FrozenDictionary<string, FolderListItem>.Empty);

    public FolderListProjection Apply(IEnumerable<FolderProjectionEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        Dictionary<string, FolderListItem> folders = new(Folders, StringComparer.Ordinal);
        foreach (FolderProjectionEnvelope envelope in envelopes.OrderBy(static item => item.Sequence))
        {
            string key = Key(envelope.ManagedTenantId, envelope.Event.FolderId);
            folders[key] = new FolderListItem(
                envelope.ManagedTenantId,
                envelope.Event.OrganizationId,
                envelope.Event.FolderId,
                envelope.Event.DisplayName,
                envelope.Event.Description,
                envelope.Event.PathLabel,
                envelope.Event.Tags.ToArray(),
                envelope.Event.LifecycleState,
                envelope.Event.RepositoryBindingState,
                envelope.Sequence);
        }

        return new FolderListProjection(folders.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public bool Contains(string managedTenantId, string folderId)
        => Folders.ContainsKey(Key(managedTenantId, folderId));

    public FolderListItem? Get(string managedTenantId, string folderId)
        => Folders.TryGetValue(Key(managedTenantId, folderId), out FolderListItem? item) ? item : null;

    private static string Key(string managedTenantId, string folderId)
        => $"{managedTenantId}:folders:{folderId}";
}
