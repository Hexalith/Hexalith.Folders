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
    private const string Scope = "organization_baseline";
    private const string KeyPrefix = "projection:folders:organization-permissions:";

    public async Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request, CancellationToken cancellationToken = default)
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
            return EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(now, "organization_projection_missing"));
        }

        EffectivePermissionsFreshness freshness = new(request.ReadConsistency, now,
            projection.LastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Stale: false, ReasonCode: null);
        if (!string.Equals(projection.TenantId, request.ManagedTenantId, StringComparison.Ordinal)
            || !string.Equals(projection.OrganizationId, request.ManagedTenantId, StringComparison.Ordinal)
            || projection.LastSequence < 1
            || projection.Grants is null
            || projection.ProcessedMessages is null
            || projection.ProjectedAt > now)
        {
            return EffectivePermissionsReadModelResult.Malformed(freshness);
        }

        if (!projection.IsInitialized)
        {
            return EffectivePermissionsReadModelResult.NotFound(freshness);
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
        long sequence, IOrganizationAclEvent @event, CancellationToken cancellationToken = default)
        => await ApplyCoreAsync(tenantId, organizationId, messageId, sequence, @event,
            cancellationToken).ConfigureAwait(false);

    public async Task ApplyNonPermissionEventAsync(string tenantId, string organizationId,
        string messageId, long sequence, CancellationToken cancellationToken = default)
        => await ApplyCoreAsync(tenantId, organizationId, messageId, sequence, null,
            cancellationToken).ConfigureAwait(false);

    private async Task ApplyCoreAsync(string tenantId, string organizationId, string messageId,
        long sequence, IOrganizationAclEvent? @event, CancellationToken cancellationToken)
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
            && (!string.Equals(prior.TenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(prior.OrganizationId, organizationId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Organization ACL projection has conflicting identity.");
        }

        if (prior?.ProcessedMessages.ContainsKey(messageId) == true)
        {
            if (prior.ProcessedMessages[messageId] != sequence)
            {
                throw new InvalidOperationException("Organization ACL projection has conflicting replay.");
            }

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

        Dictionary<string, long> messages = prior is null
            ? new(StringComparer.Ordinal)
            : new(prior.ProcessedMessages, StringComparer.Ordinal);
        messages.Add(messageId, sequence);
        OrganizationPermissionProjection next = new(tenantId, organizationId, sequence, initialized,
            clock.UtcNow, grants, messages);
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
}

public sealed record OrganizationPermissionProjection(string TenantId, string OrganizationId,
    long LastSequence, bool IsInitialized, DateTimeOffset ProjectedAt, IReadOnlyList<OrganizationPermissionGrant> Grants,
    IReadOnlyDictionary<string, long> ProcessedMessages);

public sealed record OrganizationPermissionGrant(OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId, string Action);
