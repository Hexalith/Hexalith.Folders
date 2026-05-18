using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.TenantAccess;

public sealed class FolderTenantAccessHandlerTests
{
    private static readonly DateTimeOffset EventTimestamp = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UserAddedToTenantShouldProjectMetadataOnlyAccessEvidence()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = new(store, new FixedUtcClock(EventTimestamp.AddMinutes(1)));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000001", 1),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.UserAddedToTenant, "tenant-a", "01J00000000000000000000002", 2, principalId: "user-a", role: "TenantOwner"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.TenantId.ShouldBe("tenant-a");
        projection.Enabled.ShouldBeTrue();
        projection.Watermark.ShouldBe(2);
        projection.LastEventTimestamp.ShouldBe(EventTimestamp);
        projection.Principals.Keys.ShouldContain("user-a");
        projection.Principals["user-a"].Role.ShouldBe("TenantOwner");
    }

    [Fact]
    public async Task DuplicateMessageWithDivergentMetadataShouldRecordReplayConflictWithoutAdvancingWatermark()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = new(store, new FixedUtcClock(EventTimestamp.AddMinutes(1)));
        string messageId = "01J00000000000000000000010";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", messageId, 1, payloadFingerprint: "created"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantDisabled, "tenant-a", messageId, 2, payloadFingerprint: "disabled"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.Watermark.ShouldBe(1);
        projection.ReplayConflict.ShouldBeTrue();
        projection.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task NonFoldersConfigurationShouldBeIgnoredAndRemovedFoldersConfigurationShouldBeTombstoned()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = new(store, new FixedUtcClock(EventTimestamp.AddMinutes(1)));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000020", 1),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationSet, "tenant-a", "01J00000000000000000000021", 2, configurationKey: "billing.plan"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationSet, "tenant-a", "01J00000000000000000000022", 3, configurationKey: "folders.create.enabled"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationRemoved, "tenant-a", "01J00000000000000000000023", 4, configurationKey: "folders.create.enabled"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.ConfigurationKeys.ShouldNotContain("billing.plan");
        projection.ConfigurationKeys.ShouldNotContain("folders.create.enabled");
        projection.RemovedConfigurationKeys.ShouldContain("folders.create.enabled");
    }

    [Fact]
    public async Task FutureTimestampShouldMarkProjectionMalformedAndFailClosed()
    {
        DateTimeOffset now = EventTimestamp;
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = new(store, new FixedUtcClock(now));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000030", 1, timestamp: now.AddMinutes(5)),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.MalformedEvidence.ShouldBeTrue();
        projection.Watermark.ShouldBe(0);
    }

    private static FolderTenantAccessEvent Event(
        FolderTenantAccessEventKind kind,
        string tenantId,
        string messageId,
        long sequenceNumber,
        string? principalId = null,
        string? role = null,
        string? configurationKey = null,
        string? payloadFingerprint = null,
        DateTimeOffset? timestamp = null)
        => new(
            kind,
            tenantId,
            messageId,
            sequenceNumber,
            timestamp ?? EventTimestamp,
            "correlation-a",
            principalId,
            role,
            ConfigurationKey: configurationKey,
            PayloadFingerprint: payloadFingerprint ?? tenantId);
}
