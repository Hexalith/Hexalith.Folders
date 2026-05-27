using System.Text.Json;

using Hexalith.Folders.Observability;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Observability;

public sealed class FolderAuditObservationTests
{
    [Fact]
    public void ApprovedMetadataFieldsShouldBePreserved()
    {
        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.RestMutation,
            Result = FolderAuditResult.Success,
            TenantId = "tenant-a",
            ActorReference = "principal-a",
            TaskId = "task-a",
            OperationId = "operation-a",
            CorrelationId = "correlation-a",
            FolderId = "folder-a",
            WorkspaceId = "workspace-a",
            ProviderReference = "provider-a",
            Timestamp = DateTimeOffset.Parse("2026-05-27T12:00:00Z"),
            Duration = TimeSpan.FromMilliseconds(42.125),
            RedactionState = FolderAuditRedactionState.MetadataOnly,
            StateTransition = "pending->ready",
            SanitizedCategory = "operation_completed",
            IsRetry = false,
            IsIdempotentReplay = false,
            IsDuplicate = false,
        }.AddClassification("provider.kind", "github")
            .Build();

        observation.TenantId.ShouldBe("tenant-a");
        observation.ActorReference.ShouldBe("principal-a");
        observation.TaskId.ShouldBe("task-a");
        observation.OperationId.ShouldBe("operation-a");
        observation.CorrelationId.ShouldBe("correlation-a");
        observation.FolderId.ShouldBe("folder-a");
        observation.WorkspaceId.ShouldBe("workspace-a");
        observation.ProviderReference.ShouldBe("provider-a");
        observation.StateTransition.ShouldBe("pending->ready");
        observation.SanitizedCategory.ShouldBe("operation_completed");
        observation.DurationEvidence.ShouldBe("42.125");
        observation.Classifications["provider.kind"].ShouldBe("github");
    }

    [Theory]
    [MemberData(nameof(ForbiddenSentinelValues))]
    public void ForbiddenSentinelValuesShouldNotSurviveObservationSanitization(string sentinel)
    {
        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.RestQuery,
            Result = FolderAuditResult.Failed,
            TenantId = sentinel,
            ActorReference = sentinel,
            TaskId = sentinel,
            OperationId = sentinel,
            CorrelationId = sentinel,
            FolderId = sentinel,
            WorkspaceId = sentinel,
            ProviderReference = sentinel,
            Timestamp = DateTimeOffset.Parse("2026-05-27T12:00:00Z"),
            Duration = TimeSpan.FromMilliseconds(-5),
            RedactionState = FolderAuditRedactionState.Redacted,
            StateTransition = sentinel,
            SanitizedCategory = sentinel,
            IsRetry = true,
            IsIdempotentReplay = true,
            IsDuplicate = true,
        }.AddClassification(sentinel, sentinel)
            .Build();

        string serialized = JsonSerializer.Serialize(observation);
        serialized.ShouldNotContain(sentinel, Case.Sensitive);
        observation.ToString().ShouldNotContain(sentinel, Case.Sensitive);
        observation.Duration.ShouldBe(TimeSpan.Zero);
        observation.DurationEvidence.ShouldBe("0ms");
    }

    [Fact]
    public void RedactedDurationEvidenceShouldUseHundredMillisecondBuckets()
    {
        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            TenantId = "tenant-a",
            Duration = TimeSpan.FromMilliseconds(101),
            RedactionState = FolderAuditRedactionState.Redacted,
        }.Build();

        observation.DurationEvidence.ShouldBe("200ms");
    }

    [Fact]
    public void MetricAndTraceTagNamesShouldStayLowCardinality()
    {
        string[] rawIdentifierTags =
        [
            "folders.tenant_id",
            "folders.actor_reference",
            "folders.correlation_id",
            "folders.task_id",
            "folders.folder_id",
            "folders.workspace_id",
            "folders.provider_reference",
        ];

        string[] stableTags =
        [
            FolderTelemetryNames.OperationKindTag,
            FolderTelemetryNames.ResultTag,
            FolderTelemetryNames.CategoryTag,
            FolderTelemetryNames.RedactionStateTag,
            FolderTelemetryNames.CorrelationPresentTag,
            FolderTelemetryNames.TaskPresentTag,
            FolderTelemetryNames.TenantPresentTag,
            FolderTelemetryNames.ActorReferencePresentTag,
        ];

        foreach (string rawIdentifierTag in rawIdentifierTags)
        {
            stableTags.ShouldNotContain(rawIdentifierTag);
        }
    }

    [Fact]
    public async Task TelemetryEmitterShouldNotLetObserverFailureBreakOperationFlow()
    {
        InMemoryFolderAuditObserver recordingObserver = new();
        FolderTelemetryEmitter emitter = new(
            [new ThrowingFolderAuditObserver(), recordingObserver],
            NullLogger<FolderTelemetryEmitter>.Instance);

        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.RestMutation,
            Result = FolderAuditResult.Success,
            TenantId = "tenant-a",
            CorrelationId = "correlation-a",
            SanitizedCategory = "operation_completed",
        }.Build();

        await Should.NotThrowAsync(async () =>
            await emitter.EmitAsync(observation, TestContext.Current.CancellationToken).ConfigureAwait(false));
        recordingObserver.Observations.ShouldHaveSingleItem();
    }

    public static TheoryData<string> ForbiddenSentinelValues()
    {
        TheoryData<string> values = [];
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(AuditLeakageCorpusPath()));
        foreach (JsonElement sample in document.RootElement.GetProperty("sentinel_samples").EnumerateArray())
        {
            if (string.Equals(sample.GetProperty("classification").GetString(), "safe-provenance", StringComparison.Ordinal))
            {
                continue;
            }

            values.Add(sample.GetProperty("value").GetString() ?? throw new InvalidOperationException("Missing sentinel value."));
        }

        return values;
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

        throw new InvalidOperationException("Unable to locate tests/fixtures/audit-leakage-corpus.json.");
    }

    private sealed class ThrowingFolderAuditObserver : IFolderAuditObserver
    {
        public ValueTask ObserveAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("observer_unavailable");
    }
}
