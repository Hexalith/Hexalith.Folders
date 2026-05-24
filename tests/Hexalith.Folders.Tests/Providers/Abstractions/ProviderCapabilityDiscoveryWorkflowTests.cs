using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Abstractions;

public sealed class ProviderCapabilityDiscoveryWorkflowTests
{
    [Fact]
    public async Task DiscoveryServiceShouldReturnRequiredMetadataOnlyCapabilityProfileShape()
    {
        RecordingProviderCapabilityAuthorizer authorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-snapshot-workflow");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore evidenceStore = new();
        ProviderCapabilityDiscoveryService service = new(authorizer, resolver, evidenceStore);
        ProviderCapabilityDiscoveryRequest request = ProviderCapabilityTestData.Request(
            providerFamily: " GitHub ",
            providerKey: "configured-user-facing-label",
            correlationId: "correlation-profile-shape",
            targetEvidence: ProviderCapabilityTestData.TargetEvidence(productVersion: " 3.13.0 "))
            with
            {
                CredentialModeRequirements =
                [
                    ProviderCredentialMode.UserDelegatedReference,
                    ProviderCredentialMode.AppInstallationReference,
                    ProviderCredentialMode.UserDelegatedReference,
                ],
            };

        ProviderCapabilityDiscoveryResult result = await service.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.CorrelationId.ShouldBe("correlation-profile-shape");

        ProviderCapabilityProfile profile = result.Profile.ShouldNotBeNull();
        profile.ManagedTenantId.ShouldBe("tenant-a");
        profile.OrganizationId.ShouldBe("organization-a");
        profile.ProviderBindingRef.ShouldBe("binding-a");
        profile.ProviderFamily.ShouldBe("github");
        profile.ProviderKey.ShouldBe("github");
        profile.Version.SchemaVersion.ShouldBe("v1");
        profile.Version.ProfileFingerprint.ShouldBe(profile.Fingerprint);
        profile.AuthorizationEvidenceFingerprint.ShouldBe("authz-snapshot-workflow");
        profile.TargetEvidence.ProductVersion.ShouldBe("3.13.0");
        profile.Evidence["profile_source"].ShouldBe("static_fake");
        profile.KnownFailureMappings["rate_limited"].ShouldBe("provider_rate_limited");
        profile.RateLimit.Classification.ShouldBe("bounded");
        profile.RateLimit.Retryable.ShouldBeTrue();
        profile.RateLimit.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
        profile.CredentialModeRequirements.ShouldBe(
        [
            ProviderCredentialMode.AppInstallationReference,
            ProviderCredentialMode.UserDelegatedReference,
        ]);

        ProviderOperationCapability branchInspection = profile.Operations.Single(
            x => x.OperationId == ProviderOperationCatalog.BranchRefInspection);
        branchInspection.Support.ShouldBe(ProviderOperationSupport.Supported);
        branchInspection.Limits["max_refs"].ShouldBe("1000");

        ProviderOperationCapability fileMutation = profile.Operations.Single(
            x => x.OperationId == ProviderOperationCatalog.FileMutationSupport);
        fileMutation.Support.ShouldBe(ProviderOperationSupport.Partial);
        fileMutation.Limits["max_file_bytes"].ShouldBe("1048576");

        ProviderOperationCapability cleanup = profile.Operations.Single(
            x => x.OperationId == ProviderOperationCatalog.CleanupExpiration);
        cleanup.Support.ShouldBe(ProviderOperationSupport.Emulated);

        authorizer.Calls.ShouldBe(1);
        evidenceStore.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        resolver.ProviderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DiscoveryServiceShouldReturnUnsupportedResultWhenAuthorizedProviderCannotBeResolved()
    {
        RecordingProviderCapabilityAuthorizer authorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-snapshot-unsupported");
        MissingProviderCapabilityResolver resolver = new();
        RecordingProviderCapabilityEvidenceStore evidenceStore = new();
        ProviderCapabilityDiscoveryService service = new(authorizer, resolver, evidenceStore);

        ProviderCapabilityDiscoveryResult result = await service.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(providerFamily: "unknown provider", providerKey: "unknown provider"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnsupportedProviderCapability);
        result.ReasonCode.ShouldBe("unsupported_provider_family");
        result.Profile.ShouldBeNull();
        authorizer.Calls.ShouldBe(1);
        evidenceStore.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task DiscoveryServiceShouldPropagateProviderRateLimitAsSafeRetryableFailure()
    {
        RecordingProviderCapabilityAuthorizer authorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-snapshot-rate-limit");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.Failing(ProviderFailureCategory.ProviderRateLimited));
        RecordingProviderCapabilityEvidenceStore evidenceStore = new();
        ProviderCapabilityDiscoveryService service = new(authorizer, resolver, evidenceStore);

        ProviderCapabilityDiscoveryResult result = await service.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(correlationId: "correlation-rate-limit"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderRateLimited);
        result.CategoryCode.ShouldBe("provider_rate_limited");
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
        result.CorrelationId.ShouldBe("correlation-rate-limit");
        result.Profile.ShouldBeNull();
        authorizer.Calls.ShouldBe(1);
        evidenceStore.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        resolver.ProviderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ProviderComparisonShouldExposeChangedFingerprintDimensionsForEquivalentAndChangedProfiles()
    {
        FakeGitProvider provider = FakeGitProvider.GitHubLike();
        ProviderCapabilityDiscoveryResult current = await provider.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(correlationId: "correlation-current"),
            TestContext.Current.CancellationToken);
        ProviderCapabilityDiscoveryResult candidate = await provider.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(
                correlationId: "correlation-candidate",
                targetEvidence: ProviderCapabilityTestData.TargetEvidence(productVersion: "3.14.0")),
            TestContext.Current.CancellationToken);

        ProviderCapabilityProfile currentProfile = current.Profile.ShouldNotBeNull();
        ProviderCapabilityComparisonResult same = provider.CompareCapabilityProfiles(currentProfile, currentProfile);
        ProviderCapabilityComparisonResult changed = provider.CompareCapabilityProfiles(
            currentProfile,
            candidate.Profile.ShouldNotBeNull());

        same.Equivalent.ShouldBeTrue();
        same.ChangedDimensions.ShouldBeEmpty();
        changed.Equivalent.ShouldBeFalse();
        changed.CurrentFingerprint.ShouldBe(currentProfile.Fingerprint);
        changed.CandidateFingerprint.ShouldBe(candidate.Profile.ShouldNotBeNull().Fingerprint);
        changed.ChangedDimensions.ShouldContain("fingerprint");
    }

    private sealed class MissingProviderCapabilityResolver : IProviderCapabilityResolver
    {
        public int Calls { get; private set; }

        public Task<IGitProvider?> ResolveAsync(
            string providerFamily,
            string providerKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult<IGitProvider?>(null);
        }
    }
}
