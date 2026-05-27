using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Projections.FolderAccess;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Aggregates.Folder;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class WorkspaceLifecycleProjectionDeterminismTests
{
    private static readonly DateTimeOffset ProjectionObservedAt = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    [Fact]
    public async Task InMemoryReadModelsShouldRebuildDeterministicNormalizedSnapshots()
    {
        ProjectionSnapshots first = await BuildProjectionSnapshotsAsync().ConfigureAwait(true);
        ProjectionSnapshots second = await BuildProjectionSnapshotsAsync().ConfigureAwait(true);

        NormalizeProjectionPayload(first).ShouldBe(NormalizeProjectionPayload(second));
        first.LifecycleStatus.BindingStatus.ShouldBe(FolderRepositoryBindingStatus.Bound);
        first.BranchRefPolicy.PolicyRef.ShouldBe(FolderLifecycleReplayFixture.BranchRefPolicyRef);
        first.WorkspaceLockStatus.LockState.ShouldBe("unlocked");
        first.WorkspaceStatus.CurrentState.ShouldBe("ready");
        first.WorkspaceCleanupStatus.Status.ShouldBe("status_only");
        first.TaskStatus.CurrentState.ShouldBe("ready");
        first.FolderList.Get(FolderLifecycleReplayFixture.ManagedTenantId, FolderLifecycleReplayFixture.FolderId)
            .ShouldNotBeNull()
            .RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Bound);
        first.FolderAccess.Watermark.ShouldBe(2);
        first.TenantAccess.Enabled.ShouldBeTrue();
        first.TenantAccess.Watermark.ShouldBe(3);
        first.TenantAccess.Principals.ShouldHaveSingleItem()
            .Role.ShouldBe("TenantOwner");
        first.TenantAccess.ConfigurationKeys.ShouldBe(["folders.lifecycle.replay.enabled"]);
        first.AuditObservations.ShouldHaveSingleItem()
            .SanitizedCategory.ShouldBe("projection_rebuild_deterministic");
    }

    [Fact]
    public async Task NormalizedComparisonShouldStillFailForNonFreshnessDrift()
    {
        ProjectionSnapshots baseline = await BuildProjectionSnapshotsAsync().ConfigureAwait(true);
        ProjectionSnapshots drifted = baseline with
        {
            WorkspaceStatus = baseline.WorkspaceStatus with
            {
                CurrentState = "failed",
            },
        };

        NormalizeProjectionPayload(drifted).ShouldNotBe(NormalizeProjectionPayload(baseline));
    }

    [Fact]
    public async Task NormalizedComparisonShouldStillFailForTenantAccessWatermarkDrift()
    {
        ProjectionSnapshots baseline = await BuildProjectionSnapshotsAsync().ConfigureAwait(true);
        ProjectionSnapshots drifted = baseline with
        {
            TenantAccess = baseline.TenantAccess with
            {
                ProjectionWatermark = $"{FolderLifecycleReplayFixture.ManagedTenantId}:999",
            },
        };

        NormalizeProjectionPayload(drifted).ShouldNotBe(NormalizeProjectionPayload(baseline));
    }

    public static TheoryData<string, IReadOnlyList<IFolderEvent>, string, string, bool, string> CleanupStatusStreams()
        =>
        new()
        {
            {
                "committed_cleanup",
                FolderLifecycleReplayFixture.CommittedLifecycle(),
                "succeeded",
                "workspace_committed",
                false,
                "retry_not_required"
            },
            {
                "failed_cleanup",
                FolderLifecycleReplayFixture.CommitFailureLifecycle(),
                "failed",
                "failed_operation",
                true,
                "failed_operation"
            },
            {
                "dirty_cleanup",
                FolderLifecycleReplayFixture.DirtyLockExpiredLifecycle(),
                "status_only",
                "dirty_workspace",
                true,
                "dirty_workspace"
            },
        };

    [Theory]
    [MemberData(nameof(CleanupStatusStreams))]
    public async Task WorkspaceCleanupStatusShouldRebuildDeterministicLifecycleVisibility(
        string scenario,
        IReadOnlyList<IFolderEvent> events,
        string expectedStatus,
        string expectedReasonCode,
        bool expectedRetryEligible,
        string expectedRetryReasonCode)
    {
        WorkspaceCleanupStatusReadModelSnapshot first = await BuildCleanupStatusSnapshotAsync(events).ConfigureAwait(true);
        WorkspaceCleanupStatusReadModelSnapshot second = await BuildCleanupStatusSnapshotAsync(events).ConfigureAwait(true);

        NormalizeProjectionPayload(first).ShouldBe(NormalizeProjectionPayload(second), scenario);
        first.Status.ShouldBe(expectedStatus, scenario);
        first.ReasonCode.ShouldBe(expectedReasonCode, scenario);
        first.RetryEligibility.Eligible.ShouldBe(expectedRetryEligible, scenario);
        first.RetryEligibility.ReasonCode.ShouldBe(expectedRetryReasonCode, scenario);
    }

    [Fact]
    public async Task SerializedSnapshotsAndAuditEvidenceShouldRemainMetadataOnly()
    {
        ProjectionSnapshots snapshots = await BuildProjectionSnapshotsAsync().ConfigureAwait(true);
        string serialized = JsonSerializer.Serialize(new
        {
            Raw = snapshots,
            Normalized = NormalizeProjectionPayload(snapshots),
            Audit = snapshots.AuditObservations.Select(static observation => observation.ToString()).ToArray(),
        }, JsonOptions);

        foreach (string sentinel in ForbiddenSentinelValues())
        {
            serialized.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    [Fact]
    public void DuplicateProjectionDeliveryShouldBeIdempotentOrFailLoudByContract()
    {
        IReadOnlyList<IFolderEvent> duplicateEvents = FolderLifecycleReplayFixture.DuplicateAccessDeliveryLifecycle();
        FolderAccessProjection accessProjection = FolderAccessProjection.FromEvents(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            duplicateEvents);
        InMemoryFolderRepository repository = BuildRepository();

        accessProjection.Watermark.ShouldBe(1);
        accessProjection.Overrides.ShouldHaveSingleItem();
        Should.Throw<InvalidOperationException>(() => repository.Seed(FolderLifecycleReplayFixture.StreamName, duplicateEvents))
            .Message.ShouldContain("Seed would overwrite an existing idempotency ledger entry");
        repository.Load(FolderLifecycleReplayFixture.StreamName).IsCreated.ShouldBeFalse();
    }

    [Fact]
    public void OutOfOrderProjectionDeliveryShouldFailLoudWithMetadataOnlyDiagnostics()
    {
        IFolderEvent bindingBeforeCreate = FolderLifecycleReplayFixture.FolderListProjectionEvents()[1];
        FolderProjectionEnvelope envelope = new(
            FolderLifecycleReplayFixture.ManagedTenantId,
            Sequence: 1,
            Event: bindingBeforeCreate);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => FolderListProjection.Empty.Apply([envelope]));

        exception.Message.ShouldContain("before any FolderCreated event");
        foreach (string sentinel in ForbiddenSentinelValues())
        {
            exception.Message.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    private static async Task<ProjectionSnapshots> BuildProjectionSnapshotsAsync()
    {
        InMemoryFolderLifecycleStatusReadModel lifecycle = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryBranchRefPolicyReadModel branchRefPolicy = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryWorkspaceLockStatusReadModel workspaceLock = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryWorkspaceStatusReadModel workspaceStatus = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryWorkspaceCleanupStatusReadModel workspaceCleanup = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryTaskStatusReadModel taskStatus = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryFolderRepository repository = BuildRepository(
            lifecycle,
            branchRefPolicy,
            workspaceLock,
            workspaceStatus,
            workspaceCleanup,
            taskStatus);
        IReadOnlyList<IFolderEvent> events = FolderLifecycleReplayFixture.SuccessfulLifecycle();

        repository.Seed(FolderLifecycleReplayFixture.StreamName, events);

        FolderListProjection folderList = FolderListProjection.Empty.Apply(
            FolderLifecycleReplayFixture.Envelopes(FolderLifecycleReplayFixture.FolderListProjectionEvents()));
        FolderAccessProjection folderAccess = FolderAccessProjection.FromEvents(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            events);
        InMemoryFolderAuditObserver auditObserver = new();
        await auditObserver.ObserveAsync(new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.ReadModel,
            Result = FolderAuditResult.Success,
            TenantId = FolderLifecycleReplayFixture.ManagedTenantId,
            ActorReference = FolderLifecycleReplayFixture.ActorPrincipalId,
            TaskId = FolderLifecycleReplayFixture.TaskId,
            OperationId = "operation-projection-replay-a",
            CorrelationId = FolderLifecycleReplayFixture.CorrelationId,
            FolderId = FolderLifecycleReplayFixture.FolderId,
            WorkspaceId = FolderLifecycleReplayFixture.WorkspaceId,
            ProviderReference = FolderLifecycleReplayFixture.ProviderBindingRef,
            Timestamp = ProjectionObservedAt,
            Duration = TimeSpan.FromMilliseconds(12.5),
            RedactionState = FolderAuditRedactionState.MetadataOnly,
            StateTransition = "ready->ready",
            SanitizedCategory = "projection_rebuild_deterministic",
            IsRetry = false,
            IsIdempotentReplay = false,
            IsDuplicate = false,
        }.Build(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        return new ProjectionSnapshots(
            await LifecycleSnapshotAsync(lifecycle).ConfigureAwait(true),
            await BranchRefPolicySnapshotAsync(branchRefPolicy).ConfigureAwait(true),
            await WorkspaceLockSnapshotAsync(workspaceLock).ConfigureAwait(true),
            await WorkspaceStatusSnapshotAsync(workspaceStatus).ConfigureAwait(true),
            await WorkspaceCleanupSnapshotAsync(workspaceCleanup).ConfigureAwait(true),
            await TaskStatusSnapshotAsync(taskStatus).ConfigureAwait(true),
            folderList,
            ToAccessSnapshot(folderAccess),
            await BuildTenantAccessProjectionSnapshotAsync().ConfigureAwait(true),
            auditObserver.Observations);
    }

    private static InMemoryFolderRepository BuildRepository(
        InMemoryFolderLifecycleStatusReadModel? lifecycle = null,
        InMemoryBranchRefPolicyReadModel? branchRefPolicy = null,
        InMemoryWorkspaceLockStatusReadModel? workspaceLock = null,
        InMemoryWorkspaceStatusReadModel? workspaceStatus = null,
        InMemoryWorkspaceCleanupStatusReadModel? workspaceCleanup = null,
        InMemoryTaskStatusReadModel? taskStatus = null)
        => new(
            lifecycle,
            branchRefPolicy,
            new ConstantTimeProvider(ProjectionObservedAt),
            workspaceLock,
            workspaceStatus,
            workspaceCleanup,
            taskStatus);

    private static async Task<FolderLifecycleStatusReadModelSnapshot> LifecycleSnapshotAsync(
        InMemoryFolderLifecycleStatusReadModel readModel)
    {
        FolderLifecycleStatusReadModelResult result = await readModel.GetAsync(new FolderLifecycleStatusReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            "read_metadata",
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.CorrelationId,
            AuthorizationWatermark: null,
            ReadConsistency: "eventually_consistent"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static async Task<BranchRefPolicyReadModelSnapshot> BranchRefPolicySnapshotAsync(
        InMemoryBranchRefPolicyReadModel readModel)
    {
        BranchRefPolicyReadModelResult result = await readModel.GetAsync(new BranchRefPolicyReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            "read_branch_ref_policy",
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.CorrelationId,
            AuthorizationWatermark: null,
            ReadConsistency: "eventually_consistent"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(BranchRefPolicyReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static async Task<WorkspaceLockStatusReadModelSnapshot> WorkspaceLockSnapshotAsync(
        InMemoryWorkspaceLockStatusReadModel readModel)
    {
        WorkspaceLockStatusReadModelResult result = await readModel.GetAsync(new WorkspaceLockStatusReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.WorkspaceId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            WorkspaceLockStatusQueryHandler.ActionToken,
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.CorrelationId,
            AuthorizationWatermark: null,
            ReadConsistency: "read_your_writes"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(WorkspaceLockStatusReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static async Task<WorkspaceStatusReadModelSnapshot> WorkspaceStatusSnapshotAsync(
        InMemoryWorkspaceStatusReadModel readModel)
    {
        WorkspaceStatusReadModelResult result = await readModel.GetAsync(new WorkspaceStatusReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.WorkspaceId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            WorkspaceStatusQueryHandler.ActionToken,
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.CorrelationId,
            AuthorizationWatermark: null,
            ReadConsistency: "read_your_writes"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static async Task<WorkspaceCleanupStatusReadModelSnapshot> WorkspaceCleanupSnapshotAsync(
        InMemoryWorkspaceCleanupStatusReadModel readModel)
    {
        WorkspaceCleanupStatusReadModelResult result = await readModel.GetAsync(new WorkspaceCleanupStatusReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.WorkspaceId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            WorkspaceCleanupStatusQueryHandler.ActionToken,
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.CorrelationId,
            AuthorizationWatermark: null,
            ReadConsistency: "read_your_writes"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(WorkspaceCleanupStatusReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static async Task<WorkspaceCleanupStatusReadModelSnapshot> BuildCleanupStatusSnapshotAsync(
        IReadOnlyList<IFolderEvent> events)
    {
        InMemoryWorkspaceCleanupStatusReadModel cleanup = new(new FixedUtcClock(ProjectionObservedAt));
        InMemoryFolderRepository repository = BuildRepository(workspaceCleanup: cleanup);

        repository.Seed(FolderLifecycleReplayFixture.StreamName, events);

        return await WorkspaceCleanupSnapshotAsync(cleanup).ConfigureAwait(true);
    }

    private static async Task<TaskStatusReadModelSnapshot> TaskStatusSnapshotAsync(
        InMemoryTaskStatusReadModel readModel)
    {
        TaskStatusReadModelResult result = await readModel.GetAsync(new TaskStatusReadModelRequest(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.TaskId,
            FolderLifecycleReplayFixture.ActorPrincipalId,
            TaskStatusQueryHandler.ActionToken,
            FolderLifecycleReplayFixture.CorrelationId,
            "eventually_consistent"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Status.ShouldBe(TaskStatusReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull();
    }

    private static string NormalizeProjectionPayload(object payload)
    {
        JsonNode node = JsonSerializer.SerializeToNode(payload, JsonOptions)
            ?? throw new InvalidOperationException("projection_payload_missing");
        NormalizeNode(node, parentPropertyName: null);
        return node.ToJsonString(JsonOptions);
    }

    private static FolderAccessProjectionSnapshot ToAccessSnapshot(FolderAccessProjection projection)
        => new(
            projection.Watermark,
            projection.Overrides
                .OrderBy(static entry => entry.Key.CanonicalValue, StringComparer.Ordinal)
                .Select(static entry => new FolderAccessOverrideSnapshot(entry.Key.CanonicalValue, entry.Value))
                .ToArray());

    private static async Task<FolderTenantAccessProjectionSnapshot> BuildTenantAccessProjectionSnapshotAsync()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = new(
            store,
            new FixedUtcClock(ProjectionObservedAt.AddMinutes(1)),
            new TenantAccessOptions());
        FolderTenantAccessEvent userAdded = TenantAccessEvent(
            FolderTenantAccessEventKind.UserAddedToTenant,
            "01J00000000000000000000415B",
            2,
            principalId: FolderLifecycleReplayFixture.ActorPrincipalId,
            role: "TenantOwner",
            payloadFingerprint: "tenant-access-user-a");

        await handler.HandleAsync(
            TenantAccessEvent(
                FolderTenantAccessEventKind.TenantCreated,
                "01J00000000000000000000415A",
                1,
                payloadFingerprint: "tenant-access-created-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await handler.HandleAsync(userAdded, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await handler.HandleAsync(userAdded, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await handler.HandleAsync(
            TenantAccessEvent(
                FolderTenantAccessEventKind.TenantConfigurationSet,
                "01J00000000000000000000415C",
                3,
                configurationKey: "folders.lifecycle.replay.enabled",
                payloadFingerprint: "tenant-access-configuration-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        FolderTenantAccessProjection projection = (await store
            .GetAsync(FolderLifecycleReplayFixture.ManagedTenantId, TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).ShouldNotBeNull();

        return new FolderTenantAccessProjectionSnapshot(
            projection.TenantId,
            projection.Enabled,
            projection.Watermark,
            projection.ProjectionWatermark,
            projection.LastEventTimestamp,
            projection.ReplayConflict,
            projection.MalformedEvidence,
            projection.Principals
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => entry.Value)
                .ToArray(),
            projection.ConfigurationKeys
                .Order(StringComparer.Ordinal)
                .ToArray(),
            projection.RemovedConfigurationKeys
                .Order(StringComparer.Ordinal)
                .ToArray(),
            projection.ProcessedMessages
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => entry.Value)
                .ToArray());
    }

    private static FolderTenantAccessEvent TenantAccessEvent(
        FolderTenantAccessEventKind kind,
        string messageId,
        long sequenceNumber,
        string? principalId = null,
        string? role = null,
        string? configurationKey = null,
        string payloadFingerprint = "tenant-access-event-a")
        => new(
            kind,
            FolderLifecycleReplayFixture.ManagedTenantId,
            messageId,
            sequenceNumber,
            ProjectionObservedAt,
            FolderLifecycleReplayFixture.CorrelationId,
            principalId,
            role,
            ConfigurationKey: configurationKey,
            PayloadFingerprint: payloadFingerprint);

    private static void NormalizeNode(JsonNode node, string? parentPropertyName)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject.ToArray())
            {
                if (ShouldNormalizeFreshnessProperty(property.Key, parentPropertyName))
                {
                    jsonObject[property.Key] = "__normalized_freshness_field__";
                }
                else if (property.Value is not null)
                {
                    NormalizeNode(property.Value, property.Key);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                if (item is not null)
                {
                    NormalizeNode(item, parentPropertyName);
                }
            }
        }
    }

    private static bool ShouldNormalizeFreshnessProperty(string propertyName, string? parentPropertyName)
        => string.Equals(propertyName, "observedAt", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(propertyName, "projectionWatermark", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parentPropertyName, "freshness", StringComparison.OrdinalIgnoreCase))
            || string.Equals(propertyName, "projectionLag", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, "lastAttemptedAt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, "duration", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, "durationEvidence", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ForbiddenSentinelValues()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(AuditLeakageCorpusPath()));
        return document.RootElement.GetProperty("sentinel_samples")
            .EnumerateArray()
            .Where(static sample => !string.Equals(
                sample.GetProperty("classification").GetString(),
                "safe-provenance",
                StringComparison.Ordinal))
            .Select(static sample => sample.GetProperty("value").GetString() ?? throw new InvalidOperationException("sentinel_value_missing"))
            .ToArray();
    }

    private static string AuditLeakageCorpusPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "fixtures", "audit-leakage-corpus.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("audit_leakage_corpus_missing");
    }

    private sealed record ProjectionSnapshots(
        FolderLifecycleStatusReadModelSnapshot LifecycleStatus,
        BranchRefPolicyReadModelSnapshot BranchRefPolicy,
        WorkspaceLockStatusReadModelSnapshot WorkspaceLockStatus,
        WorkspaceStatusReadModelSnapshot WorkspaceStatus,
        WorkspaceCleanupStatusReadModelSnapshot WorkspaceCleanupStatus,
        TaskStatusReadModelSnapshot TaskStatus,
        FolderListProjection FolderList,
        FolderAccessProjectionSnapshot FolderAccess,
        FolderTenantAccessProjectionSnapshot TenantAccess,
        IReadOnlyList<FolderAuditObservation> AuditObservations);

    private sealed record FolderAccessProjectionSnapshot(
        long Watermark,
        IReadOnlyList<FolderAccessOverrideSnapshot> Overrides);

    private sealed record FolderAccessOverrideSnapshot(
        string Key,
        FolderAccessOverride Override);

    private sealed record FolderTenantAccessProjectionSnapshot(
        string TenantId,
        bool Enabled,
        long Watermark,
        string ProjectionWatermark,
        DateTimeOffset? LastEventTimestamp,
        bool ReplayConflict,
        bool MalformedEvidence,
        IReadOnlyList<FolderTenantPrincipalEvidence> Principals,
        IReadOnlyList<string> ConfigurationKeys,
        IReadOnlyList<string> RemovedConfigurationKeys,
        IReadOnlyList<FolderTenantEventEvidence> ProcessedMessages);

    private sealed class ConstantTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
