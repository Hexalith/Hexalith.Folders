using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Tests.OperationalStatus;

internal static class OperationalStatusTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    public static FolderCreated Created(string tenant = "tenant-a", string folder = "folder-a")
        => new(tenant, tenant, folder, "Folder", null, null, [], FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound, "principal-a", "correlation-a", "task-a", "create-key", "fingerprint", Now);
    public static IReadOnlyList<IFolderEvent> LockedPrefix(DateTimeOffset occurredAt)
        =>
        [
            Created() with { OccurredAt = occurredAt },
            new RepositoryBindingRequested("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "profile-a", "branch-policy-a",
                "principal-a", "correlation-a", "task-a", "binding-key", "binding-fingerprint", occurredAt),
            new RepositoryBound("tenant-a", "tenant-a", "folder-a", "binding-a", "provider-a", "correlation-a", "task-a", "bound-key", "bound-fingerprint", occurredAt),
            new BranchRefPolicyConfigured("tenant-a", "tenant-a", "folder-a", "binding-a", "branch-policy-a", "branch_ref_primary", [], [],
                "principal-a", "correlation-a", "task-a", "policy-key", "policy-fingerprint", occurredAt),
            new WorkspacePreparationRequested("tenant-a", "tenant-a", "folder-a", "workspace-a", "binding-a", "branch-policy-a", "workspace-policy-a",
                "principal-a", "correlation-a", "task-a", "prepare-key", "prepare-fingerprint", occurredAt),
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                null, "prepare-operation", "correlation-a", "task-a", "prepared-key", "prepared-fingerprint", occurredAt),
            new WorkspaceLockAcquired("tenant-a", "tenant-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspaceLocked, "lock-a", "exclusive_write",
                3600, "task-a", occurredAt, occurredAt, occurredAt.AddHours(1), "lease_until_expiry", "principal-a", "correlation-a", "task-a", "lock-key", "lock-fingerprint", occurredAt),
        ];
    public static IReadOnlyList<IFolderEvent> CleanReleasedPrefix(DateTimeOffset occurredAt)
        => [.. LockedPrefix(occurredAt), new WorkspaceLockReleased("tenant-a", "tenant-a", "folder-a", "workspace-a",
            FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, "lock-a", "task-a", "explicit_release", "lease_until_expiry",
            occurredAt, occurredAt, occurredAt.AddHours(1), "principal-a", "correlation-a", "task-a",
            "release-key", "release-fingerprint", occurredAt)];
    public static EventStoreDomainEventEnvelope Envelope(IFolderEvent value, long sequence = 1)
        => new("message-" + sequence, value.FolderId, value.ManagedTenantId, value.GetType().FullName!, sequence,
            value.OccurredAt, value.CorrelationId, "json", JsonSerializer.SerializeToUtf8Bytes(value, value.GetType()))
        { Domain = "folders", MetadataVersion = 1, GlobalPosition = sequence };
    public static OperationalStatusEvent[] Sources(IEnumerable<IFolderEvent> history)
        => history.Select((value, index) => OperationalStatusEventReader.ReadStored(Envelope(value, index + 1))).ToArray();
    public static ProjectionRequest Projection(IEnumerable<IFolderEvent> history)
        => new("tenant-a", "folders", "folder-a", history.Select((value, index) =>
        {
            EventStoreDomainEventEnvelope source = Envelope(value, index + 1);
            return new ProjectionEventDto(source.EventTypeName, source.Payload, "json", source.SequenceNumber,
                source.Timestamp, source.CorrelationId, source.MessageId)
            { MetadataVersion = 1, GlobalPosition = source.GlobalPosition ?? 0 };
        }).ToArray());
}
