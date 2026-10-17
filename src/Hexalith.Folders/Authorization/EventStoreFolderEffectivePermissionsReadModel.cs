using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

/// <summary>Projects folder actor events and combines current folder ACL with current organization grants.</summary>
public sealed class EventStoreFolderEffectivePermissionsReadModel(
    IReadModelStore store,
    IUtcClock clock,
    IOrganizationStreamSequenceReader sequenceReader,
    EventStoreOrganizationEffectivePermissionsReadModel organizationReadModel)
    : IEffectivePermissionsReadModel, IFolderAclReadModel
{
    /// <summary>The SDK state-store component shared by permission readers and maintenance.</summary>
    internal const string StoreName = "statestore";
    private const string KeyPrefix = "projection:folders:folder-permissions:";
    private const int ReplayWindow = 256;
    private static readonly TimeSpan ProjectedClockSkew = TimeSpan.FromMinutes(2);

    public Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PermissionProjectionCatchUp.ReadAsync(
            token => GetOnceAsync(request with { WaitForCatchUp = false }, token),
            request.WaitForCatchUp, clock.UtcNow, cancellationToken);
    }

    private async Task<EffectivePermissionsReadModelResult> GetOnceAsync(
        EffectivePermissionsReadModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FolderId == FolderStreamName.ReservedOrganizationPermissionScope)
        {
            return await organizationReadModel.GetAsync(request, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = clock.UtcNow;
        FolderPermissionProjection? folder;
        try
        {
            folder = (await store.GetAsync<FolderPermissionProjection>(StoreName,
                Key(request.ManagedTenantId, request.FolderId), cancellationToken).ConfigureAwait(false)).Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EffectivePermissionsReadModelResult.Unavailable("folder_projection_unavailable", now);
        }

        if (folder is null)
        {
            long missingHead;
            try
            {
                missingHead = await sequenceReader.GetCurrentSequenceAsync(request.ManagedTenantId,
                    request.FolderId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return EffectivePermissionsReadModelResult.Unavailable("folder_stream_unavailable", now);
            }

            if (missingHead > 0)
            {
                EffectivePermissionsFreshness lag = new(request.ReadConsistency, now, "0", true,
                    "folder_projection_lagging");
                return EffectivePermissionsReadModelResult.Stale(new EffectivePermissionsReadModelSnapshot(
                    request.ManagedTenantId, request.ManagedTenantId, request.FolderId,
                    EffectivePermissionsFolderLifecycleState.Stale, [], lag,
                    RevocationFreshnessEstablished: false, TaskScope: null));
            }

            return EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(now, "folder_projection_missing"));
        }

        EffectivePermissionsFreshness freshness = new(request.ReadConsistency, now,
            folder.LastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), false, null);
        if (!Valid(folder, request.ManagedTenantId, request.FolderId, now))
        {
            return EffectivePermissionsReadModelResult.Malformed(freshness);
        }

        long folderHead;
        try
        {
            folderHead = await sequenceReader.GetCurrentSequenceAsync(request.ManagedTenantId,
                request.FolderId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EffectivePermissionsReadModelResult.Unavailable("folder_stream_unavailable", now);
        }

        if (folderHead < folder.LastSequence)
        {
            return EffectivePermissionsReadModelResult.Malformed(freshness);
        }

        EffectivePermissionsReadModelResult baseline = await organizationReadModel.GetAsync(
            request with { FolderId = FolderStreamName.ReservedOrganizationPermissionScope }, cancellationToken).ConfigureAwait(false);
        if (baseline.Status is not (EffectivePermissionsReadModelStatus.Available
            or EffectivePermissionsReadModelStatus.Stale or EffectivePermissionsReadModelStatus.NotFound)
            || baseline.Status != EffectivePermissionsReadModelStatus.NotFound && baseline.Snapshot is null)
        {
            return baseline.Status switch
            {
                EffectivePermissionsReadModelStatus.Malformed => EffectivePermissionsReadModelResult.Malformed(freshness),
                _ => EffectivePermissionsReadModelResult.Unavailable("organization_projection_unavailable", now),
            };
        }

        List<EffectivePermissionEvidenceRow> rows = [.. (baseline.Snapshot?.EvidenceRows ?? []).Select(
            row => row with { Sequence = 0 })];
        foreach (FolderPermissionOverrideEntry item in folder.Overrides)
        {
            rows.Add(new EffectivePermissionEvidenceRow(item.IsGranted
                    ? EffectivePermissionEvidenceSource.FolderOverrideGrant
                    : EffectivePermissionEvidenceSource.FolderOverrideRevoke,
                ToPrincipal(item.PrincipalKind, item.PrincipalId), item.Action,
                folder.LastSequence, item.OccurredAt));
        }

        bool folderStale = folderHead != folder.LastSequence;
        bool baselineStale = baseline.Status == EffectivePermissionsReadModelStatus.Stale;
        freshness = freshness with
        {
            Stale = folderStale || baselineStale,
            ReasonCode = folderStale ? "folder_projection_lagging"
                : baselineStale ? baseline.Freshness.ReasonCode ?? "organization_projection_lagging" : null,
        };
        EffectivePermissionsReadModelSnapshot snapshot = new(request.ManagedTenantId,
            folder.OrganizationId, folder.FolderId,
            !folder.IsCreated ? EffectivePermissionsFolderLifecycleState.Missing
                : folder.IsArchived ? EffectivePermissionsFolderLifecycleState.Archived
                : EffectivePermissionsFolderLifecycleState.Active,
            rows, freshness,
            RevocationFreshnessEstablished: baseline.Snapshot?.RevocationFreshnessEstablished
                ?? baseline.Status == EffectivePermissionsReadModelStatus.NotFound,
            TaskScope: null);
        return freshness.Stale
            ? EffectivePermissionsReadModelResult.Stale(snapshot)
            : EffectivePermissionsReadModelResult.Available(snapshot);
    }

    public async Task<FolderPermissionProjection?> GetCurrentAsync(string tenantId, string folderId,
        CancellationToken cancellationToken = default)
    {
        ReadModelEntry<FolderPermissionProjection> entry = await store.GetAsync<FolderPermissionProjection>(
            StoreName, Key(tenantId, folderId), cancellationToken).ConfigureAwait(false);
        FolderPermissionProjection? folder = entry.Value;
        if (folder is null)
        {
            if (await sequenceReader.GetCurrentSequenceAsync(tenantId, folderId,
                cancellationToken).ConfigureAwait(false) > 0)
            {
                throw new InvalidOperationException("Folder ACL projection is not current.");
            }

            return null;
        }

        if (!Valid(folder, tenantId, folderId, clock.UtcNow)
            || await sequenceReader.GetCurrentSequenceAsync(tenantId, folderId,
                cancellationToken).ConfigureAwait(false) != folder.LastSequence)
        {
            throw new InvalidOperationException("Folder ACL projection is not current.");
        }

        return folder;
    }

    public async Task ApplyAsync(string tenantId, string folderId, string messageId,
        long sequence, string payloadDigest, IFolderEvent? @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadDigest);
        if (tenantId == folderId || sequence < 1 || @event is not null
            && (@event.ManagedTenantId != tenantId || @event.OrganizationId != tenantId
                || @event.FolderId != folderId))
        {
            throw new InvalidOperationException("Folder projection identity is invalid.");
        }

        string key = Key(tenantId, folderId);
        ReadModelEntry<FolderPermissionProjection> current = await store.GetAsync<FolderPermissionProjection>(
            StoreName, key, cancellationToken).ConfigureAwait(false);
        FolderPermissionProjection? prior = current.Value;
        if (prior is not null && !Valid(prior, tenantId, folderId, clock.UtcNow))
        {
            throw new InvalidOperationException("Folder projection state is invalid.");
        }

        FolderProjectionMessage? replay = prior?.RecentMessages.FirstOrDefault(item => item.Sequence == sequence);
        if (replay is not null)
        {
            if (replay.MessageId != messageId || replay.PayloadDigest != payloadDigest)
            {
                throw new InvalidOperationException("Conflicting folder event redelivery.");
            }

            return;
        }

        if (prior is not null && sequence <= prior.LastSequence - ReplayWindow)
        {
            return;
        }

        if (prior?.RecentMessages.Any(item => item.MessageId == messageId) == true)
        {
            throw new InvalidOperationException("Folder projection has conflicting message identity.");
        }

        if (sequence != (prior?.LastSequence ?? 0) + 1)
        {
            throw new PermissionProjectionConcurrencyException("Folder projection sequence is not contiguous.");
        }

        bool created = prior?.IsCreated ?? false;
        bool archived = prior?.IsArchived ?? false;
        List<FolderPermissionOverrideEntry> overrides = [.. prior?.Overrides ?? []];
        switch (@event)
        {
            case FolderCreated when !created:
                created = true;
                break;
            case FolderCreated:
                throw new InvalidOperationException("Folder projection contains a duplicate create.");
            case FolderArchived when created && !archived:
                archived = true;
                break;
            case FolderArchived:
                throw new InvalidOperationException("Folder projection contains a duplicate archive.");
            case FolderAccessGranted granted when created:
                SetOverride(granted.PrincipalKind, granted.PrincipalId, granted.Action, true,
                    granted.AccessSequence, granted.OccurredAt);
                break;
            case FolderAccessRevoked revoked when created:
                SetOverride(revoked.PrincipalKind, revoked.PrincipalId, revoked.Action, false,
                    revoked.AccessSequence, revoked.OccurredAt);
                break;
            case null:
                break;
            case IFolderEvent when created:
                break;
            default:
                throw new InvalidOperationException("Folder projection event order is invalid.");
        }

        List<FolderProjectionMessage> recent = [.. prior?.RecentMessages ?? []];
        recent.Add(new FolderProjectionMessage(sequence, messageId, payloadDigest));
        if (recent.Count > ReplayWindow)
        {
            recent.RemoveRange(0, recent.Count - ReplayWindow);
        }

        FolderPermissionProjection next = new(tenantId, tenantId, folderId, sequence, created, archived,
            clock.UtcNow, overrides, recent);
        if (!await store.TrySaveAsync(StoreName, key, next, current.ETag ?? string.Empty,
            cancellationToken).ConfigureAwait(false))
        {
            throw new PermissionProjectionConcurrencyException("Folder projection write conflicted.");
        }

        void SetOverride(FolderAccessPrincipalKind kind, string principalId, string action, bool grant,
            long accessSequence, DateTimeOffset occurredAt)
        {
            FolderPermissionOverrideEntry? existing = overrides.FirstOrDefault(item =>
                item.PrincipalKind == kind && item.PrincipalId == principalId && item.Action == action);
            if (existing is not null && accessSequence < existing.AccessSequence)
            {
                return;
            }

            overrides.RemoveAll(item => item.PrincipalKind == kind && item.PrincipalId == principalId
                && item.Action == action);
            overrides.Add(new FolderPermissionOverrideEntry(kind, principalId, action, grant,
                accessSequence, occurredAt));
        }
    }

    private static bool Valid(FolderPermissionProjection folder, string tenantId, string folderId,
        DateTimeOffset now)
        => folder.TenantId == tenantId && folder.OrganizationId == tenantId
            && folder.FolderId == folderId && folder.LastSequence > 0
            && folder.ProjectedAt <= now + ProjectedClockSkew && folder.Overrides is not null
            && folder.RecentMessages is not null;

    /// <summary>Builds the derived folder permission row key shared with conditional maintenance.</summary>
    internal static string Key(string tenantId, string folderId) => KeyPrefix + tenantId + ":" + folderId;

    private static EffectivePermissionPrincipal ToPrincipal(FolderAccessPrincipalKind kind, string id)
        => kind switch
        {
            FolderAccessPrincipalKind.User => EffectivePermissionPrincipal.User(id),
            FolderAccessPrincipalKind.Group => EffectivePermissionPrincipal.Group(id),
            FolderAccessPrincipalKind.Role => EffectivePermissionPrincipal.Role(id),
            FolderAccessPrincipalKind.DelegatedServiceAgent => EffectivePermissionPrincipal.DelegatedServiceAgent(id),
            _ => throw new InvalidOperationException("Unknown folder ACL principal kind."),
        };
}
