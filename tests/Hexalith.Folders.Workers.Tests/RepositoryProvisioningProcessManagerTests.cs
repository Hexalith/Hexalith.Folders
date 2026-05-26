using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Workers.RepositoryProvisioning;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class RepositoryProvisioningProcessManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequestedEventShouldCreateProviderRepositoryAndAppendBoundOutcome()
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        RecordingGitProvider provider = RecordingGitProvider.Success();
        RepositoryProvisioningProcessManager manager = CreateManager(repository, provider);

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(RepositoryProvisioningResultCode.Bound);
        provider.CreateRepositoryCalls.ShouldBe(1);
        ProviderRepositoryCreationRequest sent = provider.LastRequest.ShouldNotBeNull();
        sent.ManagedTenantId.ShouldBe("tenant-a");
        sent.OrganizationId.ShouldBe("organization-a");
        sent.ProviderBindingRef.ShouldBe("provider-binding-a");
        sent.RepositoryBindingId.ShouldBe("binding-a");
        sent.TargetEvidence.Metadata["operation_scope"].ShouldBe(ProviderOperationCatalog.RepositoryCreation);
        sent.CredentialModeRequirements.ShouldBe([ProviderCredentialMode.AppInstallationReference]);
        sent.AuthorizationEvidence.Fingerprint.ShouldBe("authz-a");
        sent.CorrelationId.ShouldBe("correlation-a");
        sent.IdempotencyKey.ShouldBe("idempotency-a");
        repository.EventsAppended.ShouldBe(1);
        RepositoryBound bound = repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<RepositoryBound>();
        bound.RepositoryBindingId.ShouldBe("binding-a");
        bound.ProviderBindingRef.ShouldBe("provider-binding-a");
        bound.IdempotencyKey.ShouldStartWith("provisioning-");
    }

    [Fact]
    public async Task KnownProviderFailureShouldAppendRepositoryBindingFailed()
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        RecordingGitProvider provider = RecordingGitProvider.Failing(ProviderFailureCategory.ProviderConflict, "provider_conflict");
        RepositoryProvisioningProcessManager manager = CreateManager(repository, provider);

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(RepositoryProvisioningResultCode.Failed);
        RepositoryBindingFailed failed = repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<RepositoryBindingFailed>();
        failed.FailureCategory.ShouldBe("provider_conflict");
    }

    [Theory]
    [InlineData(nameof(ProviderFailureCategory.UnknownProviderOutcome), RepositoryProvisioningResultCode.UnknownProviderOutcome, false)]
    [InlineData(nameof(ProviderFailureCategory.ReconciliationRequired), RepositoryProvisioningResultCode.ReconciliationRequired, true)]
    public async Task AmbiguousProviderOutcomesShouldAppendUnknownOutcome(
        string categoryName,
        RepositoryProvisioningResultCode expectedCode,
        bool expectedReconciliation)
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        ProviderFailureCategory category = Enum.Parse<ProviderFailureCategory>(categoryName);
        RecordingGitProvider provider = RecordingGitProvider.Failing(category, category.ToCategoryCode());
        RepositoryProvisioningProcessManager manager = CreateManager(repository, provider);

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        provider.CreateRepositoryCalls.ShouldBe(1);
        ProviderOutcomeUnknown unknown = repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<ProviderOutcomeUnknown>();
        unknown.ReconciliationRequired.ShouldBe(expectedReconciliation);
        unknown.OutcomeCategory.ShouldBe(category.ToCategoryCode());
    }

    [Fact]
    public async Task ProviderExceptionDuringCreationShouldAppendUnknownOutcome()
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        RecordingGitProvider provider = RecordingGitProvider.Throwing(new TimeoutException("repository-secret-timeout"));
        RepositoryProvisioningProcessManager manager = CreateManager(repository, provider);

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(RepositoryProvisioningResultCode.UnknownProviderOutcome);
        provider.CreateRepositoryCalls.ShouldBe(1);
        ProviderOutcomeUnknown unknown = repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<ProviderOutcomeUnknown>();
        unknown.ReconciliationRequired.ShouldBeFalse();
        unknown.OutcomeCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode());
        unknown.IdempotencyKey.ShouldStartWith("provisioning-");
    }

    [Fact]
    public async Task AlreadyProcessedRequestedEventShouldNotCallProviderAgain()
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        RecordingGitProvider provider = RecordingGitProvider.Success();
        RepositoryProvisioningProcessManager manager = CreateManager(repository, provider);

        await manager.HandleAsync(Requested(), Context(), TestContext.Current.CancellationToken);
        RepositoryProvisioningResult replay = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        replay.Code.ShouldBe(RepositoryProvisioningResultCode.AlreadyProcessed);
        provider.CreateRepositoryCalls.ShouldBe(1);
        repository.EventsAppended.ShouldBe(1);
    }

    [Fact]
    public async Task StateUnavailableShouldNotResolveProviderOrAppendOutcome()
    {
        RecordingFolderRepository repository = RepositoryWithUnboundFolder();
        RecordingProviderResolver resolver = new(RecordingGitProvider.Success());
        RepositoryProvisioningProcessManager manager = new(repository, resolver, new FixedTimeProvider(Now));

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(RepositoryProvisioningResultCode.StateUnavailable);
        resolver.ResolveCalls.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public async Task ContextMismatchShouldNotResolveProviderOrAppendOutcome()
    {
        RecordingFolderRepository repository = RepositoryWithRequestedBinding();
        RecordingProviderResolver resolver = new(RecordingGitProvider.Success());
        RepositoryProvisioningProcessManager manager = new(repository, resolver, new FixedTimeProvider(Now));

        RepositoryProvisioningResult result = await manager.HandleAsync(
            Requested(),
            Context() with { ManagedTenantId = "tenant-b" },
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(RepositoryProvisioningResultCode.ContextMismatch);
        resolver.ResolveCalls.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    private static RepositoryProvisioningProcessManager CreateManager(
        RecordingFolderRepository repository,
        RecordingGitProvider provider)
        => new(repository, new RecordingProviderResolver(provider), new FixedTimeProvider(Now));

    private static RecordingFolderRepository RepositoryWithRequestedBinding()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        repository.Seed(streamName, [Created(), Requested()]);
        return repository;
    }

    private static RecordingFolderRepository RepositoryWithUnboundFolder()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        repository.Seed(streamName, [Created()]);
        return repository;
    }

    private static RepositoryBindingRequested Requested()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            RepositoryBindingId: "binding-a",
            ProviderBindingRef: "provider-binding-a",
            RepositoryProfileRef: "profile-a",
            BranchRefPolicyRef: "policy-a",
            ActorPrincipalId: "user-a",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-a",
            IdempotencyFingerprint: "fingerprint-a",
            OccurredAt: Now);

    private static FolderCreated Created()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-a",
            DisplayName: "Folder A",
            Description: null,
            PathLabel: null,
            Tags: [],
            LifecycleState: FolderLifecycleState.Active,
            RepositoryBindingState: FolderRepositoryBindingState.Unbound,
            ActorPrincipalId: "user-a",
            CorrelationId: "correlation-create",
            TaskId: "task-create",
            IdempotencyKey: "idempotency-create",
            IdempotencyFingerprint: "fingerprint-create",
            OccurredAt: Now.AddMinutes(-1));

    private static RepositoryProvisioningContext Context()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "provider-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: new ProviderTargetEvidence(
                Product: "github",
                ProductVersion: "github-rest",
                ApiSurfaceVersion: "github-rest-2022-11-28",
                EvidenceVersion: "target-v1",
                IsStale: false,
                ObservedAt: Now,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = ProviderOperationCatalog.RepositoryCreation,
                }),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authz-a",
                Now,
                "fresh"));

    private sealed class RecordingGitProvider(ProviderRepositoryCreationResult? result, Exception? exception = null) : IGitProvider
    {
        public string ProviderFamily => "github";

        public string ProviderKey => "github";

        public int CreateRepositoryCalls { get; private set; }

        public ProviderRepositoryCreationRequest? LastRequest { get; private set; }

        public static RecordingGitProvider Success()
            => new(null);

        public static RecordingGitProvider Failing(ProviderFailureCategory category, string reasonCode)
            => new(ProviderRepositoryCreationResult.Failure(
                MinimalRequest(),
                category,
                reasonCode));

        public static RecordingGitProvider Throwing(Exception exception)
            => new(null, exception);

        public Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
            ProviderCapabilityDiscoveryRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderCapabilityDiscoveryResult.Failure(
                ProviderFailureCategory.UnsupportedProviderCapability,
                "not_used",
                request.CorrelationId));

        public Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
            ProviderRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateRepositoryCalls++;
            LastRequest = request;
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(result ?? ProviderRepositoryCreationResult.Success(request, equivalentExisting: false, "safe-target-a"));
        }

        public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
            ProviderCapabilityProfile current,
            ProviderCapabilityProfile candidate)
            => ProviderCapabilityProfileFactory.Compare(current, candidate);
    }

    private sealed class RecordingProviderResolver(IGitProvider provider) : IProviderCapabilityResolver
    {
        public int ResolveCalls { get; private set; }

        public Task<IGitProvider?> ResolveAsync(
            string providerFamily,
            string providerKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveCalls++;
            return Task.FromResult<IGitProvider?>(provider);
        }
    }

    private sealed class RecordingFolderRepository : IFolderRepository
    {
        private readonly Dictionary<string, FolderState> _states = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fingerprints = new(StringComparer.Ordinal);

        public int EventsAppended { get; private set; }

        public IReadOnlyList<IFolderEvent> LastAppendedEvents { get; private set; } = [];

        public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
            => FolderStreamName.Create(managedTenantId, folderId);

        public FolderState Load(FolderStreamName streamName)
            => _states.TryGetValue(streamName.Value, out FolderState? state) ? state : FolderState.Empty;

        public FolderAppendOutcome AppendIfFingerprintAbsent(
            FolderStreamName streamName,
            string idempotencyKey,
            string fingerprint,
            IReadOnlyList<IFolderEvent> events)
        {
            string ledgerKey = $"{streamName.Value}|{idempotencyKey}";
            if (_fingerprints.TryGetValue(ledgerKey, out string? existing))
            {
                return string.Equals(existing, fingerprint, StringComparison.Ordinal)
                    ? FolderAppendOutcome.FingerprintMatched
                    : FolderAppendOutcome.FingerprintConflict;
            }

            EventsAppended += events.Count;
            LastAppendedEvents = events;
            _states[streamName.Value] = Load(streamName).Apply(events, streamName);
            _fingerprints[ledgerKey] = fingerprint;
            return FolderAppendOutcome.Appended;
        }

        public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
            FolderStreamName streamName,
            string idempotencyKey,
            out string? fingerprint)
        {
            string ledgerKey = $"{streamName.Value}|{idempotencyKey}";
            return _fingerprints.TryGetValue(ledgerKey, out fingerprint)
                ? FolderIdempotencyLookupResult.Found
                : FolderIdempotencyLookupResult.Missing;
        }

        public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
            => _states[streamName.Value] = FolderState.Empty.Apply(events, streamName);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ProviderRepositoryCreationRequest MinimalRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "provider-binding-a",
            RepositoryBindingId: "binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: new ProviderTargetEvidence(
                "github",
                "github-rest",
                "github-rest-2022-11-28",
                "target-v1",
                IsStale: false,
                ObservedAt: Now,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot("authz-a", Now, "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a");
}
