using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.LoadTests.Scenarios;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.LoadTests.Tests;

public sealed class LifecycleCapacityHarnessTests
{
    [Fact]
    public async Task QuickLifecycleDriverShouldRunPrepareLockMutateCommitAndStatus()
    {
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.Quick;
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityDriver driver = new(profile.CreateIteration(1), recorder);

        FolderResultCode prepare = await driver.PrepareAsync(TestContext.Current.CancellationToken);
        FolderResultCode lockResult = await driver.AcquireLockAsync(TestContext.Current.CancellationToken);
        FolderResultCode mutation = await driver.MutateFileAsync(TestContext.Current.CancellationToken);
        FolderResultCode commit = await driver.CommitAsync(TestContext.Current.CancellationToken);
        WorkspaceStatusQueryResultCode status = await driver.ReadStatusAsync(TestContext.Current.CancellationToken);

        prepare.ShouldBe(FolderResultCode.Accepted);
        lockResult.ShouldBe(FolderResultCode.Accepted);
        mutation.ShouldBe(FolderResultCode.Accepted);
        commit.ShouldBe(FolderResultCode.Accepted);
        status.ShouldBe(WorkspaceStatusQueryResultCode.Allowed);
        recorder.TenantCount.ShouldBe(1);
        recorder.FolderCount.ShouldBe(1);
        recorder.WorkspaceCount.ShouldBe(1);
        recorder.TaskCount.ShouldBe(1);
        recorder.OperationCount.ShouldBe(4);
        recorder.IdempotencyKeyCount.ShouldBe(4);
        recorder.ResultCodes[FolderResultCode.Accepted.ToString()].ShouldBe(4);
        recorder.ResultCodes[WorkspaceStatusQueryResultCode.Allowed.ToString()].ShouldBe(1);
        recorder.MeasuredSteps.ShouldBe(
            [
                LifecycleCapacityScenario.LockStepName,
                LifecycleCapacityScenario.CommitStepName,
                LifecycleCapacityScenario.MutateStepName,
                LifecycleCapacityScenario.PrepareStepName,
                LifecycleCapacityScenario.StatusStepName,
            ],
            ignoreOrder: true);
        recorder.ObservedStepCounts[LifecycleCapacityScenario.StatusStepName].ShouldBe(1);
    }

    [Fact]
    public void QuickProfileShouldGenerateSafeTenantScopedIdentifiersAndUniqueMutatingKeys()
    {
        LifecycleCapacityIteration first = LifecycleCapacityProfile.Quick.CreateIteration(1);
        LifecycleCapacityIteration second = LifecycleCapacityProfile.Quick.CreateIteration(2);

        first.TenantId.ShouldNotBe(second.TenantId);
        first.FolderId.ShouldBe(second.FolderId);
        first.WorkspaceId.ShouldBe(second.WorkspaceId);
        SafeOrdinalId.IsSafe(first.TenantId).ShouldBeTrue();
        SafeOrdinalId.IsSafe(first.FolderId).ShouldBeTrue();
        SafeOrdinalId.IsSafe(first.WorkspaceId).ShouldBeTrue();
        SafeOrdinalId.IsSafe(first.TaskId).ShouldBeTrue();
        SafeOrdinalId.IsSafe(first.PrepareIdempotencyKey).ShouldBeTrue();

        string[] mutatingKeys =
        [
            first.PrepareIdempotencyKey,
            first.LockIdempotencyKey,
            first.MutationIdempotencyKey,
            first.CommitIdempotencyKey
        ];
        mutatingKeys.Distinct(StringComparer.Ordinal).Count().ShouldBe(mutatingKeys.Length);
    }

    [Fact]
    public void RecorderShouldCountOverlappingIdentifiersByTenantScope()
    {
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityIteration first = LifecycleCapacityProfile.Quick.CreateIteration(1);
        LifecycleCapacityIteration second = LifecycleCapacityProfile.Quick.CreateIteration(2);

        recorder.RecordIteration(first);
        recorder.RecordIteration(second);
        recorder.RecordOperation(first, "operation-0001", "idempotency-0001");
        recorder.RecordOperation(second, "operation-0001", "idempotency-0001");

        recorder.TenantCount.ShouldBe(2);
        recorder.FolderCount.ShouldBe(2);
        recorder.WorkspaceCount.ShouldBe(2);
        recorder.TaskCount.ShouldBe(2);
        recorder.OperationCount.ShouldBe(2);
        recorder.IdempotencyKeyCount.ShouldBe(2);
    }

    [Fact]
    public void EvidenceWriterShouldEmitReferencePendingMetadataOnlyShape()
    {
        string reportFolder = Path.Combine(Path.GetTempPath(), $"folders-load-evidence-{Guid.NewGuid():N}");
        string absoluteReportPath = Path.Combine(reportFolder, "lifecycle-capacity.md");
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.Quick;
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityIteration iteration = profile.CreateIteration(1);
        recorder.RecordIteration(iteration);
        recorder.RecordMeasuredStep(LifecycleCapacityScenario.StatusStepName);
        recorder.RecordOperation(iteration, iteration.CommitOperationId, iteration.CommitIdempotencyKey);
        recorder.RecordResult(FolderResultCode.Accepted);
        recorder.RecordResult(WorkspaceStatusQueryResultCode.Allowed.ToString());

        string evidencePath = LifecycleCapacityEvidenceWriter.Write(
            reportFolder,
            profile,
            recorder,
            [LifecycleCapacityScenario.FullLifecycleScenarioName],
            [absoluteReportPath],
            "run-0001",
            new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero));

        string json = File.ReadAllText(evidencePath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.GetProperty("run_id").GetString().ShouldBe("run-0001");
        root.GetProperty("profile_name").GetString().ShouldBe("quick");
        root.GetProperty("thresholds").GetString().ShouldBe("reference_pending");
        root.GetProperty("measured_steps")[0].GetString().ShouldBe(LifecycleCapacityScenario.StatusStepName);
        root.GetProperty("observed_step_counts").GetProperty(LifecycleCapacityScenario.StatusStepName).GetInt32().ShouldBe(1);
        root.GetProperty("dimensions").GetProperty("tenant_count").GetInt32().ShouldBe(profile.TenantCount);
        root.GetProperty("load_simulations")[0].GetProperty("duration_seconds").GetDouble().ShouldBe(profile.Duration.TotalSeconds);
        root.GetProperty("load_simulations")[0].TryGetProperty("copies", out _).ShouldBeFalse();
        root.GetProperty("observed_counts").GetProperty("idempotency_key_count").GetInt32().ShouldBe(1);
        root.GetProperty("result_codes").GetProperty(WorkspaceStatusQueryResultCode.Allowed.ToString()).GetInt32().ShouldBe(1);
        root.GetProperty("result_artifact_paths")[0].GetString().ShouldBe("lifecycle-capacity.md");
        json.ShouldNotContain(reportFolder, Case.Sensitive);
        json.ShouldNotContain("throughput", Case.Insensitive);
        json.ShouldNotContain("p95", Case.Insensitive);
        json.ShouldNotContain("raw file", Case.Insensitive);
    }

    [Fact]
    public void EvidenceWriterShouldEmitCompleteMeasuredStepInventoryForSuccessfulSmoke()
    {
        string reportFolder = Path.Combine(Path.GetTempPath(), $"folders-load-complete-evidence-{Guid.NewGuid():N}");
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.Quick;
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityIteration iteration = profile.CreateIteration(1);
        recorder.RecordIteration(iteration);

        foreach (string step in RequiredMeasuredSteps())
        {
            recorder.RecordMeasuredStep(step);
        }

        recorder.RecordOperation(iteration, iteration.PrepareOperationId, iteration.PrepareIdempotencyKey);
        recorder.RecordOperation(iteration, iteration.LockOperationId, iteration.LockIdempotencyKey);
        recorder.RecordOperation(iteration, iteration.MutationOperationId, iteration.MutationIdempotencyKey);
        recorder.RecordOperation(iteration, iteration.CommitOperationId, iteration.CommitIdempotencyKey);
        recorder.RecordResult(FolderResultCode.Accepted);
        recorder.RecordResult(FolderResultCode.Accepted);
        recorder.RecordResult(FolderResultCode.Accepted);
        recorder.RecordResult(FolderResultCode.Accepted);
        recorder.RecordResult(WorkspaceStatusQueryResultCode.Allowed.ToString());

        string evidencePath = LifecycleCapacityEvidenceWriter.Write(
            reportFolder,
            profile,
            recorder,
            [LifecycleCapacityScenario.FullLifecycleScenarioName],
            [Path.Combine(reportFolder, "capacity-smoke-ci.md"), "reports\\capacity-smoke-ci.txt"],
            "capacity-smoke-ci",
            new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement root = document.RootElement;

        root.GetProperty("measured_steps")
            .EnumerateArray()
            .Select(static step => step.GetString().ShouldNotBeNull())
            .ToArray()
            .ShouldBe(RequiredMeasuredSteps(), ignoreOrder: true);

        JsonElement observedStepCounts = root.GetProperty("observed_step_counts");
        foreach (string step in RequiredMeasuredSteps())
        {
            observedStepCounts.GetProperty(step).GetInt32().ShouldBe(1);
        }

        root.GetProperty("observed_counts").GetProperty("operation_count").GetInt32().ShouldBe(4);
        root.GetProperty("observed_counts").GetProperty("idempotency_key_count").GetInt32().ShouldBe(4);
        root.GetProperty("result_codes").GetProperty(FolderResultCode.Accepted.ToString()).GetInt32().ShouldBe(4);
        root.GetProperty("result_codes").GetProperty(WorkspaceStatusQueryResultCode.Allowed.ToString()).GetInt32().ShouldBe(1);
        root.GetProperty("result_artifact_paths")[0].GetString().ShouldBe("capacity-smoke-ci.md");
        root.GetProperty("result_artifact_paths")[1].GetString().ShouldBe("reports/capacity-smoke-ci.txt");
    }

    [Fact]
    public void EvidenceWriterShouldPreservePartialExecutionSignalsForGateFailure()
    {
        string reportFolder = Path.Combine(Path.GetTempPath(), $"folders-load-partial-evidence-{Guid.NewGuid():N}");
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.Quick;
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityIteration iteration = profile.CreateIteration(1);
        recorder.RecordIteration(iteration);
        recorder.RecordMeasuredStep(LifecycleCapacityScenario.PrepareStepName);
        recorder.RecordOperation(iteration, iteration.PrepareOperationId, iteration.PrepareIdempotencyKey);
        recorder.RecordResult(FolderResultCode.Accepted);

        string evidencePath = LifecycleCapacityEvidenceWriter.Write(
            reportFolder,
            profile,
            recorder,
            [LifecycleCapacityScenario.FullLifecycleScenarioName],
            ["capacity-smoke-ci.md"],
            "partial-smoke",
            new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement root = document.RootElement;

        string[] measuredSteps = root.GetProperty("measured_steps")
            .EnumerateArray()
            .Select(static step => step.GetString().ShouldNotBeNull())
            .ToArray();
        measuredSteps.ShouldBe([LifecycleCapacityScenario.PrepareStepName]);
        measuredSteps.ShouldNotContain(LifecycleCapacityScenario.StatusStepName);

        JsonElement observedStepCounts = root.GetProperty("observed_step_counts");
        observedStepCounts.GetProperty(LifecycleCapacityScenario.PrepareStepName).GetInt32().ShouldBe(1);
        observedStepCounts.TryGetProperty(LifecycleCapacityScenario.StatusStepName, out _).ShouldBeFalse();
        root.GetProperty("result_codes").TryGetProperty(WorkspaceStatusQueryResultCode.Allowed.ToString(), out _).ShouldBeFalse();
    }

    [Fact]
    public void UnsupportedProfileAndInvalidOrdinalShouldFailFast()
    {
        Should.Throw<ArgumentException>(() => LifecycleCapacityProfile.FromName("release"));
        Should.Throw<ArgumentException>(() => SafeOrdinalId.Create("Tenant", 1));
        Should.Throw<ArgumentException>(() => SafeOrdinalId.Create("tenant_", 1));
        Should.Throw<ArgumentOutOfRangeException>(() => SafeOrdinalId.Create("tenant", 0));
    }

    private static string[] RequiredMeasuredSteps()
        =>
        [
            LifecycleCapacityScenario.PrepareStepName,
            LifecycleCapacityScenario.LockStepName,
            LifecycleCapacityScenario.MutateStepName,
            LifecycleCapacityScenario.CommitStepName,
            LifecycleCapacityScenario.StatusStepName,
        ];
}
