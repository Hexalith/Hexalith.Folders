namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FolderTenantAccessHandler(
    IFolderTenantAccessProjectionStore store,
    IUtcClock clock)
{
    public async Task HandleAsync(FolderTenantAccessEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (string.IsNullOrWhiteSpace(@event.TenantId))
        {
            return;
        }

        FolderTenantAccessProjection projection = await store.GetAsync(@event.TenantId, cancellationToken).ConfigureAwait(false)
            ?? new FolderTenantAccessProjection { TenantId = @event.TenantId };

        if (IsMalformed(@event))
        {
            projection.MalformedEvidence = true;
            await store.SaveAsync(projection, cancellationToken).ConfigureAwait(false);
            return;
        }

        FolderTenantEventEvidence evidence = CreateEvidence(@event);
        if (projection.ProcessedMessages.TryGetValue(@event.MessageId, out FolderTenantEventEvidence? existing))
        {
            if (existing != evidence)
            {
                projection.ReplayConflict = true;
                await store.SaveAsync(projection, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (@event.SequenceNumber <= projection.Watermark)
        {
            projection.MalformedEvidence = true;
            await store.SaveAsync(projection, cancellationToken).ConfigureAwait(false);
            return;
        }

        Apply(projection, @event);
        projection.ProcessedMessages[@event.MessageId] = evidence;
        projection.Watermark = @event.SequenceNumber;
        projection.LastEventTimestamp = @event.Timestamp;
        projection.ProjectionWatermark = $"{@event.TenantId}:{@event.SequenceNumber}";

        await store.SaveAsync(projection, cancellationToken).ConfigureAwait(false);
    }

    private static void Apply(FolderTenantAccessProjection projection, FolderTenantAccessEvent @event)
    {
        switch (@event.Kind)
        {
            case FolderTenantAccessEventKind.TenantCreated:
            case FolderTenantAccessEventKind.TenantEnabled:
                projection.Enabled = true;
                break;
            case FolderTenantAccessEventKind.TenantDisabled:
                projection.Enabled = false;
                break;
            case FolderTenantAccessEventKind.UserAddedToTenant:
            case FolderTenantAccessEventKind.UserRoleChanged:
                if (!string.IsNullOrWhiteSpace(@event.PrincipalId) && !string.IsNullOrWhiteSpace(@event.Role))
                {
                    projection.Principals[@event.PrincipalId] = new FolderTenantPrincipalEvidence(@event.PrincipalId, @event.Role);
                }

                break;
            case FolderTenantAccessEventKind.UserRemovedFromTenant:
                if (!string.IsNullOrWhiteSpace(@event.PrincipalId))
                {
                    _ = projection.Principals.Remove(@event.PrincipalId);
                }

                break;
            case FolderTenantAccessEventKind.TenantConfigurationSet:
                AddConfigurationKey(projection, @event.ConfigurationKey);
                break;
            case FolderTenantAccessEventKind.TenantConfigurationRemoved:
                RemoveConfigurationKey(projection, @event.ConfigurationKey);
                break;
            case FolderTenantAccessEventKind.TenantUpdated:
            default:
                break;
        }
    }

    private static void AddConfigurationKey(FolderTenantAccessProjection projection, string? key)
    {
        if (key is null || !IsFoldersConfigurationKey(key))
        {
            return;
        }

        _ = projection.ConfigurationKeys.Add(key);
        _ = projection.RemovedConfigurationKeys.Remove(key);
    }

    private static void RemoveConfigurationKey(FolderTenantAccessProjection projection, string? key)
    {
        if (key is null || !IsFoldersConfigurationKey(key))
        {
            return;
        }

        _ = projection.ConfigurationKeys.Remove(key);
        _ = projection.RemovedConfigurationKeys.Add(key);
    }

    private bool IsMalformed(FolderTenantAccessEvent @event)
        => string.IsNullOrWhiteSpace(@event.MessageId)
            || @event.SequenceNumber <= 0
            || @event.Timestamp > clock.UtcNow
            || ((@event.Kind is FolderTenantAccessEventKind.UserAddedToTenant
                or FolderTenantAccessEventKind.UserRemovedFromTenant
                or FolderTenantAccessEventKind.UserRoleChanged)
                && string.IsNullOrWhiteSpace(@event.PrincipalId));

    private static bool IsFoldersConfigurationKey(string key)
        => key.StartsWith("folders.", StringComparison.Ordinal);

    private static FolderTenantEventEvidence CreateEvidence(FolderTenantAccessEvent @event)
        => new(
            @event.MessageId,
            @event.TenantId,
            @event.Kind.ToString(),
            @event.SequenceNumber,
            @event.Timestamp,
            @event.CorrelationId,
            @event.PayloadFingerprint);
}
