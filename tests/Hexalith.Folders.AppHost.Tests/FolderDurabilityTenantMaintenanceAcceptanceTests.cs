using System.Text.Json;

using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Qualifies genuine fixture maintenance separately from the unchanged full durability acceptance body.</summary>
[Collection("Folder durability acceptance")]
public sealed class FolderDurabilityTenantMaintenanceAcceptanceTests(FolderDurabilityAcceptanceFixture fixture, ITestOutputHelper output)
    : IClassFixture<FolderDurabilityAcceptanceFixture>
{
    /// <summary>Waits for natural native-source age, verifies one real update and proves an immediate fresh no-op.</summary>
    [Fact]
    public async Task NaturallyAgedTenantShouldMaintainRealSourceAndWorkersAuthorityAndSkipFreshUpdate()
    {
        if (!fixture.Enabled)
        {
            Assert.Skip($"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true to execute real tenant maintenance acceptance.");
            return;
        }

        CancellationToken caller = TestContext.Current.CancellationToken;
        // This uses the same real authenticated source/projection barrier as the strict fixture.
        // Slow bootstrap may already have crossed the trigger; any initial renewal remains genuine.
        await fixture.MaintainTenantAsync(caller).ConfigureAwait(true);
        FolderTenantAccessProjection before = await SnapshotAsync(caller).ConfigureAwait(true);
        DateTimeOffset originalTimestamp = before.LastEventTimestamp
            ?? throw new InvalidOperationException("Verified tenant evidence has no original native timestamp.");

        output.WriteLine("Tenant maintenance control: waiting for original native source age to reach the existing trigger.");
        while (DateTimeOffset.UtcNow - originalTimestamp < FolderDurabilityTenantMaintenance.RenewalAge)
        {
            TimeSpan remaining = originalTimestamp + FolderDurabilityTenantMaintenance.RenewalAge - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, caller).ConfigureAwait(true);
        }
        (DateTimeOffset.UtcNow - originalTimestamp >= FolderDurabilityTenantMaintenance.RenewalAge)
            .ShouldBeTrue("Maintenance must follow natural source age, without a forced renewal or changed clock.");

        // True is returned only after Completed, the matching original TenantUpdated and unchanged
        // retained prefix, and exact normal Workers message/sequence/native-timestamp verification.
        (await fixture.MaintainTenantAsync(caller).ConfigureAwait(true)).ShouldBeTrue();
        FolderTenantAccessProjection after = await SnapshotAsync(caller).ConfigureAwait(true);
        after.Watermark.ShouldBe(checked(before.Watermark + 1));
        after.ProcessedMessages.Count.ShouldBe(before.ProcessedMessages.Count + 1);
        (after.Version > before.Version).ShouldBeTrue();
        (after.LastEventTimestamp > originalTimestamp).ShouldBeTrue();
        SameAuthority(before, after).ShouldBeTrue("Metadata maintenance must preserve all persisted authority.");
        before.ProcessedMessages.All(pair => after.ProcessedMessages.TryGetValue(pair.Key, out var evidence) && evidence == pair.Value)
            .ShouldBeTrue("Maintenance must preserve every original persisted delivery record.");
        FolderTenantEventEvidence[] newMessages = after.ProcessedMessages
            .Where(pair => !before.ProcessedMessages.ContainsKey(pair.Key)).Select(pair => pair.Value).ToArray();
        newMessages.Length.ShouldBe(1);
        FolderTenantEventEvidence added = newMessages[0];
        added.EventTypeName.ShouldBe(nameof(FolderTenantAccessEventKind.TenantUpdated));
        added.SequenceNumber.ShouldBe(after.Watermark);
        (added.Timestamp == after.LastEventTimestamp).ShouldBeTrue();
        string.Equals(added.TenantId, fixture.TenantId, StringComparison.Ordinal).ShouldBeTrue();
        string.IsNullOrWhiteSpace(added.MessageId).ShouldBeFalse();
        FolderTenantAccessEvent expected = new FoldersTenantAccessEventMapper().Map(FolderTenantAccessEventKind.TenantUpdated,
            fixture.TenantId, fixture.TenantId, added.MessageId, added.SequenceNumber, added.Timestamp, string.Empty,
            fingerprintParts: [FolderDurabilityTenantMaintenance.TenantName]);
        string.Equals(added.PayloadFingerprint, expected.PayloadFingerprint, StringComparison.Ordinal).ShouldBeTrue();

        (await fixture.MaintainTenantAsync(caller).ConfigureAwait(true)).ShouldBeFalse();
        FolderTenantAccessProjection fresh = await SnapshotAsync(caller).ConfigureAwait(true);
        string.Equals(JsonSerializer.Serialize(fresh), JsonSerializer.Serialize(after), StringComparison.Ordinal)
            .ShouldBeTrue("An immediate fresh no-op must leave the exact persisted evidence unchanged.");
        output.WriteLine("PASS real tenant maintenance acceptance: natural age; original TenantUpdated and retained prefix verified; persisted Workers authority unchanged; immediate fresh no-op verified.");
    }

    private async Task<FolderTenantAccessProjection> SnapshotAsync(CancellationToken caller)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(FolderDurabilityTenantMaintenance.Budget);
        FolderTenantAccessProjection? projection = await new EventStoreFolderTenantAccessProjectionStore(fixture.RawStore)
            .GetAsync(fixture.TenantId, deadline.Token).ConfigureAwait(false);
        deadline.Token.ThrowIfCancellationRequested();
        if (projection is null || !string.Equals(projection.TenantId, fixture.TenantId, StringComparison.Ordinal)
            || !projection.Enabled || projection.ReplayConflict || projection.MalformedEvidence
            || !projection.Principals.TryGetValue(fixture.PrincipalId, out var principal)
            || principal != new FolderTenantPrincipalEvidence(fixture.PrincipalId, "TenantOwner"))
            throw new InvalidOperationException("Verified tenant maintenance authority is absent, malformed or changed.");
        return projection.Clone();
    }

    private static bool SameAuthority(FolderTenantAccessProjection before, FolderTenantAccessProjection after)
        => before.Enabled == after.Enabled && before.Principals.Count == after.Principals.Count
            && before.Principals.All(pair => after.Principals.TryGetValue(pair.Key, out var value) && value == pair.Value)
            && before.ConfigurationKeys.SetEquals(after.ConfigurationKeys)
            && before.RemovedConfigurationKeys.SetEquals(after.RemovedConfigurationKeys);
}
