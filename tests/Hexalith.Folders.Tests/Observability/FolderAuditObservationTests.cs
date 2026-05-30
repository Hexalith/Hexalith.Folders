using System.Diagnostics.Metrics;
using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Tests.Aggregates.Folder;

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

    [Theory]
    [MemberData(nameof(ForbiddenSentinelValues))]
    public void LifecycleSecurityOutputsShouldNotSerializeForbiddenSentinels(string sentinel)
    {
        object[] outputs =
        [
            FolderLifecycleReplayFixture.SuccessfulLifecycle(),
            FolderResult.Rejected(
                FolderResultCode.PathPolicyDenied,
                sentinel,
                sentinel,
                sentinel,
                sentinel,
                sentinel,
                sentinel,
                sentinel),
            new FolderAuditObservationBuilder
            {
                OperationKind = FolderAuditOperationKind.RestMutation,
                Result = FolderAuditResult.Denied,
                TenantId = sentinel,
                ActorReference = sentinel,
                TaskId = sentinel,
                OperationId = sentinel,
                CorrelationId = sentinel,
                FolderId = sentinel,
                WorkspaceId = sentinel,
                Timestamp = DateTimeOffset.Parse("2026-05-27T12:00:00Z"),
                RedactionState = FolderAuditRedactionState.Redacted,
                SanitizedCategory = sentinel,
            }.Build(),
        ];

        foreach (object output in outputs)
        {
            string serialized = JsonSerializer.Serialize(output);
            serialized.ShouldNotContain(sentinel, Case.Sensitive);
            (output.ToString() ?? string.Empty).ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    [Theory]
    [InlineData("authorization_denied")]
    [InlineData("folder_acl_denied")]
    [InlineData("path_policy_denied")]
    [InlineData("projection_stale")]
    [InlineData("lock_not_owned")]
    [InlineData("state_transition_invalid")]
    [InlineData("idempotency_conflict")]
    [InlineData("content_store_unavailable")]
    [InlineData("delete_order_unavailable")]
    [InlineData("provider_unavailable")]
    [InlineData("read_model_unavailable")]
    public void DeniedAuditObservationsShouldStayBoundedAndMetadataOnly(string category)
    {
        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.RestMutation,
            Result = FolderAuditResult.Denied,
            TenantId = "tenant-a",
            ActorReference = "actor_present",
            TaskId = "task-a",
            OperationId = "operation-a",
            CorrelationId = "correlation-a",
            FolderId = "folder-a",
            WorkspaceId = "workspace-a",
            Timestamp = DateTimeOffset.Parse("2026-05-27T12:00:00Z"),
            Duration = TimeSpan.FromMilliseconds(42),
            RedactionState = FolderAuditRedactionState.Redacted,
            SanitizedCategory = category,
            IsRetry = false,
            IsIdempotentReplay = false,
            IsDuplicate = false,
        }.AddClassification("result.category", category)
            .AddClassification("redaction.state", "metadata_only")
            .Build();

        observation.IsFailure.ShouldBeTrue();
        observation.SanitizedCategory.ShouldBe(category);
        observation.Classifications["result.category"].ShouldBe(category);
        string serialized = JsonSerializer.Serialize(observation);
        serialized.ShouldContain(category);
        serialized.ShouldNotContain("://", Case.Sensitive);
        serialized.ShouldNotContain("/", Case.Sensitive);
        serialized.ShouldNotContain("payload", Case.Insensitive);
        serialized.ShouldNotContain("secret", Case.Insensitive);
        observation.ToString().ShouldNotContain("folder-a", Case.Sensitive);
        observation.ToString().ShouldNotContain("workspace-a", Case.Sensitive);
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

            // Story 7.12 operational-signal tags: bounded categories, severities, and presence booleans only.
            FolderTelemetryNames.SignalTag,
            FolderTelemetryNames.SeverityTag,
            FolderTelemetryNames.StateSourceTag,
            FolderTelemetryNames.ThresholdExceededTag,
            FolderTelemetryNames.DomainTag,
            FolderTelemetryNames.ProviderFailureCategoryTag,
            FolderTelemetryNames.LockStateTag,
            FolderTelemetryNames.CleanupStatusTag,
            FolderTelemetryNames.ReasonCodeTag,
            FolderTelemetryNames.RetryEligibleTag,
        ];

        foreach (string rawIdentifierTag in rawIdentifierTags)
        {
            stableTags.ShouldNotContain(rawIdentifierTag);
        }
    }

    [Fact]
    public void OperationalSignalInstrumentsShouldEmitBoundedLowCardinalityTags()
    {
        List<KeyValuePair<string, object?>> captured = CaptureOperationalSignalTags(emitter =>
        {
            emitter.RecordProjectionLag(900, "status");
            emitter.RecordDeadLetterDepth("folders", 4);
            emitter.RecordProviderFailure(ProviderFailureCategory.ProviderUnavailable);
            emitter.RecordStaleLock("lock_expired");
            emitter.RecordCleanupFailure("cleanup_failed", "retry_exhausted", true);
        });

        captured.ShouldNotBeEmpty();

        captured.Where(tag => tag.Key == FolderTelemetryNames.SignalTag).Select(tag => (string?)tag.Value)
            .ShouldBe(
                [
                    FolderTelemetryNames.ProjectionLagSignal,
                    FolderTelemetryNames.DeadLetterDepthSignal,
                    FolderTelemetryNames.ProviderFailureSignal,
                    FolderTelemetryNames.StaleLockSignal,
                    FolderTelemetryNames.CleanupFailureSignal,
                ],
                ignoreOrder: true);

        // Projection lag of 900 ms exceeds the pinned C2 500 ms target.
        captured.Single(tag => tag.Key == FolderTelemetryNames.ThresholdExceededTag).Value.ShouldBe(true);
        captured.Single(tag => tag.Key == FolderTelemetryNames.DomainTag).Value.ShouldBe("folders");
        captured.Single(tag => tag.Key == FolderTelemetryNames.ProviderFailureCategoryTag).Value
            .ShouldBe(ProviderFailureCategory.ProviderUnavailable.ToCategoryCode());

        // Every severity stays inside the bounded log-level convention vocabulary.
        captured.Where(tag => tag.Key == FolderTelemetryNames.SeverityTag).Select(tag => (string?)tag.Value)
            .ShouldAllBe(severity => severity == FolderTelemetryNames.SeverityWarning || severity == FolderTelemetryNames.SeverityError);
    }

    [Theory]
    [MemberData(nameof(ForbiddenSentinelValues))]
    public void OperationalSignalTagsShouldNotLeakForbiddenSentinels(string sentinel)
    {
        List<KeyValuePair<string, object?>> captured = CaptureOperationalSignalTags(emitter =>
        {
            emitter.RecordProjectionLag(900, sentinel);
            emitter.RecordDeadLetterDepth(sentinel, 5);
            emitter.RecordStaleLock(sentinel);
            emitter.RecordCleanupFailure(sentinel, sentinel, false);
        });

        captured.ShouldNotBeEmpty();
        foreach (KeyValuePair<string, object?> tag in captured)
        {
            (tag.Value as string ?? string.Empty).ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    private static List<KeyValuePair<string, object?>> CaptureOperationalSignalTags(Action<FolderTelemetryEmitter> record)
    {
        List<KeyValuePair<string, object?>> captured = [];
        void Capture(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                captured.Add(tag);
            }
        }

        using MeterListener listener = new()
        {
            InstrumentPublished = static (instrument, activeListener) =>
            {
                if (string.Equals(instrument.Meter.Name, FolderTelemetryNames.MeterName, StringComparison.Ordinal))
                {
                    activeListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => Capture(tags));
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => Capture(tags));

        // Force instrument publication before Start so the listener enumerates the operational-signal instruments.
        FolderTelemetryEmitter emitter = new([], NullLogger<FolderTelemetryEmitter>.Instance);
        listener.Start();

        record(emitter);

        listener.Dispose();
        return captured;
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
