using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessProjectionReplayTests
{
    [Fact]
    public void ProjectionShouldPreserveRevocationWatermarkAndMetadata()
    {
        IReadOnlyList<IFolderEvent> events = GrantThenRevokeEvents("tenant-a", "folder-a");

        FolderAccessProjection projection = FolderAccessProjection.FromEvents("tenant-a", "folder-a", events);

        projection.Watermark.ShouldBe(2);
        FolderAccessOverride access = projection.Overrides.Values.Single();
        access.IsGranted.ShouldBeFalse();
        access.OperationIntent.ShouldBe("revoke");
        access.CorrelationId.ShouldBe("correlation-revoke");
        access.IdempotencyKey.ShouldBe("idempotency-revoke");
    }

    [Fact]
    public void ProjectionShouldIsolateTenantsWithMatchingFolderPrincipalAndAction()
    {
        IReadOnlyList<IFolderEvent> tenantAEvents = GrantThenRevokeEvents("tenant-a", "folder-shared");
        IReadOnlyList<IFolderEvent> tenantBEvents = GrantEvents("tenant-b", "folder-shared");

        FolderAccessProjection tenantA = FolderAccessProjection.FromEvents("tenant-a", "folder-shared", tenantAEvents);
        FolderAccessProjection tenantB = FolderAccessProjection.FromEvents("tenant-b", "folder-shared", tenantBEvents);

        tenantA.Overrides.Keys.Single().ManagedTenantId.ShouldBe("tenant-a");
        tenantA.Overrides.Values.Single().IsGranted.ShouldBeFalse();
        tenantB.Overrides.Keys.Single().ManagedTenantId.ShouldBe("tenant-b");
        tenantB.Overrides.Values.Single().IsGranted.ShouldBeTrue();
    }

    [Fact]
    public void GrantRevokeGrantReplayShouldProduceDeterministicFinalGrant()
    {
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-a"));
        FolderState granted = created.Apply(grant.Events, streamName);
        FolderResult revoke = FolderAggregate.Handle(granted, FolderCommandFactory.RevokeAccess(idempotencyKey: "idempotency-revoke"));
        FolderState revoked = granted.Apply(revoke.Events, streamName);
        FolderResult grantAgain = FolderAggregate.Handle(revoked, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-b"));

        FolderState final = revoked.Apply(grantAgain.Events, streamName);

        final.AccessSequence.ShouldBe(3);
        final.AccessOverrides.Values.Single().IsGranted.ShouldBeTrue();
        final.AccessOverrides.Values.Single().OperationIntent.ShouldBe("grant");
    }

    private static IReadOnlyList<IFolderEvent> GrantThenRevokeEvents(string tenantId, string folderId)
    {
        FolderState created = CreatedState(tenantId, folderId);
        FolderStreamName streamName = FolderStreamName.Create(tenantId, folderId);
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            idempotencyKey: "idempotency-grant"));
        FolderState granted = created.Apply(grant.Events, streamName);
        FolderResult revoke = FolderAggregate.Handle(granted, FolderCommandFactory.RevokeAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            correlationId: "correlation-revoke",
            idempotencyKey: "idempotency-revoke"));

        return [.. CreatedEvent(tenantId, folderId), .. grant.Events, .. revoke.Events];
    }

    private static IReadOnlyList<IFolderEvent> GrantEvents(string tenantId, string folderId)
    {
        FolderState created = CreatedState(tenantId, folderId);
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            idempotencyKey: "idempotency-grant"));
        return [.. CreatedEvent(tenantId, folderId), .. grant.Events];
    }

    private static IReadOnlyList<IFolderEvent> CreatedEvent(string tenantId, string folderId)
        => FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(managedTenantId: tenantId, folderId: folderId)).Events;

    private static FolderState CreatedState(string tenantId, string folderId)
        => FolderState.Empty.Apply(CreatedEvent(tenantId, folderId), FolderStreamName.Create(tenantId, folderId));
}
