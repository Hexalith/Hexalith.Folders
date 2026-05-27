using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.LoadTests.Scenarios;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.LoadTests.Tests;

public sealed class LifecycleCapacityHarnessTests
{
    [Fact]
    public async Task QuickLifecycleDriverShouldRunPrepareLockMutateAndCommit()
    {
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.Quick;
        LifecycleCapacityRunRecorder recorder = new();
        LifecycleCapacityDriver driver = new(profile.CreateIteration(1), recorder);

        FolderResultCode prepare = await driver.PrepareAsync(TestContext.Current.CancellationToken);
        FolderResultCode lockResult = await driver.AcquireLockAsync(TestContext.Current.CancellationToken);
        FolderResultCode mutation = await driver.MutateFileAsync(TestContext.Current.CancellationToken);
        FolderResultCode commit = await driver.CommitAsync(TestContext.Current.CancellationToken);

        prepare.ShouldBe(FolderResultCode.Accepted);
        lockResult.ShouldBe(FolderResultCode.Accepted);
        mutation.ShouldBe(FolderResultCode.Accepted);
        commit.ShouldBe(FolderResultCode.Accepted);
        recorder.TenantCount.ShouldBe(1);
        recorder.FolderCount.ShouldBe(1);
        recorder.WorkspaceCount.ShouldBe(1);
        recorder.TaskCount.ShouldBe(1);
        recorder.OperationCount.ShouldBe(4);
        recorder.IdempotencyKeyCount.ShouldBe(4);
        recorder.ResultCodes[FolderResultCode.Accepted.ToString()].ShouldBe(4);
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
        recorder.RecordOperation(iteration, iteration.CommitOperationId, iteration.CommitIdempotencyKey);
        recorder.RecordResult(FolderResultCode.Accepted);

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
        root.GetProperty("dimensions").GetProperty("tenant_count").GetInt32().ShouldBe(profile.TenantCount);
        root.GetProperty("load_simulations")[0].GetProperty("duration_seconds").GetDouble().ShouldBe(profile.Duration.TotalSeconds);
        root.GetProperty("load_simulations")[0].TryGetProperty("copies", out _).ShouldBeFalse();
        root.GetProperty("observed_counts").GetProperty("idempotency_key_count").GetInt32().ShouldBe(1);
        root.GetProperty("result_artifact_paths")[0].GetString().ShouldBe("lifecycle-capacity.md");
        json.ShouldNotContain(reportFolder, Case.Sensitive);
        json.ShouldNotContain("throughput", Case.Insensitive);
        json.ShouldNotContain("p95", Case.Insensitive);
        json.ShouldNotContain("raw file", Case.Insensitive);
    }

    [Fact]
    public void UnsupportedProfileAndInvalidOrdinalShouldFailFast()
    {
        Should.Throw<ArgumentException>(() => LifecycleCapacityProfile.FromName("release"));
        Should.Throw<ArgumentException>(() => SafeOrdinalId.Create("Tenant", 1));
        Should.Throw<ArgumentException>(() => SafeOrdinalId.Create("tenant_", 1));
        Should.Throw<ArgumentOutOfRangeException>(() => SafeOrdinalId.Create("tenant", 0));
    }
}
