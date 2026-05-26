using System.Text.Json;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoProviderReadinessValidationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAsyncShouldBuildForgejoDiscoveryFromAuthorizedBindingMetadata()
    {
        InMemoryProviderCapabilityEvidenceStore capabilityEvidence = new();
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("forgejo-token-1234567890"),
            new RecordingForgejoApiClientFactory(apiClient));
        RecordingProviderCapabilityResolver resolver = new(provider);
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(Binding()),
            readinessStore,
            resolver,
            capabilityEvidence);

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("degraded");
        result.Evidence.ShouldNotBeNull().FileOperations.ShouldBe("temporarily_unavailable");
        result.CapabilityProfileRef.ShouldNotBeNullOrWhiteSpace();
        apiClient.LastRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("15.0.2");
        apiClient.LastRequest.ShouldNotBeNull().CredentialMode.ShouldBe(ProviderCredentialMode.UserDelegatedReference);
        ProviderCapabilityDiscoveryRequest storedAttempt = capabilityEvidence.Attempts.ShouldHaveSingleItem();
        storedAttempt.CredentialModeRequirements.ShouldBe([ProviderCredentialMode.UserDelegatedReference]);
        storedAttempt.TargetEvidence.Metadata["operation_scope"].ShouldBe(ProviderOperationCatalog.RepositoryCreation);
        string attemptJson = JsonSerializer.Serialize(capabilityEvidence.Attempts);
        attemptJson.ShouldNotContain("https://forgejo.example.test", Case.Sensitive);
        readinessStore.LastStored.ShouldNotBeNull().DiagnosticJson.ShouldNotContain("https://forgejo.example.test", Case.Sensitive);
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
                    ["snapshot_version"] = "15.0.2",
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = ProviderOperationCatalog.RepositoryCreation,
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

        public static RecordingForgejoApiClient Success()
            => new(ForgejoReadinessResult.Success(
                new ForgejoVersionEvidence(
                    "15.0.2",
                    "15.0.2",
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
                    RequiredScopePosture: "repository_contents_status_scope"),
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
    }

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
