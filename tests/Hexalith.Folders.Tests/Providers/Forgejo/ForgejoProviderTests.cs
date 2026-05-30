using System.Net;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoProviderTests
{
    [Fact]
    public async Task DiscoversForgejoCapabilityProfileThroughInternalHttpSeam()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("forgejo-token-1234567890");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        RecordingForgejoApiClientFactory apiClientFactory = new(apiClient);
        ForgejoProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        ProviderCapabilityProfile profile = result.Profile.ShouldNotBeNull();
        profile.ProviderFamily.ShouldBe("forgejo");
        profile.ProviderKey.ShouldBe("forgejo");
        profile.TargetEvidence.ApiSurfaceVersion.ShouldBe("forgejo-rest-v1");
        profile.TargetEvidence.Metadata["snapshot_version"].ShouldBe("15.0.2");
        profile.Evidence["profile_source"].ShouldBe("forgejo_http_seam");
        profile.Evidence["forgejo_product_version"].ShouldBe("15.0.2");
        profile.Evidence["forgejo_snapshot_version"].ShouldBe("15.0.2");
        profile.Evidence["forgejo_drift_classification"].ShouldBe("supported");
        profile.Evidence["credential_mode"].ShouldBe("userdelegatedreference");
        profile.KnownFailureMappings["timeout_mutation"].ShouldBe("unknown_provider_outcome");
        profile.KnownFailureMappings["schema_drift_breaking"].ShouldBe("reconciliation_required");
        profile.Operations.Select(o => o.OperationId).ShouldContain(ProviderOperationCatalog.RepositoryCreation);
        profile.Operations.Select(o => o.OperationId).ShouldContain(ProviderOperationCatalog.RepositoryBinding);
        profile.Operations.Single(o => o.OperationId == ProviderOperationCatalog.FileMutationSupport).Support.ShouldBe(ProviderOperationSupport.Partial);
        profile.RateLimit.Metadata["header_posture"].ShouldBe("forgejo_headers_metadata_only");

        credentialResolver.Calls.ShouldBe(1);
        apiClientFactory.Calls.ShouldBe(1);
        apiClient.ReadinessCalls.ShouldBe(1);
        apiClientFactory.LastRequest.ShouldNotBeNull().BaseUri.AbsoluteUri.ShouldBe("https://forgejo.example.test/");
        apiClient.LastRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("15.0.2");

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("forgejo-token-1234567890", Case.Sensitive);
        serialized.ShouldNotContain("owner-secret", Case.Sensitive);
        serialized.ShouldNotContain("repo-secret", Case.Sensitive);
    }

    [Fact]
    public async Task CreatesForgejoRepositoryThroughInternalApiSeam()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("forgejo-token-1234567890");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.RepositoryBindingId.ShouldBe("repository-binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        credentialResolver.Calls.ShouldBe(1);
        apiClient.RepositoryCreationCalls.ShouldBe(1);
        apiClient.LastRepositoryCreationRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("15.0.2");
        apiClient.LastRepositoryCreationRequest.ShouldNotBeNull().SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("forgejo-token-1234567890", Case.Sensitive);
        serialized.ShouldNotContain("repo-secret", Case.Sensitive);
        serialized.ShouldNotContain("https://", Case.Sensitive);
    }

    [Fact]
    public async Task ValidatesForgejoExistingRepositoryBindingThroughInternalApiSeam()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("forgejo-token-1234567890");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.RepositoryBindingId.ShouldBe("repository-binding-a");
        result.ProviderBindingRef.ShouldBe("binding-a");
        result.SafeTargetFingerprint.ShouldNotBeNullOrWhiteSpace();
        credentialResolver.Calls.ShouldBe(1);
        apiClient.RepositoryBindingCalls.ShouldBe(1);
        apiClient.LastRepositoryBindingRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("15.0.2");
        apiClient.LastRepositoryBindingRequest.ShouldNotBeNull().ExternalRepositoryRef.ShouldBe("external-repository-a");
        apiClient.LastRepositoryBindingRequest.ShouldNotBeNull().ExternalRepositoryRefFingerprint.ShouldBe("external-ref-fingerprint-a");

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("forgejo-token-1234567890", Case.Sensitive);
        serialized.ShouldNotContain("repo-secret", Case.Sensitive);
        serialized.ShouldNotContain("https://", Case.Sensitive);
    }

    [Fact]
    public async Task MapsForgejoEquivalentExistingRepositoryCreationAsSuccess()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryCreationEquivalentExisting();
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentExisting.ShouldBeTrue();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        apiClient.RepositoryCreationCalls.ShouldBe(1);
    }

    [Fact]
    public async Task MapsForgejoEquivalentExistingRepositoryBindingAsSuccess()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryBindingEquivalentExisting();
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.EquivalentExisting.ShouldBeTrue();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.None);
        apiClient.RepositoryBindingCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed")]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required")]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient")]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing")]
    [InlineData("MissingRepository", ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing")]
    [InlineData("MissingBranchOrPath", ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing")]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict")]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict")]
    [InlineData("RedirectCrossOrigin", ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected")]
    [InlineData("RateLimit", ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited")]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable")]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown")]
    [InlineData("CancellationDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown")]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response")]
    [InlineData("UnsupportedCapability", ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported")]
    [InlineData("VersionIncompatible", ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible")]
    [InlineData("SchemaDriftBreaking", ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking")]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown")]
    public async Task MapsForgejoRepositoryCreationFailures(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        ForgejoApiFailureCondition condition = Enum.Parse<ForgejoApiFailureCondition>(conditionName);
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryCreationFailure(condition);
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed")]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required")]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient")]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing")]
    [InlineData("MissingRepository", ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing")]
    [InlineData("MissingBranchOrPath", ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing")]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict")]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict")]
    [InlineData("RedirectCrossOrigin", ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected")]
    [InlineData("RateLimit", ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited")]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable")]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown")]
    [InlineData("CancellationDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown")]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response")]
    [InlineData("UnsupportedCapability", ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported")]
    [InlineData("VersionIncompatible", ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible")]
    [InlineData("SchemaDriftBreaking", ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking")]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown")]
    public async Task MapsForgejoRepositoryBindingFailures(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        ForgejoApiFailureCondition condition = Enum.Parse<ForgejoApiFailureCondition>(conditionName);
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryBindingFailure(condition);
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Fact]
    public async Task MapsForgejoRepositoryCreationExceptionToUnknownOutcomeWithoutLeakingDetails()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryCreationThrows(
            new TimeoutException("repo-secret-timeout"));
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("forgejo_repository_creation_outcome_unknown");
        apiClient.RepositoryCreationCalls.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("repo-secret-timeout", Case.Sensitive);
    }

    [Fact]
    public async Task MapsForgejoRepositoryBindingExceptionToUnknownOutcomeWithoutLeakingDetails()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.RepositoryBindingThrows(
            new TimeoutException("repo-secret-timeout"));
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderRepositoryBindingResult result = await provider.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
        result.ReasonCode.ShouldBe("forgejo_repository_binding_outcome_unknown");
        apiClient.RepositoryBindingCalls.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("repo-secret-timeout", Case.Sensitive);
    }

    [Fact]
    public async Task UnsupportedProviderIdentityFailsBeforeCredentialOrHttpConstruction()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request() with { ProviderFamily = "github", ProviderKey = "github" },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnsupportedProviderCapability);
        result.ReasonCode.ShouldBe("unsupported_provider_family");
        credentialResolver.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData("missing", null, "missing_forgejo_authorized_base_url")]
    [InlineData("userinfo", "https://user:password@forgejo.example.test", "forgejo_base_url_userinfo_rejected")]
    [InlineData("token-query", "https://forgejo.example.test?access_token=abc", "forgejo_base_url_token_query_rejected")]
    [InlineData("http", "http://forgejo.example.test", "forgejo_base_url_invalid")]
    public async Task InvalidAuthorizedBaseUrlsFailBeforeCredentialOrHttpConstruction(
        string _,
        string? authorizedBaseUrl,
        string expectedReason)
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryRequest request = Request(authorizedBaseUrl);
        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        result.ReasonCode.ShouldBe(expectedReason);
        credentialResolver.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task StaleAuthorizationEvidenceFailsBeforeAnyProviderObservation()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

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
    public async Task UnsafeTargetLabelsFailBeforeCredentialLookupAndDoNotLeakSentinels()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));
        string[] sentinels =
        [
            "owner-secret",
            "repo-secret",
            "branch-secret",
            "https://user:token@forgejo.example.test/acme/repo",
            "person@example.test",
            "raw-forgejo-payload",
        ];

        ProviderCapabilityDiscoveryRequest request = Request() with
        {
            TargetEvidence = ProviderCapabilityTestData.TargetEvidence() with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authorized_base_url"] = "https://forgejo.example.test",
                    ["owner"] = sentinels[0],
                    ["repository"] = sentinels[1],
                    ["branch"] = sentinels[2],
                    ["clone_url"] = sentinels[3],
                    ["email"] = sentinels[4],
                    ["raw_payload"] = sentinels[5],
                },
            },
        };

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("unsafe_forgejo_target_metadata");
        credentialResolver.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);

        string serialized = JsonSerializer.Serialize(result);
        foreach (string sentinel in sentinels)
        {
            serialized.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    [Fact]
    public async Task CredentialResolutionFailuresShortCircuitBeforeHttpClientCreation()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Failure(
            ProviderFailureCategory.ProviderAuthenticationRequired,
            "forgejo_credential_unavailable",
            TimeSpan.FromSeconds(30));
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        RecordingForgejoApiClientFactory apiClientFactory = new(apiClient);
        ForgejoProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderAuthenticationRequired);
        result.ReasonCode.ShouldBe("forgejo_credential_unavailable");
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        credentialResolver.Calls.ShouldBe(1);
        apiClientFactory.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizationHeaderIsBearerOnlyAndTokenQueryParametersAreNeverProduced()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        RecordingForgejoApiClientFactory apiClientFactory = new(apiClient);
        ForgejoProvider provider = new(credentialResolver, apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request("https://forgejo.example.test/root/path?safe=value"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        apiClientFactory.LastRequest.ShouldNotBeNull().BaseUri.Query.ShouldBeEmpty();
        apiClientFactory.LastRequest.ShouldNotBeNull().BaseUri.AbsoluteUri.ShouldNotContain("token=", Case.Insensitive);
        apiClientFactory.LastRequest.ShouldNotBeNull().BaseUri.AbsoluteUri.ShouldNotContain("access_token=", Case.Insensitive);
        apiClientFactory.CredentialWasAvailableAtCreation.ShouldBeTrue();
    }

    [Fact]
    public async Task UnsupportedSnapshotEvidenceFailsClosed()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success(
            version: new ForgejoVersionEvidence(
                "16.0.0",
                "16.0.0",
                "forgejo-rest-v1",
                "unknown-unclassified",
                "unknown-unclassified"));
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_snapshot_version_unsupported");
    }

    [Fact]
    public async Task UnsupportedTargetVersionFailsBeforeCredentialOrHttpConstruction()
    {
        RecordingForgejoCredentialResolver credentialResolver = RecordingForgejoCredentialResolver.Success("token");
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success();
        ForgejoProvider provider = new(credentialResolver, new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(targetEvidence: TargetEvidence("readiness", "15.0.3")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_target_version_unsupported");
        credentialResolver.Calls.ShouldBe(0);
        apiClient.ReadinessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnsupportedSameFamilyLiveVersionCannotDowngradeToPinnedSnapshot()
    {
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"version":"15.0.3"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://forgejo.example.test/"),
        };
        ForgejoHttpApiClient client = new(httpClient, httpClient.BaseAddress);

        ForgejoReadinessResult result = await client.GetReadinessAsync(
            new ForgejoReadinessRequest(
                "tenant-a",
                "organization-a",
                "binding-a",
                ProviderCredentialMode.UserDelegatedReference,
                "forgejo-rest-v1",
                "15.0.2",
                "safe-target-a",
                "correlation-a"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.VersionIncompatible);
    }

    [Fact]
    public async Task TargetVersionSelectsTheMatchingPinnedSnapshot()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success(
            version: new ForgejoVersionEvidence(
                "11.0.14",
                "11.0.14",
                "forgejo-rest-v1",
                "supported",
                "supported"));
        RecordingForgejoApiClientFactory apiClientFactory = new(apiClient);
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            apiClientFactory);

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(targetEvidence: TargetEvidence("readiness", "11.0.14")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        apiClient.LastRequest.ShouldNotBeNull().SupportedSnapshotVersion.ShouldBe("11.0.14");
        result.Profile.ShouldNotBeNull().TargetEvidence.Metadata["snapshot_version"].ShouldBe("11.0.14");
        result.Profile.ShouldNotBeNull().Evidence["forgejo_snapshot_version"].ShouldBe("11.0.14");
        apiClientFactory.LastRequest.ShouldNotBeNull().BaseUri.AbsoluteUri.ShouldBe("https://forgejo.example.test/");
    }

    [Fact]
    public async Task ObservedSnapshotMustMatchAuthorizedTargetSnapshot()
    {
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Success(
            version: new ForgejoVersionEvidence(
                "11.0.14",
                "11.0.14",
                "forgejo-rest-v1",
                "supported",
                "supported"));
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(targetEvidence: TargetEvidence("readiness", "15.0.2")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("forgejo_snapshot_version_mismatch");
    }

    [Theory]
    [InlineData("ValidationFailure", ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed", false)]
    [InlineData("AuthenticationRequired", ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required", false)]
    [InlineData("PermissionInsufficient", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient", false)]
    [InlineData("NotFoundOrHidden", ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing", false)]
    [InlineData("MissingRepository", ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing", false)]
    [InlineData("MissingBranchOrPath", ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing", false)]
    [InlineData("RepositoryConflict", ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict", false)]
    [InlineData("BranchProtectionConflict", ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict", false)]
    [InlineData("RedirectCrossOrigin", ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected", false)]
    [InlineData("RateLimit", ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited", true)]
    [InlineData("ServerUnavailable", ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable", true)]
    [InlineData("TimeoutDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown", false)]
    [InlineData("CancellationDuringMutation", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown", false)]
    [InlineData("MalformedResponse", ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response", false)]
    [InlineData("UnsupportedCapability", ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported", false)]
    [InlineData("VersionIncompatible", ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible", false)]
    [InlineData("SchemaDriftBreaking", ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking", false)]
    [InlineData("UnexpectedTransportFailure", ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown", false)]
    public async Task MapsForgejoFailuresToCanonicalProviderResults(
        string conditionName,
        ProviderFailureCategory expectedCategory,
        string expectedReason,
        bool expectedRetryable)
    {
        ForgejoApiFailureCondition condition = Enum.Parse<ForgejoApiFailureCondition>(conditionName);
        RecordingForgejoApiClient apiClient = RecordingForgejoApiClient.Failure(condition);
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(apiClient));

        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.Retryable.ShouldBe(expectedRetryable);
        result.SafeRemediationCode.ShouldNotContain("owner", Case.Sensitive);
        result.SafeRemediationCode.ShouldNotContain("repository", Case.Sensitive);
    }

    [Fact]
    public async Task SafeTargetFingerprintIncludesBindingAuthorizationCredentialModeBaseUrlAndSnapshot()
    {
        ForgejoProvider provider = new(
            RecordingForgejoCredentialResolver.Success("token"),
            new RecordingForgejoApiClientFactory(RecordingForgejoApiClient.Success()));
        ProviderCapabilityDiscoveryRequest baseline = Request(targetEvidence: TargetEvidence("readiness"));

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
            baseline with { CredentialModeRequirements = [ProviderCredentialMode.ServiceAccountReference] });
        string baseUrlFingerprint = await DiscoverSafeTargetFingerprintAsync(
            provider,
            Request("https://forgejo-b.example.test"));

        bindingFingerprint.ShouldNotBe(baselineFingerprint);
        authorizationFingerprint.ShouldNotBe(baselineFingerprint);
        credentialModeFingerprint.ShouldNotBe(baselineFingerprint);
        baseUrlFingerprint.ShouldNotBe(baselineFingerprint);
    }

    [Fact]
    public async Task HttpApiClientMapsVersionEndpointToMetadataOnlyReadiness()
    {
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"version":"15.0.2"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://forgejo.example.test/"),
        };
        ForgejoHttpApiClient client = new(httpClient, httpClient.BaseAddress);

        ForgejoReadinessResult result = await client.GetReadinessAsync(
            new ForgejoReadinessRequest(
                "tenant-a",
                "organization-a",
                "binding-a",
                ProviderCredentialMode.UserDelegatedReference,
                "forgejo-rest-v1",
                "15.0.2",
                "safe-target-a",
                "correlation-a"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Version.ShouldNotBeNull().SnapshotVersion.ShouldBe("15.0.2");
        result.Permissions.ShouldNotBeNull().SupportsRepositoryCreation.ShouldBeTrue();
        handler.LastRequestUri.ShouldNotBeNull().AbsoluteUri.ShouldBe("https://forgejo.example.test/api/v1/version");
        handler.LastRequestUri.ShouldNotBeNull().Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task HttpApiClientRejectsCrossOriginRedirectWithoutFollowingCredentials()
    {
        StubHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = new Uri("https://other.example.test/api/v1/version"),
                },
            };
            return response;
        });
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://forgejo.example.test/"),
        };
        ForgejoHttpApiClient client = new(httpClient, httpClient.BaseAddress);

        ForgejoReadinessResult result = await client.GetReadinessAsync(
            new ForgejoReadinessRequest(
                "tenant-a",
                "organization-a",
                "binding-a",
                ProviderCredentialMode.UserDelegatedReference,
                "forgejo-rest-v1",
                "15.0.2",
                "safe-target-a",
                "correlation-a"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.RedirectCrossOrigin);
        handler.Calls.ShouldBe(1);
    }

    private static async Task<string> DiscoverSafeTargetFingerprintAsync(
        ForgejoProvider provider,
        ProviderCapabilityDiscoveryRequest request)
    {
        ProviderCapabilityDiscoveryResult result = await provider.DiscoverCapabilitiesAsync(
            request,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        return result.Profile.ShouldNotBeNull().TargetEvidence.Metadata["safe_target_fingerprint"];
    }

    private static ProviderTargetEvidence TargetEvidence(string operationScope, string productVersion = "15.0.2")
        => ProviderCapabilityTestData.TargetEvidence(productVersion) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authorized_base_url"] = "https://forgejo.example.test",
                ["safe_target_fingerprint"] = "safe-target-a",
                ["operation_scope"] = operationScope,
            },
        };

    private static ProviderCapabilityDiscoveryRequest Request(
        string? authorizedBaseUrl = "https://forgejo.example.test",
        ProviderTargetEvidence? targetEvidence = null)
    {
        ProviderTargetEvidence evidence = targetEvidence ?? ProviderCapabilityTestData.TargetEvidence("15.0.2") with
        {
            Metadata = authorizedBaseUrl is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "readiness",
                }
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authorized_base_url"] = authorizedBaseUrl,
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "readiness",
                },
        };

        return ProviderCapabilityTestData.Request(
            providerFamily: "forgejo",
            providerKey: "forgejo",
            targetEvidence: evidence) with
        {
            CredentialModeRequirements = [ProviderCredentialMode.UserDelegatedReference],
        };
    }

    private static ProviderRepositoryCreationRequest CreationRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "forgejo",
            ProviderKey: "forgejo",
            TargetEvidence: TargetEvidence("repository_creation"),
            CredentialModeRequirements: [ProviderCredentialMode.UserDelegatedReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authz-snapshot-default",
                DateTimeOffset.Parse("2026-05-24T07:00:00+00:00"),
                "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-binding-a");

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
            ProviderFamily: "forgejo",
            ProviderKey: "forgejo",
            TargetEvidence: TargetEvidence("existing_repository_binding"),
            CredentialModeRequirements: [ProviderCredentialMode.UserDelegatedReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authz-snapshot-default",
                DateTimeOffset.Parse("2026-05-24T07:00:00+00:00"),
                "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-binding-a");

    private sealed class RecordingForgejoCredentialResolver : IForgejoCredentialResolver
    {
        private readonly ForgejoCredentialResolutionResult _result;

        private RecordingForgejoCredentialResolver(ForgejoCredentialResolutionResult result)
        {
            _result = result;
        }

        public int Calls { get; private set; }

        public ForgejoCredentialResolutionRequest? LastRequest { get; private set; }

        public static RecordingForgejoCredentialResolver Success(string token)
            => new(ForgejoCredentialResolutionResult.Success(ForgejoCredentialLease.CreateForTesting(token)));

        public static RecordingForgejoCredentialResolver Failure(
            ProviderFailureCategory category,
            string reasonCode,
            TimeSpan? retryAfter = null)
            => new(ForgejoCredentialResolutionResult.Failure(category, reasonCode, retryAfter));

        public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
            ForgejoCredentialResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequest = request;
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class RecordingForgejoApiClientFactory : IForgejoApiClientFactory
    {
        private readonly RecordingForgejoApiClient _apiClient;

        public RecordingForgejoApiClientFactory(RecordingForgejoApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public int Calls { get; private set; }

        public ForgejoApiClientRequest? LastRequest { get; private set; }

        public bool CredentialWasAvailableAtCreation { get; private set; }

        public ValueTask<IForgejoApiClient> CreateAsync(
            ForgejoApiClientRequest request,
            ForgejoCredentialLease credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequest = request;
            CredentialWasAvailableAtCreation = !string.IsNullOrWhiteSpace(credential.AccessToken);
            return ValueTask.FromResult<IForgejoApiClient>(_apiClient);
        }
    }

    private sealed class RecordingForgejoApiClient : IForgejoApiClient
    {
        private readonly ForgejoReadinessResult _result;
        private readonly Exception? _repositoryCreationException;
        private readonly ForgejoRepositoryCreationResult? _repositoryCreationResult;
        private readonly Exception? _repositoryBindingException;
        private readonly ForgejoRepositoryBindingResult? _repositoryBindingResult;

        private RecordingForgejoApiClient(
            ForgejoReadinessResult result,
            ForgejoRepositoryCreationResult? repositoryCreationResult = null,
            Exception? repositoryCreationException = null,
            ForgejoRepositoryBindingResult? repositoryBindingResult = null,
            Exception? repositoryBindingException = null)
        {
            _result = result;
            _repositoryCreationResult = repositoryCreationResult;
            _repositoryCreationException = repositoryCreationException;
            _repositoryBindingResult = repositoryBindingResult;
            _repositoryBindingException = repositoryBindingException;
        }

        public int ReadinessCalls { get; private set; }

        public int RepositoryCreationCalls { get; private set; }

        public int RepositoryBindingCalls { get; private set; }

        public ForgejoReadinessRequest? LastRequest { get; private set; }

        public ForgejoRepositoryCreationRequest? LastRepositoryCreationRequest { get; private set; }

        public ForgejoRepositoryBindingRequest? LastRepositoryBindingRequest { get; private set; }

        public static RecordingForgejoApiClient Success(ForgejoVersionEvidence? version = null)
            => new(ForgejoReadinessResult.Success(
                version ?? new ForgejoVersionEvidence(
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

        public static RecordingForgejoApiClient Failure(ForgejoApiFailureCondition condition)
            => new(ForgejoReadinessResult.Failure(
                condition,
                condition is ForgejoApiFailureCondition.RateLimit ? TimeSpan.FromSeconds(120) : null));

        public static RecordingForgejoApiClient RepositoryCreationFailure(ForgejoApiFailureCondition condition)
            => new(
                Success()._result,
                ForgejoRepositoryCreationResult.Failure(
                    condition,
                    condition is ForgejoApiFailureCondition.RateLimit ? TimeSpan.FromSeconds(120) : null));

        public static RecordingForgejoApiClient RepositoryCreationEquivalentExisting()
            => new(Success()._result, ForgejoRepositoryCreationResult.Success(equivalentExisting: true));

        public static RecordingForgejoApiClient RepositoryCreationThrows(Exception exception)
            => new(Success()._result, repositoryCreationException: exception);

        public static RecordingForgejoApiClient RepositoryBindingFailure(ForgejoApiFailureCondition condition)
            => new(
                Success()._result,
                repositoryBindingResult: ForgejoRepositoryBindingResult.Failure(
                    condition,
                    condition is ForgejoApiFailureCondition.RateLimit ? TimeSpan.FromSeconds(120) : null));

        public static RecordingForgejoApiClient RepositoryBindingEquivalentExisting()
            => new(Success()._result, repositoryBindingResult: ForgejoRepositoryBindingResult.Success(equivalentExisting: true));

        public static RecordingForgejoApiClient RepositoryBindingThrows(Exception exception)
            => new(Success()._result, repositoryBindingException: exception);

        public Task<ForgejoReadinessResult> GetReadinessAsync(
            ForgejoReadinessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadinessCalls++;
            LastRequest = request;
            return Task.FromResult(_result);
        }

        public Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
            ForgejoRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryCreationCalls++;
            LastRepositoryCreationRequest = request;
            if (_repositoryCreationException is not null)
            {
                throw _repositoryCreationException;
            }

            return Task.FromResult(_repositoryCreationResult ?? ForgejoRepositoryCreationResult.Success());
        }

        public Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(
            ForgejoRepositoryBindingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryBindingCalls++;
            LastRepositoryBindingRequest = request;
            if (_repositoryBindingException is not null)
            {
                throw _repositoryBindingException;
            }

            return Task.FromResult(_repositoryBindingResult ?? ForgejoRepositoryBindingResult.Success());
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int Calls { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
