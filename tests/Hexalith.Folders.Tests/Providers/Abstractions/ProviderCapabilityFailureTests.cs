using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Abstractions;

public sealed class ProviderCapabilityFailureTests
{
    [Theory]
    [InlineData(ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_capability", false)]
    [InlineData(ProviderFailureCategory.ProviderUnavailable, "provider_unavailable", true)]
    [InlineData(ProviderFailureCategory.ProviderAuthenticationRequired, "provider_authentication_required", false)]
    [InlineData(ProviderFailureCategory.ProviderConfigurationMissing, "provider_configuration_missing", false)]
    [InlineData(ProviderFailureCategory.ProviderPermissionInsufficient, "provider_permission_insufficient", false)]
    [InlineData(ProviderFailureCategory.ProviderRateLimited, "provider_rate_limited", true)]
    [InlineData(ProviderFailureCategory.ProviderValidationFailed, "provider_validation_failed", false)]
    [InlineData(ProviderFailureCategory.ProviderConflict, "provider_conflict", false)]
    [InlineData(ProviderFailureCategory.ProviderReadinessFailed, "provider_readiness_failed", false)]
    [InlineData(ProviderFailureCategory.ProviderFailureKnown, "provider_failure_known", false)]
    [InlineData(ProviderFailureCategory.ProviderTransientFailure, "provider_transient_failure", true)]
    [InlineData(ProviderFailureCategory.UnknownProviderOutcome, "unknown_provider_outcome", false)]
    [InlineData(ProviderFailureCategory.ReconciliationRequired, "reconciliation_required", false)]
    public async Task FailureResultsShouldUseCanonicalCategoriesAndRetryHints(ProviderFailureCategory category, string expectedCode, bool retryable)
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.Failing(category).DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(category);
        result.CategoryCode.ShouldBe(expectedCode);
        result.Retryable.ShouldBe(retryable);
        result.Profile.ShouldBeNull();
        result.SafeRemediationCode.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DuplicateCanonicalOperationRowsShouldReturnConflictInsteadOfLastWriterWins()
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.WithOperationRows(
            ProviderCapabilityOperationRow.Supported("readiness validation"),
            ProviderCapabilityOperationRow.Supported("READINESS_VALIDATION"))
            .DiscoverCapabilitiesAsync(ProviderCapabilityTestData.Request(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe("duplicate_operation_capability");
    }

    [Fact]
    public async Task ConflictingOperationRowsShouldReturnConflictInsteadOfChoosingByOrder()
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.WithOperationRows(
            ProviderCapabilityOperationRow.Supported("commit support"),
            ProviderCapabilityOperationRow.Partial("commit_support"))
            .DiscoverCapabilitiesAsync(ProviderCapabilityTestData.Request(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
        result.ReasonCode.ShouldBe("conflicting_operation_capability");
    }

    [Theory]
    [InlineData("", "missing_profile_schema_version", ProviderFailureCategory.ProviderValidationFailed)]
    [InlineData("future", "profile_schema_version_incompatible", ProviderFailureCategory.ReconciliationRequired)]
    public async Task MissingOrIncompatibleProfileVersionsShouldReturnSafeFailures(string schemaVersion, string reasonCode, ProviderFailureCategory category)
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.GitHubLike(schemaVersion: schemaVersion)
            .DiscoverCapabilitiesAsync(ProviderCapabilityTestData.Request(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(category);
        result.ReasonCode.ShouldBe(reasonCode);
    }

    [Fact]
    public async Task StaleOrDriftedTargetEvidenceShouldRequireReconciliation()
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.GitHubLike()
            .DiscoverCapabilitiesAsync(
                ProviderCapabilityTestData.Request(targetEvidence: ProviderCapabilityTestData.TargetEvidence(isStale: true)),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ReconciliationRequired);
        result.ReasonCode.ShouldBe("target_evidence_stale");
    }

    [Fact]
    public async Task UnsupportedProviderFamiliesShouldNotBecomeTwoProviderSwitches()
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.GitHubLike()
            .DiscoverCapabilitiesAsync(
                ProviderCapabilityTestData.Request(providerFamily: "unsupported-provider", providerKey: "unsupported-provider"),
                TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.UnsupportedProviderCapability);
        result.ReasonCode.ShouldBe("unsupported_provider_family");
    }
}
