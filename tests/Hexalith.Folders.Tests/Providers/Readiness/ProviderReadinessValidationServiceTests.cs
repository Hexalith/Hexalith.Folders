using System.Text.Json;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Readiness;

public sealed class ProviderReadinessValidationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAsync_ShouldReturnReadyOnlyWhenAllRequiredCreationCapabilitiesAreSupported()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityEvidenceStore capabilityEvidence = new();
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.WithOperationRows(
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence)));
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            capabilityEvidence);

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(requestedCapability: ProviderReadinessRequestedCapability.RepositoryCreation),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("ready");
        result.ReasonCode.ShouldBe("success");
        result.Retryable.ShouldBeFalse();
        result.RemediationCategory.ShouldBe("none");
        result.ProviderReference.ShouldBe("binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.CapabilityProfileRef.ShouldNotBeNullOrWhiteSpace();
        result.CorrelationId.ShouldBe("corr-a");
        result.Freshness.ReadConsistency.ShouldBe("snapshot_per_task");
        result.Evidence.ShouldNotBeNull().RepositoryCreation.ShouldBe("supported");
        result.Evidence.ExistingRepositoryBinding.ShouldBe("supported");
        bindingReader.Calls.ShouldBe(1);
        capabilityAuthorizer.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        capabilityEvidence.Calls.ShouldBe(1);
        readinessStore.Calls.ShouldBe(1);
        readinessStore.LastStored.ShouldNotBeNull().CapabilityProfileRef.ShouldBe(result.CapabilityProfileRef);
    }

    [Fact]
    public async Task ValidateAsync_ShouldNotRequireRepositoryCreationForBranchRefPolicy()
    {
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(Binding()),
            readinessStore,
            RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
            new RecordingProviderCapabilityResolver(FakeGitProvider.WithOperationRows(
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Unsupported(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence))),
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(requestedCapability: ProviderReadinessRequestedCapability.BranchRefPolicy),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("ready");
        result.Evidence.ShouldNotBeNull().RepositoryCreation.ShouldBe("unsupported");
        result.Evidence.BranchRefPolicy.ShouldBe("supported");
        readinessStore.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAsync_ShouldStoreTenantScopedMetadataOnlyEvidence()
    {
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(Binding()),
            readinessStore,
            RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
            new RecordingProviderCapabilityResolver(FakeGitProvider.WithOperationRows(
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence))),
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-scoped-evidence"),
            TestContext.Current.CancellationToken);

        ProviderReadinessEvidenceRecord stored = readinessStore.LastStored.ShouldNotBeNull();
        stored.ManagedTenantId.ShouldBe("tenant-a");
        stored.OrganizationId.ShouldBe("organization-a");
        stored.ProviderBindingRef.ShouldBe("binding-a");
        stored.ProviderFamily.ShouldBe("github");
        stored.ProviderKey.ShouldBe("github");
        stored.CapabilityProfileRef.ShouldBe(result.CapabilityProfileRef);
        stored.Status.ShouldBe("ready");
        stored.ReasonCode.ShouldBe("success");
        stored.Retryable.ShouldBeFalse();
        stored.RemediationCategory.ShouldBe("none");
        stored.FreshnessWatermark.ShouldBe("tenant-a:42");
        stored.CorrelationId.ShouldBe("corr-scoped-evidence");
        stored.DiagnosticJson.ShouldContain("\"providerBindingRef\":\"binding-a\"");
        stored.DiagnosticJson.ShouldNotContain("credential-ref-a", Case.Sensitive);
        stored.DiagnosticJson.ShouldNotContain("binding-idempotency-a", Case.Sensitive);
    }

    [Fact]
    public async Task ValidateAsync_ShouldDegradeWhenARequiredCapabilityIsPartialAndPreserveSafeMetadata()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(requestedCapability: ProviderReadinessRequestedCapability.RepositoryCreation),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("degraded");
        result.ReasonCode.ShouldBe("required_capability_degraded");
        result.Retryable.ShouldBeFalse();
        result.RemediationCategory.ShouldBe("fix_provider_configuration");
        result.Evidence.ShouldNotBeNull().FileOperations.ShouldBe("temporarily_unavailable");
        readinessStore.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveDiscoveryFailureDiagnosticsWithoutRetryingPermanentCategories()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(ProviderFailureCategory.UnsupportedProviderCapability));
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe("failed");
        result.ReasonCode.ShouldBe("unsupported_provider_capability");
        result.SafeRemediationCode.ShouldBe("unsupported_provider_capability_remediation");
        result.RemediationCategory.ShouldBe("contact_operator");
        result.Retryable.ShouldBeFalse();
        result.CorrelationId.ShouldBe("corr-a");
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnsupportedProviderCapability);
        readinessStore.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAsync_ShouldKeepProviderRateLimitRetryHintOnlyWhenDiscoveryMarksItSafe()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(ProviderFailureCategory.ProviderRateLimited));
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-rate"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe("degraded");
        result.ReasonCode.ShouldBe("provider_rate_limited");
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
        result.RemediationCategory.ShouldBe("retry_later");
        result.CorrelationId.ShouldBe("corr-rate");
        readinessStore.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(ProviderFailureCategory.ProviderConfigurationMissing, "failed", false, "fix_provider_configuration")]
    [InlineData(ProviderFailureCategory.ProviderAuthenticationRequired, "failed", false, "fix_credential_reference")]
    [InlineData(ProviderFailureCategory.ProviderPermissionInsufficient, "failed", false, "contact_operator")]
    [InlineData(ProviderFailureCategory.ProviderUnavailable, "degraded", true, "retry_later")]
    [InlineData(ProviderFailureCategory.ProviderReadinessFailed, "failed", false, "contact_operator")]
    [InlineData(ProviderFailureCategory.ProviderFailureKnown, "failed", false, "contact_operator")]
    [InlineData(ProviderFailureCategory.ProviderValidationFailed, "failed", false, "fix_provider_configuration")]
    [InlineData(ProviderFailureCategory.UnknownProviderOutcome, "failed", false, "contact_operator")]
    [InlineData(ProviderFailureCategory.ReconciliationRequired, "failed", false, "reconciliation_required")]
    public async Task ValidateAsync_ShouldMapDiscoveryFailureCategoriesToSafeReadinessDiagnostics(
        ProviderFailureCategory category,
        string expectedStatus,
        bool expectedRetryable,
        string expectedRemediation)
    {
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(Binding()),
            readinessStore,
            RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
            new RecordingProviderCapabilityResolver(FakeGitProvider.Failing(category)),
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-failure-map"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expectedStatus);
        result.FailureCategory.ShouldBe(category);
        result.ReasonCode.ShouldBe(category.ToCategoryCode());
        result.Retryable.ShouldBe(expectedRetryable);
        result.RemediationCategory.ShouldBe(expectedRemediation);
        result.CorrelationId.ShouldBe("corr-failure-map");
        readinessStore.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(TenantAccessOutcome.Denied)]
    [InlineData(TenantAccessOutcome.StaleProjection)]
    [InlineData(TenantAccessOutcome.UnavailableProjection)]
    [InlineData(TenantAccessOutcome.UnknownTenant)]
    [InlineData(TenantAccessOutcome.DisabledTenant)]
    [InlineData(TenantAccessOutcome.MalformedEvidence)]
    [InlineData(TenantAccessOutcome.ReplayConflict)]
    public async Task ValidateAsync_ShouldDenyBeforeBindingProviderOrEvidenceObservation(TenantAccessOutcome outcome)
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore capabilityEvidence = new();
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            capabilityEvidence,
            tenantStore: TenantStore(outcome));

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-denied"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldNotBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("failed");
        result.ReasonCode.ShouldNotBeNullOrWhiteSpace();
        result.ProviderBindingRef.ShouldBeNull();
        result.ProviderReference.ShouldBeNull();
        bindingReader.Calls.ShouldBe(0);
        readinessStore.Calls.ShouldBe(0);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
        capabilityEvidence.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldDenyMissingReadPermissionBeforeBindingObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_metadata"])),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.AuthorizationDenied);
        result.ReasonCode.ShouldBe("provider_readiness_read_denied");
        result.ProviderBindingRef.ShouldBeNull();
        bindingReader.Calls.ShouldBe(0);
        readinessStore.Calls.ShouldBe(0);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldDenyBoundedStaleTenantEvidenceBeforeBindingObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore capabilityEvidence = new();
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            capabilityEvidence,
            tenantStore: TenantStore(TenantAccessOutcome.Allowed, lastEventTimestamp: Now - TimeSpan.FromMinutes(10)));

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-bounded-stale"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.ProjectionStale);
        result.ReasonCode.ShouldBe("projection_stale");
        result.ProviderBindingRef.ShouldBeNull();
        bindingReader.Calls.ShouldBe(0);
        readinessStore.Calls.ShouldBe(0);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        capabilityEvidence.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldDenyReservedSystemTenantBeforeObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore capabilityEvidence = new();
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            capabilityEvidence);

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("system", "user-a", [ProviderReadinessValidationService.ReadActionToken]))
                with
                {
                    AuthoritativeTenantId = "system",
                },
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.AuthorizationDenied);
        result.ReasonCode.ShouldBe("provider_readiness_read_denied");
        result.ProviderBindingRef.ShouldBeNull();
        bindingReader.Calls.ShouldBe(0);
        readinessStore.Calls.ShouldBe(0);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
        capabilityEvidence.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectMalformedRequestBeforeAnyObservation()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding());
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-malformed") with { ProviderBindingRef = "bad.binding.ref" },
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.ValidationFailed);
        result.ReasonCode.ShouldBe("malformed_provider_readiness_request");
        bindingReader.Calls.ShouldBe(0);
        readinessStore.Calls.ShouldBe(0);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnReconciliationRequiredForMismatchedBindingScope()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(Binding(managedTenantId: "tenant-other"));
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: "corr-reconcile"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("failed");
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("reconciliation_required");
        result.Retryable.ShouldBeFalse();
        result.RemediationCategory.ShouldBe("reconciliation_required");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.Freshness.Stale.ShouldBeTrue();
        bindingReader.Calls.ShouldBe(1);
        readinessStore.Calls.ShouldBe(1);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnSafeMissingBindingFailureAfterAuthorization()
    {
        RecordingProviderReadinessBindingReader bindingReader = new(null);
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        RecordingProviderCapabilityAuthorizer capabilityAuthorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            capabilityAuthorizer,
            resolver,
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe("failed");
        result.ReasonCode.ShouldBe("provider_configuration_missing");
        result.ProviderReference.ShouldBe("binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.Retryable.ShouldBeFalse();
        result.RemediationCategory.ShouldBe("fix_provider_configuration");
        bindingReader.Calls.ShouldBe(1);
        readinessStore.Calls.ShouldBe(1);
        capabilityAuthorizer.Calls.ShouldBe(0);
        resolver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldSerializeMetadataOnlyWhenInputsContainSecretSentinels()
    {
        const string token = "ghp_abcdefghijklmnopqrstuvwxyz123456";
        const string jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abcde12345.fghij67890";
        const string credentialUrl = "https://user:pass@example.invalid/repo.git";
        OrganizationProviderBinding binding = Binding(
            namingPolicy: new OrganizationProviderBindingPolicy(
                "naming-policy-a",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_ref"] = "target-a",
                }),
            branchPolicy: new OrganizationProviderBindingPolicy(
                "branch-policy-a",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["secret_branch_hint"] = "feature/" + token,
                    ["raw_url"] = credentialUrl,
                    ["jwt_marker"] = jwt,
                }));
        RecordingProviderReadinessBindingReader bindingReader = new(binding);
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        ProviderReadinessValidationService service = Service(
            bindingReader,
            readinessStore,
            RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
            new RecordingProviderCapabilityResolver(FakeGitProvider.WithOperationRows(
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence))),
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(requestedCapability: ProviderReadinessRequestedCapability.ExistingRepositoryBinding),
            TestContext.Current.CancellationToken);

        string json = JsonSerializer.Serialize(result);
        json.ShouldNotContain(token, Case.Sensitive);
        json.ShouldNotContain(jwt, Case.Sensitive);
        json.ShouldNotContain(credentialUrl, Case.Sensitive);
        readinessStore.LastStored.ShouldNotBeNull().DiagnosticJson.ShouldNotContain(token, Case.Sensitive);
        readinessStore.LastStored.DiagnosticJson.ShouldNotContain(jwt, Case.Sensitive);
        readinessStore.LastStored.DiagnosticJson.ShouldNotContain(credentialUrl, Case.Sensitive);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReplaceSecretShapedCorrelationBeforeDiagnostics()
    {
        const string token = "ghp_abcdefghijklmnopqrstuvwxyz123456";
        RecordingProviderReadinessEvidenceStore readinessStore = new();
        ProviderReadinessValidationService service = Service(
            new RecordingProviderReadinessBindingReader(Binding()),
            readinessStore,
            RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"),
            new RecordingProviderCapabilityResolver(FakeGitProvider.WithOperationRows(
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence))),
            new RecordingProviderCapabilityEvidenceStore());

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            Request(correlationId: token),
            TestContext.Current.CancellationToken);

        result.CorrelationId.ShouldStartWith("correlation_");
        result.CorrelationId.ShouldNotBe(token);
        JsonSerializer.Serialize(result).ShouldNotContain(token, Case.Sensitive);
        readinessStore.LastStored.ShouldNotBeNull().CorrelationId.ShouldBe(result.CorrelationId);
        readinessStore.LastStored.DiagnosticJson.ShouldNotContain(token, Case.Sensitive);
    }

    private static ProviderReadinessValidationService Service(
        IProviderReadinessBindingReader bindingReader,
        IProviderReadinessEvidenceStore readinessStore,
        IProviderCapabilityAuthorizer capabilityAuthorizer,
        IProviderCapabilityResolver resolver,
        IProviderCapabilityEvidenceStore capabilityEvidence,
        IFolderTenantAccessProjectionStore? tenantStore = null)
        => new(
            new TenantAccessAuthorizer(
                tenantStore ?? TenantStore(TenantAccessOutcome.Allowed),
                new FixedClock(Now),
                new TenantAccessOptions()),
            bindingReader,
            new ProviderCapabilityDiscoveryService(capabilityAuthorizer, resolver, capabilityEvidence),
            readinessStore,
            new FixedClock(Now));

    private static ProviderReadinessValidationRequest Request(
        ProviderReadinessRequestedCapability requestedCapability = ProviderReadinessRequestedCapability.RepositoryCreation,
        string correlationId = "corr-a",
        EventStoreClaimTransformEvidence? claimTransformEvidence = null)
        => new(
            AuthoritativeTenantId: "tenant-a",
            PrincipalId: "user-a",
            ProviderBindingRef: "binding-a",
            RequestedCapability: requestedCapability,
            CorrelationId: correlationId,
            ClaimTransformEvidence: claimTransformEvidence
                ?? EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [ProviderReadinessValidationService.ReadActionToken]),
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static OrganizationProviderBinding Binding(
        string managedTenantId = "tenant-a",
        OrganizationProviderBindingPolicy? namingPolicy = null,
        OrganizationProviderBindingPolicy? branchPolicy = null)
        => new(
            ManagedTenantId: managedTenantId,
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            ProviderKind: "github",
            CredentialReferenceId: "credential-ref-a",
            NamingPolicy: namingPolicy ?? new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            BranchPolicy: branchPolicy ?? new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
            CorrelationId: "binding-corr-a",
            TaskId: "binding-task-a",
            IdempotencyKey: "binding-idempotency-a",
            IdempotencyFingerprint: "binding-fingerprint-a",
            ConfiguredStatus: "configured",
            OccurredAt: Now);

    private static IFolderTenantAccessProjectionStore TenantStore(
        TenantAccessOutcome outcome,
        DateTimeOffset? lastEventTimestamp = null)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        if (outcome == TenantAccessOutcome.UnknownTenant)
        {
            return store;
        }

        FolderTenantAccessProjection projection = new()
        {
            TenantId = "tenant-a",
            Enabled = outcome != TenantAccessOutcome.DisabledTenant,
            Principals = outcome == TenantAccessOutcome.Denied
                ? new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                : new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    ["user-a"] = new("user-a", "Member"),
                },
            Watermark = 42,
            ProjectionWatermark = "tenant-a:42",
            LastEventTimestamp = outcome is TenantAccessOutcome.MalformedEvidence
                ? null
                : lastEventTimestamp ?? Now,
            ReplayConflict = outcome == TenantAccessOutcome.ReplayConflict,
            MalformedEvidence = outcome == TenantAccessOutcome.MalformedEvidence,
        };

        if (outcome == TenantAccessOutcome.StaleProjection)
        {
            projection.LastEventTimestamp = Now - TimeSpan.FromDays(7);
        }

        store.SaveAsync(projection).GetAwaiter().GetResult();
        return outcome == TenantAccessOutcome.UnavailableProjection
            ? new ThrowingTenantAccessProjectionStore()
            : store;
    }

    private sealed class RecordingProviderReadinessBindingReader(OrganizationProviderBinding? binding) : IProviderReadinessBindingReader
    {
        public int Calls { get; private set; }

        public ProviderReadinessBindingReadRequest? LastRequest { get; private set; }

        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequest = request;
            return Task.FromResult(binding);
        }
    }

    private sealed class RecordingProviderReadinessEvidenceStore : IProviderReadinessEvidenceStore
    {
        public int Calls { get; private set; }

        public ProviderReadinessEvidenceRecord? LastStored { get; private set; }

        public Task StoreAsync(ProviderReadinessEvidenceRecord evidence, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastStored = evidence;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTenantAccessProjectionStore : IFolderTenantAccessProjectionStore
    {
        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("tenant projection unavailable raw diagnostic");

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock
    {
        public DateTimeOffset UtcNow => now;
    }

}
