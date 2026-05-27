using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceFileMutationAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstAcceptedMutationShouldStageChangesWithoutRawPathEvidence()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);

        FolderResult result = FolderAggregate.Handle(locked, Mutation(), Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceFileMutationAccepted accepted = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceFileMutationAccepted>();
        accepted.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.FileMutated);
        accepted.PathMetadataDigest.ShouldNotBeNullOrWhiteSpace();
        accepted.PathMetadataDigest.ShouldNotContain("docs/readme.md", Case.Sensitive);
        accepted.FileOperationKind.ShouldBe("add");
        accepted.PathPolicyClass.ShouldBe("tenant_sensitive_document");
        accepted.ContentHashReference.ShouldBe("hashref-a");
        accepted.ByteLength.ShouldBe(12);
        accepted.MediaType.ShouldBe("text/plain");
        accepted.TransportEvidenceKind.ShouldBe("inline_decoded");
        accepted.ObservedByteLength.ShouldBe(12);

        FolderState applied = locked.Apply(result.Events, streamName);
        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
    }

    [Fact]
    public void AcceptedMutationSafetySurfacesShouldNotEchoForbiddenSentinelCorpusValues()
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            Mutation(),
            Now);
        WorkspaceFileMutationAccepted accepted = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceFileMutationAccepted>();
        Dictionary<string, string> mutationSurfaces = new(StringComparer.Ordinal)
        {
            ["event"] = JsonSerializer.Serialize(accepted),
            ["result"] = JsonSerializer.Serialize(result with { Events = [] }),
            ["audit-record"] = string.Join('|', "MutateWorkspaceFile", "accepted", accepted.FolderId, accepted.WorkspaceId, accepted.OperationId, accepted.CorrelationId, accepted.TaskId),
            ["projection"] = JsonSerializer.Serialize(new
            {
                changesStaged = true,
                workspaceId = accepted.WorkspaceId,
                pathPolicyClass = accepted.PathPolicyClass,
                pathMetadataDigest = accepted.PathMetadataDigest,
            }),
            ["problem-details"] = JsonSerializer.Serialize(new
            {
                category = "path_policy_denied",
                code = "path_policy_denied",
                correlationId = accepted.CorrelationId,
                taskId = accepted.TaskId,
            }),
            ["log-template"] = "MutateWorkspaceFile completed: Result=accepted, PathPolicy=accepted",
            ["trace-tags"] = "operation=MutateWorkspaceFile result=accepted path_policy=accepted",
            ["metric-labels"] = "operation=MutateWorkspaceFile,result=accepted",
        };

        foreach (string sentinel in ForbiddenSentinelValues())
        {
            foreach (KeyValuePair<string, string> surface in mutationSurfaces)
            {
                surface.Value.ShouldNotContain(sentinel, Case.Sensitive, $"Surface {surface.Key} leaked sentinel corpus value.");
            }
        }
    }

    [Fact]
    public void AdditionalAcceptedMutationShouldRemainChangesStaged()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = LockedState(streamName).Apply(
            FolderAggregate.Handle(LockedState(streamName), Mutation(), Now).Events,
            streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Mutation(operationId: "operation-b", idempotencyKey: "idempotency-file-b"),
            Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.Accepted);
        staged.Apply(result.Events, streamName).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
    }

    [Fact]
    public void EquivalentReplayShouldNotAppendDuplicateMutationEvent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);
        MutateWorkspaceFile command = Mutation();
        FolderState applied = locked.Apply(FolderAggregate.Handle(locked, command, Now).Events, streamName);

        FolderResult replay = FolderAggregate.Handle(applied, command, Now.AddMinutes(1));

        replay.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        replay.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameIdempotencyKeyWithDifferentPayloadShouldConflict()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);
        FolderState applied = locked.Apply(FolderAggregate.Handle(locked, Mutation(), Now).Events, streamName);

        FolderResult conflict = FolderAggregate.Handle(
            applied,
            Mutation(operationId: "operation-b"),
            Now.AddMinutes(1));

        conflict.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        conflict.Events.ShouldBeEmpty();
    }

    [Fact]
    public void MutationShouldRejectWrongWorkspaceAndWrongTaskBeforeStateChange()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);

        FolderAggregate.Handle(locked, Mutation(workspaceId: "workspace-b"), Now).Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        FolderAggregate.Handle(locked, Mutation(taskId: "task-b"), Now).Code.ShouldBe(FolderResultCode.LockNotOwned);
    }

    [Fact]
    public void MutationShouldRejectExpiredLock()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName, Now.AddHours(-2));

        FolderResult result = FolderAggregate.Handle(locked, Mutation(), Now);

        result.Code.ShouldBe(FolderResultCode.LockExpired);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveMutationShouldNotRequireContentHashReference()
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            Mutation(fileOperationKind: "remove", transportOperation: "metadataOnlyRemoval", contentHashReference: null, byteLength: null),
            Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
    }

    [Theory]
    [InlineData("v2", "add", "PutFileInline", "hashref-a", 12)]
    [InlineData("v1", "add", "metadataOnlyRemoval", "hashref-a", 12)]
    [InlineData("v1", "remove", "metadataOnlyRemoval", "hashref-a", null)]
    public void MutationShouldRejectInvalidSchemaAndTransportPairing(
        string schemaVersion,
        string fileOperationKind,
        string transportOperation,
        string? contentHashReference,
        int? byteLength)
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            Mutation(
                fileOperationKind: fileOperationKind,
                transportOperation: transportOperation,
                contentHashReference: contentHashReference,
                byteLength: byteLength,
                requestSchemaVersion: schemaVersion),
            Now);

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("text/plain;charset=utf-8", "inline_decoded", 12)]
    [InlineData("text/plain", "stream_observed", 12)]
    [InlineData("text/plain", "inline_decoded", 13)]
    public void MutationShouldRejectInvalidTransportEvidence(
        string mediaType,
        string transportEvidenceKind,
        int observedByteLength)
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            Mutation(
                mediaType: mediaType,
                transportEvidenceKind: transportEvidenceKind,
                observedByteLength: observedByteLength),
            Now);

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(NonMutableStates))]
    public void MutationShouldRejectNonMutableStates(FolderState state, FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(state, Mutation(), Now);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    public static IEnumerable<object[]> NonMutableStates()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        yield return [FolderState.Empty, FolderResultCode.FolderNotFound];
        yield return [ReadyState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReadyState(streamName) with { WorkspaceLockId = null }, FolderResultCode.StateTransitionInvalid];
        yield return [LockedState(streamName) with { WorkspaceLockHolderTaskId = null }, FolderResultCode.LockNotOwned];
        yield return [LockedState(streamName).Apply(
            [WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.LockLeaseExpired, "lease-expired-a")],
            streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReadyState(streamName).Apply(
            [WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.AuthRevocationDetected, "auth-revoked-a")],
            streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReadyState(streamName).Apply(
            [WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.ReconciliationRequested, "reconciliation-a")],
            streamName), FolderResultCode.StateTransitionInvalid];
    }

    private static MutateWorkspaceFile Mutation(
        string workspaceId = "workspace-a",
        string operationId = "operation-a",
        string fileOperationKind = "add",
        string transportOperation = "PutFileInline",
        string? contentHashReference = "hashref-a",
        int? byteLength = 12,
        string? mediaType = null,
        string? transportEvidenceKind = null,
        int? observedByteLength = null,
        string taskId = "task-a",
        string idempotencyKey = "idempotency-file-a",
        string requestSchemaVersion = "v1")
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            requestSchemaVersion,
            workspaceId,
            operationId,
            fileOperationKind,
            transportOperation,
            new PathMetadata("docs/readme.md", "readme.md", "tenant_sensitive_document", "NFC"),
            contentHashReference,
            ByteLength: byteLength,
            MediaType: mediaType ?? (fileOperationKind is "add" or "change" ? "text/plain" : null),
            TransportEvidenceKind: transportEvidenceKind ?? (transportOperation == "PutFileInline" ? "inline_decoded" : transportOperation == "PutFileStream" ? "stream_observed" : null),
            ObservedByteLength: observedByteLength ?? byteLength,
            "principal-a",
            "correlation-file-a",
            taskId,
            idempotencyKey,
            PayloadTenantId: null);

    private static FolderWorkspaceLifecycleEventRecorded WorkspaceLifecycleEvent(
        FolderWorkspaceLifecycleEvent lifecycleEvent,
        string idempotencyKey)
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            lifecycleEvent,
            DirtyResolution: null,
            OperationId: "workspace-a",
            $"correlation-{idempotencyKey}",
            "task-a",
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            Now);

    private static FolderState LockedState(FolderStreamName streamName, DateTimeOffset? lockAcquiredAt = null)
    {
        FolderState ready = ReadyState(streamName);
        FolderResult locked = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), lockAcquiredAt ?? Now);
        return ready.Apply(locked.Events, streamName);
    }

    private static FolderState ReadyState(FolderStreamName streamName)
    {
        FolderState bound = BoundState(streamName);
        FolderResult configured = FolderAggregate.Handle(
            bound,
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "branch-ref-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "principal-a",
                "correlation-policy-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);
        FolderState configuredState = bound.Apply(configured.Events, streamName);
        FolderState preparing = configuredState.Apply(FolderAggregate.Handle(configuredState, FolderCommandFactory.PrepareWorkspace(), Now).Events, streamName);
        return preparing.Apply(
            [
                new FolderWorkspaceLifecycleEventRecorded(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "workspace-a",
                    FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                    DirtyResolution: null,
                    OperationId: "workspace-a",
                    "correlation-prepared-a",
                    "task-a",
                    "idempotency-workspace-outcome-a",
                    "fingerprint-workspace-outcome-a",
                    Now),
            ],
            streamName);
    }

    private static FolderState BoundState(FolderStreamName streamName)
    {
        FolderState created = FolderState.Empty.Apply(FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create()).Events, streamName);
        FolderState requested = created.Apply(FolderAggregate.Handle(created, FolderCommandFactory.CreateRepositoryBackedFolder(), Now).Events, streamName);
        return requested.Apply(
            [
                new RepositoryBound(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "repository-binding-a",
                    "provider-binding-a",
                    "correlation-bound-a",
                    "task-bound-a",
                    "idempotency-bound-a",
                    "fingerprint-bound-a",
                    Now),
            ],
            streamName);
    }

    private static IReadOnlyList<string> ForbiddenSentinelValues()
    {
        string path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(sample => sample.GetProperty("value").GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
