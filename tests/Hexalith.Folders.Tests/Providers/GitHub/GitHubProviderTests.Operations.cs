using System.Text.Json;
using System.Net;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Testing.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed partial class GitHubProviderTests
{
    private const string SafeFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CorrelationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private static readonly DateTimeOffset OperationNow =
        DateTimeOffset.Parse("2026-08-25T08:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task StagesOrderedChangesThroughAuthorizedGitHubBoundaryWithoutMovingRef()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient, outcomeStore);

        ProviderFileMutationResult result = await provider.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentReplay.ShouldBeFalse();
        result.SafeOutcomeFingerprint.ShouldNotBeNullOrWhiteSpace();
        result.OpaqueOperationReference.ShouldNotBeNullOrWhiteSpace();
        sourceResolver.FileMutationCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.CredentialIsDisposed.ShouldBeTrue();
        apiClient.FileMutationCalls.ShouldBe(1);
        apiClient.LastFileMutationRequest!.Changes.Select(static change => change.Sequence).ShouldBe([0, 1]);
        apiClient.LastFileMutationRequest.Changes.Select(static change => change.Kind)
            .ShouldBe([ProviderFileChangeKind.Add, ProviderFileChangeKind.Remove]);
        outcomeStore.Records.Select(static record => record.Kind).ShouldBe([ProviderOperationOutcomeKind.StagedTree]);
    }

    [Fact]
    public async Task CommitsOnceThroughAuthorizedGitHubBoundaryAndReturnsOnlySafeEvidence()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.SafeCommitFingerprint.ShouldNotBeNullOrWhiteSpace();
        result.OpaqueOperationReference.ShouldNotBeNullOrWhiteSpace();
        sourceResolver.CommitCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.CredentialIsDisposed.ShouldBeTrue();
        apiClient.CommitCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DenialStalenessPolicyAndIntentFailuresShortCircuitBeforePrivateSources()
    {
        ProviderFileMutationRequest baseline = FileMutationRequest();
        ProviderFileMutationRequest[] deniedRequests =
        [
            baseline with
            {
                AuthorizationEvidence = baseline.AuthorizationEvidence with { FreshnessClass = "stale" },
            },
            baseline with
            {
                LockEvidence = baseline.LockEvidence with { IsRevoked = true },
            },
            baseline with
            {
                LockEvidence = baseline.LockEvidence with { IsOwnedByDelegatedTask = false },
            },
            baseline with
            {
                RefPolicyEvidence = baseline.RefPolicyEvidence with { AllowsFileMutation = false },
            },
            baseline with
            {
                FilePolicyEvidence = baseline.FilePolicyEvidence with { AllowsAdd = false },
            },
            baseline with
            {
                IdempotencyAdmission = baseline.IdempotencyAdmission with
                {
                    Disposition = ProviderIdempotencyDisposition.Conflict,
                },
            },
            baseline with
            {
                IdempotencyAdmission = baseline.IdempotencyAdmission with
                {
                    Disposition = ProviderIdempotencyDisposition.Expired,
                },
            },
        ];

        foreach (ProviderFileMutationRequest request in deniedRequests)
        {
            RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            RecordingGitHubApiClientFactory apiClientFactory = new(RecordingGitHubApiClient.Success());
            GitHubProvider provider = new(
                credentialResolver,
                apiClientFactory,
                RecordingProviderRepositoryTargetResolver.Success(),
                sourceResolver);

            ProviderFileMutationResult result = await provider.StageFileChangesAsync(
                request,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            sourceResolver.FileMutationCalls.ShouldBe(0);
            credentialResolver.Calls.ShouldBe(0);
            apiClientFactory.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ReservedSystemTenantIsRejectedForEveryOperationBeforePrivateSourceAccess()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest() with { ManagedTenantId = " system " },
            TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(
            CommitRequest() with { ManagedTenantId = "SYSTEM" },
            TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(
            StatusRequest() with { ManagedTenantId = " System" },
            TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        sourceResolver.FileMutationCalls.ShouldBe(0);
        sourceResolver.CommitCalls.ShouldBe(0);
        sourceResolver.StatusCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
        apiClient.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentMutationAndCommitReplayNeverDispatchesASecondProviderEffect()
    {
        ProviderIdempotencyAdmission replay = new(
            ProviderIdempotencyDisposition.EquivalentReplay,
            "safe-intent-reference",
            SafeFingerprint,
            PriorReconciliationReference: null,
            PriorOperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
            PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Success);
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest() with { IdempotencyAdmission = replay },
            TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(
            CommitRequest() with { IdempotencyAdmission = replay },
            TestContext.Current.CancellationToken);

        mutation.IsSuccess.ShouldBeTrue();
        mutation.EquivalentReplay.ShouldBeTrue();
        commit.IsSuccess.ShouldBeTrue();
        commit.EquivalentReplay.ShouldBeTrue();
        sourceResolver.FileMutationCalls.ShouldBe(0);
        sourceResolver.CommitCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
        mutation.OpaqueOperationReference.ShouldBe(replay.PriorOperationReference);
        mutation.ReconciliationReference.ShouldBeNull();
        commit.OpaqueOperationReference.ShouldBe(replay.PriorOperationReference);
        commit.ReconciliationReference.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidIdempotencyAndNullBoundaryShapesFailBeforeSourceAccess()
    {
        ProviderFileMutationRequest baseline = FileMutationRequest();
        ProviderFileMutationRequest[] invalid =
        [
            baseline with { IdempotencyKey = string.Empty },
            baseline with { CredentialModeRequirements = null! },
            baseline with { Changes = [null!] },
            baseline with { TargetEvidence = baseline.TargetEvidence with { Metadata = null! } },
            baseline with { TargetEvidence = baseline.TargetEvidence with { Metadata = new Dictionary<string, string> { ["operation_scope"] = ProviderOperationCatalog.CommitSupport } } },
        ];

        foreach (ProviderFileMutationRequest request in invalid)
        {
            RecordingProviderOperationSourceResolver source = RecordingProviderOperationSourceResolver.Success();
            ProviderFileMutationResult result = await OperationProvider(
                source,
                RecordingGitHubCredentialResolver.Success("token-sentinel"),
                RecordingGitHubApiClient.Success()).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            source.FileMutationCalls.ShouldBe(0);
        }
    }

    [Theory]
    [InlineData("heads/main", true)]
    [InlineData("heads/feature/story-3", true)]
    [InlineData("heads/", false)]
    [InlineData("heads/a..b", false)]
    [InlineData("heads/a b", false)]
    [InlineData("heads/a@{b", false)]
    [InlineData("heads/a.lock", false)]
    [InlineData("heads//a", false)]
    [InlineData("tags/main", false)]
    public void ResolvedTargetAcceptsOnlyValidBranchRefs(string refName, bool expected)
    {
        ProviderGitOperationResolvedTarget target = new("owner", "repository", refName, RecordingProviderOperationSourceResolver.HeadSha);

        target.TryValidate(out _).ShouldBe(expected);
    }

    [Theory]
    [InlineData("cafe\u0301", "repository", "heads/main")]
    [InlineData("owner", "cafe\u0301", "heads/main")]
    [InlineData("owner", "repository", "heads/cafe\u0301")]
    public void ResolvedTargetRejectsNonCanonicalOrInvalidUnicode(string owner, string repository, string refName)
    {
        ProviderGitOperationResolvedTarget target = new(owner, repository, refName, RecordingProviderOperationSourceResolver.HeadSha);

        target.TryValidate(out _).ShouldBeFalse();
    }

    [Fact]
    public void ResolvedTargetRejectsInvalidUnicode()
    {
        string invalidOwner = new(['o', 'w', 'n', 'e', 'r', '\uD800']);
        ProviderGitOperationResolvedTarget target = new(
            invalidOwner,
            "repository",
            "heads/main",
            RecordingProviderOperationSourceResolver.HeadSha);

        target.TryValidate(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task MalformedAndUnsafeResolverResultsFailClosedWithoutSerializationLeakage()
    {
        foreach (RecordingProviderOperationSourceResolver source in new[]
        {
            RecordingProviderOperationSourceResolver.NullResults(),
            RecordingProviderOperationSourceResolver.SuccessWithNullSources(),
            RecordingProviderOperationSourceResolver.UnsafeFailure(),
        })
        {
            ProviderFileMutationResult result = await OperationProvider(
                source,
                RecordingGitHubCredentialResolver.Success("token-sentinel"),
                RecordingGitHubApiClient.Success()).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            JsonSerializer.Serialize(result).ShouldNotContain("token-sentinel", Case.Sensitive);
            result.RetryAfter.ShouldBeNull();
        }
    }

    [Fact]
    public async Task MalformedProviderSuccessesNeverBecomePublicSuccess()
    {
        RecordingGitHubApiClient api = RecordingGitHubApiClient.MalformedOperationSuccesses();
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel"),
            api);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(StatusRequest(), TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        status.IsSuccess.ShouldBeFalse();
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
    }

    [Fact]
    public async Task MissingOperationSourceFailsClosedBeforeCredentialOrOctokitAccess()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Failure();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClientFactory apiClientFactory = new(RecordingGitHubApiClient.Success());
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success(),
            sourceResolver);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);

        mutation.IsSuccess.ShouldBeFalse();
        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        sourceResolver.FileMutationCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PredispatchUnknownSourceFailureIsSanitizedWithoutCredentialOrProviderAccess()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.UnknownFailure();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        mutation.ReconciliationReference.ShouldBeNull();
        commit.ReconciliationReference.ShouldBeNull();
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
        apiClient.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AmbiguousMutationAndCommitOutcomesAreNotRetryableAndExposeOnlyOpaqueReconciliationEvidence()
    {
        ProviderFileMutationResult mutation = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.FileMutationFailure(GitHubApiFailureCondition.AmbiguousMutationResponse))
            .StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.CommitFailure(GitHubApiFailureCondition.TimeoutDuringMutation))
            .CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Status(ProviderOperationStatusKind.Conflicting))
            .GetOperationStatusAsync(StatusRequest(), TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        mutation.Retryable.ShouldBeFalse();
        mutation.ReconciliationReference.ShouldNotBeNullOrWhiteSpace();
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        commit.Retryable.ShouldBeFalse();
        commit.ReconciliationReference.ShouldNotBeNullOrWhiteSpace();
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        string serialized = JsonSerializer.Serialize(new { mutation, commit, status });
        serialized.ShouldNotContain("token-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("octokit-owner-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("octokit-repository-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("docs/one.txt", Case.Sensitive);
        serialized.ShouldNotContain("safe commit message", Case.Sensitive);
    }

    [Fact]
    public async Task StatusRejectsIdempotencyKeyBeforeSourceAccess()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();

        ProviderOperationStatusResult result = await OperationProvider(sourceResolver, credentialResolver, apiClient)
            .GetOperationStatusAsync(
                StatusRequest() with { IdempotencyKey = "idempotency-not-allowed" },
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("idempotency_key_not_allowed");
        sourceResolver.StatusCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StatusRejectsEachInvalidBudgetDimensionAgainstTheFixedOperationClock()
    {
        ProviderOperationStatusRequest baseline = StatusRequest();
        (ProviderOperationStatusRequest Request, DateTimeOffset Now)[] invalidRequests =
        [
            (baseline with { CheckNumber = 6 }, OperationNow.AddMinutes(1)),
            (baseline with { ReconciliationStartedAt = OperationNow.AddMinutes(2) }, OperationNow.AddMinutes(1)),
            (baseline with { RequestedAt = OperationNow.AddSeconds(-1) }, OperationNow.AddMinutes(1)),
            (baseline with { RequestedAt = OperationNow.AddMinutes(3) }, OperationNow.AddMinutes(1)),
            (baseline, OperationNow.AddMinutes(15)),
        ];

        foreach ((ProviderOperationStatusRequest request, DateTimeOffset now) in invalidRequests)
        {
            RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
            GitHubProvider provider = OperationProvider(
                sourceResolver,
                credentialResolver,
                apiClient,
                timeProvider: new FixedTimeProvider(now));

            ProviderOperationStatusResult result = await provider.GetOperationStatusAsync(
                request,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
            result.ReasonCode.ShouldBe("github_reconciliation_budget_exhausted");
            sourceResolver.StatusCalls.ShouldBe(0);
            credentialResolver.Calls.ShouldBe(0);
            apiClient.StatusCalls.ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(ProviderOperationStatusKind.Confirmed, true, ProviderFailureCategory.None)]
    [InlineData(ProviderOperationStatusKind.NotApplied, true, ProviderFailureCategory.None)]
    [InlineData(ProviderOperationStatusKind.Conflicting, false, ProviderFailureCategory.ReconciliationRequired)]
    public async Task StatusPerformsOneAuthorizedObservationAndMapsCanonicalOutcome(
        ProviderOperationStatusKind status,
        bool expectedSuccess,
        ProviderFailureCategory expectedCategory)
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Status(status);
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderOperationStatusResult result = await provider.GetOperationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBe(expectedSuccess);
        result.Status.ShouldBe(status);
        result.FailureCategory.ShouldBe(expectedCategory);
        sourceResolver.StatusCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(1);
        apiClient.StatusCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task EarlyUnavailableAndNotAppliedStatusObservationsRemainExactlyRetryable(int checkNumber)
    {
        ProviderOperationStatusResult unavailable = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.StatusFailure(GitHubApiFailureCondition.ServerUnavailable))
            .GetOperationStatusAsync(StatusRequest(checkNumber), TestContext.Current.CancellationToken);

        unavailable.IsSuccess.ShouldBeFalse();
        unavailable.Status.ShouldBe(ProviderOperationStatusKind.Unavailable);
        unavailable.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        unavailable.ReasonCode.ShouldBe("github_status_evidence_unavailable");
        unavailable.Retryable.ShouldBeTrue();

        ProviderOperationStatusResult notApplied = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Status(ProviderOperationStatusKind.NotApplied))
            .GetOperationStatusAsync(StatusRequest(checkNumber), TestContext.Current.CancellationToken);

        notApplied.IsSuccess.ShouldBeTrue();
        notApplied.Status.ShouldBe(ProviderOperationStatusKind.NotApplied);
        notApplied.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        notApplied.ReasonCode.ShouldBe("not_applied");
        notApplied.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task FifthUnavailableStatusObservationRequiresReconciliationWithoutMutationRetry()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.StatusFailure(GitHubApiFailureCondition.ServerUnavailable);
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderOperationStatusResult result = await provider.GetOperationStatusAsync(
            StatusRequest(checkNumber: 5),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("github_reconciliation_checks_exhausted");
        result.Retryable.ShouldBeFalse();
        apiClient.StatusCalls.ShouldBe(1);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task SourceFailureCategoryReasonAndRetryMetadataAreSanitizedTogether()
    {
        RecordingProviderOperationSourceResolver sourceResolver =
            RecordingProviderOperationSourceResolver.MismatchedFailure(
                ProviderFailureCategory.ProviderValidationFailed,
                "provider_rate_limited",
                TimeSpan.FromSeconds(19));
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();

        ProviderFileMutationResult result = await OperationProvider(
            sourceResolver,
            credentialResolver,
            apiClient).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("github_file_mutation_source_unavailable");
        result.Retryable.ShouldBeFalse();
        result.RetryAfter.ShouldBeNull();
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileMutationCalls.ShouldBe(0);
    }

    [Fact]
    public async Task NonCanonicalOpaqueOperationIdentifiersFailBeforeSourceAccess()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        GitHubProvider provider = OperationProvider(
            sourceResolver,
            RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel"),
            RecordingGitHubApiClient.Success());

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest() with { OrganizationId = "cafe\u0301" },
            TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(
            CommitRequest() with { DelegatedTaskId = "cafe\u0301" },
            TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(
            StatusRequest() with { FolderId = "cafe\u0301" },
            TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        sourceResolver.FileMutationCalls.ShouldBe(0);
        sourceResolver.CommitCalls.ShouldBe(0);
        sourceResolver.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task FifthNotAppliedStatusObservationRequiresReconciliation()
    {
        ProviderOperationStatusResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Status(ProviderOperationStatusKind.NotApplied))
            .GetOperationStatusAsync(StatusRequest(checkNumber: 5), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("github_reconciliation_checks_exhausted");
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task FifthUnknownStatusObservationRequiresReconciliation()
    {
        ProviderOperationStatusResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.StatusFailure(GitHubApiFailureCondition.AmbiguousMutationResponse))
            .GetOperationStatusAsync(StatusRequest(checkNumber: 5), TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentAcquiredAndPendingReservationsAllowExactlyOneDispatch()
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.WithReservations(
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.Acquired,
                RecordingProviderOperationOutcomeStore.OperationReference,
                Generation: 1),
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.Pending,
                RecordingProviderOperationOutcomeStore.OperationReference,
                Generation: 1));
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            apiClient,
            outcomeStore);

        ProviderFileMutationResult[] results = await Task.WhenAll(
            provider.StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken),
            provider.StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken));

        results.Count(static result => result.IsSuccess).ShouldBe(1);
        results.Count(static result => result.ReasonCode == "github_operation_pending").ShouldBe(1);
        results.ShouldAllBe(static result => result.OpaqueOperationReference == RecordingProviderOperationOutcomeStore.OperationReference);
        outcomeStore.ReserveCalls.ShouldBe(2);
        apiClient.FileMutationCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReservationInvalidationFinalizesWithoutProviderMutation()
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired(validationResult: false);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            apiClient,
            outcomeStore);

        ProviderFileMutationResult result = await provider.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("github_operation_reservation_invalidated");
        result.OpaqueOperationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
        outcomeStore.ValidateCalls.ShouldBe(1);
        outcomeStore.FinalizeCalls.ShouldBe(1);
        outcomeStore.Records.Single().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitRecorderNegativeOrNullAcknowledgementReturnsUnknownWithReservedIdentity(bool nullAcknowledgement)
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired(
            recordResult: nullAcknowledgement ? null : false);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            apiClient,
            outcomeStore);

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.Retryable.ShouldBeFalse();
        result.OpaqueOperationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
        result.ReconciliationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
        apiClient.CommitCalls.ShouldBe(1);
    }

    [Fact]
    public async Task KnownTerminalReservationReplayPreservesExactSafeDispositionWithoutDispatch()
    {
        TimeSpan retryAfter = TimeSpan.FromSeconds(37);
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.WithReservations(
            new ProviderOperationReservationResult(
                ProviderOperationReservationDisposition.ReplayKnownFailure,
                RecordingProviderOperationOutcomeStore.OperationReference,
                Generation: 0,
                SafeOutcomeFingerprint: SafeFingerprint,
                FailureCategory: ProviderFailureCategory.ProviderRateLimited,
                ReasonCode: "github_primary_rate_limited",
                RemediationCode: "provider_rate_limited_remediation",
                Retryable: true,
                RetryAfter: retryAfter));
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            credentialResolver,
            apiClient,
            outcomeStore);

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.EquivalentReplay.ShouldBeTrue();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderRateLimited);
        result.ReasonCode.ShouldBe("github_primary_rate_limited");
        result.SafeRemediationCode.ShouldBe("provider_rate_limited_remediation");
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(retryAfter);
        result.SafeCommitFingerprint.ShouldBe(SafeFingerprint);
        result.OpaqueOperationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ResolverSubstitutionForTargetChangeSetOrContentFailsBeforeReservationAndCredential()
    {
        ProviderFileMutationRequest baseline = FileMutationRequest();
        ProviderFileMutationRequest[] substituted =
        [
            baseline with { SafeResolvedTargetFingerprint = new string('b', 64) },
            baseline with { SafeChangeSetFingerprint = new string('b', 64) },
            baseline with
            {
                Changes =
                [
                    baseline.Changes[0] with { SafeContentFingerprint = new string('b', 64) },
                    baseline.Changes[1],
                ],
            },
        ];

        foreach (ProviderFileMutationRequest request in substituted)
        {
            RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            ProviderFileMutationResult result = await OperationProvider(
                RecordingProviderOperationSourceResolver.Success(),
                credentialResolver,
                RecordingGitHubApiClient.Success(),
                outcomeStore).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.ReasonCode.ShouldBe("github_file_mutation_source_malformed");
            outcomeStore.ReserveCalls.ShouldBe(0);
            credentialResolver.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task CredentialReferenceIdentitySubstitutionInvalidatesEveryResolvedTargetBinding()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient, outcomeStore);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(
            FileMutationRequest() with { CredentialReferenceId = "credential-substituted" },
            TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(
            CommitRequest() with { CredentialReferenceId = "credential-substituted" },
            TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(
            StatusRequest() with { CredentialReferenceId = "credential-substituted" },
            TestContext.Current.CancellationToken);

        mutation.ReasonCode.ShouldBe("github_file_mutation_source_malformed");
        commit.ReasonCode.ShouldBe("github_commit_source_malformed");
        status.ReasonCode.ShouldBe("github_operation_status_source_malformed");
        outcomeStore.ReserveCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
        apiClient.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ResolvedMutationChangesAreSnapshottedBeforeLaterCollaboratorAwaits()
    {
        byte[] content = "one"u8.ToArray();
        List<ProviderResolvedFileChange> changes = RecordingProviderOperationSourceResolver.FileChanges().ToList();
        changes[0] = changes[0] with { Content = content };
        ProviderFileMutationRequest request = BindFileMutationRequest(FileMutationRequest(), changes);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.FromFactory(() =>
        {
            content[0] = (byte)'X';
            changes[0] = changes[0] with { Path = "docs/substituted.txt" };
            return GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting("token-sentinel"));
        });

        ProviderFileMutationResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.WithFileChanges(changes),
            credentialResolver,
            apiClient).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        ProviderResolvedFileChange dispatched = apiClient.LastFileMutationRequest!.Changes[0];
        dispatched.Path.ShouldBe("docs/one.txt");
        dispatched.Content.ToArray().ShouldBe("one"u8.ToArray());
    }

    [Fact]
    public async Task DeclaredMutationChangesAreSnapshottedBeforeSourceResolution()
    {
        ProviderFileMutationRequest baseline = FileMutationRequest();
        List<ProviderOrderedFileChange> callerChanges = baseline.Changes.ToList();
        ProviderFileMutationRequest request = baseline with { Changes = callerChanges };
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingProviderOperationSourceResolver sourceResolver =
            RecordingProviderOperationSourceResolver.WithFileChangesAndCallback(
                RecordingProviderOperationSourceResolver.FileChanges(),
                () => callerChanges[0] = callerChanges[0] with { PathReference = "substituted-after-snapshot" });

        ProviderFileMutationResult result = await OperationProvider(
            sourceResolver,
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            apiClient).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        apiClient.FileMutationCalls.ShouldBe(1);
        callerChanges[0].PathReference.ShouldBe("substituted-after-snapshot");
    }

    [Fact]
    public async Task HostileDeclaredAndResolvedCollectionsFailClosedWithoutEscapingOrDownstreamAccess()
    {
        RecordingProviderOperationSourceResolver requestSource = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver requestCredential = RecordingGitHubCredentialResolver.Success("token-sentinel");
        ProviderFileMutationResult declaredResult = await OperationProvider(
            requestSource,
            requestCredential,
            RecordingGitHubApiClient.Success()).StageFileChangesAsync(
                FileMutationRequest() with { Changes = new ThrowingReadOnlyList<ProviderOrderedFileChange>() },
                TestContext.Current.CancellationToken);

        RecordingProviderOperationSourceResolver resolvedSource = RecordingProviderOperationSourceResolver.WithFileChanges(
            new ThrowingReadOnlyList<ProviderResolvedFileChange>());
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubCredentialResolver resolvedCredential = RecordingGitHubCredentialResolver.Success("token-sentinel");
        ProviderFileMutationResult resolvedResult = await OperationProvider(
            resolvedSource,
            resolvedCredential,
            RecordingGitHubApiClient.Success(),
            outcomeStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        declaredResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        declaredResult.ReasonCode.ShouldBe("github_change_set_malformed");
        requestSource.FileMutationCalls.ShouldBe(0);
        requestCredential.Calls.ShouldBe(0);
        resolvedResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        resolvedResult.ReasonCode.ShouldBe("github_file_mutation_source_malformed");
        outcomeStore.ReserveCalls.ShouldBe(0);
        resolvedCredential.Calls.ShouldBe(0);
    }

    [Fact]
    public void ResolvedTargetBindingsMatchFixedVectorsForEveryOperationShape()
    {
        GitHubOperationSourceBindings.ResolvedTarget(FileMutationRequest(), RecordingProviderOperationSourceResolver.Target())
            .ShouldBe("c290e7887d6444c8b5e1c7fed231584e9206a4005f9a5ba307dcb61a78837d93");
        GitHubOperationSourceBindings.ResolvedTarget(CommitRequest(), RecordingProviderOperationSourceResolver.Target())
            .ShouldBe("8a52fcf96e769647b1e76cc5f9c66bce667c9258fabda530490ad31b693a2cda");
        GitHubOperationSourceBindings.ResolvedTarget(StatusRequest(), RecordingProviderOperationSourceResolver.Target())
            .ShouldBe("2dd4b6f751c99d01d0e65f2a7229f754526f47ca6b9af1a1fdca1aaa4515d716");
    }

    [Fact]
    public async Task CanonicallyEquivalentNonNfcMutationPathAndCommitMessageNeverDispatch()
    {
        ProviderResolvedFileChange[] canonicalChanges = RecordingProviderOperationSourceResolver.FileChanges();
        canonicalChanges[0] = canonicalChanges[0] with { Path = "docs/caf\u00e9.txt" };
        ProviderFileMutationRequest mutationRequest = BindFileMutationRequest(FileMutationRequest(), canonicalChanges);
        ProviderResolvedFileChange[] nonCanonicalChanges = canonicalChanges.ToArray();
        nonCanonicalChanges[0] = nonCanonicalChanges[0] with { Path = "docs/cafe\u0301.txt" };
        RecordingProviderOperationOutcomeStore mutationStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubCredentialResolver mutationCredentials = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient mutationApi = RecordingGitHubApiClient.Success();

        ProviderFileMutationResult mutation = await OperationProvider(
            RecordingProviderOperationSourceResolver.WithFileChanges(nonCanonicalChanges),
            mutationCredentials,
            mutationApi,
            mutationStore).StageFileChangesAsync(mutationRequest, TestContext.Current.CancellationToken);

        ProviderCommitResolvedSource canonicalCommitSource = RecordingProviderOperationSourceResolver.CommitSource() with
        {
            CommitMessage = "caf\u00e9",
        };
        ProviderCommitRequest commitRequest = BindCommitRequest(CommitRequest(), canonicalCommitSource);
        ProviderCommitResolvedSource nonCanonicalCommitSource = canonicalCommitSource with { CommitMessage = "cafe\u0301" };
        RecordingProviderOperationOutcomeStore commitStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubCredentialResolver commitCredentials = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient commitApi = RecordingGitHubApiClient.Success();

        ProviderCommitResult commit = await OperationProvider(
            RecordingProviderOperationSourceResolver.WithCommitSource(nonCanonicalCommitSource),
            commitCredentials,
            commitApi,
            commitStore).CommitAsync(commitRequest, TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        mutationStore.ReserveCalls.ShouldBe(0);
        commitStore.ReserveCalls.ShouldBe(0);
        mutationCredentials.Calls.ShouldBe(0);
        commitCredentials.Calls.ShouldBe(0);
        mutationApi.FileMutationCalls.ShouldBe(0);
        commitApi.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task EmptyDuplicateAndAncestorConflictingChangesFailBeforeReservationOrCredential()
    {
        ProviderFileMutationRequest baseline = FileMutationRequest();
        ProviderFileMutationRequest[] boundaryInvalid =
        [
            baseline with { Changes = [] },
            baseline with
            {
                Changes =
                [
                    baseline.Changes[0],
                    baseline.Changes[1] with { PathReference = baseline.Changes[0].PathReference },
                ],
            },
        ];

        foreach (ProviderFileMutationRequest request in boundaryInvalid)
        {
            RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            ProviderFileMutationResult result = await OperationProvider(
                RecordingProviderOperationSourceResolver.Success(),
                credentialResolver,
                RecordingGitHubApiClient.Success(),
                outcomeStore).StageFileChangesAsync(request, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            outcomeStore.ReserveCalls.ShouldBe(0);
            credentialResolver.Calls.ShouldBe(0);
        }

        ProviderResolvedFileChange[] conflictingChanges = RecordingProviderOperationSourceResolver.FileChanges();
        conflictingChanges[0] = conflictingChanges[0] with { Path = "docs" };
        ProviderFileMutationRequest conflictRequest = BindFileMutationRequest(baseline, conflictingChanges);
        RecordingProviderOperationOutcomeStore conflictStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubCredentialResolver conflictCredentials = RecordingGitHubCredentialResolver.Success("token-sentinel");

        ProviderFileMutationResult conflict = await OperationProvider(
            RecordingProviderOperationSourceResolver.WithFileChanges(conflictingChanges),
            conflictCredentials,
            RecordingGitHubApiClient.Success(),
            conflictStore).StageFileChangesAsync(conflictRequest, TestContext.Current.CancellationToken);

        conflict.IsSuccess.ShouldBeFalse();
        conflictStore.ReserveCalls.ShouldBe(0);
        conflictCredentials.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ProductionRegistrationResolvesOneConcreteGitHubProviderAndFailsClosedWithoutOperationSource()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddFoldersProviderReadiness();
        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        IGitProvider[] gitProviders = provider.GetServices<IGitProvider>().ToArray();
        gitProviders.Count(static candidate => candidate is GitHubProvider).ShouldBe(1);
        IProviderCapabilityResolver resolver = provider.GetRequiredService<IProviderCapabilityResolver>();
        IGitProvider? github = await resolver.ResolveAsync(
            GitHubProviderConstants.ProviderFamily,
            GitHubProviderConstants.ProviderKey,
            TestContext.Current.CancellationToken);

        github.ShouldBeOfType<GitHubProvider>();
        ProviderFileMutationResult result = await github.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("provider_file_mutation_source_unconfigured");

        ProviderOperationStatusResult status = await github.GetOperationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        status.ReasonCode.ShouldBe("provider_operation_status_source_unconfigured");
    }

    [Fact]
    public async Task DaprCredentialCompositionKeepsOneConcreteGitHubProviderAndFailsBeforeCredentialAccess()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddFoldersDaprProviderCredentialResolution();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IGitProvider[] gitProviders = provider.GetServices<IGitProvider>().ToArray();
        gitProviders.Count(static candidate => candidate is GitHubProvider).ShouldBe(1);
        GitHubProvider github = gitProviders.OfType<GitHubProvider>().Single();

        ProviderCommitResult result = await github.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("provider_commit_source_unconfigured");

        ProviderOperationStatusResult status = await github.GetOperationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
    }

    [Fact]
    public async Task ProductionCanonicalRegistrationTraversesRealOctokitTransportHermetically()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => OperationJson(HttpStatusCode.OK, JsonSerializer.Serialize(new { @ref = "refs/heads/main", @object = new { type = "commit", sha = RecordingProviderOperationSourceResolver.HeadSha } })),
            2 => OperationJson(HttpStatusCode.OK, JsonSerializer.Serialize(new { sha = RecordingProviderOperationSourceResolver.HeadSha, tree = new { sha = "4444444444444444444444444444444444444444" }, parents = Array.Empty<object>() })),
            3 => OperationJson(HttpStatusCode.OK, """{"sha":"4444444444444444444444444444444444444444","tree":[{"path":"docs/two.txt","mode":"100644","type":"blob","sha":"6666666666666666666666666666666666666666"}],"truncated":false}"""),
            4 => OperationJson(HttpStatusCode.Created, """{"sha":"5555555555555555555555555555555555555555"}"""),
            5 => OperationJson(HttpStatusCode.Created, JsonSerializer.Serialize(new { sha = RecordingProviderOperationSourceResolver.TreeSha })),
            _ => OperationJson(HttpStatusCode.OK, $$"""{"sha":"{{RecordingProviderOperationSourceResolver.TreeSha}}","tree":[{"path":"docs/one.txt","mode":"100644","type":"blob","sha":"5555555555555555555555555555555555555555"}],"truncated":false}"""),
        }));
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IProviderOperationSourceResolver>(RecordingProviderOperationSourceResolver.Success());
        services.AddSingleton<IProviderOperationOutcomeStore>(RecordingProviderOperationOutcomeStore.Acquired());
        services.AddSingleton<IGitHubCredentialResolver>(RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel"));
        services.AddSingleton<IGitHubApiClientFactory>(new OctokitGitHubApiClientFactory(
            () => new Octokit.Internal.HttpClientAdapter(() => handler),
            () => new PooledGitHubHttpMessageHandler(handler)));
        services.AddFoldersProviderReadiness();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IGitProvider github = serviceProvider.GetServices<IGitProvider>().Single(static provider => provider is GitHubProvider);
        ProviderFileMutationResult result = await github.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        handler.Requests.Count.ShouldBe(6);
    }

    [Fact]
    public async Task ProductionCanonicalRegistrationTraversesRealCommitAndStatusTransportHermetically()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => OperationJson(HttpStatusCode.OK, OperationReferenceJson(RecordingProviderOperationSourceResolver.HeadSha)),
            2 => OperationJson(HttpStatusCode.Created, JsonSerializer.Serialize(new { sha = RecordingProviderOperationSourceResolver.CommitSha })),
            3 => OperationJson(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                sha = RecordingProviderOperationSourceResolver.CommitSha,
                message = "safe commit message",
                tree = new { sha = RecordingProviderOperationSourceResolver.TreeSha },
                parents = new[] { new { sha = RecordingProviderOperationSourceResolver.HeadSha } },
            })),
            4 => OperationJson(HttpStatusCode.OK, OperationReferenceJson(RecordingProviderOperationSourceResolver.CommitSha)),
            _ => OperationJson(HttpStatusCode.OK, OperationReferenceJson(RecordingProviderOperationSourceResolver.CommitSha)),
        }));
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(OperationNow.AddMinutes(1)));
        services.AddSingleton<IProviderOperationSourceResolver>(RecordingProviderOperationSourceResolver.Success());
        services.AddSingleton<IProviderOperationOutcomeStore>(RecordingProviderOperationOutcomeStore.Acquired());
        services.AddSingleton<IGitHubCredentialResolver>(RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel"));
        services.AddSingleton<IGitHubApiClientFactory>(new OctokitGitHubApiClientFactory(
            () => new Octokit.Internal.HttpClientAdapter(() => handler),
            () => new PooledGitHubHttpMessageHandler(handler)));
        services.AddFoldersProviderReadiness();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IGitProvider github = serviceProvider.GetRequiredService<IGitProvider>();

        ProviderCommitResult commit = await github.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await github.GetOperationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);

        commit.IsSuccess.ShouldBeTrue(commit.ReasonCode);
        status.IsSuccess.ShouldBeTrue(status.ReasonCode);
        status.Status.ShouldBe(ProviderOperationStatusKind.Confirmed);
        handler.Requests.Select(static request => request.Method.Method)
            .ShouldBe(["GET", "POST", "GET", "PATCH", "GET", "GET"]);
    }

    [Fact]
    public void RepeatedCompositionMapsPreRegisteredConcreteGitHubProviderExactlyOnce()
    {
        ServiceCollection services = new();
        GitHubProvider preRegistered = new();
        services.AddSingleton(preRegistered);
        services.AddFoldersProviderReadiness();
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GitHubProvider[] providers = serviceProvider.GetServices<IGitProvider>().OfType<GitHubProvider>().ToArray();
        providers.ShouldBe([preRegistered]);
        serviceProvider.GetRequiredService<GitHubProvider>().ShouldBeSameAs(preRegistered);
    }

    [Fact]
    public async Task CompositionPreservesCustomProviderAndNormalizesTypeAndFactoryGitHubRegistrationsToSingleton()
    {
        ServiceCollection services = new();
        FakeGitProvider customProvider = FakeGitProvider.CustomFamily();
        services.AddLogging();
        services.AddSingleton<IGitProvider>(customProvider);
        services.AddScoped<GitHubProvider>();
        services.AddScoped<IGitProvider, GitHubProvider>();
        services.AddScoped<IGitProvider>(CreatePreRegisteredGitHubProvider);
        services.AddFoldersProviderReadiness();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        IGitProvider[] providers = serviceProvider.GetServices<IGitProvider>().ToArray();
        providers.ShouldContain(customProvider);
        GitHubProvider github = providers.OfType<GitHubProvider>().ShouldHaveSingleItem();
        serviceProvider.GetRequiredService<GitHubProvider>().ShouldBeSameAs(github);
        serviceProvider.GetRequiredService<IGitProvider>().ShouldBeSameAs(github);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<GitHubProvider>().ShouldBeSameAs(github);

        IProviderCapabilityResolver resolver = serviceProvider.GetRequiredService<IProviderCapabilityResolver>();
        IGitProvider? resolvedCustom = await resolver.ResolveAsync(
            customProvider.ProviderFamily,
            customProvider.ProviderKey,
            TestContext.Current.CancellationToken);
        resolvedCustom.ShouldBeSameAs(customProvider);
        IGitProvider? resolvedGitHub = await resolver.ResolveAsync(
            GitHubProviderConstants.ProviderFamily,
            GitHubProviderConstants.ProviderKey,
            TestContext.Current.CancellationToken);
        resolvedGitHub.ShouldBeSameAs(github);
    }

    [Fact]
    public async Task ClosedAdmissionStatesRejectContradictoryReplayCompanionsBeforeSourceAccess()
    {
        ProviderIdempotencyAdmission[] malformedAdmissions =
        [
            new(ProviderIdempotencyDisposition.Fresh, "safe-intent-reference", PriorOperationReference: "operation-a"),
            new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                SafeFingerprint,
                PriorOperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Success,
                PriorReasonCode: "success"),
            new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                SafeFingerprint,
                PriorReconciliationReference: "reconciliation-a",
                PriorOperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Unknown),
            new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                SafeFingerprint,
                PriorOperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.KnownFailure,
                PriorFailureCategory: ProviderFailureCategory.UnknownProviderOutcome,
                PriorReasonCode: "github_mutation_outcome_unknown",
                PriorRemediationCode: "unknown_provider_outcome_remediation"),
            new(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "safe-intent-reference",
                SafeFingerprint,
                PriorReconciliationReference: "reconciliation-a",
                PriorOperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
                PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.KnownFailure,
                PriorFailureCategory: ProviderFailureCategory.ProviderConflict,
                PriorReasonCode: "github_ref_head_conflict",
                PriorRemediationCode: "provider_conflict_remediation"),
        ];

        foreach (ProviderIdempotencyAdmission admission in malformedAdmissions)
        {
            RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
            ProviderFileMutationResult result = await OperationProvider(
                sourceResolver,
                RecordingGitHubCredentialResolver.Success("token-sentinel"),
                RecordingGitHubApiClient.Success()).StageFileChangesAsync(
                    FileMutationRequest() with { IdempotencyAdmission = admission },
                    TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            sourceResolver.FileMutationCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ClosedReservationStatesRejectContradictoryCompanionsBeforeCredentialAccess()
    {
        ProviderOperationReservationResult[] malformedReservations =
        [
            new(ProviderOperationReservationDisposition.Acquired, RecordingProviderOperationOutcomeStore.OperationReference, 1, SafeOutcomeFingerprint: SafeFingerprint),
            new(ProviderOperationReservationDisposition.Pending, RecordingProviderOperationOutcomeStore.OperationReference, 1, FailureCategory: ProviderFailureCategory.ProviderUnavailable),
            new(ProviderOperationReservationDisposition.ReplaySuccess, RecordingProviderOperationOutcomeStore.OperationReference, 1, SafeOutcomeFingerprint: SafeFingerprint),
            new(ProviderOperationReservationDisposition.ReplayUnknown, RecordingProviderOperationOutcomeStore.OperationReference, 0, SafeOutcomeFingerprint: SafeFingerprint, ReconciliationReference: "reconciliation-a"),
            new(
                ProviderOperationReservationDisposition.ReplayKnownFailure,
                RecordingProviderOperationOutcomeStore.OperationReference,
                0,
                SafeOutcomeFingerprint: SafeFingerprint,
                FailureCategory: ProviderFailureCategory.UnknownProviderOutcome,
                ReasonCode: "github_mutation_outcome_unknown",
                RemediationCode: "unknown_provider_outcome_remediation"),
        ];

        foreach (ProviderOperationReservationResult reservation in malformedReservations)
        {
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            ProviderCommitResult result = await OperationProvider(
                RecordingProviderOperationSourceResolver.Success(),
                credentialResolver,
                RecordingGitHubApiClient.Success(),
                RecordingProviderOperationOutcomeStore.WithReservations(reservation)).CommitAsync(
                    CommitRequest(),
                    TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
            credentialResolver.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task MalformedCredentialResultsFinalizeReservationsDisposeLeasesAndNeverDispatch()
    {
        GitHubCredentialLease malformedLease = GitHubCredentialLease.CreateForTesting("token-sentinel");
        GitHubCredentialResolutionResult[] malformedResults =
        [
            new(true, null, ProviderFailureCategory.None, "success", null),
            new(false, null, (ProviderFailureCategory)999, "github_credential_resolution_unavailable", null),
            new(false, malformedLease, ProviderFailureCategory.ProviderUnavailable, "github_credential_resolution_unavailable", null),
        ];

        foreach (GitHubCredentialResolutionResult malformed in malformedResults)
        {
            RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
            RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
            ProviderCommitResult result = await OperationProvider(
                RecordingProviderOperationSourceResolver.Success(),
                RecordingGitHubCredentialResolver.FromResult(malformed),
                apiClient,
                outcomeStore).CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
            outcomeStore.FinalizeCalls.ShouldBe(1);
            apiClient.CommitCalls.ShouldBe(0);
        }

        malformedLease.AccessToken.ShouldBeEmpty();
    }

    [Fact]
    public async Task RuntimeNullCredentialResultFinalizesAcquiredReservationWithoutDispatch()
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();

        ProviderFileMutationResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.NullResult(),
            apiClient,
            outcomeStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        result.ReasonCode.ShouldBe("github_credential_resolution_unavailable");
        outcomeStore.FinalizeCalls.ShouldBe(1);
        outcomeStore.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        apiClient.FileMutationCalls.ShouldBe(0);

        RecordingProviderOperationOutcomeStore commitStore = RecordingProviderOperationOutcomeStore.Acquired();
        RecordingGitHubApiClient commitApi = RecordingGitHubApiClient.Success();
        ProviderCommitResult commit = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.NullResult(),
            commitApi,
            commitStore).CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);

        commit.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        commit.ReasonCode.ShouldBe("github_credential_resolution_unavailable");
        commitStore.FinalizeCalls.ShouldBe(1);
        commitStore.Records.ShouldHaveSingleItem().Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        commitApi.CommitCalls.ShouldBe(0);

        RecordingGitHubApiClient statusApi = RecordingGitHubApiClient.Success();
        ProviderOperationStatusResult status = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.NullResult(),
            statusApi).GetOperationStatusAsync(StatusRequest(), TestContext.Current.CancellationToken);

        status.Status.ShouldBe(ProviderOperationStatusKind.Unavailable);
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        statusApi.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ProductionCredentialFailureReasonsArePreservedAndAcknowledgedWithoutDispatch()
    {
        (ProviderFailureCategory Category, string Reason)[] failures =
        [
            (ProviderFailureCategory.ProviderConfigurationMissing, "provider_credential_reference_missing"),
            (ProviderFailureCategory.ProviderPermissionInsufficient, "provider_credential_reference_denied"),
            (ProviderFailureCategory.ProviderValidationFailed, "provider_credential_secret_malformed"),
            (ProviderFailureCategory.ProviderUnavailable, "provider_credential_store_unavailable"),
        ];

        foreach ((ProviderFailureCategory category, string reason) in failures)
        {
            RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
            RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
            ProviderFileMutationResult result = await OperationProvider(
                RecordingProviderOperationSourceResolver.Success(),
                RecordingGitHubCredentialResolver.Failure(category, reason),
                apiClient,
                outcomeStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

            result.FailureCategory.ShouldBe(category);
            result.ReasonCode.ShouldBe(reason);
            outcomeStore.FinalizeCalls.ShouldBe(1);
            apiClient.FileMutationCalls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task CredentialNoDispatchFinalizationPreservesSanitizedRetryEvidence()
    {
        TimeSpan retryAfter = TimeSpan.FromSeconds(37);
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderCommitResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Failure(
                ProviderFailureCategory.ProviderUnavailable,
                "provider_credential_store_unavailable",
                retryAfter),
            RecordingGitHubApiClient.Success(),
            outcomeStore).CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);

        result.RetryAfter.ShouldBe(retryAfter);
        ProviderOperationOutcomeRecord terminal = outcomeStore.Records.ShouldHaveSingleItem();
        terminal.Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        terminal.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        terminal.ReasonCode.ShouldBe("provider_credential_store_unavailable");
        terminal.Retryable.ShouldBeTrue();
        terminal.RetryAfter.ShouldBe(retryAfter);

        RecordingProviderOperationOutcomeStore mutationStore = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderFileMutationResult mutation = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Failure(
                ProviderFailureCategory.ProviderUnavailable,
                "provider_credential_store_unavailable",
                retryAfter),
            RecordingGitHubApiClient.Success(),
            mutationStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        mutation.RetryAfter.ShouldBe(retryAfter);
        ProviderOperationOutcomeRecord mutationTerminal = mutationStore.Records.ShouldHaveSingleItem();
        mutationTerminal.Kind.ShouldBe(ProviderOperationOutcomeKind.NoDispatch);
        mutationTerminal.Retryable.ShouldBeTrue();
        mutationTerminal.RetryAfter.ShouldBe(retryAfter);
    }

    [Fact]
    public async Task NonRetryableCredentialFailureDropsContradictoryRetryEvidenceFromResultAndRecord()
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderFileMutationResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Failure(
                ProviderFailureCategory.ProviderPermissionInsufficient,
                "provider_credential_reference_denied",
                TimeSpan.FromSeconds(23)),
            RecordingGitHubApiClient.Success(),
            outcomeStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        result.Retryable.ShouldBeFalse();
        result.RetryAfter.ShouldBeNull();
        ProviderOperationOutcomeRecord terminal = outcomeStore.Records.ShouldHaveSingleItem();
        terminal.Retryable.ShouldBeFalse();
        terminal.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task RejectedNoDispatchAndKnownFailureRecordsDoNotReturnUnrecordedTerminalResults()
    {
        RecordingProviderOperationOutcomeStore rejectedFinalization = RecordingProviderOperationOutcomeStore.Acquired(finalizeResult: false);
        ProviderFileMutationResult noDispatch = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Failure(ProviderFailureCategory.ProviderUnavailable, "provider_credential_store_unavailable"),
            RecordingGitHubApiClient.Success(),
            rejectedFinalization).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        noDispatch.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        noDispatch.ReasonCode.ShouldBe("github_outcome_recording_failed");

        RecordingProviderOperationOutcomeStore rejectedKnownFailure = RecordingProviderOperationOutcomeStore.Acquired(recordResult: false);
        ProviderFileMutationResult knownFailure = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.FileMutationFailure(GitHubApiFailureCondition.ContentPolicyViolation),
            rejectedKnownFailure).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        knownFailure.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        rejectedKnownFailure.Records.Select(static record => record.Kind)
            .ShouldBe([ProviderOperationOutcomeKind.KnownTerminalFailure, ProviderOperationOutcomeKind.Unknown]);
    }

    [Theory]
    [InlineData((int)GitHubApiFailureCondition.ValidationFailure, ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed", (int)ProviderOperationOutcomeKind.KnownTerminalFailure)]
    [InlineData((int)GitHubApiFailureCondition.ResponseLimitExceeded, ProviderFailureCategory.ProviderFailureKnown, "github_response_limit_exceeded", (int)ProviderOperationOutcomeKind.KnownTerminalFailure)]
    [InlineData((int)GitHubApiFailureCondition.PrimaryRateLimit, ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited", (int)ProviderOperationOutcomeKind.KnownTerminalFailure)]
    [InlineData((int)GitHubApiFailureCondition.TimeoutDuringMutation, ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown", (int)ProviderOperationOutcomeKind.Unknown)]
    public async Task ProviderOperationFailureMappingsProduceTheExpectedRecordedTerminalOutcome(
        int condition,
        ProviderFailureCategory expectedCategory,
        string expectedReason,
        int expectedKind)
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();

        ProviderFileMutationResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.FileMutationFailure((GitHubApiFailureCondition)condition),
            outcomeStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        ProviderOperationOutcomeRecord recorded = outcomeStore.Records.ShouldHaveSingleItem();
        recorded.Kind.ShouldBe((ProviderOperationOutcomeKind)expectedKind);
        recorded.FailureCategory.ShouldBe(expectedCategory);
        recorded.ReasonCode.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task ThrowingPostDispatchOutcomeStoreReturnsSafeUnknownForStagingAndCommit()
    {
        RecordingProviderOperationOutcomeStore stagingStore = RecordingProviderOperationOutcomeStore.Acquired(throwOnRecord: true);
        ProviderFileMutationResult staging = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Success(),
            stagingStore).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        RecordingProviderOperationOutcomeStore commitStore = RecordingProviderOperationOutcomeStore.Acquired(throwOnRecord: true);
        ProviderCommitResult commit = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Success(),
            commitStore).CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);

        staging.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        staging.ReconciliationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        commit.ReconciliationReference.ShouldBe(RecordingProviderOperationOutcomeStore.OperationReference);
    }

    [Fact]
    public async Task FailedStagingRecordFallsBackToUnknownAndCreatedCommitRecordPinsExactEvidence()
    {
        RecordingProviderOperationOutcomeStore rejectedStaging = RecordingProviderOperationOutcomeStore.Acquired(recordResult: false);
        ProviderFileMutationResult staged = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Success(),
            rejectedStaging).StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);

        staged.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        rejectedStaging.Records.Select(static record => record.Kind)
            .ShouldBe([ProviderOperationOutcomeKind.StagedTree, ProviderOperationOutcomeKind.Unknown]);

        RecordingProviderOperationOutcomeStore commitStore = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderCommitRequest request = CommitRequest();
        ProviderCommitResolvedSource source = RecordingProviderOperationSourceResolver.CommitSource();
        ProviderCommitResult committed = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Success(),
            commitStore).CommitAsync(request, TestContext.Current.CancellationToken);

        committed.IsSuccess.ShouldBeTrue(committed.ReasonCode);
        commitStore.Records.Select(static record => record.Kind)
            .ShouldBe([ProviderOperationOutcomeKind.CreatedCommit, ProviderOperationOutcomeKind.RefUpdateConfirmed]);
        ProviderOperationOutcomeRecord created = commitStore.Records.Single(static record => record.Kind == ProviderOperationOutcomeKind.CreatedCommit);
        GitHubSafeTargetFingerprint.TryCreate(
            request,
            ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence? safeTargetEvidence,
            out _).ShouldBeTrue();
        string expected = GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:created-commit",
            request.AuthorizationEvidence.Fingerprint,
            RecordingProviderOperationOutcomeStore.OperationReference,
            safeTargetEvidence!.Metadata["safe_target_fingerprint"],
            request.IdempotencyAdmission.IntentFingerprint,
            source.TreeSha,
            source.Target.ExpectedHeadSha,
            source.CommitMessage,
            RecordingProviderOperationSourceResolver.CommitSha);
        created.SafeOutcomeFingerprint.ShouldBe(expected);
    }

    [Fact]
    public async Task InvalidTransportCompanionFieldsAndEqualStatusHeadsFailClosed()
    {
        const string objectId = RecordingProviderOperationSourceResolver.HeadSha;
        RecordingGitHubApiClient malformed = new(
            GitHubReadinessResult.Success(
                new GitHubPermissionEvidence(true, true, true, true, true, true, true),
                new GitHubRateLimitEvidence("bounded", true, TimeSpan.FromSeconds(1))),
            fileMutationResult: new GitHubFileMutationResult(true, GitHubApiFailureCondition.ValidationFailure, null, objectId),
            commitResult: new GitHubCommitResult(true, GitHubApiFailureCondition.None, null, objectId, RecordingProviderOperationSourceResolver.CommitSha),
            statusResult: GitHubOperationStatusResult.Observed(
                ProviderOperationStatusKind.Confirmed,
                RecordingProviderOperationSourceResolver.CommitSha,
                "refs/heads/other"));
        GitHubProvider provider = OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.SuccessPerCall("token-sentinel"),
            malformed);

        (await provider.StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken))
            .FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        (await provider.CommitAsync(CommitRequest(), TestContext.Current.CancellationToken))
            .FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        (await provider.GetOperationStatusAsync(StatusRequest(), TestContext.Current.CancellationToken))
            .FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);

        ProviderOperationStatusResolvedSource equalHeads = new(
            RecordingProviderOperationSourceResolver.Target(),
            RecordingProviderOperationSourceResolver.HeadSha);
        RecordingProviderOperationSourceResolver equalSourceResolver = RecordingProviderOperationSourceResolver.WithStatusSource(equalHeads);
        ProviderOperationStatusRequest equalRequest = BindStatusRequest(StatusRequest(), equalHeads);
        RecordingGitHubCredentialResolver credentials = RecordingGitHubCredentialResolver.Success("token-sentinel");
        ProviderOperationStatusResult equalResult = await OperationProvider(
            equalSourceResolver,
            credentials,
            RecordingGitHubApiClient.Success()).GetOperationStatusAsync(equalRequest, TestContext.Current.CancellationToken);

        equalResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        credentials.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CredentialResolverCancellationFinalizesMutationAndMalformedStatusLeaseIsDisposed()
    {
        RecordingProviderOperationOutcomeStore outcomeStore = RecordingProviderOperationOutcomeStore.Acquired();
        ProviderCommitResult cancelled = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Throws(new OperationCanceledException()),
            RecordingGitHubApiClient.Success(),
            outcomeStore).CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);

        cancelled.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        outcomeStore.FinalizeCalls.ShouldBe(1);

        GitHubCredentialLease disposedLease = GitHubCredentialLease.CreateForTesting("token-sentinel");
        await disposedLease.DisposeAsync();
        RecordingGitHubCredentialResolver malformedCredentials = RecordingGitHubCredentialResolver.FromResult(
            GitHubCredentialResolutionResult.Success(disposedLease));
        ProviderOperationStatusResult malformedStatus = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            malformedCredentials,
            RecordingGitHubApiClient.Success()).GetOperationStatusAsync(
                StatusRequest(),
                TestContext.Current.CancellationToken);

        malformedStatus.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        malformedCredentials.CredentialIsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanonicalGitHubSelectionDoesNotChangeFirstMatchForDuplicateNonGitHubProviders()
    {
        FakeGitProvider first = FakeGitProvider.CustomFamily();
        FakeGitProvider second = FakeGitProvider.CustomFamily();
        ServiceCollection services = new();
        services.AddSingleton<IGitProvider>(first);
        services.AddSingleton<IGitProvider>(second);
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IProviderCapabilityResolver resolver = serviceProvider.GetRequiredService<IProviderCapabilityResolver>();
        IGitProvider? resolved = await resolver.ResolveAsync(
            first.ProviderFamily,
            first.ProviderKey,
            TestContext.Current.CancellationToken);

        resolved.ShouldBeSameAs(first);
    }

    [Fact]
    public void EmptyScriptedOutcomeStoreSequenceIsRejectedAtConstruction()
        => Should.Throw<ArgumentException>(() => RecordingProviderOperationOutcomeStore.WithReservations());

    private static GitHubProvider CreatePreRegisteredGitHubProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return new GitHubProvider();
    }

    private static HttpResponseMessage OperationJson(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static string OperationReferenceJson(string sha)
        => JsonSerializer.Serialize(new
        {
            @ref = "refs/heads/main",
            @object = new { type = "commit", sha },
        });

    private static GitHubProvider OperationProvider(
        RecordingProviderOperationSourceResolver sourceResolver,
        RecordingGitHubCredentialResolver credentialResolver,
        RecordingGitHubApiClient apiClient,
        RecordingProviderOperationOutcomeStore? outcomeStore = null,
        TimeProvider? timeProvider = null)
        => new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            sourceResolver,
            outcomeStore ?? RecordingProviderOperationOutcomeStore.Acquired(),
            timeProvider ?? new FixedTimeProvider(OperationNow.AddMinutes(1)));

    private static ProviderFileMutationRequest FileMutationRequest()
    {
        ProviderOrderedFileChange[] declaredChanges =
        [
            new ProviderOrderedFileChange(
                0,
                ProviderFileChangeKind.Add,
                "path-reference-a",
                SafeFingerprint,
                "content-reference-a",
                SafeFingerprint),
            new ProviderOrderedFileChange(
                1,
                ProviderFileChangeKind.Remove,
                "path-reference-b",
                SafeFingerprint,
                ContentReference: null,
                SafeContentFingerprint: null),
        ];
        ProviderFileMutationRequest request = new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            DelegatedTaskId: "task-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence(ProviderOperationCatalog.FileMutationSupport),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot("authorization-a", OperationNow, "fresh"),
            LockEvidence: new ProviderOperationLockEvidence(SafeFingerprint, OperationNow, "fresh", true, false),
            RefPolicyEvidence: RefPolicyEvidence(),
            FilePolicyEvidence: new ProviderFilePolicyEvidence(
                SafeFingerprint,
                OperationNow,
                "fresh",
                MaximumFileBytes: 1024,
                MaximumChangeCount: 2,
                AllowsAdd: true,
                AllowsChange: true,
                AllowsRemove: true),
            SafeResolvedTargetFingerprint: SafeFingerprint,
            ChangeSetReference: "change-set-a",
            SafeChangeSetFingerprint: SafeFingerprint,
            Changes: declaredChanges,
            CorrelationId: CorrelationId,
            IdempotencyKey: "idempotency-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.Fresh,
                "safe-intent-reference"));

        ProviderResolvedFileChange[] resolvedChanges = RecordingProviderOperationSourceResolver.FileChanges();
        ProviderOrderedFileChange[] boundChanges =
        [
            declaredChanges[0] with
            {
                SafePathFingerprint = GitHubOperationSourceBindings.Path(request, declaredChanges[0], resolvedChanges[0].Path),
                SafeContentFingerprint = GitHubOperationSourceBindings.Content(request, declaredChanges[0], resolvedChanges[0].Content),
            },
            declaredChanges[1] with
            {
                SafePathFingerprint = GitHubOperationSourceBindings.Path(request, declaredChanges[1], resolvedChanges[1].Path),
            },
        ];
        return request with
        {
            SafeResolvedTargetFingerprint = GitHubOperationSourceBindings.ResolvedTarget(request, RecordingProviderOperationSourceResolver.Target()),
            SafeChangeSetFingerprint = GitHubOperationSourceBindings.ChangeSet(request, resolvedChanges),
            Changes = boundChanges,
        };
    }

    private static ProviderFileMutationRequest BindFileMutationRequest(
        ProviderFileMutationRequest request,
        IReadOnlyList<ProviderResolvedFileChange> resolvedChanges)
    {
        ProviderOrderedFileChange[] boundChanges = request.Changes
            .Select((declared, index) => declared with
            {
                SafePathFingerprint = GitHubOperationSourceBindings.Path(request, declared, resolvedChanges[index].Path),
                SafeContentFingerprint = declared.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    ? GitHubOperationSourceBindings.Content(request, declared, resolvedChanges[index].Content)
                    : null,
            })
            .ToArray();
        return request with
        {
            SafeChangeSetFingerprint = GitHubOperationSourceBindings.ChangeSet(request, resolvedChanges),
            Changes = boundChanges,
        };
    }

    private static ProviderCommitRequest CommitRequest()
    {
        ProviderCommitRequest request = new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            DelegatedTaskId: "task-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence(ProviderOperationCatalog.CommitSupport),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot("authorization-a", OperationNow, "fresh"),
            LockEvidence: new ProviderOperationLockEvidence(SafeFingerprint, OperationNow, "fresh", true, false),
            RefPolicyEvidence: RefPolicyEvidence(),
            SafeResolvedTargetFingerprint: SafeFingerprint,
            StagedChangeSetReference: "staged-change-set-a",
            SafeStagedChangeSetFingerprint: SafeFingerprint,
            CommitMessageReference: "commit-message-a",
            SafeCommitMessageFingerprint: SafeFingerprint,
            SafeExpectedHeadFingerprint: SafeFingerprint,
            CorrelationId: CorrelationId,
            IdempotencyKey: "idempotency-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.Fresh,
                "safe-intent-reference"));

        ProviderCommitResolvedSource source = RecordingProviderOperationSourceResolver.CommitSource();
        return request with
        {
            SafeResolvedTargetFingerprint = GitHubOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeStagedChangeSetFingerprint = GitHubOperationSourceBindings.StagedTree(request, source.TreeSha),
            SafeCommitMessageFingerprint = GitHubOperationSourceBindings.CommitMessage(request, source.CommitMessage),
            SafeExpectedHeadFingerprint = GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
        };
    }

    private static ProviderCommitRequest BindCommitRequest(
        ProviderCommitRequest request,
        ProviderCommitResolvedSource source)
        => request with
        {
            SafeResolvedTargetFingerprint = GitHubOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeStagedChangeSetFingerprint = GitHubOperationSourceBindings.StagedTree(request, source.TreeSha),
            SafeCommitMessageFingerprint = GitHubOperationSourceBindings.CommitMessage(request, source.CommitMessage),
            SafeExpectedHeadFingerprint = GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
        };

    private static ProviderOperationStatusRequest StatusRequest(int checkNumber = 1)
    {
        ProviderOperationStatusRequest request = new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            DelegatedTaskId: "task-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence(ProviderOperationCatalog.StatusQuery),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot("authorization-a", OperationNow, "fresh"),
            LockEvidence: new ProviderOperationLockEvidence(SafeFingerprint, OperationNow, "fresh", true, false),
            RefPolicyEvidence: RefPolicyEvidence(),
            OperationReference: RecordingProviderOperationOutcomeStore.OperationReference,
            SafeResolvedTargetFingerprint: SafeFingerprint,
            SafeFullRefFingerprint: SafeFingerprint,
            SafeExpectedHeadFingerprint: SafeFingerprint,
            SafeIntendedCommitFingerprint: SafeFingerprint,
            SafeCheckWindowFingerprint: SafeFingerprint,
            CheckNumber: checkNumber,
            ReconciliationStartedAt: OperationNow,
            RequestedAt: OperationNow.AddMinutes(1),
            CorrelationId: CorrelationId);

        ProviderOperationStatusResolvedSource source = RecordingProviderOperationSourceResolver.StatusSource();
        return request with
        {
            SafeResolvedTargetFingerprint = GitHubOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeFullRefFingerprint = GitHubOperationSourceBindings.FullRef(request, source.Target.FullRef),
            SafeExpectedHeadFingerprint = GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
            SafeIntendedCommitFingerprint = GitHubOperationSourceBindings.IntendedCommit(request, source.IntendedCommitSha),
            SafeCheckWindowFingerprint = GitHubOperationSourceBindings.CheckWindow(request),
        };
    }

    private static ProviderOperationStatusRequest BindStatusRequest(
        ProviderOperationStatusRequest request,
        ProviderOperationStatusResolvedSource source)
    {
        ProviderOperationStatusRequest bound = request with
        {
            SafeResolvedTargetFingerprint = GitHubOperationSourceBindings.ResolvedTarget(request, source.Target),
            SafeFullRefFingerprint = GitHubOperationSourceBindings.FullRef(request, source.Target.FullRef),
            SafeExpectedHeadFingerprint = GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha),
            SafeIntendedCommitFingerprint = GitHubOperationSourceBindings.IntendedCommit(request, source.IntendedCommitSha),
        };
        return bound with
        {
            SafeCheckWindowFingerprint = GitHubOperationSourceBindings.CheckWindow(bound),
        };
    }

    private static ProviderTargetEvidence OperationTargetEvidence(string operationScope)
        => new(
            Product: "github",
            ProductVersion: "github-rest",
            ApiSurfaceVersion: "github-rest-2022-11-28",
            EvidenceVersion: "target-v1",
            IsStale: false,
            ObservedAt: OperationNow,
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["safe_target_fingerprint"] = SafeFingerprint,
                ["operation_scope"] = operationScope,
            });

    private static ProviderRefPolicyEvidence RefPolicyEvidence()
        => new(
            SafeFingerprint,
            OperationNow,
            "fresh",
            AllowsFileMutation: true,
            AllowsCommit: true,
            AllowsNonForceUpdate: true);
}
