using System.Text.Json;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Providers.GitHub;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoProviderReadinessValidationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 6, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("existing_repository_binding")]
    public async Task ValidateAsyncShouldBuildForgejoDiscoveryFromAuthorizedBindingMetadata(string? policyScope)
    {
        OrganizationProviderBinding binding = Binding();
        Dictionary<string, string> metadata = new(binding.BranchPolicy.Metadata, StringComparer.Ordinal);
        if (policyScope is null) metadata.Remove("operation_scope");
        else metadata["operation_scope"] = policyScope;
        binding = binding with { BranchPolicy = binding.BranchPolicy with { Metadata = metadata } };
        InMemoryProviderCapabilityEvidenceStore capabilityEvidence = new();
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("forgejo-token-1234567890"),
            new RecordingForgejoApiClientFactory(apiClient),
            new UnconfiguredProviderRepositoryTargetResolver(),
            new UnconfiguredProviderOperationSourceResolver(),
            timeProvider: new ForgejoFixedTimeProvider(Now));
        RecordingProviderCapabilityResolver resolver = new(provider);
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(binding),
            readinessStore,
            resolver,
            capabilityEvidence);

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("ready", result.ReasonCode);
        result.Evidence.ShouldNotBeNull().RepositoryCreation.ShouldBe("temporarily_unavailable");
        result.Evidence.FileOperations.ShouldBe("supported");
        result.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        result.CapabilityProfileRef.ShouldNotBeNullOrWhiteSpace();
        apiClient.LastRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("16.0.3");
        apiClient.LastRequest.ShouldNotBeNull().CredentialMode.ShouldBe(ProviderCredentialMode.UserDelegatedReference);
        ProviderCapabilityDiscoveryRequest storedAttempt = capabilityEvidence.Attempts.ShouldHaveSingleItem();
        storedAttempt.CredentialModeRequirements.ShouldBe([ProviderCredentialMode.UserDelegatedReference]);
        storedAttempt.TargetEvidence.Metadata["operation_scope"].ShouldBe("readiness");
        string attemptJson = JsonSerializer.Serialize(capabilityEvidence.Attempts);
        attemptJson.ShouldNotContain("https://forgejo.example.test", Case.Sensitive);
        readinessStore.LastStored.ShouldNotBeNull().DiagnosticJson.ShouldNotContain("https://forgejo.example.test", Case.Sensitive);
    }

    [Fact]
    public async Task BoundWorkspaceScopeReachesForgejoTargetResolutionAndActualReadinessRequestAsync()
    {
        var binding = Binding() with
        {
            NamingPolicy = new("target-policy", new Dictionary<string, string> { ["external_repository_ref"] = "opaque-repository" }),
        };
        var target = new ProviderRepositoryResolvedTarget("owner", "repository", ProviderRepositoryVisibility.Private,
            "main", "main", false, true, false, null, false);
        var targetResolver = RecordingProviderRepositoryTargetResolver.Success(target);
        var api = RecordingForgejoApiClient.Success(supportsWorkspacePreparation: true);
        ForgejoProvider provider = new(RecordingForgejoCredentialResolver.Success("fixture-token"), new RecordingForgejoApiClientFactory(api),
            targetResolver, new UnconfiguredProviderOperationSourceResolver(), timeProvider: new ForgejoFixedTimeProvider(Now));
        var service = Service(new RecordingProviderReadinessBindingReader(binding), new RecordingProviderReadinessEvidenceStore(),
            new RecordingProviderCapabilityResolver(provider), new InMemoryProviderCapabilityEvidenceStore());
        var result = await service.ValidateAsync(Request() with
        {
            RequestedCapability = ProviderReadinessRequestedCapability.WorkspacePreparation,
            BoundFolderId = "folder-a",
            BoundRepositoryBindingId = "repository-binding",
            BoundBranchRefPolicyRef = binding.BranchPolicy.PolicyRef,
            BoundExternalRepositoryRefFingerprint = FolderCommandValidator.ExternalRepositoryRefFingerprint("tenant-a", "binding-a", "opaque-repository"),
        }, TestContext.Current.CancellationToken);
        result.Status.ShouldBe("ready", result.ReasonCode);
        targetResolver.BindingCalls.ShouldBe(1);
        targetResolver.LastBindingRequest.ShouldNotBeNull().RepositoryBindingId.ShouldBe("repository-binding");
        targetResolver.LastBindingRequest.ExternalRepositoryRef.ShouldBe("opaque-repository");
        targetResolver.LastBindingRequest.AuthorizationEvidenceFingerprint.ShouldNotBeNullOrWhiteSpace();
        api.LastRequest.ShouldNotBeNull().BoundTarget.ShouldBeSameAs(target);
    }

    private static ProviderReadinessValidationService Service(
        IProviderReadinessBindingReader bindingReader,
        IProviderReadinessEvidenceStore readinessStore,
        IProviderCapabilityResolver resolver,
        IProviderCapabilityEvidenceStore capabilityEvidence)
        => new(
            new TenantAccessAuthorizer(
                TenantStore(),
                new FixedClock(Now),
                new TenantAccessOptions()),
            bindingReader,
            new ProviderCapabilityDiscoveryService(
                RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
                resolver,
                capabilityEvidence),
            readinessStore,
            new FixedClock(Now));

    private static ProviderReadinessValidationRequest Request()
        => new(
            AuthoritativeTenantId: "tenant-a",
            PrincipalId: "user-a",
            ProviderBindingRef: "binding-a",
            RequestedCapability: ProviderReadinessRequestedCapability.RepositoryCreation,
            CorrelationId: "corr-forgejo",
            ClaimTransformEvidence: EventStoreClaimTransformEvidence.Allowed(
                "tenant-a",
                "user-a",
                [ProviderReadinessValidationService.ReadActionToken]),
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static OrganizationProviderBinding Binding()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            ProviderKind: "forgejo",
            CredentialReferenceId: "credential-ref-a",
            NamingPolicy: new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            BranchPolicy: new OrganizationProviderBindingPolicy(
                "branch-policy-a",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authorized_base_url"] = "https://forgejo.example.test",
                    ["snapshot_version"] = "16.0.3",
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "readiness",
                }),
            CorrelationId: "binding-corr-a",
            TaskId: "binding-task-a",
            IdempotencyKey: "binding-idempotency-a",
            IdempotencyFingerprint: "binding-fingerprint-a",
            ConfiguredStatus: "configured",
            OccurredAt: Now);

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
            Watermark = 42,
            ProjectionWatermark = "tenant-a:42",
            LastEventTimestamp = Now,
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class RecordingProviderReadinessBindingReader(OrganizationProviderBinding binding) : IProviderReadinessBindingReader
    {
        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<OrganizationProviderBinding?>(binding);
        }
    }

    private sealed class RecordingProviderReadinessEvidenceStore : IProviderReadinessEvidenceStore
    {
        public ProviderReadinessEvidenceRecord? LastStored { get; private set; }

        public Task StoreAsync(ProviderReadinessEvidenceRecord evidence, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastStored = evidence;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingForgejoCredentialResolver : IForgejoCredentialResolver
    {
        public static RecordingForgejoCredentialResolver Success(string token)
            => new(ForgejoCredentialResolutionResult.Success(ForgejoCredentialLease.CreateForTesting(token)));

        private readonly ForgejoCredentialResolutionResult _result;

        private RecordingForgejoCredentialResolver(ForgejoCredentialResolutionResult result)
        {
            _result = result;
        }

        public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
            ForgejoCredentialResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class RecordingForgejoApiClientFactory(RecordingForgejoApiClient apiClient) : IForgejoApiClientFactory
    {
        public ValueTask<IForgejoApiClient> CreateAsync(
            ForgejoApiClientRequest request,
            ForgejoCredentialLease credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IForgejoApiClient>(apiClient);
        }
    }

    private sealed class RecordingForgejoApiClient : IForgejoApiClient
    {
        private readonly ForgejoReadinessResult _result;

        private RecordingForgejoApiClient(ForgejoReadinessResult result)
        {
            _result = result;
        }

        public ForgejoReadinessRequest? LastRequest { get; private set; }

        public static RecordingForgejoApiClient Success(bool supportsWorkspacePreparation = false)
            => new(ForgejoReadinessResult.Success(
                new ForgejoVersionEvidence(
                    "16.0.3",
                    "16.0.3",
                    "forgejo-rest-v1",
                    "supported",
                    "supported"),
                new ForgejoPermissionEvidence(
                    SupportsRepositoryCreation: true,
                    SupportsRepositoryBinding: true,
                    SupportsBranchRefInspection: true,
                    SupportsFileMutation: true,
                    SupportsCommit: true,
                    SupportsStatus: true,
                    SupportsMetadata: true,
                    SupportsPagination: true,
                    SupportsContentsApi: true,
                    RequiredScopePosture: "repository_contents_status_scope",
                    SupportsWorkspacePreparation: supportsWorkspacePreparation),
                new ForgejoRateLimitEvidence(
                    "bounded",
                    Retryable: true,
                    TimeSpan.FromSeconds(120),
                    "forgejo_headers_metadata_only")));

        public Task<ForgejoReadinessResult> GetReadinessAsync(
            ForgejoReadinessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(_result);
        }

        public Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
            ForgejoRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ForgejoRepositoryCreationResult.Success());
        }

        public Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(
            ForgejoRepositoryBindingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ForgejoRepositoryBindingResult.Success());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
