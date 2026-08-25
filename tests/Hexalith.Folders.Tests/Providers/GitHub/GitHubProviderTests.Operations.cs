using System.Text.Json;
using System.Net;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed partial class GitHubProviderTests
{
    private const string SafeFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset OperationNow =
        DateTimeOffset.Parse("2026-08-25T08:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task StagesOrderedChangesThroughAuthorizedGitHubBoundaryWithoutMovingRef()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

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
    public async Task EquivalentMutationAndCommitReplayNeverDispatchesASecondProviderEffect()
    {
        ProviderIdempotencyAdmission replay = new(
            ProviderIdempotencyDisposition.EquivalentReplay,
            "safe-intent-reference",
            SafeFingerprint,
            SafeFingerprint);
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
        mutation.OpaqueOperationReference.ShouldNotBe(replay.PriorReconciliationReference);
        mutation.ReconciliationReference.ShouldBe(replay.PriorReconciliationReference);
        commit.OpaqueOperationReference.ShouldNotBe(replay.PriorReconciliationReference);
        commit.ReconciliationReference.ShouldBe(replay.PriorReconciliationReference);
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
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            api);

        ProviderFileMutationResult mutation = await provider.StageFileChangesAsync(FileMutationRequest(), TestContext.Current.CancellationToken);
        ProviderCommitResult commit = await provider.CommitAsync(CommitRequest(), TestContext.Current.CancellationToken);
        ProviderOperationStatusResult status = await provider.GetOperationStatusAsync(StatusRequest(), TestContext.Current.CancellationToken);

        mutation.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        commit.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        status.IsSuccess.ShouldBeFalse();
        status.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderFailureKnown);
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
    public async Task StatusRejectsIdempotencyKeyAndInvalidBudgetBeforeSourceAccess()
    {
        ProviderOperationStatusRequest baseline = StatusRequest();
        ProviderOperationStatusRequest[] invalidRequests =
        [
            baseline with { IdempotencyKey = "idempotency-not-allowed" },
            baseline with { CheckNumber = 6 },
            baseline with { RequestedAt = baseline.ReconciliationStartedAt.AddMinutes(16) },
        ];

        foreach (ProviderOperationStatusRequest request in invalidRequests)
        {
            RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
            RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
            RecordingGitHubApiClientFactory clientFactory = new(RecordingGitHubApiClient.Success());
            GitHubProvider provider = new(
                credentialResolver,
                clientFactory,
                RecordingProviderRepositoryTargetResolver.Success(),
                sourceResolver);

            ProviderOperationStatusResult result = await provider.GetOperationStatusAsync(
                request,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            sourceResolver.StatusCalls.ShouldBe(0);
            credentialResolver.Calls.ShouldBe(0);
            clientFactory.Calls.ShouldBe(0);
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

    [Fact]
    public async Task FifthUnavailableStatusObservationRequiresReconciliationWithoutMutationRetry()
    {
        RecordingProviderOperationSourceResolver sourceResolver = RecordingProviderOperationSourceResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.StatusFailure(GitHubApiFailureCondition.ServerUnavailable);
        GitHubProvider provider = OperationProvider(sourceResolver, credentialResolver, apiClient);

        ProviderOperationStatusResult result = await provider.GetOperationStatusAsync(
            StatusRequest() with { CheckNumber = 5 },
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.Retryable.ShouldBeFalse();
        apiClient.StatusCalls.ShouldBe(1);
        apiClient.FileMutationCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task FifthNotAppliedStatusObservationRequiresReconciliation()
    {
        ProviderOperationStatusResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.Status(ProviderOperationStatusKind.NotApplied))
            .GetOperationStatusAsync(StatusRequest() with { CheckNumber = 5 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task FifthUnknownStatusObservationRequiresReconciliation()
    {
        ProviderOperationStatusResult result = await OperationProvider(
            RecordingProviderOperationSourceResolver.Success(),
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            RecordingGitHubApiClient.StatusFailure(GitHubApiFailureCondition.AmbiguousMutationResponse))
            .GetOperationStatusAsync(StatusRequest() with { CheckNumber = 5 }, TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task ProductionRegistrationResolvesOneConcreteGitHubProviderAndFailsClosedWithoutOperationSource()
    {
        ServiceCollection services = new();
        services.AddLogging();
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
            1 => OperationJson(HttpStatusCode.OK, JsonSerializer.Serialize(new { @ref = "refs/heads/main", @object = new { sha = RecordingProviderOperationSourceResolver.HeadSha } })),
            2 => OperationJson(HttpStatusCode.OK, JsonSerializer.Serialize(new { sha = RecordingProviderOperationSourceResolver.HeadSha, tree = new { sha = "4444444444444444444444444444444444444444" }, parents = Array.Empty<object>() })),
            3 => OperationJson(HttpStatusCode.OK, """{"sha":"4444444444444444444444444444444444444444","tree":[{"path":"docs/two.txt","type":"blob"}],"truncated":false}"""),
            4 => OperationJson(HttpStatusCode.Created, """{"sha":"5555555555555555555555555555555555555555"}"""),
            _ => OperationJson(HttpStatusCode.Created, JsonSerializer.Serialize(new { sha = RecordingProviderOperationSourceResolver.TreeSha })),
        }));
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IProviderOperationSourceResolver>(RecordingProviderOperationSourceResolver.Success());
        services.AddSingleton<IGitHubCredentialResolver>(RecordingGitHubCredentialResolver.Success("token-sentinel"));
        services.AddSingleton<IGitHubApiClientFactory>(new OctokitGitHubApiClientFactory(
            () => new Octokit.Internal.HttpClientAdapter(() => handler),
            () => handler));
        services.AddFoldersProviderReadiness();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IGitProvider github = serviceProvider.GetServices<IGitProvider>().Single(static provider => provider is GitHubProvider);
        ProviderFileMutationResult result = await github.StageFileChangesAsync(
            FileMutationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        handler.Requests.Count.ShouldBe(5);
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

    private static HttpResponseMessage OperationJson(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static GitHubProvider OperationProvider(
        RecordingProviderOperationSourceResolver sourceResolver,
        RecordingGitHubCredentialResolver credentialResolver,
        RecordingGitHubApiClient apiClient)
        => new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            sourceResolver);

    private static ProviderFileMutationRequest FileMutationRequest()
        => new(
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
            ChangeSetReference: "change-set-a",
            SafeChangeSetFingerprint: SafeFingerprint,
            Changes:
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
            ],
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.Fresh,
                "safe-intent-reference"));

    private static ProviderCommitRequest CommitRequest()
        => new(
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
            StagedChangeSetReference: "staged-change-set-a",
            SafeStagedChangeSetFingerprint: SafeFingerprint,
            CommitMessageReference: "commit-message-a",
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.Fresh,
                "safe-intent-reference"));

    private static ProviderOperationStatusRequest StatusRequest()
        => new(
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
            OperationReference: SafeFingerprint,
            SafeExpectedHeadFingerprint: SafeFingerprint,
            SafeIntendedCommitFingerprint: SafeFingerprint,
            CheckNumber: 1,
            ReconciliationStartedAt: OperationNow,
            RequestedAt: OperationNow.AddMinutes(1),
            CorrelationId: "correlation-a");

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
