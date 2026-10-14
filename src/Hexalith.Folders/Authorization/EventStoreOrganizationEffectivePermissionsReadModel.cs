using Hexalith.Folders.Aggregates.Folder;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

/// <summary>Event-derived organization baseline shared by Server and Workers.</summary>
public sealed class EventStoreOrganizationEffectivePermissionsReadModel(
    IReadModelStore store, IUtcClock clock, IOrganizationStreamSequenceReader sequenceReader)
    : IEffectivePermissionsReadModel
{
    private const string StoreName = "statestore";
    private const string Scope = FolderStreamName.ReservedOrganizationPermissionScope;
    private const string KeyPrefix = "projection:folders:organization-permissions:";
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
        DateTimeOffset now = clock.UtcNow;
        if (request.FolderId != Scope)
        {
            return EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(now, "folder_projection_missing"));
        }

        ReadModelEntry<OrganizationPermissionProjection> entry;
        try
        {
            entry = await store.GetAsync<OrganizationPermissionProjection>(StoreName,
                KeyPrefix + request.ManagedTenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EffectivePermissionsReadModelResult.Unavailable("organization_projection_unavailable", now);
        }

        OrganizationPermissionProjection? projection = entry.Value;
        if (projection is null)
        {
            long emptyHead;
            try
            {
                emptyHead = await sequenceReader.GetCurrentSequenceAsync(request.ManagedTenantId,
                    request.ManagedTenantId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return EffectivePermissionsReadModelResult.Unavailable("organization_stream_unavailable", now);
            }

            if (emptyHead > 0)
            {
                EffectivePermissionsFreshness missingFreshness = new(request.ReadConsistency, now,
                    "0", Stale: true, ReasonCode: "organization_projection_lagging");
                return EffectivePermissionsReadModelResult.Stale(new EffectivePermissionsReadModelSnapshot(
                    request.ManagedTenantId, request.ManagedTenantId, Scope,
                    EffectivePermissionsFolderLifecycleState.Stale, [], missingFreshness,
                    RevocationFreshnessEstablished: false, TaskScope: null));
            }

            return EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(now, "organization_projection_missing"));
        }

        EffectivePermissionsFreshness freshness = new(request.ReadConsistency, now,
            projection.LastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Stale: false, ReasonCode: null);
        if (!HasValidState(projection, request.ManagedTenantId, request.ManagedTenantId, now))
        {
            return EffectivePermissionsReadModelResult.Malformed(freshness);
        }

        long authoritativeSequence;
        try
        {
            authoritativeSequence = await sequenceReader.GetCurrentSequenceAsync(
                request.ManagedTenantId, projection.OrganizationId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EffectivePermissionsReadModelResult.Unavailable("organization_stream_unavailable", now);
        }

        if (authoritativeSequence < projection.LastSequence)
        {
            return EffectivePermissionsReadModelResult.Malformed(freshness);
        }

        freshness = freshness with { Stale = authoritativeSequence != projection.LastSequence };
        if (!projection.IsInitialized)
        {
            return freshness.Stale
                ? EffectivePermissionsReadModelResult.Stale(new EffectivePermissionsReadModelSnapshot(
                    request.ManagedTenantId, projection.OrganizationId, Scope,
                    EffectivePermissionsFolderLifecycleState.Stale, [], freshness,
                    RevocationFreshnessEstablished: false, TaskScope: null))
                : EffectivePermissionsReadModelResult.NotFound(freshness);
        }

        EffectivePermissionEvidenceRow[] rows = projection.Grants.Select(grant =>
            new EffectivePermissionEvidenceRow(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                ToPrincipal(grant.PrincipalKind, grant.PrincipalId), grant.Action,
                projection.LastSequence, projection.ProjectedAt)).ToArray();
        EffectivePermissionsReadModelSnapshot snapshot = new(request.ManagedTenantId,
            projection.OrganizationId, Scope, EffectivePermissionsFolderLifecycleState.Active, rows,
            freshness, RevocationFreshnessEstablished: true, TaskScope: null);
        return authoritativeSequence == projection.LastSequence
            ? EffectivePermissionsReadModelResult.Available(snapshot)
            : EffectivePermissionsReadModelResult.Stale(snapshot);
    }

    public async Task ApplyAsync(string tenantId, string organizationId, string messageId,
        long sequence, IOrganizationAclEvent @event, CancellationToken cancellationToken = default,
        string? payloadDigest = null)
        => await ApplyCoreAsync(tenantId, organizationId, messageId, sequence, @event,
            cancellationToken, payloadDigest).ConfigureAwait(false);

    public async Task ApplyNonPermissionEventAsync(string tenantId, string organizationId,
        string messageId, long sequence, CancellationToken cancellationToken = default,
        string? payloadDigest = null)
        => await ApplyCoreAsync(tenantId, organizationId, messageId, sequence, null,
            cancellationToken, payloadDigest).ConfigureAwait(false);

    private async Task ApplyCoreAsync(string tenantId, string organizationId, string messageId,
        long sequence, IOrganizationAclEvent? @event, CancellationToken cancellationToken,
        string? payloadDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (!string.Equals(tenantId, organizationId, StringComparison.Ordinal)
            || @event is not null
                && (!string.Equals(@event.ManagedTenantId, tenantId, StringComparison.Ordinal)
                    || !string.Equals(@event.OrganizationId, organizationId, StringComparison.Ordinal))
            || sequence < 1)
        {
            throw new InvalidOperationException("Organization ACL projection identity is invalid.");
        }

        string key = KeyPrefix + tenantId;
        ReadModelEntry<OrganizationPermissionProjection> current = await store
            .GetAsync<OrganizationPermissionProjection>(StoreName, key, cancellationToken).ConfigureAwait(false);
        OrganizationPermissionProjection? prior = current.Value;
        if (prior is not null
            && !HasValidState(prior, tenantId, organizationId, clock.UtcNow))
        {
            throw new InvalidOperationException("Organization ACL projection state is invalid.");
        }

        string digest = payloadDigest ?? (@event is null ? "non-permission" :
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType()))));
        FolderProjectionMessage? replay = prior?.RecentMessages?.FirstOrDefault(item => item.Sequence == sequence);
        if (replay is not null)
        {
            if (replay.MessageId != messageId || replay.PayloadDigest != digest)
            {
                throw new InvalidOperationException("Organization ACL projection has conflicting replay.");
            }

            return;
        }

        if (prior is not null && sequence <= prior.LastSequence - ReplayWindow)
        {
            return;
        }

        if (sequence != (prior?.LastSequence ?? 0) + 1)
        {
            throw new InvalidOperationException("Organization ACL projection sequence is not contiguous.");
        }

        List<OrganizationPermissionGrant> grants = [.. prior?.Grants ?? []];
        bool initialized = prior?.IsInitialized ?? false;
        switch (@event)
        {
            case null:
                break;
            case OrganizationAclBaselineInitialized when !initialized:
                initialized = true;
                break;
            case OrganizationAclPrincipalGranted granted when initialized:
                grants.RemoveAll(item => item.PrincipalKind == granted.PrincipalKind
                    && item.PrincipalId == granted.PrincipalId && item.Action == granted.Action);
                grants.Add(new OrganizationPermissionGrant(granted.PrincipalKind, granted.PrincipalId, granted.Action));
                break;
            case OrganizationAclPrincipalRevoked revoked when initialized:
                grants.RemoveAll(item => item.PrincipalKind == revoked.PrincipalKind
                    && item.PrincipalId == revoked.PrincipalId && item.Action == revoked.Action);
                break;
            default:
                throw new InvalidOperationException("Organization ACL projection event order is invalid.");
        }

        if (prior?.ProcessedMessages.ContainsKey(messageId) == true)
        {
            throw new InvalidOperationException("Organization ACL projection has conflicting message identity.");
        }

        Dictionary<string, long> messages = prior is null ? new(StringComparer.Ordinal)
            : new(prior.ProcessedMessages, StringComparer.Ordinal);
        messages.Add(messageId, sequence);
        foreach (string old in messages.Where(pair => pair.Value <= sequence - ReplayWindow)
            .Select(static pair => pair.Key).ToArray())
        {
            messages.Remove(old);
        }

        List<FolderProjectionMessage> recent = [.. prior?.RecentMessages ?? []];
        recent.Add(new FolderProjectionMessage(sequence, messageId, digest));
        if (recent.Count > ReplayWindow)
        {
            recent.RemoveRange(0, recent.Count - ReplayWindow);
        }

        OrganizationPermissionProjection next = new(tenantId, organizationId, sequence, initialized,
            clock.UtcNow, grants, messages, recent);
        if (!await store.TrySaveAsync(StoreName, key, next, current.ETag ?? string.Empty,
            cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Organization ACL projection write conflicted.");
        }
    }

    private static EffectivePermissionPrincipal ToPrincipal(OrganizationAclPrincipalKind kind, string principalId)
        => kind switch
        {
            OrganizationAclPrincipalKind.User => EffectivePermissionPrincipal.User(principalId),
            OrganizationAclPrincipalKind.Group => EffectivePermissionPrincipal.Group(principalId),
            OrganizationAclPrincipalKind.Role => EffectivePermissionPrincipal.Role(principalId),
            OrganizationAclPrincipalKind.DelegatedServiceAgent => EffectivePermissionPrincipal.DelegatedServiceAgent(principalId),
            _ => throw new InvalidOperationException("Unknown ACL principal kind."),
        };

    private static bool HasValidState(OrganizationPermissionProjection projection, string tenantId,
        string organizationId, DateTimeOffset now)
        => string.Equals(projection.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(projection.OrganizationId, organizationId, StringComparison.Ordinal)
            && projection.LastSequence >= 1
            && projection.Grants is not null
            && projection.ProcessedMessages is not null
            && projection.ProjectedAt <= now + ProjectedClockSkew
            && (projection.IsInitialized || projection.Grants.Count == 0);
}
