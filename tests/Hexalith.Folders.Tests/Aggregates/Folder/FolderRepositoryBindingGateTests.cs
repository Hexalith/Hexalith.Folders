using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderRepositoryBindingGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeFolderReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = new();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MissingAuthoritativeTenant);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task PayloadTenantMismatchShouldDenyBeforeFolderReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(payloadTenantId: "tenant-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.TenantAccessDenied);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentReplayShouldReturnBeforeReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = RequestedRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessFailed());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(ProviderFailureCategory.ProviderUnavailable));
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task BindingInProgressWithDifferentKeyShouldRejectBeforeReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = RequestedRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(idempotencyKey: "idempotency-bind-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ArchivedFolderShouldRejectBeforeIdempotencyReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = ArchivedRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(idempotencyKey: "idempotency-bind-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task BoundRepositoryWithDifferentKeyShouldRejectBeforeIdempotencyReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = BoundRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(idempotencyKey: "idempotency-bind-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task FailedReadinessShouldRejectBeforeBindingReaderProviderOrAppend()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessFailed());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.ProviderReadinessFailed);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.ExistingRepositoryBinding);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(ProviderReadinessResultCode.ProjectionStale), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.StaleProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.ProjectionUnavailable), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.UnavailableProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.ReadModelUnavailable), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.UnavailableProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.AuthenticationRequired), nameof(ProviderFailureCategory.ProviderAuthenticationRequired), "failed", nameof(FolderResultCode.TenantAccessDenied))]
    [InlineData(nameof(ProviderReadinessResultCode.AuthorizationDenied), nameof(ProviderFailureCategory.ProviderPermissionInsufficient), "failed", nameof(FolderResultCode.TenantAccessDenied))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.UnsupportedProviderCapability), "failed", nameof(FolderResultCode.UnsupportedProviderCapability))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.UnknownProviderOutcome), "failed", nameof(FolderResultCode.UnknownProviderOutcome))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ReconciliationRequired), "failed", nameof(FolderResultCode.ReconciliationRequired))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderUnavailable), "degraded", nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderRateLimited), "degraded", nameof(FolderResultCode.ProviderRateLimited))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderTransientFailure), "degraded", nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderPermissionInsufficient), "failed", nameof(FolderResultCode.ProviderPermissionInsufficient))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderConflict), "failed", nameof(FolderResultCode.RepositoryConflict))]
    public async Task ReadinessFailuresShouldMapToCanonicalFolderResultBeforeBindingReaderProviderOrAppend(
        string readinessCodeName,
        string failureCategoryName,
        string status,
        string expectedResultName)
    {
        RecordingFolderRepository repository = SeededRepository();
        ProviderReadinessResultCode readinessCode = Enum.Parse<ProviderReadinessResultCode>(readinessCodeName);
        ProviderFailureCategory failureCategory = Enum.Parse<ProviderFailureCategory>(failureCategoryName);
        FolderResultCode expectedResult = Enum.Parse<FolderResultCode>(expectedResultName);
        RecordingRepositoryBindingReadinessValidator readiness = new(
            Readiness(readinessCode, status, failureCategory));
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedResult);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.ExistingRepositoryBinding);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DurableEquivalentReplayShouldReturnBeforeReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = SeededRepository();
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-bind-a", BindingFingerprint());
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessFailed());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(ProviderFailureCategory.ProviderUnavailable));
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DurableIdempotencyConflictShouldReturnBeforeReadinessBindingOrProviderObservation()
    {
        RecordingFolderRepository repository = SeededRepository();
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-bind-a", "different-fingerprint");
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
        bindingReader.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(ProviderFailureCategory.ProviderConflict), nameof(FolderResultCode.RepositoryConflict))]
    [InlineData(nameof(ProviderFailureCategory.ProviderRateLimited), nameof(FolderResultCode.ProviderRateLimited))]
    [InlineData(nameof(ProviderFailureCategory.UnknownProviderOutcome), nameof(FolderResultCode.UnknownProviderOutcome))]
    [InlineData(nameof(ProviderFailureCategory.ReconciliationRequired), nameof(FolderResultCode.ReconciliationRequired))]
    public async Task ProviderBindingFailuresShouldMapToCanonicalResultAndAppendMetadataOnlyOutcome(
        string failureCategoryName,
        string expectedResultName)
    {
        ProviderFailureCategory failureCategory = Enum.Parse<ProviderFailureCategory>(failureCategoryName);
        FolderResultCode expectedResult = Enum.Parse<FolderResultCode>(expectedResultName);
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(failureCategory));
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedResult);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.Count.ShouldBe(2);
        repository.LastAppendedEvents[0].ShouldBeOfType<ExistingRepositoryBindingRequested>();
        if (failureCategory is ProviderFailureCategory.UnknownProviderOutcome or ProviderFailureCategory.ReconciliationRequired)
        {
            repository.LastAppendedEvents[1].ShouldBeOfType<ProviderOutcomeUnknown>();
        }
        else
        {
            repository.LastAppendedEvents[1].ShouldBeOfType<RepositoryBindingFailed>();
        }

        readiness.Calls.ShouldBe(1);
        bindingReader.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        resolver.ProviderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReadyRequestShouldValidateProviderThenAppendRequestedAndBoundEvents()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryBindingReadinessValidator readiness = new(ReadinessReady());
        RecordingProviderBindingReader bindingReader = new(Binding());
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = Service(repository, readiness, bindingReader, resolver);

        FolderResult result = await service.BindAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents[0].ShouldBeOfType<ExistingRepositoryBindingRequested>();
        repository.LastAppendedEvents[1].ShouldBeOfType<RepositoryBound>();
        readiness.Calls.ShouldBe(1);
        bindingReader.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        resolver.ProviderCalls.ShouldBe(1);
    }

    private static RepositoryBindingService Service(
        IFolderRepository repository,
        IRepositoryBindingReadinessValidator readiness,
        IProviderReadinessBindingReader bindingReader,
        IProviderCapabilityResolver providerResolver)
        => new(
            AuthorizationService(),
            readiness,
            bindingReader,
            providerResolver,
            repository,
            new FixedTimeProvider(Now));

    private static BindRepositoryRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string idempotencyKey = "idempotency-bind-a",
        string? payloadTenantId = null)
        => new(
            authoritativeTenantId,
            principalId,
            EventStoreClaimTransformEvidence.Allowed(
                authoritativeTenantId,
                principalId,
                [RepositoryBindingService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            ProviderBindingRef: "provider-binding-a",
            ExternalRepositoryRef: "external-repository-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            CredentialScopeClass: "tenant-installation",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: payloadTenantId,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static string BindingFingerprint()
    {
        BindRepository command = FolderCommandFactory.BindRepository(
            repositoryBindingId: FolderCommandValidator.DeriveRepositoryBindingId(
                "tenant-a",
                "folder-a",
                "provider-binding-a",
                "external-repository-a",
                "branch-ref-policy-a"),
            credentialScopeClass: "tenant-installation",
            actorPrincipalId: "user-a");

        return FolderCommandValidator.Validate(command).IdempotencyFingerprint.ShouldNotBeNull();
    }

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static RecordingFolderRepository RequestedRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderState active = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(
            active,
            FolderCommandFactory.BindRepository(
                repositoryBindingId: FolderCommandValidator.DeriveRepositoryBindingId(
                    "tenant-a",
                    "folder-a",
                    "provider-binding-a",
                    "external-repository-a",
                    "branch-ref-policy-a"),
                credentialScopeClass: "tenant-installation",
                actorPrincipalId: "user-a"),
            Now);
        repository.Seed(streamName, [.. created.Events, .. requested.Events]);
        return repository;
    }

    private static RecordingFolderRepository ArchivedRepository()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState active = repository.Load(streamName);
        repository = new RecordingFolderRepository();
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive());
        repository.Seed(streamName, [.. created.Events, .. archived.Events]);
        return repository;
    }

    private static RecordingFolderRepository BoundRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        string repositoryBindingId = FolderCommandValidator.DeriveRepositoryBindingId(
            "tenant-a",
            "folder-a",
            "provider-binding-a",
            "external-repository-a",
            "branch-ref-policy-a");
        FolderResult bindRequested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, streamName),
            FolderCommandFactory.BindRepository(
                repositoryBindingId: repositoryBindingId,
                credentialScopeClass: "tenant-installation",
                actorPrincipalId: "user-a"),
            Now);
        RepositoryBound bound = new(
            "tenant-a",
            "organization-a",
            "folder-a",
            repositoryBindingId,
            "provider-binding-a",
            "correlation-bound-a",
            "task-bound-a",
            "idempotency-bound-a",
            "fingerprint-bound-a",
            Now.AddMinutes(1));
        repository.Seed(streamName, [.. created.Events, .. bindRequested.Events, bound]);
        return repository;
    }

    private static LayeredFolderAuthorizationService AuthorizationService()
        => new(
            new TenantAccessAuthorizer(
                TenantStore(),
                new FixedUtcClock(Now),
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            new RecordingFolderPermissionEvidenceProvider(),
            new RecordingEventStoreAuthorizationValidator(),
            new RecordingDaprPolicyEvidenceProvider(),
            new FixedUtcClock(Now));

    private static IFolderTenantAccessProjectionStore TenantStore()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private static OrganizationProviderBinding Binding()
        => new(
            "tenant-a",
            "organization-a",
            "provider-binding-a",
            "github",
            "credential-reference-a",
            new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            "correlation-binding-a",
            "task-binding-a",
            "idempotency-provider-binding-a",
            "fingerprint-provider-binding-a",
            "configured",
            Now.AddMinutes(-1));

    private static ProviderReadinessValidationResult ReadinessReady()
        => Readiness("ready", ProviderFailureCategory.None);

    private static ProviderReadinessValidationResult ReadinessFailed()
        => Readiness("failed", ProviderFailureCategory.ProviderReadinessFailed);

    private static ProviderReadinessValidationResult Readiness(string status, ProviderFailureCategory category)
        => Readiness(ProviderReadinessResultCode.Allowed, status, category);

    private static ProviderReadinessValidationResult Readiness(
        ProviderReadinessResultCode code,
        string status,
        ProviderFailureCategory category)
        => new(
            code,
            status,
            category == ProviderFailureCategory.None ? "success" : category.ToCategoryCode(),
            category == ProviderFailureCategory.None ? "none" : $"{category.ToCategoryCode()}_remediation",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: category == ProviderFailureCategory.None ? "none" : "contact_operator",
            CorrelationId: "correlation-a",
            ProviderReference: "provider-binding-a",
            ProviderBindingRef: "provider-binding-a",
            CapabilityProfileRef: "profile-a",
            Evidence: null,
            new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
            category,
            category.ToCategoryCode());

    private sealed class RecordingRepositoryBindingReadinessValidator(ProviderReadinessValidationResult result)
        : IRepositoryBindingReadinessValidator
    {
        public int Calls { get; private set; }

        public ProviderReadinessValidationRequest? LastRequest { get; private set; }

        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingProviderBindingReader(OrganizationProviderBinding? binding)
        : IProviderReadinessBindingReader
    {
        public int Calls { get; private set; }

        public ProviderReadinessBindingReadRequest? LastRequest { get; private set; }

        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(binding);
        }
    }

    private sealed class RecordingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed(
                "folder-a:7",
                organizationId: "organization-a"));
    }

    private sealed class RecordingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator-a"));
    }

    private sealed class RecordingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
