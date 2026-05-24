using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubFailureMapper
{
    public static IReadOnlyDictionary<string, string> KnownFailureMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["success"] = ProviderFailureCategory.None.ToCategoryCode(),
            ["existing_equivalent"] = ProviderFailureCategory.None.ToCategoryCode(),
            ["validation"] = ProviderFailureCategory.ProviderValidationFailed.ToCategoryCode(),
            ["authentication"] = ProviderFailureCategory.ProviderAuthenticationRequired.ToCategoryCode(),
            ["permission"] = ProviderFailureCategory.ProviderPermissionInsufficient.ToCategoryCode(),
            ["hidden_or_missing"] = ProviderFailureCategory.ProviderPermissionInsufficient.ToCategoryCode(),
            ["conflict"] = ProviderFailureCategory.ProviderConflict.ToCategoryCode(),
            ["rate_limited"] = ProviderFailureCategory.ProviderRateLimited.ToCategoryCode(),
            ["unavailable"] = ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(),
            ["malformed"] = ProviderFailureCategory.ProviderFailureKnown.ToCategoryCode(),
            ["timeout_mutation"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
            ["unexpected_transport"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
        };

    public static ProviderCapabilityDiscoveryResult ToProviderFailure(
        GitHubReadinessResult readiness,
        ProviderCapabilityDiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(request);

        (ProviderFailureCategory Category, string ReasonCode) mapped = readiness.FailureCondition switch
        {
            GitHubApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed"),
            GitHubApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required"),
            GitHubApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient"),
            GitHubApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing"),
            GitHubApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "github_repository_conflict"),
            GitHubApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict"),
            GitHubApiFailureCondition.PrimaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited"),
            GitHubApiFailureCondition.SecondaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited"),
            GitHubApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable"),
            GitHubApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown"),
            GitHubApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response"),
            GitHubApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "github_unmapped_outcome"),
        };

        return ProviderCapabilityDiscoveryResult.Failure(
            mapped.Category,
            mapped.ReasonCode,
            request.CorrelationId,
            readiness.RetryAfter,
            safeRemediationCode: mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{mapped.Category.ToCategoryCode()}_remediation");
    }
}

