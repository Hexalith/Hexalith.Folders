using Hexalith.Folders.Authorization;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FolderTenantAccessHandler(
    IFolderTenantAccessProjectionStore store,
    IUtcClock clock,
    TenantAccessOptions options,
    ILogger<FolderTenantAccessHandler>? logger = null)
{
    public async Task HandleAsync(FolderTenantAccessEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (string.IsNullOrWhiteSpace(@event.TenantId))
        {
            return;
        }

        int attempts = Math.Max(1, options.ConcurrencyRetryAttempts);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await ApplyOnceAsync(@event, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (TenantAccessConcurrencyException) when (attempt + 1 < attempts)
            {
                logger?.LogDebug(
                    "Optimistic concurrency conflict applying tenant event {EventKind} for tenant {TenantId}; retry {Attempt} of {Attempts}.",
                    @event.Kind, @event.TenantId, attempt + 2, attempts);
            }
            catch (TenantAccessTransientPersistenceException) when (attempt + 1 < attempts)
            {
                logger?.LogDebug(
                    "Transient persistence failure applying tenant event {EventKind} for tenant {TenantId}; retry {Attempt} of {Attempts}.",
                    @event.Kind, @event.TenantId, attempt + 2, attempts);
            }
            catch (TimeoutException) when (attempt + 1 < attempts)
            {
                logger?.LogDebug(
                    "Timeout applying tenant event {EventKind} for tenant {TenantId}; retry {Attempt} of {Attempts}.",
                    @event.Kind, @event.TenantId, attempt + 2, attempts);
            }
        }
    }

    private async Task ApplyOnceAsync(FolderTenantAccessEvent @event, CancellationToken cancellationToken)
    {
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
            // Legitimate out-of-order at-least-once delivery: a newer-sequence event already advanced the
            // watermark. Dropping silently (rather than flagging MalformedEvidence) keeps the tenant healthy
            // through normal pub/sub redelivery patterns.
            logger?.LogDebug(
                "Dropping out-of-order tenant event {EventKind} for tenant {TenantId}: sequence {Sequence} <= watermark {Watermark}.",
                @event.Kind, @event.TenantId, @event.SequenceNumber, projection.Watermark);
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
            || @event.Timestamp - clock.UtcNow > options.ClockSkewTolerance
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
            @event.PayloadFingerprint);
}
