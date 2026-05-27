using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceCommitAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CommitSuccessShouldTransitionChangesStagedToCommittedWithMetadataOnlyEvidence()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Commit(),
            WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"),
            Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitSucceeded succeeded = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitSucceeded>();
        succeeded.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.CommitSucceeded);
        succeeded.CommitReference.ShouldBe("commitref_abc123");
        succeeded.AuthorMetadataReference.ShouldBe("authorref_service");
        succeeded.BranchRefTarget.ShouldBe("branchref_primary");
        succeeded.CommitMessageClassification.ShouldBe("generated_summary");
        succeeded.ChangedPathMetadataDigest.ShouldBe("digest_workspace_a");
        JsonSerializer.Serialize(succeeded).ShouldNotContain("docs/readme.md", Case.Sensitive);

        FolderState applied = staged.Apply(result.Events, streamName);
        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Committed);
        applied.WorkspaceCommitReference.ShouldBe("commitref_abc123");
    }

    [Fact]
    public void CommitSuccessSafetySurfacesShouldNotEchoForbiddenSentinelCorpusValues()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Commit(),
            WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"),
            Now);

        WorkspaceCommitSucceeded succeeded = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitSucceeded>();
        Dictionary<string, string> commitSurfaces = new(StringComparer.Ordinal)
        {
            ["success-payload"] = JsonSerializer.Serialize(new
            {
                status = "accepted",
                committed = true,
                correlationId = succeeded.CorrelationId,
                taskId = succeeded.TaskId,
                operationId = succeeded.OperationId,
            }),
            ["problem-details"] = JsonSerializer.Serialize(new
            {
                category = "idempotency_conflict",
                code = "idempotency_conflict",
                correlationId = succeeded.CorrelationId,
                taskId = succeeded.TaskId,
                details = new { visibility = "metadata_only" },
            }),
            ["event"] = JsonSerializer.Serialize(succeeded),
            ["projection-status"] = JsonSerializer.Serialize(new
            {
                workspaceLifecycleState = "committed",
                workspaceId = succeeded.WorkspaceId,
                providerOutcomeCategory = succeeded.ProviderOutcomeCategory,
                operationId = succeeded.OperationId,
                commitReference = succeeded.CommitReference,
                branchRefTarget = succeeded.BranchRefTarget,
                changedPathMetadataDigest = succeeded.ChangedPathMetadataDigest,
                commitMessageClassification = succeeded.CommitMessageClassification,
            }),
            ["log-template"] = "CommitWorkspace completed: Result=accepted, ProviderOutcome=succeeded",
            ["test-diagnostic"] = "CommitWorkspace metadata-only evidence validated for committed state.",
            ["docs-example"] = "CommitWorkspace accepted; inspect workspace status for committed metadata evidence.",
        };

        foreach (string sentinel in ForbiddenSentinelValues())
        {
            foreach (KeyValuePair<string, string> surface in commitSurfaces)
            {
                surface.Value.ShouldNotContain(sentinel, Case.Sensitive, $"Surface {surface.Key} leaked sentinel corpus value.");
            }
        }
    }

    [Fact]
    public void CommitKnownFailureShouldTransitionChangesStagedToFailed()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Commit(),
            WorkspaceCommitExecutionResult.KnownFailure("provider_unavailable"),
            Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitFailed failed = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitFailed>();
        failed.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.CommitFailed);
        failed.FailureCategory.ShouldBe("provider_unavailable");
        staged.Apply(result.Events, streamName).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Failed);
    }

    [Fact]
    public void CommitUnknownOutcomeShouldTransitionChangesStagedToUnknownProviderOutcome()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Commit(),
            WorkspaceCommitExecutionResult.UnknownOutcome("reconcile_commit_a"),
            Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitOutcomeUnknown unknown = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitOutcomeUnknown>();
        unknown.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown);
        unknown.ProviderOutcomeCategory.ShouldBe("unknown_provider_outcome");
        unknown.ReconciliationReference.ShouldStartWith("reconcile_");
        staged.Apply(result.Events, streamName).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.UnknownProviderOutcome);
    }

    [Fact]
    public void CommitReconciliationRequiredShouldRecordMetadataOnlyReconciliationEvidence()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            staged,
            Commit(),
            WorkspaceCommitExecutionResult.ReconciliationRequired("reconcile_commit_a"),
            Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitOutcomeUnknown unknown = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitOutcomeUnknown>();
        unknown.ProviderOutcomeCategory.ShouldBe("reconciliation_required");
        unknown.ReconciliationRequired.ShouldBeTrue();
        unknown.ReconciliationReference.ShouldStartWith("reconcile_");
        JsonSerializer.Serialize(unknown).ShouldNotContain("docs/readme.md", Case.Sensitive);
    }

    [Fact]
    public void EquivalentReplayAndConflictShouldNotEmitDuplicateCommitEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState staged = StagedState(streamName);
        CommitWorkspace command = Commit();
        FolderState committed = staged.Apply(
            FolderAggregate.Handle(staged, command, WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"), Now).Events,
            streamName);

        FolderAggregate.Handle(committed, command, WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"), Now.AddMinutes(1))
            .Code.ShouldBe(FolderResultCode.IdempotentReplay);
        FolderAggregate.Handle(committed, Commit(operationId: "operation-b"), WorkspaceCommitExecutionResult.Succeeded("commitref_def456"), Now.AddMinutes(1))
            .Code.ShouldBe(FolderResultCode.IdempotencyConflict);
    }

    [Theory]
    [InlineData("workspace-b", "task-a", FolderResultCode.StateTransitionInvalid)]
    [InlineData("workspace-a", "task-b", FolderResultCode.LockNotOwned)]
    public void CommitShouldRejectWrongWorkspaceAndWrongTaskBeforeOutcomeEvent(
        string workspaceId,
        string taskId,
        FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(
            StagedState(FolderStreamName.Create("tenant-a", "folder-a")),
            Commit(workspaceId: workspaceId, taskId: taskId),
            WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"),
            Now);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    private static CommitWorkspace Commit(
        string workspaceId = "workspace-a",
        string operationId = "operation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-commit-a")
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "v1",
            workspaceId,
            operationId,
            "authorref_service",
            "branchref_primary",
            "generated_summary",
            "digest_workspace_a",
            "principal-a",
            "correlation-commit-a",
            taskId,
            idempotencyKey,
            PayloadTenantId: null);

    private static FolderState StagedState(FolderStreamName streamName)
    {
        IReadOnlyList<IFolderEvent> readyEvents = SeedReadyEvents();
        FolderState ready = FolderState.Empty.Apply(readyEvents, streamName);
        FolderResult locked = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), Now);
        FolderState lockedState = ready.Apply(locked.Events, streamName);
        FolderResult mutated = FolderAggregate.Handle(lockedState, Mutation(), Now);
        return lockedState.Apply(mutated.Events, streamName);
    }

    private static MutateWorkspaceFile Mutation()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "v1",
            "workspace-a",
            "operation-file-a",
            "add",
            "PutFileInline",
            new PathMetadata("docs/readme.md", "readme.md", "tenant_sensitive_document", "NFC"),
            "hashref-a",
            12,
            "text/plain",
            "inline_decoded",
            12,
            "principal-a",
            "correlation-file-a",
            "task-a",
            "idempotency-file-a",
            PayloadTenantId: null);

    private static IReadOnlyList<IFolderEvent> SeedReadyEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create(), Now);
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(createdState, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        FolderState boundState = createdState.Apply(
            [
                .. requested.Events,
                new RepositoryBound("tenant-a", "organization-a", "folder-a", "repository-binding-a", "provider-binding-a", "correlation-bound-a", "task-bound-a", "idempotency-bound-a", "fingerprint-bound-a", Now),
            ],
            streamName);
        FolderResult configured = FolderAggregate.Handle(
            boundState,
            new ConfigureBranchRefPolicy("tenant-a", "organization-a", "folder-a", "v1", "repository-binding-a", "branch-ref-policy-a", "branch_ref_primary", ["branch_ref_feature"], ["branch_ref_release"], "principal-a", "correlation-policy-a", "task-a", "idempotency-policy-a", PayloadTenantId: null),
            Now);
        FolderState configuredState = boundState.Apply(configured.Events, streamName);
        FolderResult prepare = FolderAggregate.Handle(configuredState, FolderCommandFactory.PrepareWorkspace(), Now);

        return
        [
            .. created.Events,
            .. requested.Events,
            new RepositoryBound("tenant-a", "organization-a", "folder-a", "repository-binding-a", "provider-binding-a", "correlation-bound-a", "task-bound-a", "idempotency-bound-a", "fingerprint-bound-a", Now),
            .. configured.Events,
            .. prepare.Events,
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "organization-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared, DirtyResolution: null, OperationId: "workspace-a", "correlation-prepared-a", "task-a", "idempotency-workspace-outcome-a", "fingerprint-workspace-outcome-a", Now),
        ];
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
