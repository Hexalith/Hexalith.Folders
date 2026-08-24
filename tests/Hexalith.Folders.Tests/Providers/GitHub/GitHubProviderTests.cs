using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class GitHubProviderTests
{
    [Fact]
    public async Task DiscoversGitHubCapabilityProfileThroughInternalApiSeam()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("ghp_123456789012345678901234567890123456");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        ProviderCapabilityProfile profile = result.Profile.ShouldNotBeNull();
        profile.ProviderFamily.ShouldBe("github");
        profile.ProviderKey.ShouldBe("github");
        profile.TargetEvidence.ApiSurfaceVersion.ShouldBe("github-rest-2022-11-28");
        profile.Evidence["profile_source"].ShouldBe("github_octokit_seam");
        profile.Evidence["github_api_version"].ShouldBe("2022-11-28");
        profile.Evidence["credential_mode"].ShouldBe("appinstallationreference");
        profile.Evidence["authorization_freshness"].ShouldBe("fresh");
        profile.Evidence["safe_target_fingerprint"].ShouldNotBeNullOrWhiteSpace();
        profile.KnownFailureMappings["timeout_mutation"].ShouldBe("unknown_provider_outcome");
        profile.Operations.Select(o => o.OperationId).ShouldContain(ProviderOperationCatalog.RepositoryCreation);
        profile.Operations.Select(o => o.OperationId).ShouldContain(ProviderOperationCatalog.BranchRefInspection);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.FileMutationSupport).Support.ShouldBe(ProviderOperationSupport.Partial);
        profile.RateLimit.RetryAfter.ShouldBe(TimeSpan.FromSeconds(90));

        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialReferenceId.ShouldBe("credential-ref-a");
        credentialResolver.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        apiClientFactory.Calls.ShouldBe(1);
        apiClient.ReadinessCalls.ShouldBe(1);

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("ghp_123456789012345678901234567890123456", Case.Sensitive);
        serialized.ShouldNotContain("unauthorized-owner", Case.Sensitive);
        serialized.ShouldNotContain("repository-secret", Case.Sensitive);
    }

    [Fact]
    public async Task CreatesGitHubRepositoryThroughInternalApiSeam()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("ghp_123456789012345678901234567890123456");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingProviderRepositoryTargetResolver targetResolver = RecordingProviderRepositoryTargetResolver.Success();
        GitHubProvider provider = new(credentialResolver, new RecordingGitHubApiClientFactory(apiClient), targetResolver);

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.RepositoryBindingId.ShouldBe("repository-binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialReferenceId.ShouldBe("credential-ref-a");
        credentialResolver.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        targetResolver.CreationCalls.ShouldBe(1);
        targetResolver.LastCreationRequest.ShouldNotBeNull().RepositoryProfileRef.ShouldBe("repository-profile-a");
        apiClient.RepositoryCreationCalls.ShouldBe(1);
        GitHubRepositoryCreationRequest sent = apiClient.LastRepositoryCreationRequest.ShouldNotBeNull();
        sent.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        sent.Target.Owner.ShouldBe("octokit-owner-sentinel");
        sent.Target.RepositoryName.ShouldBe("octokit-repository-sentinel");

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("ghp_123456789012345678901234567890123456", Case.Sensitive);
        serialized.ShouldNotContain("repository-secret", Case.Sensitive);
        serialized.ShouldNotContain("https://", Case.Sensitive);
    }

    [Fact]
    public async Task ValidatesGitHubExistingRepositoryBindingThroughInternalApiSeam()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("ghp_123456789012345678901234567890123456");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingProviderRepositoryTargetResolver targetResolver = RecordingProviderRepositoryTargetResolver.Success();
        GitHubProvider provider = new(credentialResolver, new RecordingGitHubApiClientFactory(apiClient), targetResolver);

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.RepositoryBindingId.ShouldBe("repository-binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialReferenceId.ShouldBe("credential-ref-a");
        credentialResolver.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        targetResolver.BindingCalls.ShouldBe(1);
        targetResolver.LastBindingRequest.ShouldNotBeNull().ExternalRepositoryRef.ShouldBe("external-repository-a");
        apiClient.RepositoryBindingCalls.ShouldBe(1);
        GitHubRepositoryBindingRequest sent = apiClient.LastRepositoryBindingRequest.ShouldNotBeNull();
        sent.Target.Owner.ShouldBe("octokit-owner-sentinel");
        sent.Target.RepositoryName.ShouldBe("octokit-repository-sentinel");
        sent.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("ghp_123456789012345678901234567890123456", Case.Sensitive);
        serialized.ShouldNotContain("repository-secret", Case.Sensitive);
        serialized.ShouldNotContain("https://", Case.Sensitive);
    }

    [Fact]
    public async Task MapsGitHubEquivalentExistingRepositoryCreationAsSuccess()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryCreationEquivalentExisting();
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentExisting.ShouldBeTrue();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        apiClient.RepositoryCreationCalls.ShouldBe(1);
    }

    [Fact]
    public async Task MapsGitHubEquivalentExistingRepositoryBindingAsSuccess()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryBindingEquivalentExisting();
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentExisting.ShouldBeTrue();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        apiClient.RepositoryBindingCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed")]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required")]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient")]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing")]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "github_repository_conflict")]
    [InlineData("DefaultBranchConflict", ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict")]
    [InlineData("MissingBranchOrRef", ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing")]
    [InlineData("UnsupportedRefOperation", ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported")]
    [InlineData("ContentsPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient")]
    [InlineData("AdministrationPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient")]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict")]
    [InlineData("PrimaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited")]
    [InlineData("SecondaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited")]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable")]
    [InlineData("CancellationBeforeDispatch", ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch")]
    [InlineData("TimeoutDuringObservation", ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable")]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown")]
    [InlineData("AmbiguousMutationResponse", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous")]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response")]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown")]
    public async Task MapsGitHubRepositoryCreationFailures(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        GitHubApiFailureCondition condition = Enum.Parse<GitHubApiFailureCondition>(conditionName);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryCreationFailure(condition);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed")]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required")]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient")]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing")]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "github_repository_conflict")]
    [InlineData("DefaultBranchConflict", ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict")]
    [InlineData("MissingBranchOrRef", ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing")]
    [InlineData("UnsupportedRefOperation", ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported")]
    [InlineData("ContentsPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient")]
    [InlineData("AdministrationPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient")]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict")]
    [InlineData("PrimaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited")]
    [InlineData("SecondaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited")]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable")]
    [InlineData("CancellationBeforeDispatch", ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch")]
    [InlineData("TimeoutDuringObservation", ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable")]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown")]
    [InlineData("AmbiguousMutationResponse", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous")]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response")]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown")]
    public async Task MapsGitHubRepositoryBindingFailures(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        GitHubApiFailureCondition condition = Enum.Parse<GitHubApiFailureCondition>(conditionName);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryBindingFailure(condition);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Fact]
    public async Task MapsGitHubRepositoryCreationExceptionToUnknownOutcomeWithoutLeakingDetails()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryCreationThrows(
            new TimeoutException("repository-secret-timeout"));
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("github_repository_creation_outcome_unknown");
        apiClient.RepositoryCreationCalls.ShouldBe(1);
        credentialResolver.CredentialIsDisposed.ShouldBeTrue();
        JsonSerializer.Serialize(result).ShouldNotContain("repository-secret-timeout", Case.Sensitive);
    }

    [Fact]
    public async Task MapsGitHubRepositoryBindingExceptionToUnknownOutcomeWithoutLeakingDetails()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.RepositoryBindingThrows(
            new TimeoutException("repository-secret-timeout"));
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("github_repository_binding_outcome_unknown");
        apiClient.RepositoryBindingCalls.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("repository-secret-timeout", Case.Sensitive);
    }

    [Fact]
    public async Task UnsupportedCredentialModesFailBeforeCredentialsOrOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(
                credentialModes:
                [
                    ProviderCredentialMode.AppInstallationReference,
                    ProviderCredentialMode.UserDelegatedReference,
                ]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("ambiguous_github_credential_mode");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task MissingCredentialModesFailBeforeCredentialsOrOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(credentialModes: []),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("missing_github_credential_mode");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnsupportedSingleCredentialModeFailsBeforeCredentialsOrOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(credentialModes: [ProviderCredentialMode.ServiceAccountReference]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("unsupported_github_credential_mode");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnsafeTargetLabelsFailBeforeCredentialsOrProviderObservation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);
        ProviderCapabilityDiscoveryRequest request = Request() with
        {
            TargetEvidence = ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["owner"] = "unauthorized-owner",
                    ["repository"] = "repository-secret",
                    ["branch"] = "branch-secret-prod",
                },
            },
        };

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("unsafe_github_target_metadata");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("unauthorized-owner", Case.Sensitive);
        serialized.ShouldNotContain("repository-secret", Case.Sensitive);
        serialized.ShouldNotContain("branch-secret-prod", Case.Sensitive);
    }

    [Fact]
    public async Task StaleTargetEvidenceFailsBeforeCredentialsOrProviderObservation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request() with
            {
                TargetEvidence = ProviderCapabilityTestData.TargetEvidence(isStale: true),
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("target_evidence_stale");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task PropagatesPinnedGitHubCompatibilityMetadataOnlyToInternalApiSeam()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("ghp_123456789012345678901234567890123456");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryRequest request = Request(
            credentialModes: [ProviderCredentialMode.UserDelegatedReference],
            correlationId: "correlation-github-compatibility");

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialMode.ShouldBe(ProviderCredentialMode.UserDelegatedReference);
        credentialResolver.LastRequest.ShouldNotBeNull().AuthorizationEvidenceFingerprint.ShouldBe("authz-snapshot-default");
        apiClientFactory.LastRequest.ShouldNotBeNull().ProductHeader.ShouldBe("Hexalith-Folders");
        apiClientFactory.LastRequest.ShouldNotBeNull().ApiVersion.ShouldBe("2022-11-28");
        apiClientFactory.LastRequest.ShouldNotBeNull().CredentialMode.ShouldBe(ProviderCredentialMode.UserDelegatedReference);
        apiClientFactory.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        apiClientFactory.LastRequest.ShouldNotBeNull().CorrelationId.ShouldBe("correlation-github-compatibility");
        apiClientFactory.CredentialWasAvailableAtCreation.ShouldBeTrue();

        GitHubReadinessRequest readinessRequest = apiClient.LastRequest.ShouldNotBeNull();
        readinessRequest.ManagedTenantId.ShouldBe("tenant-a");
        readinessRequest.OrganizationId.ShouldBe("organization-a");
        readinessRequest.ProviderBindingRef.ShouldBe("binding-a");
        readinessRequest.CredentialMode.ShouldBe(ProviderCredentialMode.UserDelegatedReference);
        readinessRequest.ApiVersion.ShouldBe("2022-11-28");
        readinessRequest.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        readinessRequest.CorrelationId.ShouldBe("correlation-github-compatibility");

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("ghp_123456789012345678901234567890123456", Case.Sensitive);
    }

    [Fact]
    public async Task CredentialResolutionFailuresShortCircuitBeforeOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Failure(
            ProviderFailureCategory.ProviderAuthenticationRequired,
            "github_credential_unavailable",
            TimeSpan.FromSeconds(30));
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderAuthenticationRequired);
        result.ReasonCode.ShouldBe("github_credential_unavailable");
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        credentialResolver.Calls.ShouldBe(1);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RepositoryCreationCredentialResolutionFailuresShortCircuitBeforeOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_credential_reference_missing");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("provider_credential_reference_missing");
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialReferenceId.ShouldBe("credential-ref-a");
        credentialResolver.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.RepositoryCreationCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RepositoryBindingCredentialResolutionFailuresShortCircuitBeforeOctokitClientCreation()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Failure(
            ProviderFailureCategory.ProviderPermissionInsufficient,
            "provider_credential_reference_denied");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success());

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderPermissionInsufficient);
        result.ReasonCode.ShouldBe("provider_credential_reference_denied");
        credentialResolver.Calls.ShouldBe(1);
        credentialResolver.LastRequest.ShouldNotBeNull().CredentialReferenceId.ShouldBe("credential-ref-a");
        credentialResolver.LastRequest.ShouldNotBeNull().ProviderBindingRef.ShouldBe("binding-a");
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.RepositoryBindingCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RepositoryTargetResolutionFailureShortCircuitsBeforeCredentialsAndClientCreation()
    {
        RecordingProviderRepositoryTargetResolver targetResolver = RecordingProviderRepositoryTargetResolver.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "github_repository_target_unavailable");
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory, targetResolver);

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("github_repository_target_unavailable");
        targetResolver.CreationCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.RepositoryCreationCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StaleCreationEvidenceFailsBeforeTargetResolutionOrCredentialAccess()
    {
        RecordingProviderRepositoryTargetResolver targetResolver = RecordingProviderRepositoryTargetResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClientFactory apiClientFactory = new(RecordingGitHubApiClient.Success());
        GitHubProvider provider = new(credentialResolver, apiClientFactory, targetResolver);

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest() with
            {
                AuthorizationEvidence = new ProviderAuthorizationEvidenceSnapshot(
                    "stale-authorization-evidence",
                    DateTimeOffset.Parse("2026-07-19T00:00:00+00:00"),
                    "stale"),
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("authorization_evidence_stale");
        targetResolver.CreationCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PermissionEvidenceMapsUnavailableGitHubCapabilitiesWithoutRawProviderDetails()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success(
            new GitHubPermissionEvidence(
                SupportsRepositoryCreation: false,
                SupportsRepositoryBinding: true,
                SupportsBranchRefInspection: false,
                SupportsFileMutation: false,
                SupportsCommit: false,
                SupportsStatus: false,
                SupportsMetadata: false));
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        ProviderCapabilityProfile profile = result.Profile.ShouldNotBeNull();
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.ProviderSupportEvidence).Support.ShouldBe(ProviderOperationSupport.Unavailable);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.RepositoryCreation).Support.ShouldBe(ProviderOperationSupport.Unavailable);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.BranchRefInspection).FailureCategory.ShouldBe(ProviderFailureCategory.ProviderPermissionInsufficient);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.FileMutationSupport).Support.ShouldBe(ProviderOperationSupport.Unavailable);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.CommitSupport).Support.ShouldBe(ProviderOperationSupport.Unavailable);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.StatusQuery).Support.ShouldBe(ProviderOperationSupport.Unavailable);

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("permissions", Case.Insensitive);
        serialized.ShouldNotContain("raw_payload", Case.Sensitive);
    }

    [Fact]
    public async Task SafeTargetFingerprintIsIsolatedByBindingAuthorizationCredentialModeAndOperationScope()
    {
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(RecordingGitHubApiClient.Success()));

        ProviderCapabilityDiscoveryRequest baseline = Request(targetEvidence: TargetEvidenceWithoutDeclaredFingerprint("readiness"));

        string baselineFingerprint = await DiscoverSafeTargetFingerprintAsync(provider, baseline);
        string bindingFingerprint = await DiscoverSafeTargetFingerprintAsync(provider, baseline with { ProviderBindingRef = "binding-b" });
        string authorizationFingerprint = await DiscoverSafeTargetFingerprintAsync(
            provider,
            baseline with
            {
                AuthorizationEvidence = new ProviderAuthorizationEvidenceSnapshot(
                    "authz-snapshot-b",
                    DateTimeOffset.Parse("2026-05-24T07:00:00+00:00"),
                    "fresh"),
            });
        string credentialModeFingerprint = await DiscoverSafeTargetFingerprintAsync(
            provider,
            baseline with { CredentialModeRequirements = [ProviderCredentialMode.UserDelegatedReference] });
        string operationScopeFingerprint = await DiscoverSafeTargetFingerprintAsync(
            provider,
            baseline with { TargetEvidence = TargetEvidenceWithoutDeclaredFingerprint("repository_creation") });

        bindingFingerprint.ShouldNotBe(baselineFingerprint);
        authorizationFingerprint.ShouldNotBe(baselineFingerprint);
        credentialModeFingerprint.ShouldNotBe(baselineFingerprint);
        operationScopeFingerprint.ShouldNotBe(baselineFingerprint);
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed", false)]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required", false)]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient", false)]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing", false)]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "github_repository_conflict", false)]
    [InlineData("DefaultBranchConflict", ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict", false)]
    [InlineData("MissingBranchOrRef", ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing", false)]
    [InlineData("UnsupportedRefOperation", ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported", false)]
    [InlineData("ContentsPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient", false)]
    [InlineData("AdministrationPermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient", false)]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict", false)]
    [InlineData("PrimaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited", true)]
    [InlineData("SecondaryRateLimit", ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited", true)]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable", true)]
    [InlineData("CancellationBeforeDispatch", ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch", true)]
    [InlineData("TimeoutDuringObservation", ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable", true)]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown", false)]
    [InlineData("AmbiguousMutationResponse", ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous", false)]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response", false)]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown", false)]
    public async Task MapsGitHubFailuresToCanonicalProviderResults(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason,
        bool expectedRetryable)
    {
        GitHubApiFailureCondition condition = Enum.Parse<GitHubApiFailureCondition>(conditionName);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Failure(condition);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token"),
            new RecordingGitHubApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.Retryable.ShouldBe(expectedRetryable);
        result.SafeRemediationCode.ShouldNotContain("unauthorized", Case.Sensitive);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Fact]
    public async Task StaleAuthorizationEvidenceFailsBeforeCredentialLookup()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(credentialResolver, new RecordingGitHubApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request() with
            {
                AuthorizationEvidence = new ProviderAuthorizationEvidenceSnapshot(
                    "stale-authz",
                    DateTimeOffset.Parse("2026-05-24T00:00:00+00:00"),
                    "stale"),
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("authorization_evidence_stale");
        credentialResolver.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ForbiddenTargetLabelsAreRejectedWithoutLeakingSentinels()
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(credentialResolver, apiClientFactory);

        string[] sentinels =
        [
            "owner-acme-secret",
            "repository-secret",
            "branch-secret-prod",
            "installation-id-998877",
            "https://user:ghp_secret@github.com/acme/repo.git",
            "person@example.com",
            "Display Name Secret",
            "raw-github-payload-blob",
        ];

        ProviderCapabilityDiscoveryRequest request = Request() with
        {
            TargetEvidence = ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["owner"] = sentinels[0],
                    ["repository"] = sentinels[1],
                    ["branch"] = sentinels[2],
                    ["installation_id"] = sentinels[3],
                    ["clone_url"] = sentinels[4],
                    ["email"] = sentinels[5],
                    ["display_name"] = sentinels[6],
                    ["raw_payload"] = sentinels[7],
                },
            },
        };

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("unsafe_github_target_metadata");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);

        string serialized = JsonSerializer.Serialize(result);
        foreach (string sentinel in sentinels)
        {
            serialized.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    [Theory]
    [InlineData("ghp_123456789012345678901234567890123456")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dQw4w9WgXcQ")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----MIIabc-----END RSA PRIVATE KEY-----")]
    public async Task SensitiveTargetValuesAreRejectedWithoutLeakingSentinels(string sentinel)
    {
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(credentialResolver, new RecordingGitHubApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryRequest request = Request() with
        {
            TargetEvidence = ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["operation_scope"] = sentinel,
                },
            },
        };

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("sensitive_provider_metadata_rejected");

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain(sentinel, Case.Sensitive);
    }

    [Fact]
    public void ResolvedTargetStringRepresentationShouldNotExposeRawProviderValues()
    {
        ProviderRepositoryResolvedTarget target = new(
            Owner: "owner-sentinel",
            RepositoryName: "repository-sentinel",
            Visibility: ProviderRepositoryVisibility.Private,
            DefaultBranch: "default-branch-sentinel",
            SelectedRef: "selected-ref-sentinel",
            RequireProtectedRef: true,
            RequireContentsPermission: true,
            RequireAdministrationPermission: true,
            ExpectedCanonicalRepositoryId: "canonical-sentinel",
            EquivalentExistingAuthorized: true);

        string rendered = target.ToString();

        rendered.ShouldBe(nameof(ProviderRepositoryResolvedTarget));
        rendered.ShouldNotContain("sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task StagesOrderedFileChangesOnlyAfterCurrentEvidenceAndPrivateResolution()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest();
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.SafeStagedChangeSetFingerprint.ShouldNotBeNullOrWhiteSpace();
        operationResolver.ChangeSetCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(1);
        apiClient.FileChangeSetCalls.ShouldBe(1);
        apiClient.LastFileChangeSetRequest.ShouldNotBeNull().Changes
            .Select(static change => change.OperationReference)
            .ShouldBe(["change-a", "change-b"]);
        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("provider-owner-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("provider-repository-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("src/a.txt", Case.Sensitive);
        serialized.ShouldNotContain("alpha", Case.Sensitive);
        serialized.ShouldNotContain("token-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task EquivalentFileChangeReplayReturnsPriorSafeOutcomeWithoutSourceOrProviderAccess()
    {
        const string priorOutcome = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ProviderFileChangeSetRequest request = ChangeSetRequest(ProviderIdempotencyDisposition.EquivalentReplay) with
        {
            IdempotencyAdmission = new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "intent-a",
                priorOutcome),
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentReplay.ShouldBeTrue();
        result.SafeStagedChangeSetFingerprint.ShouldBe(priorOutcome);
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentReplayRejectsUnsafePriorReconciliationEvidenceBeforeSourceAccess()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest(ProviderIdempotencyDisposition.EquivalentReplay) with
        {
            IdempotencyAdmission = new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "intent-a",
                new string('a', 64),
                "raw-reconciliation-sentinel"),
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("github_replay_evidence_malformed");
        JsonSerializer.Serialize(result).ShouldNotContain("raw-reconciliation-sentinel", Case.Sensitive);
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderIdempotencyDisposition.Conflict, "idempotency_conflict")]
    [InlineData(ProviderIdempotencyDisposition.Expired, "idempotency_key_expired")]
    public async Task RejectedFileChangeAdmissionNeverAccessesProtectedSourceOrProvider(
        ProviderIdempotencyDisposition disposition,
        string expectedReasonCode)
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest(disposition);
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe(expectedReasonCode);
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StaleMutationEvidenceFailsBeforeSourceCredentialOrProviderObservation()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest() with
        {
            OperationEvidence = ChangeSetRequest().OperationEvidence with { FreshnessClass = "stale" },
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ProtectedSourceFailureCannotLeakRawReasonEvidence()
    {
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.ChangeSetFailure(
            ProviderFailureCategory.ProviderUnavailable,
            "raw protected source sentinel");
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        result.ReasonCode.ShouldBe(ProviderFailureCategory.ProviderUnavailable.ToCategoryCode());
        JsonSerializer.Serialize(result).ShouldNotContain("raw protected source sentinel", Case.Sensitive);
        operationResolver.ChangeSetCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task WrongTenantMutationEvidenceFailsBeforeProtectedSourceAccess()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest();
        request = request with
        {
            OperationEvidence = request.OperationEvidence with { AuthorizedManagedTenantId = "tenant-b" },
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderPermissionInsufficient);
        result.ReasonCode.ShouldBe("provider_operation_scope_denied");
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AmbiguousFileMutationReturnsOpaqueReconciliationIdentityWithoutRetry()
    {
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.FileChangeSetFailure(
            GitHubApiFailureCondition.AmbiguousMutationResponse);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReconciliationReference.ShouldNotBeNullOrWhiteSpace();
        result.ReconciliationReference!.Length.ShouldBe(64);
        result.ReadOnlyReconciliationSupported.ShouldBeFalse();
        apiClient.FileChangeSetCalls.ShouldBe(1);
    }

    [Fact]
    public async Task StageCommitAndStatusEvidenceComposeAtTheProviderBoundary()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());

        ProviderFileChangeSetResult staged = await provider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);
        ProviderCommitRequest commitRequest = CommitRequest() with
        {
            SafeStagedChangeSetFingerprint = staged.SafeStagedChangeSetFingerprint!,
        };
        ProviderCommitResult committed = await provider.CommitAsync(
            commitRequest,
            TestContext.Current.CancellationToken);
        ProviderMutationStatusRequest statusRequest = StatusRequest() with
        {
            SafeExpectedCommitFingerprint = committed.SafeExpectedCommitFingerprint!,
        };
        ProviderMutationStatusResult status = await provider.GetMutationStatusAsync(
            statusRequest,
            TestContext.Current.CancellationToken);

        staged.IsSuccess.ShouldBeTrue(staged.ReasonCode);
        committed.IsSuccess.ShouldBeTrue(committed.ReasonCode);
        status.Disposition.ShouldBe(ProviderMutationStatusDisposition.Confirmed);
        apiClient.FileChangeSetCalls.ShouldBe(1);
        apiClient.CommitCalls.ShouldBe(1);
        apiClient.MutationStatusCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CrossScopeAndForeignTargetEvidenceAreRejectedWithoutProtectedAccess()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest();
        ProviderFileChangeSetRequest[] invalidRequests =
        [
            request with { TargetEvidence = OperationTargetEvidence("commit") },
            request with { TargetEvidence = OperationTargetEvidence("file_mutation") with { Product = "forgejo" } },
        ];
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        foreach (ProviderFileChangeSetRequest invalidRequest in invalidRequests)
        {
            ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
                invalidRequest,
                TestContext.Current.CancellationToken);
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            result.ReasonCode.ShouldBe("github_target_evidence_profile_invalid");
        }

        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrongOrganizationOrCredentialEvidenceIsRejectedWithoutProtectedAccess(bool wrongOrganization)
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest();
        request = request with
        {
            OperationEvidence = wrongOrganization
                ? request.OperationEvidence with { AuthorizedOrganizationId = "organization-b" }
                : request.OperationEvidence with { AuthorizedCredentialReferenceId = "credential-ref-b" },
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderPermissionInsufficient);
        result.ReasonCode.ShouldBe("provider_operation_scope_denied");
        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnsafeCorrelationAndNullCollectionsAreRejectedWithoutProtectedAccess()
    {
        ProviderFileChangeSetRequest request = ChangeSetRequest();
        ProviderFileChangeSetRequest[] invalidRequests =
        [
            request with { CorrelationId = "unsafe\ncorrelation" },
            request with { CredentialModeRequirements = null! },
            request with { TargetEvidence = request.TargetEvidence with { Metadata = null! } },
            request with { Changes = null! },
        ];
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory apiClientFactory = new(apiClient);
        GitHubProvider provider = new(
            credentialResolver,
            apiClientFactory,
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        foreach (ProviderFileChangeSetRequest invalidRequest in invalidRequests)
        {
            ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
                invalidRequest,
                TestContext.Current.CancellationToken);
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        }

        operationResolver.ChangeSetCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.FileChangeSetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ExplicitCommitReturnsOnlySafeCommitEvidence()
    {
        ProviderCommitRequest request = CommitRequest();
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderCommitResult result = await provider.CommitAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.SafeCommitFingerprint.ShouldNotBeNullOrWhiteSpace();
        operationResolver.CommitCalls.ShouldBe(1);
        apiClient.CommitCalls.ShouldBe(1);
        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("provider commit message sentinel", Case.Sensitive);
        serialized.ShouldNotContain("3333333333333333333333333333333333333333", Case.Sensitive);
    }

    [Fact]
    public async Task AmbiguousCommitPreservesOnlySafeExpectedCommitEvidence()
    {
        const string rawCommitSha = "3333333333333333333333333333333333333333";
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.CommitFailure(
            GitHubApiFailureCondition.AmbiguousMutationResponse,
            rawCommitSha);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReconciliationReference.ShouldNotBeNullOrWhiteSpace();
        result.SafeExpectedCommitFingerprint.ShouldNotBeNullOrWhiteSpace();
        JsonSerializer.Serialize(result).ShouldNotContain(rawCommitSha, Case.Sensitive);
        apiClient.CommitCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IndependentlyMismatchedCommitResolutionEvidenceFailsBeforeCredentialAccess(bool mismatchHead)
    {
        RecordingProviderGitOperationResolver operationResolver = mismatchHead
            ? RecordingProviderGitOperationResolver.Success(expectedHeadSha: new string('9', 40))
            : RecordingProviderGitOperationResolver.Success(stagedTreeSha: new string('8', 40));
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderCommitResult result = await provider.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("resolved_provider_commit_malformed");
        operationResolver.CommitCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CredentialFactoryAndDisposalFailuresAreSanitizedAndNeverDuplicateProviderAccess()
    {
        const string credentialSentinel = "credential resolver sentinel";
        RecordingGitHubApiClient credentialApi = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory credentialFactory = new(credentialApi);
        GitHubProvider credentialProvider = new(
            RecordingGitHubCredentialResolver.Throws(new InvalidOperationException(credentialSentinel)),
            credentialFactory,
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());
        ProviderFileChangeSetResult credentialResult = await credentialProvider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        RecordingGitHubCredentialResolver factoryCredential = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient factoryApi = RecordingGitHubApiClient.Success();
        RecordingGitHubApiClientFactory throwingFactory = new(
            factoryApi,
            new InvalidOperationException("factory sentinel"));
        GitHubProvider factoryProvider = new(
            factoryCredential,
            throwingFactory,
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());
        ProviderFileChangeSetResult factoryResult = await factoryProvider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        RecordingGitHubCredentialResolver disposalCredential = RecordingGitHubCredentialResolver.Success(
            "token-sentinel",
            () => ValueTask.FromException(new InvalidOperationException("dispose sentinel")));
        RecordingGitHubApiClient disposalApi = RecordingGitHubApiClient.Success();
        GitHubProvider disposalProvider = new(
            disposalCredential,
            new RecordingGitHubApiClientFactory(disposalApi),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());
        ProviderFileChangeSetResult disposalResult = await disposalProvider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        credentialResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        JsonSerializer.Serialize(credentialResult).ShouldNotContain(credentialSentinel, Case.Sensitive);
        credentialFactory.Calls.ShouldBe(0);
        credentialApi.FileChangeSetCalls.ShouldBe(0);
        factoryResult.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderUnavailable);
        throwingFactory.Calls.ShouldBe(1);
        factoryApi.FileChangeSetCalls.ShouldBe(0);
        factoryCredential.CredentialIsDisposed.ShouldBeTrue();
        disposalResult.IsSuccess.ShouldBeTrue(disposalResult.ReasonCode);
        disposalApi.FileChangeSetCalls.ShouldBe(1);
        disposalCredential.CredentialIsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task PostDispatchClientFailureIsAmbiguousWithoutDuplicateMutation()
    {
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.FileChangeSetThrows(
            new InvalidOperationException("provider sentinel"));
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());

        ProviderFileChangeSetResult result = await provider.StageFileChangesAsync(
            ChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        apiClient.FileChangeSetCalls.ShouldBe(1);
    }

    [Fact]
    public async Task EquivalentCommitReplayReturnsPriorSafeOutcomeWithoutSourceOrProviderAccess()
    {
        const string priorOutcome = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ProviderCommitRequest request = CommitRequest() with
        {
            IdempotencyAdmission = new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.EquivalentReplay,
                "intent-commit-a",
                priorOutcome),
        };
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderCommitResult result = await provider.CommitAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentReplay.ShouldBeTrue();
        result.SafeCommitFingerprint.ShouldBe(priorOutcome);
        operationResolver.CommitCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StatusRejectsIdempotencyKeyAndExhaustedChecksBeforeProtectedSourceAccess()
    {
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderMutationStatusResult idempotent = await provider.GetMutationStatusAsync(
            StatusRequest() with { IdempotencyKey = "forbidden-key" },
            TestContext.Current.CancellationToken);
        ProviderMutationStatusResult exhausted = await provider.GetMutationStatusAsync(
            StatusRequest() with { CheckNumber = 6 },
            TestContext.Current.CancellationToken);
        ProviderMutationStatusResult expiredWindow = await provider.GetMutationStatusAsync(
            StatusRequest() with { RequestedAt = DateTimeOffset.Parse("2026-08-24T10:16:00+00:00") },
            TestContext.Current.CancellationToken);

        idempotent.ReasonCode.ShouldBe("status_idempotency_key_forbidden");
        exhausted.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        expiredWindow.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        operationResolver.StatusCalls.ShouldBe(0);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.MutationStatusCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData("Confirmed", ProviderMutationStatusDisposition.Confirmed, ProviderFailureCategory.None)]
    [InlineData("NotApplied", ProviderMutationStatusDisposition.NotApplied, ProviderFailureCategory.None)]
    [InlineData("Conflicting", ProviderMutationStatusDisposition.Conflicting, ProviderFailureCategory.ReconciliationRequired)]
    public async Task StatusMapsReadOnlyEvidenceWithoutMutation(
        string githubDispositionName,
        ProviderMutationStatusDisposition expectedDisposition,
        ProviderFailureCategory expectedCategory)
    {
        RecordingProviderGitOperationResolver operationResolver = RecordingProviderGitOperationResolver.Success();
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.MutationStatus(
            Enum.Parse<GitHubMutationStatusDisposition>(githubDispositionName));
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderMutationStatusResult result = await provider.GetMutationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(expectedDisposition);
        result.FailureCategory.ShouldBe(expectedCategory);
        apiClient.MutationStatusCalls.ShouldBe(1);
        apiClient.FileChangeSetCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData("TimeoutDuringObservation")]
    [InlineData("ServerUnavailable")]
    [InlineData("MalformedResponse")]
    [InlineData("PrimaryRateLimit")]
    [InlineData("NotFoundOrHidden")]
    public async Task StatusFailuresStayReadOnlyUnavailableAndNeverBecomeUnknownOutcome(
        string conditionName)
    {
        GitHubApiFailureCondition condition = Enum.Parse<GitHubApiFailureCondition>(conditionName);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.MutationStatusFailure(
            condition,
            condition == GitHubApiFailureCondition.PrimaryRateLimit ? TimeSpan.FromSeconds(90) : null);
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success());

        ProviderMutationStatusResult result = await provider.GetMutationStatusAsync(
            StatusRequest(),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ProviderMutationStatusDisposition.Unavailable);
        result.FailureCategory.ShouldNotBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReconciliationReference.ShouldBe(StatusRequest().ReconciliationReference);
        if (condition == GitHubApiFailureCondition.PrimaryRateLimit)
        {
            result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(90));
        }

        apiClient.MutationStatusCalls.ShouldBe(1);
        apiClient.FileChangeSetCalls.ShouldBe(0);
        apiClient.CommitCalls.ShouldBe(0);
    }

    [Fact]
    public async Task FifthStatusCheckAtExactlyFifteenMinutesIsPermitted()
    {
        DateTimeOffset observedAt = DateTimeOffset.Parse("2026-08-24T10:00:00+00:00");
        DateTimeOffset requestedAt = observedAt.AddMinutes(15);
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            RecordingGitHubCredentialResolver.Success("token-sentinel"),
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            RecordingProviderGitOperationResolver.Success(
                authoritativeUnknownOutcomeObservedAt: observedAt,
                authoritativeRequestedAt: requestedAt,
                authoritativeCheckNumber: 5));

        ProviderMutationStatusResult result = await provider.GetMutationStatusAsync(
            StatusRequest() with
            {
                UnknownOutcomeObservedAt = observedAt,
                RequestedAt = requestedAt,
                CheckNumber = 5,
            },
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ProviderMutationStatusDisposition.Confirmed);
        apiClient.MutationStatusCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCannotRefreshReconciliationBudgetEvidence(bool replayFirstCheck)
    {
        ProviderMutationStatusRequest request = StatusRequest();
        RecordingProviderGitOperationResolver operationResolver = replayFirstCheck
            ? RecordingProviderGitOperationResolver.Success(authoritativeCheckNumber: 2)
            : RecordingProviderGitOperationResolver.Success(
                authoritativeRequestedAt: request.RequestedAt.AddMinutes(1));
        RecordingGitHubCredentialResolver credentialResolver = RecordingGitHubCredentialResolver.Success("token-sentinel");
        RecordingGitHubApiClient apiClient = RecordingGitHubApiClient.Success();
        GitHubProvider provider = new(
            credentialResolver,
            new RecordingGitHubApiClientFactory(apiClient),
            RecordingProviderRepositoryTargetResolver.Success(),
            operationResolver);

        ProviderMutationStatusResult result = await provider.GetMutationStatusAsync(
            request,
            TestContext.Current.CancellationToken);

        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("resolved_provider_status_budget_mismatch");
        operationResolver.StatusCalls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.MutationStatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DefaultUnsupportedMutationMethodsHonorPreCancelledTokens()
    {
        IGitProvider provider = new DefaultMutationGitProvider();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.StageFileChangesAsync(ChangeSetRequest(), cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => provider.CommitAsync(CommitRequest(), cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetMutationStatusAsync(StatusRequest(), cancellation.Token));
    }

    [Theory]
    [InlineData(ProviderMutationStatusDisposition.Unavailable)]
    [InlineData((ProviderMutationStatusDisposition)999)]
    public void AvailableStatusRejectsUnavailableAndUndefinedDispositions(ProviderMutationStatusDisposition disposition)
    {
        ProviderMutationStatusResult result = ProviderMutationStatusResult.Available(StatusRequest(), disposition);

        result.Disposition.ShouldBe(ProviderMutationStatusDisposition.Unavailable);
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe("provider_status_disposition_invalid");
    }

    private static async Task<string> DiscoverSafeTargetFingerprintAsync(
        GitHubProvider provider,
        ProviderCapabilityDiscoveryRequest request)
    {
        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        return result.Profile.ShouldNotBeNull().TargetEvidence.Metadata["safe_target_fingerprint"];
    }

    private static ProviderTargetEvidence TargetEvidenceWithoutDeclaredFingerprint(string operationScope)
        => ProviderCapabilityTestData.TargetEvidence() with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operation_scope"] = operationScope,
            },
        };

    private static ProviderCapabilityDiscoveryRequest Request(
        IReadOnlyList<ProviderCredentialMode>? credentialModes = null,
        string correlationId = "correlation-a",
        ProviderTargetEvidence? targetEvidence = null)
        => ProviderCapabilityTestData.Request() with
        {
            CorrelationId = correlationId,
            TargetEvidence = targetEvidence ?? ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "readiness",
                },
            },
            CredentialModeRequirements = credentialModes ?? [ProviderCredentialMode.AppInstallationReference],
        };

    private static ProviderRepositoryCreationRequest CreationRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "repository_creation",
                },
            },
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authz-snapshot-default",
                DateTimeOffset.Parse("2026-05-24T07:00:00+00:00"),
                "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-binding-a",
            RepositoryProfileRef: "repository-profile-a");

    private static ProviderRepositoryBindingRequest BindingRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ExternalRepositoryRef: "external-repository-a",
            ExternalRepositoryRefFingerprint: "external-ref-fingerprint-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "existing_repository_binding",
                },
            },
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authz-snapshot-default",
                DateTimeOffset.Parse("2026-05-24T07:00:00+00:00"),
                "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-binding-a");

    private static ProviderFileChangeSetRequest ChangeSetRequest(
        ProviderIdempotencyDisposition disposition = ProviderIdempotencyDisposition.Fresh)
    {
        ProviderFileChangeSetRequest request = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence("file_mutation"),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: FreshAuthorizationEvidence(),
            OperationEvidence: OperationEvidence(new string('0', 64)),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-mutation-a",
            ChangeSetReference: "change-set-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(disposition, "intent-a"),
            Changes:
            [
                new ProviderFileChange("change-a", "path-a", ProviderFileChangeKind.Add, "content-a", 5, "text/plain"),
                new ProviderFileChange("change-b", "path-b", ProviderFileChangeKind.Remove, null, 0, null),
            ]);
        GitHubSafeTargetFingerprint.TryCreate(
            request,
            ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence safeTarget,
            out _).ShouldBeTrue();
        string expectedHeadFingerprint = GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
            safeTarget.Metadata["safe_target_fingerprint"],
            request.ChangeSetReference,
            "1111111111111111111111111111111111111111");
        return request with { OperationEvidence = request.OperationEvidence with { ExpectedHeadFingerprint = expectedHeadFingerprint } };
    }

    private static ProviderCommitRequest CommitRequest()
    {
        ProviderCommitRequest request = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence("commit"),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: FreshAuthorizationEvidence(),
            OperationEvidence: OperationEvidence(new string('0', 64)),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-commit-a",
            StagedChangeSetReference: "change-set-a",
            SafeStagedChangeSetFingerprint: new string('0', 64),
            CommitMessageReference: "commit-message-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(ProviderIdempotencyDisposition.Fresh, "intent-commit-a"));
        GitHubSafeTargetFingerprint.TryCreate(
            request,
            ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence safeTarget,
            out _).ShouldBeTrue();
        string fingerprint = safeTarget.Metadata["safe_target_fingerprint"];
        return request with
        {
            OperationEvidence = request.OperationEvidence with
            {
                ExpectedHeadFingerprint = GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
                    fingerprint,
                    request.StagedChangeSetReference,
                    "1111111111111111111111111111111111111111"),
            },
            SafeStagedChangeSetFingerprint = GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
                request.OperationEvidence.RepositoryBindingFingerprint,
                request.StagedChangeSetReference,
                "2222222222222222222222222222222222222222"),
        };
    }

    private static ProviderMutationStatusRequest StatusRequest()
    {
        ProviderMutationStatusRequest request = new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            TargetEvidence: OperationTargetEvidence("status"),
            CredentialModeRequirements: [ProviderCredentialMode.AppInstallationReference],
            AuthorizationEvidence: FreshAuthorizationEvidence(),
            OperationEvidence: OperationEvidence(new string('0', 64)),
            CorrelationId: "correlation-a",
            OperationReference: "change-set-a",
            ReconciliationReference: new string('a', 64),
            SafeExpectedCommitFingerprint: new string('0', 64),
            UnknownOutcomeObservedAt: DateTimeOffset.Parse("2026-08-24T10:00:00+00:00"),
            RequestedAt: DateTimeOffset.Parse("2026-08-24T10:05:00+00:00"),
            CheckNumber: 1);
        GitHubSafeTargetFingerprint.TryCreate(
            request,
            ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence safeTarget,
            out _).ShouldBeTrue();
        string fingerprint = safeTarget.Metadata["safe_target_fingerprint"];
        return request with
        {
            OperationEvidence = request.OperationEvidence with
            {
                ExpectedHeadFingerprint = GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
                    fingerprint,
                    request.OperationReference,
                    "1111111111111111111111111111111111111111"),
            },
            SafeExpectedCommitFingerprint = GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
                request.OperationEvidence.RepositoryBindingFingerprint,
                request.OperationReference,
                "3333333333333333333333333333333333333333"),
        };
    }

    private static ProviderAuthorizationEvidenceSnapshot FreshAuthorizationEvidence()
        => new(
            "authz-snapshot-default",
            DateTimeOffset.Parse("2026-08-24T09:55:00+00:00"),
            "fresh");

    private static ProviderTargetEvidence OperationTargetEvidence(string operationScope)
        => new(
            "github",
            "github-rest",
            "github-rest-2022-11-28",
            "github-target-evidence-v1",
            IsStale: false,
            DateTimeOffset.Parse("2026-08-24T09:55:00+00:00"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["api_version"] = "2022-11-28",
                ["operation_scope"] = operationScope,
            });

    private static ProviderOperationEvidenceSnapshot OperationEvidence(string expectedHeadFingerprint)
        => new(
            "tenant-a",
            "folder-a",
            "organization-a",
            "credential-ref-a",
            "repository-binding-a",
            "delegated-task-fingerprint-a",
            "repository-binding-fingerprint-a",
            "ref-policy-fingerprint-a",
            "canonical-lock-fingerprint-a",
            expectedHeadFingerprint,
            DateTimeOffset.Parse("2026-08-24T09:55:00+00:00"),
            "fresh");

    private sealed class DefaultMutationGitProvider : IGitProvider
    {
        private readonly IGitProvider _inner = FakeGitProvider.GitHubLike();

        public string ProviderFamily => _inner.ProviderFamily;

        public string ProviderKey => _inner.ProviderKey;

        public Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
            ProviderCapabilityDiscoveryRequest request,
            CancellationToken cancellationToken = default)
            => _inner.DiscoverCapabilitiesAsync(request, cancellationToken);

        public Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
            ProviderRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
            => _inner.CreateRepositoryAsync(request, cancellationToken);

        public Task<ProviderRepositoryBindingResult> ValidateRepositoryBindingAsync(
            ProviderRepositoryBindingRequest request,
            CancellationToken cancellationToken = default)
            => _inner.ValidateRepositoryBindingAsync(request, cancellationToken);

        public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
            ProviderCapabilityProfile current,
            ProviderCapabilityProfile candidate)
            => _inner.CompareCapabilityProfiles(current, candidate);
    }
}
