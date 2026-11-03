using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Events;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Maintains only the disposable tenant through real public commands and ordinary Workers evidence.</summary>
internal sealed class FolderDurabilityTenantMaintenance(
    string tenantId,
    string principalId,
    Func<CancellationToken, Task<StreamReadPage>> readSource,
    Func<CancellationToken, Task<FolderTenantAccessProjection?>> readProjection,
    Func<SubmitCommandRequest, string, CancellationToken, Task<JsonElement>> submit,
    TimeProvider? timeProvider = null)
{
    /// <summary>The original metadata supplied by this fixture's CreateTenant command.</summary>
    internal const string TenantName = "Durability acceptance";
    /// <summary>The original description, with no authority or membership changes.</summary>
    internal const string TenantDescription = "Disposable local acceptance tenant";
    /// <summary>Starts foreground maintenance well before the unchanged five-minute production limit.</summary>
    internal static readonly TimeSpan RenewalAge = TimeSpan.FromMinutes(2);
    /// <summary>Bounds the entire source, command and projection barrier without extending existing I/O limits.</summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(90);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Verifies original evidence and renews only when its native timestamp reaches the trigger age.</summary>
    internal async Task<bool> EnsureFreshAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(Budget, _time);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        CancellationToken token = deadline.Token;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            StreamReadPage beforeSource = await readSource(token).ConfigureAwait(false);
            ValidateSource(beforeSource);
            StreamReadEvent prior = beforeSource.Events[^1];
            FolderTenantAccessProjection before = await WaitForProjectionAsync(prior, null, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (_time.GetUtcNow() - prior.Timestamp < RenewalAge) return false;

            string messageId = FolderDurabilityAcceptanceFixture.NewId();
            string correlationId = FolderDurabilityAcceptanceFixture.NewId();
            DateTimeOffset started = _time.GetUtcNow();
            JsonElement status = await submit(Command(tenantId, messageId), correlationId, token).ConfigureAwait(false);
            if (status.GetProperty("status").GetString() != "Completed"
                || status.GetProperty("messageId").GetString() != messageId
                || status.GetProperty("correlationId").GetString() != correlationId)
                throw new InvalidOperationException("Tenant maintenance has no matching Completed command.");

            StreamReadEvent updated;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                StreamReadPage source = await readSource(token).ConfigureAwait(false);
                ValidateSource(source);
                if (source.Events.Count < beforeSource.Events.Count
                    || JsonSerializer.Serialize(source.Events.Take(beforeSource.Events.Count), Json)
                        != JsonSerializer.Serialize(beforeSource.Events, Json))
                    throw new InvalidOperationException("Tenant maintenance changed its original retained source prefix.");
                if (source.Events.Count == beforeSource.Events.Count)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                updated = source.Events[^1];
                TenantUpdated? payload = JsonSerializer.Deserialize<TenantUpdated>(updated.Payload, Json);
                if (source.Events.Count != beforeSource.Events.Count + 1
                    || updated.EventTypeName.Split(',', 2)[0].Trim() != typeof(TenantUpdated).FullName
                    || updated.CausationId != messageId || updated.CorrelationId != correlationId || updated.UserId != "admin-user"
                    || updated.Timestamp < started || updated.Timestamp <= prior.Timestamp
                    || updated.SerializationFormat != "json" || updated.MetadataVersion != 1 || updated.PayloadVersion is not (null or 1)
                    || payload is null || payload.TenantId != tenantId || payload.Name != TenantName
                    || payload.Description != TenantDescription || payload.UpdatedAt < started || payload.UpdatedAt > _time.GetUtcNow())
                    throw new InvalidOperationException("Tenant maintenance has no matching original metadata-only source event.");
                break;
            }

            await WaitForProjectionAsync(updated, before, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (_time.GetUtcNow() - updated.Timestamp >= RenewalAge)
                throw new InvalidOperationException("Tenant maintenance evidence expired before its foreground barrier completed.");
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Uses the exact approved own-tenant command under the existing explicit platform scope.</summary>
    internal static SubmitCommandRequest Command(string tenantId, string messageId)
        => new(messageId, TenantPlatformScopes.Tenants, TenantIdentity.Domain, tenantId, nameof(UpdateTenant),
            JsonSerializer.SerializeToElement(new { tenantId, name = TenantName, description = TenantDescription }, Json));

    private void ValidateSource(StreamReadPage source)
    {
        if (source.Tenant != TenantIdentity.DefaultTenantId || source.Domain != TenantIdentity.Domain || source.AggregateId != tenantId
            || source.Metadata.FromSequence != 0 || source.Metadata.ToSequence is not null || source.Metadata.IsTruncated
            || source.Metadata.NextContinuationToken is not null || source.Events.Count == 0
            || source.Metadata.EventCount != source.Events.Count || source.Metadata.LatestSequence != source.Events.Count
            || source.Metadata.LastSequenceReturned != source.Metadata.LatestSequence
            || source.Events.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() != source.Events.Count)
            throw new InvalidOperationException("Tenant maintenance requires its complete authenticated retained source.");
        for (int index = 0; index < source.Events.Count; index++)
        {
            StreamReadEvent item = source.Events[index];
            if (item.SequenceNumber != index + 1 || string.IsNullOrWhiteSpace(item.MessageId)
                || item.Timestamp == default || item.Timestamp > _time.GetUtcNow())
                throw new InvalidOperationException("Tenant maintenance source metadata is malformed.");
        }
    }

    private async Task<FolderTenantAccessProjection> WaitForProjectionAsync(StreamReadEvent source,
        FolderTenantAccessProjection? before, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            FolderTenantAccessProjection? projection = await readProjection(token).ConfigureAwait(false);
            if (projection is not null)
            {
                if (projection.TenantId != tenantId || !projection.Enabled || projection.ReplayConflict || projection.MalformedEvidence
                    || projection.Version <= 0 || projection.Watermark <= 0 || projection.Watermark > source.SequenceNumber
                    || projection.LastEventTimestamp is null || projection.LastEventTimestamp > _time.GetUtcNow()
                    || projection.ProjectionWatermark != $"{tenantId}:{projection.Watermark}"
                    || !projection.Principals.TryGetValue(principalId, out FolderTenantPrincipalEvidence? principal)
                    || principal != new FolderTenantPrincipalEvidence(principalId, "TenantOwner")
                    || before is not null && !SameAuthority(before, projection))
                    throw new InvalidOperationException("Tenant maintenance observed invalid or changed tenant authority.");
                if (projection.Watermark == source.SequenceNumber)
                {
                    string kind = source.EventTypeName.Split(',', 2)[0].Split('.').Last();
                    if (projection.LastEventTimestamp != source.Timestamp
                        || !projection.ProcessedMessages.TryGetValue(source.MessageId, out FolderTenantEventEvidence? evidence)
                        || evidence.MessageId != source.MessageId || evidence.TenantId != tenantId
                        || evidence.SequenceNumber != source.SequenceNumber || evidence.Timestamp != source.Timestamp || evidence.EventTypeName != kind)
                        throw new InvalidOperationException("Tenant maintenance projection does not match original source evidence.");
                    if (before is not null)
                    {
                        FolderTenantAccessEvent expected = new FoldersTenantAccessEventMapper().Map(FolderTenantAccessEventKind.TenantUpdated,
                            tenantId, tenantId, source.MessageId, source.SequenceNumber, source.Timestamp, source.CorrelationId!, fingerprintParts: [TenantName]);
                        if (evidence.PayloadFingerprint != expected.PayloadFingerprint || projection.Version <= before.Version
                            || before.ProcessedMessages.Any(pair => !projection.ProcessedMessages.TryGetValue(pair.Key, out var value) || value != pair.Value))
                            throw new InvalidOperationException("Tenant maintenance lost original Workers delivery evidence.");
                    }
                    return projection.Clone();
                }
            }
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private static bool SameAuthority(FolderTenantAccessProjection before, FolderTenantAccessProjection after)
        => before.TenantId == after.TenantId && before.Enabled == after.Enabled
            && before.Principals.Count == after.Principals.Count
            && before.Principals.All(pair => after.Principals.TryGetValue(pair.Key, out var value) && value == pair.Value)
            && before.ConfigurationKeys.SetEquals(after.ConfigurationKeys)
            && before.RemovedConfigurationKeys.SetEquals(after.RemovedConfigurationKeys);
}
