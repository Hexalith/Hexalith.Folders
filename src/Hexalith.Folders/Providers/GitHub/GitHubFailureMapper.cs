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
            ["missing_branch_or_ref"] = ProviderFailureCategory.ProviderValidationFailed.ToCategoryCode(),
            ["unsupported_ref_operation"] = ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            ["contents_permission"] = ProviderFailureCategory.ProviderPermissionInsufficient.ToCategoryCode(),
            ["administration_permission"] = ProviderFailureCategory.ProviderPermissionInsufficient.ToCategoryCode(),
            ["rate_limited"] = ProviderFailureCategory.ProviderRateLimited.ToCategoryCode(),
            ["unavailable"] = ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(),
            ["malformed"] = ProviderFailureCategory.ProviderFailureKnown.ToCategoryCode(),
            ["ambiguous_mutation_response"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
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
            GitHubApiFailureCondition.DefaultBranchConflict => (ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict"),
            GitHubApiFailureCondition.MissingBranchOrRef => (ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing"),
            GitHubApiFailureCondition.UnsupportedRefOperation => (ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported"),
            GitHubApiFailureCondition.ContentsPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient"),
            GitHubApiFailureCondition.AdministrationPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient"),
            GitHubApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict"),
            GitHubApiFailureCondition.PrimaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited"),
            GitHubApiFailureCondition.SecondaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited"),
            GitHubApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable"),
            GitHubApiFailureCondition.CancellationBeforeDispatch => (ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch"),
            GitHubApiFailureCondition.TimeoutDuringObservation => (ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable"),
            GitHubApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown"),
            GitHubApiFailureCondition.AmbiguousMutationResponse => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous"),
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

    public static ProviderRepositoryCreationResult ToProviderFailure(
        GitHubRepositoryCreationResult result,
        ProviderRepositoryCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        if (result.FailureCondition == GitHubApiFailureCondition.ExistingEquivalent)
        {
            string safeTargetFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? value)
                ? value
                : string.Empty;
            return ProviderRepositoryCreationResult.Success(request, equivalentExisting: true, safeTargetFingerprint);
        }

        (ProviderFailureCategory Category, string ReasonCode) mapped = result.FailureCondition switch
        {
            GitHubApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed"),
            GitHubApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required"),
            GitHubApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient"),
            GitHubApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing"),
            GitHubApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "github_repository_conflict"),
            GitHubApiFailureCondition.DefaultBranchConflict => (ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict"),
            GitHubApiFailureCondition.MissingBranchOrRef => (ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing"),
            GitHubApiFailureCondition.UnsupportedRefOperation => (ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported"),
            GitHubApiFailureCondition.ContentsPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient"),
            GitHubApiFailureCondition.AdministrationPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient"),
            GitHubApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict"),
            GitHubApiFailureCondition.PrimaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited"),
            GitHubApiFailureCondition.SecondaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited"),
            GitHubApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable"),
            GitHubApiFailureCondition.CancellationBeforeDispatch => (ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch"),
            GitHubApiFailureCondition.TimeoutDuringObservation => (ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable"),
            GitHubApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown"),
            GitHubApiFailureCondition.AmbiguousMutationResponse => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous"),
            GitHubApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response"),
            GitHubApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "github_unmapped_outcome"),
        };

        return ProviderRepositoryCreationResult.Failure(
            request,
            mapped.Category,
            mapped.ReasonCode,
            result.RetryAfter,
            safeRemediationCode: mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{mapped.Category.ToCategoryCode()}_remediation");
    }

    public static ProviderRepositoryBindingResult ToProviderFailure(
        GitHubRepositoryBindingResult result,
        ProviderRepositoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        if (result.FailureCondition == GitHubApiFailureCondition.ExistingEquivalent)
        {
            string safeTargetFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? value)
                ? value
                : string.Empty;
            return ProviderRepositoryBindingResult.Success(request, equivalentExisting: true, safeTargetFingerprint);
        }

        (ProviderFailureCategory Category, string ReasonCode) mapped = result.FailureCondition switch
        {
            GitHubApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "github_validation_failed"),
            GitHubApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "github_authentication_required"),
            GitHubApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_permission_insufficient"),
            GitHubApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_resource_hidden_or_missing"),
            GitHubApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "github_repository_conflict"),
            GitHubApiFailureCondition.DefaultBranchConflict => (ProviderFailureCategory.ProviderConflict, "github_default_branch_conflict"),
            GitHubApiFailureCondition.MissingBranchOrRef => (ProviderFailureCategory.ProviderValidationFailed, "github_branch_or_ref_missing"),
            GitHubApiFailureCondition.UnsupportedRefOperation => (ProviderFailureCategory.UnsupportedProviderCapability, "github_ref_operation_unsupported"),
            GitHubApiFailureCondition.ContentsPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_contents_permission_insufficient"),
            GitHubApiFailureCondition.AdministrationPermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "github_administration_permission_insufficient"),
            GitHubApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "github_branch_protection_conflict"),
            GitHubApiFailureCondition.PrimaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_primary_rate_limited"),
            GitHubApiFailureCondition.SecondaryRateLimit => (ProviderFailureCategory.ProviderRateLimited, "github_secondary_rate_limited"),
            GitHubApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "github_server_unavailable"),
            GitHubApiFailureCondition.CancellationBeforeDispatch => (ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch"),
            GitHubApiFailureCondition.TimeoutDuringObservation => (ProviderFailureCategory.ProviderUnavailable, "github_evidence_temporarily_unavailable"),
            GitHubApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_outcome_unknown"),
            GitHubApiFailureCondition.AmbiguousMutationResponse => (ProviderFailureCategory.UnknownProviderOutcome, "github_mutation_evidence_ambiguous"),
            GitHubApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "github_malformed_response"),
            GitHubApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "github_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "github_unmapped_outcome"),
        };

        return ProviderRepositoryBindingResult.Failure(
            request,
            mapped.Category,
            mapped.ReasonCode,
            result.RetryAfter,
            safeRemediationCode: mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{mapped.Category.ToCategoryCode()}_remediation");
    }
}
