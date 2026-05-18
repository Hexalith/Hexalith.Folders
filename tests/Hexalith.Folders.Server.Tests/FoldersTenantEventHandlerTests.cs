using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Contracts.Events;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FoldersTenantEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TenantEnvelopeMismatchShouldNotMutateProjection()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler projectionHandler = new(store, new FixedUtcClock(Now), new TenantAccessOptions());
        FoldersTenantEventHandler subject = new(projectionHandler);

        // Envelope says tenant-a, payload says tenant-b: the handler must drop the event without granting access.
        TenantEventContext context = new(
            TenantId: "tenant-a",
            MessageId: "01J00000000000000000000099",
            SequenceNumber: 1,
            Timestamp: Now,
            CorrelationId: "corr-99");

        await subject.HandleAsync(new TenantCreated("tenant-b", "Tenant B", null, Now), context, TestContext.Current.CancellationToken);

        FolderTenantAccessProjection? envelopeTenantProjection = await store.GetAsync("tenant-a", TestContext.Current.CancellationToken);
        FolderTenantAccessProjection? payloadTenantProjection = await store.GetAsync("tenant-b", TestContext.Current.CancellationToken);
        envelopeTenantProjection.ShouldBeNull();
        payloadTenantProjection.ShouldBeNull();
    }

    [Fact]
    public async Task TenantUpdatedShouldAdvanceWatermarkWithoutGrantingMembership()
    {
        // Sanity: TenantUpdated is a no-op for membership but must still advance freshness.
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler projectionHandler = new(store, new FixedUtcClock(Now.AddMinutes(1)), new TenantAccessOptions());
        FoldersTenantEventHandler subject = new(projectionHandler);

        TenantEventContext createdContext = new("tenant-a", "01J00000000000000000000100", 1, Now, "corr-100");
        TenantEventContext updatedContext = new("tenant-a", "01J00000000000000000000101", 2, Now, "corr-101");

        await subject.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), createdContext, TestContext.Current.CancellationToken);
        await subject.HandleAsync(new TenantUpdated("tenant-a", "Tenant A renamed", null), updatedContext, TestContext.Current.CancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", TestContext.Current.CancellationToken);
        projection.ShouldNotBeNull();
        projection.Watermark.ShouldBe(2);
        projection.Principals.ShouldBeEmpty();
        projection.MalformedEvidence.ShouldBeFalse();
        projection.ReplayConflict.ShouldBeFalse();
    }
}
