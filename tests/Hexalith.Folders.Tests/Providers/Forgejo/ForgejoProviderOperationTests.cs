using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Providers.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoProviderOperationTests
{
    private const string SafeFingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OperationReference = "operation-01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private static readonly DateTimeOffset OperationNow = DateTimeOffset.Parse("2026-09-06T12:00:00+00:00");

    [Fact]
    public async Task PublicStageRecordsImmutableEvidenceAndDisposesProtectedResources()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        ForgejoProvider provider = Provider(source, client, store);

        ProviderFileMutationResult result = await provider.StageFileChangesAsync(
            FileMutationRequest(source.FileMutationSource),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        source.FileMutationCalls.ShouldBe(1);
        client.StageCalls.ShouldBe(1);
        store.ValidateCalls.ShouldBe(1);
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.StagedChangeSet);
        client.DisposeCalls.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("docs/", Case.Sensitive);
    }

    [Fact]
    public async Task StageRejectsEmptyAndDuplicateDeclarationsBeforeProtectedAccess()
    {
        foreach (bool duplicate in new[] { false, true })
        {
            OperationSourceResolver source = new();
            RecordingForgejoOperationClient client = new();
            RecordingForgejoCredentialResolver credentials = new();
            ProviderFileMutationRequest valid = FileMutationRequest(source.FileMutationSource);
            ProviderFileMutationRequest request = duplicate
                ? valid with
                {
                    Changes =
                    [
                        valid.Changes[0],
                        valid.Changes[1] with { PathReference = valid.Changes[0].PathReference },
                    ],
                }
                : valid with { Changes = [] };

            ProviderFileMutationResult result = await Provider(
                source,
                client,
                RecordingProviderOperationOutcomeStore.Acquired(),
                credentials).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            result.ReasonCode.ShouldBe("forgejo_change_set_malformed");
            source.FileMutationCalls.ShouldBe(0);
            credentials.Calls.ShouldBe(0);
            client.StageCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task StageRejectsAncestorConflictsAndPolicyOversizeBeforeCredentialOrHttpAccess()
    {
        ProviderResolvedFileChange[][] invalidChanges =
        [
            [
                new(0, ProviderFileChangeKind.Add, "docs", "a"u8.ToArray(), ProviderFileContentType.RegularFile),
                new(1, ProviderFileChangeKind.Remove, "docs/child.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile, "4444444444444444444444444444444444444444"),
            ],
            [
                new(0, ProviderFileChangeKind.Add, "docs/large.txt", new byte[1025], ProviderFileContentType.RegularFile),
                new(1, ProviderFileChangeKind.Remove, "docs/remove.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile, "4444444444444444444444444444444444444444"),
            ],
        ];

        foreach (ProviderResolvedFileChange[] changes in invalidChanges)
        {
            OperationSourceResolver source = new(fileMutationSource: new(OperationSourceResolver.Target(), changes));
            RecordingForgejoOperationClient client = new();
            RecordingForgejoCredentialResolver credentials = new();

            ProviderFileMutationResult result = await Provider(
                source,
                client,
                RecordingProviderOperationOutcomeStore.Acquired(),
                credentials).StageFileChangesAsync(
                    FileMutationRequest(source.FileMutationSource),
                    TestContext.Current.CancellationToken);

            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            result.ReasonCode.ShouldBe("forgejo_file_mutation_source_malformed");
            source.FileMutationCalls.ShouldBe(1);
            credentials.Calls.ShouldBe(0);
            client.StageCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task PublicCommitRecordsCreatedAndConfirmedEvidenceWithoutExposingObjectIds()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        ForgejoProvider provider = Provider(source, client, store);

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        client.CommitCalls.ShouldBe(1);
        store.ValidateCalls.ShouldBe(1);
        store.Records.Select(static record => record.Kind).ShouldBe(
            [ProviderOperationOutcomeKind.CreatedCommit, ProviderOperationOutcomeKind.RefUpdateConfirmed]);
        client.DisposeCalls.ShouldBe(1);
        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain(OperationSourceResolver.CommitSha, Case.Sensitive);
        serialized.ShouldNotContain(OperationSourceResolver.HeadSha, Case.Sensitive);
    }

    [Fact]
    public async Task AmbiguousCommitIsRecordedUnknownAndCannotAuthorizeRedispatch()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = ForgejoCommitResult.Failure(ForgejoApiFailureCondition.AmbiguousMutationResponse),
        };
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderCommitResult result = await Provider(source, client, store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReconciliationReference.ShouldBe(OperationReference);
        result.Retryable.ShouldBeFalse();
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.Unknown);
        client.CommitCalls.ShouldBe(1);
    }

    [Fact]
    public async Task PreDispatchRateLimitIsRecordedAsKnownAndRemainsRetryable()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = ForgejoCommitResult.Failure(
                ForgejoApiFailureCondition.RateLimit,
                TimeSpan.FromSeconds(30)),
        };
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderCommitResult result = await Provider(source, client, store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderRateLimited);
        result.ReasonCode.ShouldBe("forgejo_rate_limited");
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.KnownTerminalFailure);
    }

    [Fact]
    public async Task PreDispatchValidationFailureIsAClosedTerminalOutcome()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ValidationFailure),
        };
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderCommitResult result = await Provider(source, client, store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("forgejo_validation_failed");
        result.ReconciliationReference.ShouldBeNull();
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.KnownTerminalFailure);
    }

    [Fact]
    public async Task EquivalentReplayReturnsBeforeSourceCredentialAndTransportAccess()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        ProviderFileMutationRequest request = FileMutationRequest(source.FileMutationSource) with
        {
            IdempotencyAdmission = new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Success),
        };
        ForgejoProvider provider = new(
            credentials,
            new RecordingForgejoOperationClientFactory(client),
            new UnconfiguredProviderRepositoryTargetResolver(),
            source,
            RecordingProviderOperationOutcomeStore.Acquired(),
            new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1)));

        ProviderFileMutationResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentReplay.ShouldBeTrue();
        source.FileMutationCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.StageCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderPriorOutcomeDisposition.Success, ProviderFailureCategory.None, "existing_equivalent")]
    [InlineData(ProviderPriorOutcomeDisposition.KnownFailure, ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited")]
    [InlineData(ProviderPriorOutcomeDisposition.Unknown, ProviderFailureCategory.UnknownProviderOutcome, "forgejo_file_mutation_outcome_unknown")]
    public async Task EquivalentReplayMatrixReturnsExactDurableTupleWithoutDuplicateEffects(
        ProviderPriorOutcomeDisposition prior,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        ProviderIdempotencyAdmission admission = prior switch
        {
            ProviderPriorOutcomeDisposition.Success => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior),
            ProviderPriorOutcomeDisposition.KnownFailure => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior,
                PriorFailureCategory: ProviderFailureCategory.ProviderRateLimited,
                PriorReasonCode: "forgejo_rate_limited",
                PriorRemediationCode: "provider_rate_limited_remediation",
                PriorRetryable: true,
                PriorRetryAfter: TimeSpan.FromSeconds(30)),
            _ => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorReconciliationReference: $"{OperationReference}-reconciliation",
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior),
        };
        ProviderFileMutationRequest request = FileMutationRequest(source.FileMutationSource) with
        {
            IdempotencyAdmission = admission,
        };

        ProviderFileMutationResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired(),
            credentials).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.EquivalentReplay.ShouldBeTrue();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        source.FileMutationCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.StageCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderIdempotencyDisposition.Conflict, "idempotency_conflict")]
    [InlineData(ProviderIdempotencyDisposition.Expired, "idempotency_key_expired")]
    public async Task ConflictAndExpiredAdmissionsFailBeforeDuplicateEffects(
        ProviderIdempotencyDisposition disposition,
        string expectedReason)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        ProviderFileMutationRequest request = FileMutationRequest(source.FileMutationSource) with
        {
            IdempotencyAdmission = new(disposition, "safe-intent-reference"),
        };

        ProviderFileMutationResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired(),
            credentials).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe(expectedReason);
        source.FileMutationCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.StageCalls.ShouldBe(0);
    }

    [Fact]
    public async Task PendingReservationReturnsUnknownBeforeCredentialOrDuplicateEffect()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.WithReservations(
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.Pending,
                OperationReference,
                Generation: 1));

        ProviderFileMutationResult result = await Provider(source, client, store, credentials).StageFileChangesAsync(
            FileMutationRequest(source.FileMutationSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("forgejo_operation_pending");
        result.ReconciliationReference.ShouldBe(OperationReference);
        source.FileMutationCalls.ShouldBe(1);
        credentials.Calls.ShouldBe(0);
        client.StageCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderPriorOutcomeDisposition.Success, ProviderFailureCategory.None, "existing_equivalent")]
    [InlineData(ProviderPriorOutcomeDisposition.KnownFailure, ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited")]
    [InlineData(ProviderPriorOutcomeDisposition.Unknown, ProviderFailureCategory.UnknownProviderOutcome, "forgejo_commit_outcome_unknown")]
    public async Task CommitEquivalentReplayReturnsDurableTupleBeforeEveryProtectedCollaborator(
        ProviderPriorOutcomeDisposition prior,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderIdempotencyAdmission admission = prior switch
        {
            ProviderPriorOutcomeDisposition.Success => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior),
            ProviderPriorOutcomeDisposition.KnownFailure => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior,
                PriorFailureCategory: ProviderFailureCategory.ProviderRateLimited,
                PriorReasonCode: "forgejo_rate_limited",
                PriorRemediationCode: "provider_rate_limited_remediation",
                PriorRetryable: true,
                PriorRetryAfter: TimeSpan.FromSeconds(30)),
            _ => new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorReconciliationReference: $"{OperationReference}-reconciliation",
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: prior),
        };
        ProviderCommitRequest request = CommitRequest(source.CommitSource) with { IdempotencyAdmission = admission };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            request,
            TestContext.Current.CancellationToken);

        result.EquivalentReplay.ShouldBeTrue();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        source.CommitCalls.ShouldBe(0);
        store.ReserveCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.CommitCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderIdempotencyDisposition.Conflict, "idempotency_conflict")]
    [InlineData(ProviderIdempotencyDisposition.Expired, "idempotency_key_expired")]
    public async Task CommitConflictAndExpiryStopBeforeEveryProtectedCollaborator(
        ProviderIdempotencyDisposition disposition,
        string expectedReason)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderCommitRequest request = CommitRequest(source.CommitSource) with
        {
            IdempotencyAdmission = new(disposition, "safe-intent-reference"),
        };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe(expectedReason);
        source.CommitCalls.ShouldBe(0);
        store.ReserveCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CommitPendingReservationReturnsUnknownWithoutCredentialClientOrTransportAccess()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.WithReservations(
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.Pending,
                OperationReference,
                Generation: 1));

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("forgejo_operation_pending");
        result.ReconciliationReference.ShouldBe(OperationReference);
        source.CommitCalls.ShouldBe(1);
        store.ReserveCalls.ShouldBe(1);
        credentials.Calls.ShouldBe(0);
        client.CommitCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatedCommitRecordingRejectionOrExceptionReturnsMetadataOnlyUnknown(bool throwOnRecord)
    {
        OperationSourceResolver source = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired(
            recordResult: false,
            throwOnRecord: throwOnRecord);

        ProviderCommitResult result = await Provider(source, new RecordingForgejoOperationClient(), store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        AssertCommitUnknown(result, "forgejo_outcome_recording_failed");
        store.ValidateCalls.ShouldBe(1);
        store.Records.ShouldNotContain(static record => record.Kind == ProviderOperationOutcomeKind.RefUpdateConfirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownFailureRecordingRejectionOrExceptionReturnsMetadataOnlyUnknown(bool throwOnRecord)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ValidationFailure),
        };
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired(
            recordResult: false,
            throwOnRecord: throwOnRecord);

        ProviderCommitResult result = await Provider(source, client, store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        AssertCommitUnknown(result, "forgejo_outcome_recording_failed");
        store.Records.ShouldNotContain(static record => record.Kind == ProviderOperationOutcomeKind.RefUpdateConfirmed);
    }

    [Fact]
    public async Task FinalConfirmationRecordingRejectionAfterCreatedCommitReturnsMetadataOnlyUnknown()
    {
        OperationSourceResolver source = new();
        SequencedOutcomeStore store = new(true, false, true);

        ProviderCommitResult result = await new ForgejoProvider(
            new RecordingForgejoCredentialResolver(),
            new RecordingForgejoOperationClientFactory(new RecordingForgejoOperationClient()),
            new UnconfiguredProviderRepositoryTargetResolver(),
            source,
            store,
            new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1))).CommitAsync(
                CommitRequest(source.CommitSource),
                TestContext.Current.CancellationToken);

        AssertCommitUnknown(result, "forgejo_outcome_recording_failed");
        store.Records.Select(static record => record.Kind).ShouldBe(
            [ProviderOperationOutcomeKind.CreatedCommit, ProviderOperationOutcomeKind.RefUpdateConfirmed, ProviderOperationOutcomeKind.Unknown]);
    }

    [Theory]
    [InlineData("CancellationBeforeDispatch", ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch", true)]
    [InlineData("ReservationInvalidated", ProviderFailureCategory.ProviderConflict, "forgejo_operation_reservation_invalidated", false)]
    public async Task CommitNoDispatchConditionsFinalizeWithoutTerminalKnownFailure(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason,
        bool expectedRetryable)
    {
        ForgejoApiFailureCondition condition = Enum.Parse<ForgejoApiFailureCondition>(conditionName);
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = condition == ForgejoApiFailureCondition.CancellationBeforeDispatch
                ? ForgejoCommitResult.Failure(condition)
                : ForgejoCommitResult.Success(OperationSourceResolver.CommitSha),
        };
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired(
            validationResult: condition != ForgejoApiFailureCondition.ReservationInvalidated);

        ProviderCommitResult result = await Provider(source, client, store).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.Retryable.ShouldBe(expectedRetryable);
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        store.FinalizeCalls.ShouldBe(1);
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        store.Records.ShouldNotContain(static record => record.Kind == ProviderOperationOutcomeKind.KnownTerminalFailure);
    }

    [Fact]
    public async Task RepositoryArchivedMapsToStableProviderConflictWithoutProviderPayload()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            CommitResult = ForgejoCommitResult.Failure(ForgejoApiFailureCondition.RepositoryArchived),
        };

        ProviderCommitResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired()).CommitAsync(
                CommitRequest(source.CommitSource),
                TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe("forgejo_repository_archived");
        result.Retryable.ShouldBeFalse();
        JsonSerializer.Serialize(result).ShouldNotContain("423", Case.Sensitive);
    }

    [Fact]
    public async Task CredentialResolutionRejectsContradictorySuccessAndFinalizesNoDispatch()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingForgejoCredentialResolver credentials = new()
        {
            Result = new ForgejoCredentialResolutionResult(
                true,
                ForgejoCredentialLease.CreateForTesting("provider-secret"),
                ProviderFailureCategory.ProviderUnavailable,
                "forgejo_server_unavailable",
                TimeSpan.FromSeconds(30)),
        };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        result.ReasonCode.ShouldBe("forgejo_credential_resolution_unavailable");
        result.RetryAfter.ShouldBeNull();
        client.CommitCalls.ShouldBe(0);
        store.FinalizeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CredentialResolutionPreservesSafeRateLimitDelayInNoDispatchResult()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingForgejoCredentialResolver credentials = new()
        {
            Result = ForgejoCredentialResolutionResult.Failure(
                ProviderFailureCategory.ProviderRateLimited,
                "forgejo_rate_limited",
                TimeSpan.FromSeconds(30)),
        };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderRateLimited);
        result.ReasonCode.ShouldBe("forgejo_rate_limited");
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        store.FinalizeCalls.ShouldBe(1);
        store.Records.ShouldHaveSingleItem().RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        client.CommitCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData("ProviderConfigurationMissing", "provider_credential_reference_missing")]
    [InlineData("ProviderPermissionInsufficient", "provider_credential_reference_denied")]
    [InlineData("ProviderValidationFailed", "provider_credential_secret_malformed")]
    [InlineData("ProviderUnavailable", "provider_credential_store_unavailable")]
    public async Task CredentialResolutionPreservesCoherentProductionFailureTuples(
        string categoryName,
        string reasonCode)
    {
        ProviderFailureCategory category = Enum.Parse<ProviderFailureCategory>(categoryName);
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingForgejoCredentialResolver credentials = new()
        {
            Result = ForgejoCredentialResolutionResult.Failure(category, reasonCode),
        };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(category);
        result.ReasonCode.ShouldBe(reasonCode);
        client.CommitCalls.ShouldBe(0);
        store.FinalizeCalls.ShouldBe(1);
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
    }

    [Fact]
    public async Task CredentialResolutionCancellationIsRetrySafeAndFinalizesNoDispatch()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingForgejoCredentialResolver credentials = new() { Exception = new OperationCanceledException() };

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderTransientFailure);
        result.ReasonCode.ShouldBe("forgejo_operation_cancelled_before_dispatch");
        result.Retryable.ShouldBeTrue();
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        store.FinalizeCalls.ShouldBe(1);
        store.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        client.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ContradictoryKnownFailureAdmissionAndReservationTuplesFailClosed()
    {
        OperationSourceResolver admissionSource = new();
        RecordingForgejoCredentialResolver admissionCredentials = new();
        RecordingForgejoOperationClient admissionClient = new();
        RecordingProviderOperationOutcomeStore admissionStore = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderCommitRequest malformedAdmission = CommitRequest(admissionSource.CommitSource) with
        {
            IdempotencyAdmission = new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                PriorSafeOutcomeFingerprint: SafeFingerprint,
                PriorOperationReference: OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.KnownFailure,
                PriorFailureCategory: ProviderFailureCategory.ProviderRateLimited,
                PriorReasonCode: "forgejo_validation_failed",
                PriorRemediationCode: "provider_validation_failed_remediation",
                PriorRetryable: false),
        };

        ProviderCommitResult admissionResult = await Provider(
            admissionSource,
            admissionClient,
            admissionStore,
            admissionCredentials).CommitAsync(malformedAdmission, TestContext.Current.CancellationToken);

        admissionResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        admissionResult.ReasonCode.ShouldBe("forgejo_commit_intent_malformed");
        admissionSource.CommitCalls.ShouldBe(0);
        admissionStore.ReserveCalls.ShouldBe(0);
        admissionCredentials.Calls.ShouldBe(0);
        admissionClient.CommitCalls.ShouldBe(0);

        OperationSourceResolver reservationSource = new();
        RecordingForgejoCredentialResolver reservationCredentials = new();
        RecordingForgejoOperationClient reservationClient = new();
        RecordingProviderOperationOutcomeStore reservationStore = RecordingProviderOperationOutcomeStore.WithReservations(
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.ReplayKnownFailure,
                OperationReference,
                Generation: 0,
                SafeOutcomeFingerprint: SafeFingerprint,
                FailureCategory: ProviderFailureCategory.ProviderRateLimited,
                ReasonCode: "forgejo_validation_failed",
                RemediationCode: "provider_validation_failed_remediation",
                Retryable: false));

        ProviderCommitResult reservationResult = await Provider(
            reservationSource,
            reservationClient,
            reservationStore,
            reservationCredentials).CommitAsync(
                CommitRequest(reservationSource.CommitSource),
                TestContext.Current.CancellationToken);

        reservationResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        reservationResult.ReasonCode.ShouldBe("forgejo_operation_outcome_store_unavailable");
        reservationSource.CommitCalls.ShouldBe(1);
        reservationStore.ReserveCalls.ShouldBe(1);
        reservationCredentials.Calls.ShouldBe(0);
        reservationClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StatusSupportsUnknownCommitIdentityAndConfirmsAtTheFirstCheck()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        ForgejoProvider provider = Provider(source, client, RecordingProviderOperationOutcomeStore.Acquired());

        ProviderOperationStatusResult confirmed = await provider.GetOperationStatusAsync(
            StatusRequest(source.StatusSource),
            TestContext.Current.CancellationToken);
        confirmed.IsSuccess.ShouldBeTrue(confirmed.ReasonCode);
        confirmed.Status.ShouldBe(ProviderOperationStatusKind.Confirmed);
        confirmed.CheckNumber.ShouldBe(1);
        client.StatusCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(ProviderOperationStatusKind.NotApplied, true, ProviderFailureCategory.None, "not_applied", true)]
    [InlineData(ProviderOperationStatusKind.Conflicting, false, ProviderFailureCategory.ReconciliationRequired, "forgejo_status_evidence_conflicting", false)]
    public async Task StatusClassifiesNotAppliedAndConflictingEvidence(
        ProviderOperationStatusKind observedStatus,
        bool expectedSuccess,
        ProviderFailureCategory expectedCategory,
        string expectedReason,
        bool expectedRetryable)
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            StatusResult = ForgejoOperationStatusResult.Observed(
                observedStatus,
                observedStatus == ProviderOperationStatusKind.NotApplied
                    ? OperationSourceResolver.HeadSha
                    : "5555555555555555555555555555555555555555",
                source.StatusSource.Target.FullRef),
        };

        ProviderOperationStatusResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired()).GetOperationStatusAsync(
                StatusRequest(source.StatusSource),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBe(expectedSuccess);
        result.Status.ShouldBe(observedStatus);
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.Retryable.ShouldBe(expectedRetryable);
        client.StatusCalls.ShouldBe(1);
    }

    [Fact]
    public async Task StatusUnavailableMayRetryBeforeTheBudgetIsExhausted()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            StatusResult = ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ServerUnavailable),
        };

        ProviderOperationStatusResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired()).GetOperationStatusAsync(
                StatusRequest(source.StatusSource),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ProviderOperationStatusKind.Unavailable);
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        result.ReasonCode.ShouldBe("forgejo_server_unavailable");
        result.Retryable.ShouldBeTrue();
        client.StatusCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CheckFiveNotAppliedEvidenceRequiresReconciliation()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new()
        {
            StatusResult = ForgejoOperationStatusResult.Observed(
                ProviderOperationStatusKind.NotApplied,
                OperationSourceResolver.HeadSha,
                source.StatusSource.Target.FullRef),
        };

        ProviderOperationStatusResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired()).GetOperationStatusAsync(
                StatusRequest(source.StatusSource, checkNumber: 5),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_reconciliation_checks_exhausted");
        result.CheckNumber.ShouldBe(5);
        result.Retryable.ShouldBeFalse();
        client.StatusCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ExactFifteenMinuteWindowExhaustionFailsBeforeSourceCredentialOrHttpAccess()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        DateTimeOffset providerNow = OperationNow.AddMinutes(1);

        ProviderOperationStatusResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired(),
            credentials).GetOperationStatusAsync(
                StatusRequest(
                    source.StatusSource,
                    reconciliationStartedAt: providerNow.AddMinutes(-15),
                    requestedAt: providerNow),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_reconciliation_budget_exhausted");
        source.StatusCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnsupportedVersionFailsClosedBeforeSourceCredentialOrHttpAccess()
    {
        OperationSourceResolver source = new();
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        ProviderFileMutationRequest valid = FileMutationRequest(source.FileMutationSource);
        ProviderFileMutationRequest request = valid with
        {
            TargetEvidence = valid.TargetEvidence with { ProductVersion = "16.0.4" },
        };

        ProviderFileMutationResult result = await Provider(
            source,
            client,
            RecordingProviderOperationOutcomeStore.Acquired(),
            credentials).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_version_incompatible");
        source.FileMutationCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.StageCalls.ShouldBe(0);
    }

    [Fact]
    public async Task NullModesMetadataAndMalformedAuthorizationFailBeforeProtectedAccess()
    {
        OperationSourceResolver source = new();
        ProviderCommitRequest valid = CommitRequest(source.CommitSource);
        ProviderCommitRequest[] requests =
        [
            valid with { CredentialModeRequirements = null! },
            valid with { TargetEvidence = valid.TargetEvidence with { Metadata = null! } },
            valid with { AuthorizationEvidence = valid.AuthorizationEvidence with { Fingerprint = "https://unsafe.example" } },
        ];
        string[] reasons =
        [
            "missing_forgejo_credential_mode",
            "target_evidence_malformed",
            "authorization_evidence_malformed",
        ];

        for (int index = 0; index < requests.Length; index++)
        {
            OperationSourceResolver caseSource = new();
            RecordingForgejoOperationClient client = new();
            RecordingForgejoCredentialResolver credentials = new();
            RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

            ProviderCommitResult result = await Provider(caseSource, client, store, credentials).CommitAsync(
                requests[index],
                TestContext.Current.CancellationToken);

            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            result.ReasonCode.ShouldBe(reasons[index]);
            caseSource.CommitCalls.ShouldBe(0);
            store.ReserveCalls.ShouldBe(0);
            credentials.Calls.ShouldBe(0);
            client.CommitCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ProviderRejectsPathAndBranchBeyondForgejoDtoLimitsBeforeCredentialAccess()
    {
        ProviderResolvedFileChange[] baselineChanges = new OperationSourceResolver().FileMutationSource.Changes.ToArray();
        ProviderResolvedFileChange[] pathChanges = baselineChanges.ToArray();
        pathChanges[0] = pathChanges[0] with { Path = new string('p', 501) };
        ProviderFileMutationResolvedSource[] invalidSources =
        [
            new(OperationSourceResolver.Target(), pathChanges),
            new(new ProviderGitOperationResolvedTarget(
                "forgejo-owner",
                "forgejo-repository",
                $"heads/{new string('b', 101)}",
                OperationSourceResolver.HeadSha),
                baselineChanges),
        ];

        foreach (ProviderFileMutationResolvedSource invalidSource in invalidSources)
        {
            OperationSourceResolver source = new(fileMutationSource: invalidSource);
            RecordingForgejoOperationClient client = new();
            RecordingForgejoCredentialResolver credentials = new();
            RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

            ProviderFileMutationResult result = await Provider(source, client, store, credentials).StageFileChangesAsync(
                FileMutationRequest(source.FileMutationSource),
                TestContext.Current.CancellationToken);

            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            result.ReasonCode.ShouldBe("forgejo_file_mutation_source_malformed");
            store.ReserveCalls.ShouldBe(0);
            credentials.Calls.ShouldBe(0);
            client.StageCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task MixedSourceObjectHashWidthFailsBeforeReservationOrCredentialAccess()
    {
        ProviderResolvedFileChange[] changes =
        [
            new(0, ProviderFileChangeKind.Change, "docs/change.txt", "content"u8.ToArray(), ProviderFileContentType.RegularFile, new string('a', 64)),
        ];
        ProviderCommitResolvedSource commitSource = new(
            OperationSourceResolver.Target(),
            "2222222222222222222222222222222222222222",
            "atomic message",
            changes);
        OperationSourceResolver source = new(commitSource: commitSource);
        RecordingForgejoOperationClient client = new();
        RecordingForgejoCredentialResolver credentials = new();
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderCommitResult result = await Provider(source, client, store, credentials).CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("forgejo_commit_source_malformed");
        source.CommitCalls.ShouldBe(1);
        store.ReserveCalls.ShouldBe(0);
        credentials.Calls.ShouldBe(0);
        client.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ContradictorySuccessfulStatusShapesFallBackToMetadataOnlyUnavailable()
    {
        ProviderOperationStatusResolvedSource knownSource = new(
            OperationSourceResolver.Target(),
            OperationSourceResolver.CommitSha,
            new OperationSourceResolver().StatusSource.StagedChanges,
            "atomic message");
        (ProviderOperationStatusResolvedSource Source, ForgejoOperationStatusResult Result)[] cases =
        [
            (new OperationSourceResolver().StatusSource, ForgejoOperationStatusResult.Observed(
                ProviderOperationStatusKind.Confirmed,
                OperationSourceResolver.HeadSha,
                OperationSourceResolver.Target().FullRef)),
            (new OperationSourceResolver().StatusSource, ForgejoOperationStatusResult.Observed(
                ProviderOperationStatusKind.Conflicting,
                OperationSourceResolver.HeadSha,
                OperationSourceResolver.Target().FullRef)),
            (knownSource, ForgejoOperationStatusResult.Observed(
                ProviderOperationStatusKind.Conflicting,
                OperationSourceResolver.CommitSha,
                OperationSourceResolver.Target().FullRef)),
        ];

        foreach ((ProviderOperationStatusResolvedSource statusSource, ForgejoOperationStatusResult statusResult) in cases)
        {
            OperationSourceResolver source = new(statusSource: statusSource);
            RecordingForgejoOperationClient client = new() { StatusResult = statusResult };

            ProviderOperationStatusResult result = await Provider(
                source,
                client,
                RecordingProviderOperationOutcomeStore.Acquired()).GetOperationStatusAsync(
                    StatusRequest(source.StatusSource),
                    TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Status.ShouldBe(ProviderOperationStatusKind.Unavailable);
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
            result.ReasonCode.ShouldBe("forgejo_status_evidence_unavailable");
            result.Retryable.ShouldBeTrue();
            result.SafeObservedFingerprint.ShouldBeNull();
        }
    }

    [Fact]
    public async Task CanonicalForgejoResolutionWinsWithoutChangingOtherProviderFirstMatch()
    {
        FakeGitProvider forgejoCompetitor = FakeGitProvider.ForgejoLike();
        FakeGitProvider firstCustom = FakeGitProvider.CustomFamily();
        FakeGitProvider secondCustom = FakeGitProvider.CustomFamily();
        ServiceCollection services = new();
        services.AddSingleton<IGitProvider>(forgejoCompetitor);
        services.AddSingleton<IGitProvider>(firstCustom);
        services.AddSingleton<IGitProvider>(secondCustom);
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IProviderCapabilityResolver resolver = serviceProvider.GetRequiredService<IProviderCapabilityResolver>();

        IGitProvider? forgejo = await resolver.ResolveAsync(
            "forgejo",
            "forgejo",
            TestContext.Current.CancellationToken);
        IGitProvider? custom = await resolver.ResolveAsync(
            firstCustom.ProviderFamily,
            firstCustom.ProviderKey,
            TestContext.Current.CancellationToken);

        forgejo.ShouldBeOfType<ForgejoProvider>();
        custom.ShouldBeSameAs(firstCustom);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ProductionResolvedStageFailsClosedWhenSmartGitIsNotAdvertised(int checkNumber)
    {
        OperationSourceResolver source = new();
        QueueHttpHandler handler = new(
            JsonResponse(HttpStatusCode.OK, """{"version":"16.0.3"}"""),
            RefResponse(OperationSourceResolver.HeadSha),
            JsonResponse(HttpStatusCode.OK, "[]"));
        ServiceCollection services = new();
        services.AddSingleton<IForgejoCredentialResolver>(new RecordingForgejoCredentialResolver());
        services.AddSingleton<IForgejoApiClientFactory>(new ConcreteOperationClientFactory(handler));
        services.AddSingleton<IProviderOperationSourceResolver>(source);
        services.AddSingleton<IProviderOperationOutcomeStore>(RecordingProviderOperationOutcomeStore.Acquired());
        services.AddSingleton<TimeProvider>(new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IGitProvider provider = (await serviceProvider.GetRequiredService<IProviderCapabilityResolver>().ResolveAsync(
            "forgejo", "forgejo", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        ProviderFileMutationResult staged = await provider.StageFileChangesAsync(
            FileMutationRequest(source.FileMutationSource),
            TestContext.Current.CancellationToken);
        staged.IsSuccess.ShouldBeFalse();
        checkNumber.ShouldBeInRange(1, 4);
        handler.Methods.ShouldAllBe(static method => method == HttpMethod.Get);
    }

    [Fact]
    public async Task CombinedRestWriteIsNeverDispatchedWhenSmartGitEvidenceIsInvalid()
    {
        OperationSourceResolver source = new();
        HttpResponseMessage rateLimited = new((HttpStatusCode)429);
        rateLimited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        QueueHttpHandler handler = new(
            JsonResponse(HttpStatusCode.OK, """{"version":"16.0.3"}"""),
            RefResponse(OperationSourceResolver.HeadSha),
            rateLimited,
            JsonResponse(HttpStatusCode.OK, "[]"));
        RecordingProviderOperationOutcomeStore store = RecordingProviderOperationOutcomeStore.Acquired();
        ServiceCollection services = new();
        services.AddSingleton<IForgejoCredentialResolver>(new RecordingForgejoCredentialResolver());
        services.AddSingleton<IForgejoApiClientFactory>(new ConcreteOperationClientFactory(handler));
        services.AddSingleton<IProviderOperationSourceResolver>(source);
        services.AddSingleton<IProviderOperationOutcomeStore>(store);
        services.AddSingleton<TimeProvider>(new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IGitProvider provider = (await serviceProvider.GetRequiredService<IProviderCapabilityResolver>().ResolveAsync(
            "forgejo", "forgejo", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        handler.Methods.ShouldNotContain(HttpMethod.Post);
        store.Records.ShouldNotContain(static record => record.Kind == ProviderOperationOutcomeKind.CreatedCommit);
    }

    [Fact]
    public async Task ProductionResolvedPortNeverUsesForgejoContentsWrite()
    {
        OperationSourceResolver source = new();
        ProviderResolvedFileChange first = source.CommitSource.StagedChanges![0];
        QueueHttpHandler handler = new(
            JsonResponse(HttpStatusCode.OK, """{"version":"16.0.3"}"""),
            RefResponse(OperationSourceResolver.HeadSha),
            JsonResponse(HttpStatusCode.Created, JsonSerializer.Serialize(new
            {
                commit = new
                {
                    sha = OperationSourceResolver.CommitSha,
                    message = source.CommitSource.CommitMessage,
                    parents = new[] { new { sha = OperationSourceResolver.HeadSha } },
                },
                files = new object?[]
                {
                    new { path = first.Path, type = "file", sha = GitBlobSha1(first.Content.Span) },
                    null,
                },
            })),
            RefResponse(OperationSourceResolver.CommitSha));
        ServiceCollection services = new();
        services.AddSingleton<IForgejoCredentialResolver>(new RecordingForgejoCredentialResolver());
        services.AddSingleton<IForgejoApiClientFactory>(new ConcreteOperationClientFactory(handler));
        services.AddSingleton<IProviderOperationSourceResolver>(source);
        services.AddSingleton<IProviderOperationOutcomeStore>(RecordingProviderOperationOutcomeStore.Acquired());
        services.AddSingleton<TimeProvider>(new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IGitProvider provider = (await serviceProvider.GetRequiredService<IProviderCapabilityResolver>().ResolveAsync(
            "forgejo", "forgejo", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(source.CommitSource),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        handler.Methods.ShouldNotContain(HttpMethod.Post);
        handler.Uris.ShouldNotContain(static uri => uri.AbsolutePath.EndsWith("/contents", StringComparison.Ordinal));
    }

    private static ForgejoProvider Provider(
        OperationSourceResolver source,
        RecordingForgejoOperationClient client,
        RecordingProviderOperationOutcomeStore store,
        RecordingForgejoCredentialResolver? credentials = null)
        => new(
            credentials ?? new RecordingForgejoCredentialResolver(),
            new RecordingForgejoOperationClientFactory(client),
            new UnconfiguredProviderRepositoryTargetResolver(),
            source,
            store,
            new ForgejoFixedTimeProvider(OperationNow.AddMinutes(1)));

    private static ProviderFileMutationRequest FileMutationRequest(ProviderFileMutationResolvedSource source)
    {
        ProviderOrderedFileChange[] declared =
        [
            new(0, ProviderFileChangeKind.Add, "path-a", SafeFingerprint, "content-a", SafeFingerprint),
            new(1, ProviderFileChangeKind.Remove, "path-b", SafeFingerprint, null, null),
        ];
        ProviderFileMutationRequest request = new(
            "tenant-a", "organization-a", "folder-a", "task-a", "binding-a", "credential-a", "repository-binding-a",
            "forgejo", "forgejo", TargetEvidence(ProviderOperationCatalog.FileMutationSupport),
            [ProviderCredentialMode.UserDelegatedReference], Authorization(), Lock(), RefPolicy(),
            new ProviderFilePolicyEvidence(SafeFingerprint, OperationNow, "fresh", 1024, 2, true, true, true), SafeFingerprint,
            "change-set-a", SafeFingerprint, declared, "correlation-a", "idempotency-a",
            new ProviderIdempotencyAdmission(ProviderIdempotencyDisposition.Fresh, "safe-intent-reference"));
        ProviderOrderedFileChange[] bound = declared.Select((change, index) => change with
        {
            SafePathFingerprint = ForgejoOperationSourceBindings.Path(request, change, source.Changes[index].Path),
            SafeContentFingerprint = change.Kind == ProviderFileChangeKind.Remove
                ? null
                : ForgejoOperationSourceBindings.Content(request, change, source.Changes[index].Content),
        }).ToArray();
        return request with
        {
            SafeResolvedTargetFingerprint = ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeChangeSetFingerprint = ForgejoOperationSourceBindings.ChangeSet(request, source.Changes),
            Changes = bound,
        };
    }

    private static ProviderCommitRequest CommitRequest(ProviderCommitResolvedSource source)
    {
        ProviderCommitRequest request = new(
            "tenant-a", "organization-a", "folder-a", "task-a", "binding-a", "credential-a", "repository-binding-a",
            "forgejo", "forgejo", TargetEvidence(ProviderOperationCatalog.CommitSupport),
            [ProviderCredentialMode.UserDelegatedReference], Authorization(), Lock(), RefPolicy(), SafeFingerprint,
            "staged-a", SafeFingerprint, "message-a", SafeFingerprint, SafeFingerprint, "correlation-a", "idempotency-a",
            new ProviderIdempotencyAdmission(ProviderIdempotencyDisposition.Fresh, "safe-intent-reference"));
        return request with
        {
            SafeResolvedTargetFingerprint = ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeStagedChangeSetFingerprint = ForgejoOperationSourceBindings.StagedChanges(request, source.TreeSha, source.StagedChanges!),
            SafeCommitMessageFingerprint = ForgejoOperationSourceBindings.CommitMessage(request, source.CommitMessage),
            SafeExpectedHeadFingerprint = ForgejoOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
        };
    }

    private static ProviderOperationStatusRequest StatusRequest(
        ProviderOperationStatusResolvedSource source,
        int checkNumber = 1,
        DateTimeOffset? reconciliationStartedAt = null,
        DateTimeOffset? requestedAt = null)
    {
        DateTimeOffset startedAt = reconciliationStartedAt ?? OperationNow;
        DateTimeOffset observationRequestedAt = requestedAt ?? OperationNow.AddMinutes(1);
        ProviderOperationStatusRequest request = new(
            "tenant-a", "organization-a", "folder-a", "task-a", "binding-a", "credential-a", "repository-binding-a",
            "forgejo", "forgejo", TargetEvidence(ProviderOperationCatalog.StatusQuery),
            [ProviderCredentialMode.UserDelegatedReference], Authorization(), Lock(), RefPolicy(), OperationReference,
            SafeFingerprint, SafeFingerprint, SafeFingerprint, SafeFingerprint, SafeFingerprint, checkNumber, startedAt,
            observationRequestedAt, "correlation-a");
        ProviderOperationStatusRequest bound = request with
        {
            SafeResolvedTargetFingerprint = ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeFullRefFingerprint = ForgejoOperationSourceBindings.FullRef(request, source.Target.FullRef),
            SafeExpectedHeadFingerprint = ForgejoOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
            SafeIntendedCommitFingerprint = ForgejoOperationSourceBindings.IntendedCommit(
                request, source.IntendedCommitSha, source.StagedChanges!, source.CommitMessage!),
        };
        return bound with { SafeCheckWindowFingerprint = ForgejoOperationSourceBindings.CheckWindow(bound) };
    }

    private static ProviderTargetEvidence TargetEvidence(string scope)
        => new(
            "forgejo", "16.0.3", ForgejoProviderConstants.ApiSurfaceVersion, "forgejo-target-evidence-v2", false, OperationNow,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authorized_base_url"] = "https://forgejo.example.test",
                ["safe_target_fingerprint"] = SafeFingerprint,
                ["operation_scope"] = scope,
            });

    private static ProviderAuthorizationEvidenceSnapshot Authorization()
        => new("authorization-a", OperationNow, "fresh");

    private static ProviderOperationLockEvidence Lock()
        => new(SafeFingerprint, OperationNow, "fresh", true, false);

    private static ProviderRefPolicyEvidence RefPolicy()
        => new(SafeFingerprint, OperationNow, "fresh", true, true, true);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage RefResponse(string sha)
        => JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new[]
        {
            new { @ref = "refs/heads/main", @object = new { type = "commit", sha } },
        }));

    private static string GitBlobSha1(ReadOnlySpan<byte> content)
    {
        byte[] header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
        byte[] payload = new byte[header.Length + content.Length];
        header.CopyTo(payload, 0);
        content.CopyTo(payload.AsSpan(header.Length));
        return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(payload)).ToLowerInvariant();
    }

    private static void AssertCommitUnknown(ProviderCommitResult result, string expectedReason)
    {
        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe(expectedReason);
        result.OpaqueOperationReference.ShouldBe(OperationReference);
        result.ReconciliationReference.ShouldBe(OperationReference);
        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain(OperationSourceResolver.CommitSha, Case.Sensitive);
        serialized.ShouldNotContain(OperationSourceResolver.HeadSha, Case.Sensitive);
        serialized.ShouldNotContain("docs/", Case.Sensitive);
    }

    private sealed class OperationSourceResolver(
        ProviderFileMutationResolvedSource? fileMutationSource = null,
        ProviderCommitResolvedSource? commitSource = null,
        ProviderOperationStatusResolvedSource? statusSource = null) : IProviderOperationSourceResolver
    {
        internal const string HeadSha = "1111111111111111111111111111111111111111";
        internal const string CommitSha = "3333333333333333333333333333333333333333";

        public ProviderFileMutationResolvedSource FileMutationSource { get; } = fileMutationSource ?? new(Target(), Changes());

        public ProviderCommitResolvedSource CommitSource { get; } = commitSource ?? new(Target(), "2222222222222222222222222222222222222222", "atomic message", Changes());

        public ProviderOperationStatusResolvedSource StatusSource { get; } = statusSource ?? new(Target(), null, Changes(), "atomic message");

        public int FileMutationCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public ValueTask<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>> ResolveFileMutationAsync(ProviderFileMutationRequest request, CancellationToken cancellationToken = default)
        {
            FileMutationCalls++;
            return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Success(FileMutationSource));
        }

        public ValueTask<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>> ResolveCommitAsync(ProviderCommitRequest request, CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Success(CommitSource));
        }

        public ValueTask<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>> ResolveStatusAsync(ProviderOperationStatusRequest request, CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Success(StatusSource));
        }

        internal static ProviderGitOperationResolvedTarget Target()
            => new("forgejo-owner", "forgejo-repository", "heads/main", HeadSha);

        private static ProviderResolvedFileChange[] Changes()
            =>
            [
                new(0, ProviderFileChangeKind.Add, "docs/add.txt", "content"u8.ToArray(), ProviderFileContentType.RegularFile),
                new(1, ProviderFileChangeKind.Remove, "docs/remove.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile, "4444444444444444444444444444444444444444"),
            ];
    }

    private sealed class RecordingForgejoCredentialResolver : IForgejoCredentialResolver
    {
        public ForgejoCredentialResolutionResult? Result { get; init; }

        public Exception? Exception { get; init; }

        public int Calls { get; private set; }

        public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(ForgejoCredentialResolutionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return ValueTask.FromResult(Result
                ?? ForgejoCredentialResolutionResult.Success(ForgejoCredentialLease.CreateForTesting("provider-secret")));
        }
    }

    private sealed class RecordingForgejoOperationClientFactory(RecordingForgejoOperationClient client) : IForgejoApiClientFactory
    {
        public int Calls { get; private set; }

        public ValueTask<IForgejoApiClient> CreateAsync(ForgejoApiClientRequest request, ForgejoCredentialLease credential, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult<IForgejoApiClient>(client);
        }
    }

    private sealed class RecordingForgejoOperationClient : IForgejoApiClient
    {
        public ForgejoCommitResult CommitResult { get; init; } = ForgejoCommitResult.Success(OperationSourceResolver.CommitSha);

        public ForgejoOperationStatusResult StatusResult { get; init; } = ForgejoOperationStatusResult.Observed(
            ProviderOperationStatusKind.Confirmed,
            OperationSourceResolver.CommitSha,
            OperationSourceResolver.Target().FullRef);

        public int StageCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public Task<ForgejoFileMutationResult> StageFileChangesAsync(ForgejoFileMutationRequest request, CancellationToken cancellationToken = default)
        {
            StageCalls++;
            return StageAsync(request, cancellationToken);
        }

        public async Task<ForgejoCommitResult> CommitAsync(ForgejoCommitRequest request, CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            if (!await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false))
            {
                return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ReservationInvalidated);
            }

            if (CommitResult.IsSuccess && !await request.RecordCreatedCommitAsync(CommitResult.CommitSha!).ConfigureAwait(false))
            {
                return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.OutcomeRecordingFailed, observedCommitSha: CommitResult.CommitSha);
            }

            return CommitResult;
        }

        public Task<ForgejoOperationStatusResult> GetOperationStatusAsync(ForgejoOperationStatusRequest request, CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return Task.FromResult(StatusResult);
        }

        public Task<ForgejoReadinessResult> GetReadinessAsync(ForgejoReadinessRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));

        public Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(ForgejoRepositoryCreationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));

        public Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(ForgejoRepositoryBindingRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        private static async Task<ForgejoFileMutationResult> StageAsync(ForgejoFileMutationRequest request, CancellationToken cancellationToken)
            => await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false)
                ? ForgejoFileMutationResult.Success("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
                : ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ReservationInvalidated);
    }

    private sealed class ConcreteOperationClientFactory(QueueHttpHandler handler) : IForgejoApiClientFactory
    {
        public ValueTask<IForgejoApiClient> CreateAsync(ForgejoApiClientRequest request, ForgejoCredentialLease credential, CancellationToken cancellationToken = default)
        {
            HttpClient client = new(handler, disposeHandler: false) { BaseAddress = request.BaseUri };
            client.DefaultRequestHeaders.Authorization = new("Bearer", credential.AccessToken);
            return ValueTask.FromResult<IForgejoApiClient>(new ForgejoHttpApiClient(client, request.BaseUri));
        }
    }

    private sealed class QueueHttpHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpMethod> Methods { get; } = [];

        public List<Uri> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            Uris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class SequencedOutcomeStore(params bool[] recordResults) : IProviderOperationOutcomeStore
    {
        private readonly Queue<bool> _recordResults = new(recordResults);

        public List<ProviderOperationOutcomeRecord> Records { get; } = [];

        public ValueTask<ProviderOperationReservationResult> ReserveAsync(
            ProviderOperationReservationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.Acquired,
                OperationReference,
                Generation: 1));

        public ValueTask<bool> ValidateAsync(
            ProviderOperationReservationValidationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);

        public ValueTask<bool?> RecordAsync(
            ProviderOperationOutcomeRecord record,
            CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.FromResult<bool?>(_recordResults.Dequeue());
        }

        public ValueTask<bool?> FinalizeNoDispatchAsync(
            ProviderOperationOutcomeRecord record,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<bool?>(true);
    }
}
