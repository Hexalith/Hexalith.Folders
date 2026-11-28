using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

/// <summary>
/// Verifies bounded single-use initial process authorization reuse and readiness binding reuse.
/// </summary>
public sealed class ProcessInitialAuthorizationScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SingleUseScopeReusesOnlyImmediateMatchingMutationAndAlwaysReauthorizesLive()
    {
        CountingPermissionProvider permissions = new();
        LayeredFolderAuthorizationService service = CreateService(permissions);
        LayeredFolderAuthorizationContext context = MutationContext("folder-a", "create_folder");

        LayeredFolderAuthorizationResult initial = await service.AuthorizeAsync(context, TestContext.Current.CancellationToken);
        initial.IsAllowed.ShouldBeTrue();
        permissions.Calls.ShouldBe(1);

        using (ProcessInitialAuthorizationScope.Enter(context, initial))
        {
            LayeredFolderAuthorizationResult reused = await service.AuthorizeAsync(context, TestContext.Current.CancellationToken);
            reused.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(1);

            // Second AuthorizeAsync must hit live authority because the scope was consumed.
            LayeredFolderAuthorizationResult second = await service.AuthorizeAsync(context, TestContext.Current.CancellationToken);
            second.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(2);
        }

        // Reauthorization must never consume an unconsumed initial scope AND permanently invalidates it.
        using (ProcessInitialAuthorizationScope.Enter(context, initial))
        {
            LayeredFolderAuthorizationResult reauth = await service.ReauthorizeMutationAsync(
                context, includeFolderAcl: true, TestContext.Current.CancellationToken);
            reauth.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(3);

            // Subsequent AuthorizeAsync after reauthorization must hit live authority (invalidated).
            LayeredFolderAuthorizationResult afterReauth = await service.AuthorizeAsync(
                context, TestContext.Current.CancellationToken);
            afterReauth.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(4);
        }

        // Strict metadata reads, gateway reads, and tenant-scoped checks never consume the initial scope.
        using (ProcessInitialAuthorizationScope.Enter(context, initial))
        {
            LayeredFolderAuthorizationResult strictRead = await service.AuthorizeCurrentMetadataReadAsync(
                context, TestContext.Current.CancellationToken);
            strictRead.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(5);

            LayeredFolderAuthorizationResult gatewayRead = await service.AuthorizeGatewayReadAsync(
                context with { OperationPolicy = LayeredFolderOperationPolicy.BoundedDiagnosticRead() },
                includeFolderAcl: true,
                TestContext.Current.CancellationToken);
            gatewayRead.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(6);

            LayeredFolderAuthorizationResult tenantScoped = await service.AuthorizeTenantScopedAsync(
                context, TestContext.Current.CancellationToken);
            tenantScoped.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(6);

            // Scope is still consumable once for its exact initial mutation context.
            LayeredFolderAuthorizationResult consumed = await service.AuthorizeAsync(
                context, TestContext.Current.CancellationToken);
            consumed.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(6);
        }
    }

    [Fact]
    public async Task MismatchedScopeActorOrClientTenantNeverConsumesInitialAuthorization()
    {
        CountingPermissionProvider permissions = new();
        LayeredFolderAuthorizationService service = CreateService(permissions);
        LayeredFolderAuthorizationContext context = MutationContext("folder-a", "lock_workspace");

        LayeredFolderAuthorizationResult initial = await service.AuthorizeAsync(context, TestContext.Current.CancellationToken);
        initial.IsAllowed.ShouldBeTrue();
        permissions.Calls.ShouldBe(1);

        using (ProcessInitialAuthorizationScope.Enter(context, initial))
        {
            // Different folder scope must not consume the initial scope.
            LayeredFolderAuthorizationResult otherFolder = await service.AuthorizeAsync(
                MutationContext("folder-b", "lock_workspace"), TestContext.Current.CancellationToken);
            otherFolder.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(2);

            // Different ActorSafeIdentifier must not consume the initial scope.
            LayeredFolderAuthorizationResult otherActor = await service.AuthorizeAsync(
                context with { ActorSafeIdentifier = "actor-different" }, TestContext.Current.CancellationToken);
            otherActor.IsAllowed.ShouldBeTrue();
            otherActor.AllowedContext!.ActorSafeIdentifier.ShouldBe("actor-different");
            permissions.Calls.ShouldBe(3);

            // Client-controlled tenant mismatch must still fail closed even while the scope is active.
            LayeredFolderAuthorizationContext spoofedTenant = context with
            {
                ClientControlledTenantValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["eventstore_envelope_tenant"] = "tenant-other",
                },
            };
            LayeredFolderAuthorizationResult denied = await service.AuthorizeAsync(
                spoofedTenant, TestContext.Current.CancellationToken);
            denied.IsAllowed.ShouldBeFalse();
            denied.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.ClaimTransformDenied);
            permissions.Calls.ShouldBe(3);

            // The unconsumed scope still serves the exact matching request once.
            LayeredFolderAuthorizationResult exact = await service.AuthorizeAsync(context, TestContext.Current.CancellationToken);
            exact.IsAllowed.ShouldBeTrue();
            permissions.Calls.ShouldBe(3);
        }

        // Out-of-order disposal of nested scopes must not clobber the active inner scope.
        LayeredFolderAuthorizationContext outerContext = MutationContext("folder-outer", "create_folder");
        LayeredFolderAuthorizationContext innerContext = MutationContext("folder-inner", "create_folder");
        IDisposable outerLease = ProcessInitialAuthorizationScope.Enter(outerContext, initial);
        IDisposable innerLease = ProcessInitialAuthorizationScope.Enter(innerContext, initial);
        outerLease.Dispose();
        ProcessInitialAuthorizationScope.TryConsume(innerContext, out LayeredFolderAuthorizationResult? innerConsumed).ShouldBeTrue();
        innerConsumed.ShouldNotBeNull();
        innerLease.Dispose();
    }

    [Fact]
    public async Task RepositoryBindingServiceReusesReadinessResolvedBindingAndFallsBackWhenAbsentOrMismatched()
    {
        OrganizationProviderBinding matchingBinding = new(
            "tenant-a",
            "tenant-a",
            "provider-binding-a",
            "github",
            "cred-ref-a",
            new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            "corr-a",
            "task-a",
            "idem-bind-cfg",
            "fingerprint-bind-cfg",
            "configured",
            Now);

        CountingBindingReader reader = new(matchingBinding);
        LocalRecordingFolderRepository repository = SeededRepository();
        RepositoryBindingService serviceWithResolved = new(
            CreateService(new CountingPermissionProvider()),
            new StaticReadinessValidator(matchingBinding),
            reader,
            new FixedProviderResolver(FakeGitProvider.GitHubLike()),
            repository,
            new FixedTimeProvider(Now));

        FolderResult withResolved = await serviceWithResolved.BindAsync(BindRequest("idem-1"), TestContext.Current.CancellationToken);
        withResolved.Code.ShouldBe(FolderResultCode.Accepted);
        reader.Calls.ShouldBe(0);

        // When ResolvedBinding is null (e.g. non-service validator), the reader is consulted as before.
        LocalRecordingFolderRepository fallbackRepository = SeededRepository();
        RepositoryBindingService serviceWithoutResolved = new(
            CreateService(new CountingPermissionProvider()),
            new StaticReadinessValidator(resolvedBinding: null),
            reader,
            new FixedProviderResolver(FakeGitProvider.GitHubLike()),
            fallbackRepository,
            new FixedTimeProvider(Now));

        FolderResult withoutResolved = await serviceWithoutResolved.BindAsync(BindRequest("idem-2"), TestContext.Current.CancellationToken);
        withoutResolved.Code.ShouldBe(FolderResultCode.Accepted);
        reader.Calls.ShouldBe(1);

        // Foreign tenant ResolvedBinding is never trusted; the reader is consulted instead.
        LocalRecordingFolderRepository mismatchRepository = SeededRepository();
        RepositoryBindingService serviceWithForeign = new(
            CreateService(new CountingPermissionProvider()),
            new StaticReadinessValidator(matchingBinding with { ManagedTenantId = "tenant-foreign" }),
            reader,
            new FixedProviderResolver(FakeGitProvider.GitHubLike()),
            mismatchRepository,
            new FixedTimeProvider(Now));

        FolderResult withForeign = await serviceWithForeign.BindAsync(BindRequest("idem-3"), TestContext.Current.CancellationToken);
        withForeign.Code.ShouldBe(FolderResultCode.Accepted);
        reader.Calls.ShouldBe(2);

        // Same-tenant ProviderBindingRef mismatch is never trusted; the reader is consulted instead.
        LocalRecordingFolderRepository mismatchRefRepository = SeededRepository();
        RepositoryBindingService serviceWithOtherBindingRef = new(
            CreateService(new CountingPermissionProvider()),
            new StaticReadinessValidator(matchingBinding with { ProviderBindingRef = "provider-binding-other" }),
            reader,
            new FixedProviderResolver(FakeGitProvider.GitHubLike()),
            mismatchRefRepository,
            new FixedTimeProvider(Now));

        FolderResult withOtherRef = await serviceWithOtherBindingRef.BindAsync(BindRequest("idem-4"), TestContext.Current.CancellationToken);
        withOtherRef.Code.ShouldBe(FolderResultCode.Accepted);
        reader.Calls.ShouldBe(3);

        // Attaching ResolvedBinding must preserve record value equality and never leak credentials in ToString().
        ProviderReadinessValidationResult plain = new(
            ProviderReadinessResultCode.Allowed, "ready", "provider_ready", "no_action",
            false, null, "no_action", "corr-a", "provider-binding-a", "provider-binding-a",
            "v1", null, new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
            ProviderFailureCategory.None, "none");
        ProviderReadinessValidationResult attached = new ProviderReadinessValidationResult(
            ProviderReadinessResultCode.Allowed, "ready", "provider_ready", "no_action",
            false, null, "no_action", "corr-a", "provider-binding-a", "provider-binding-a",
            "v1", null, new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
            ProviderFailureCategory.None, "none").WithResolvedBinding(matchingBinding);
        attached.ShouldBe(plain);
        attached.GetHashCode().ShouldBe(plain.GetHashCode());
        attached.ToString().ShouldBe(plain.ToString());
        attached.ToString().ShouldNotContain("cred-ref-a");
    }

    private static LayeredFolderAuthorizationContext MutationContext(string folderId, string actionToken)
        => new(
            "tenant-a",
            "principal-a",
            "principal-a",
            actionToken,
            LayeredFolderOperationPolicy.Mutation(),
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [actionToken]),
            folderId,
            "corr-a",
            "task-a",
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal));

    private static LayeredFolderAuthorizationService CreateService(CountingPermissionProvider permissions)
    {
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["principal-a"] = new("principal-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddSeconds(-30),
        }).GetAwaiter().GetResult();
        FixedUtcClock clock = new(Now);
        return new(
            new TenantAccessAuthorizer(
                tenants,
                clock,
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            permissions,
            new AllowingEventStoreValidator(),
            new AllowingDaprPolicyProvider(),
            clock);
    }

    private static LocalRecordingFolderRepository SeededRepository()
    {
        LocalRecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static BindRepositoryRequest BindRequest(string idempotencyKey)
        => new(
            "tenant-a",
            "principal-a",
            EventStoreClaimTransformEvidence.Allowed(
                "tenant-a",
                "principal-a",
                [RepositoryBindingService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            ProviderBindingRef: "provider-binding-a",
            ExternalRepositoryRef: "external-repository-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            CredentialScopeClass: "tenant-installation",
            CorrelationId: "corr-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private sealed class CountingPermissionProvider : IFolderPermissionEvidenceProvider
    {
        public int Calls { get; private set; }

        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(FolderPermissionEvidenceResult.Allowed($"wm-{Calls}", organizationId: "tenant-a"));
        }
    }

    private sealed class CountingBindingReader(OrganizationProviderBinding binding) : IProviderReadinessBindingReader
    {
        public int Calls { get; private set; }

        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<OrganizationProviderBinding?>(binding);
        }
    }

    private sealed class StaticReadinessValidator(OrganizationProviderBinding? resolvedBinding)
        : IRepositoryBindingReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            ProviderReadinessValidationResult result = new(
                ProviderReadinessResultCode.Allowed,
                "ready",
                "provider_ready",
                "no_action",
                false,
                null,
                "no_action",
                request.CorrelationId ?? "corr-a",
                request.ProviderBindingRef,
                request.ProviderBindingRef,
                "v1",
                null,
                new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
                ProviderFailureCategory.None,
                "none");
            if (resolvedBinding is not null)
            {
                _ = result.WithResolvedBinding(resolvedBinding);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FixedProviderResolver(IGitProvider provider) : IProviderCapabilityResolver
    {
        public Task<IGitProvider?> ResolveAsync(
            string providerFamily,
            string providerKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IGitProvider?>(provider);
    }

    private sealed class AllowingEventStoreValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("es-wm"));
    }

    private sealed class AllowingDaprPolicyProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr-wm"));
    }

    private sealed class FixedUtcClock(DateTimeOffset now) : IUtcClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class LocalRecordingFolderRepository : IFolderRepository
    {
        private readonly Dictionary<string, List<IFolderEvent>> _streams = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Stream, string Key), string> _fingerprints = [];

        public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
            => FolderStreamName.Create(managedTenantId, folderId);

        public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
            FolderStreamName streamName,
            string idempotencyKey,
            out string? idempotencyFingerprint)
            => _fingerprints.TryGetValue((streamName.Value, idempotencyKey), out idempotencyFingerprint)
                ? FolderIdempotencyLookupResult.Found
                : FolderIdempotencyLookupResult.Missing;

        public FolderState Load(FolderStreamName streamName)
            => _streams.TryGetValue(streamName.Value, out List<IFolderEvent>? events)
                ? FolderState.Empty.Apply(events, streamName)
                : FolderState.Empty;

        public FolderAppendOutcome AppendIfFingerprintAbsent(
            FolderStreamName streamName,
            string idempotencyKey,
            string idempotencyFingerprint,
            IReadOnlyList<IFolderEvent> events)
        {
            if (_fingerprints.TryGetValue((streamName.Value, idempotencyKey), out string? existingFingerprint))
            {
                return string.Equals(existingFingerprint, idempotencyFingerprint, StringComparison.Ordinal)
                    ? FolderAppendOutcome.FingerprintMatched
                    : FolderAppendOutcome.FingerprintConflict;
            }

            if (!_streams.TryGetValue(streamName.Value, out List<IFolderEvent>? existing))
            {
                existing = [];
                _streams[streamName.Value] = existing;
            }

            existing.AddRange(events);
            _fingerprints[(streamName.Value, idempotencyKey)] = idempotencyFingerprint;
            return FolderAppendOutcome.Appended;
        }

        public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
            => _streams[streamName.Value] = [.. events];
    }
}
