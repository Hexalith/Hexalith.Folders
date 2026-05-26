using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoFailureMapper
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
            ["missing_repository"] = ProviderFailureCategory.ProviderValidationFailed.ToCategoryCode(),
            ["missing_branch_or_path"] = ProviderFailureCategory.ProviderValidationFailed.ToCategoryCode(),
            ["conflict"] = ProviderFailureCategory.ProviderConflict.ToCategoryCode(),
            ["redirect_cross_origin"] = ProviderFailureCategory.ProviderReadinessFailed.ToCategoryCode(),
            ["rate_limited"] = ProviderFailureCategory.ProviderRateLimited.ToCategoryCode(),
            ["unavailable"] = ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(),
            ["malformed"] = ProviderFailureCategory.ProviderFailureKnown.ToCategoryCode(),
            ["unsupported"] = ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            ["version_incompatible"] = ProviderFailureCategory.ReconciliationRequired.ToCategoryCode(),
            ["schema_drift_breaking"] = ProviderFailureCategory.ReconciliationRequired.ToCategoryCode(),
            ["timeout_mutation"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
            ["cancellation_mutation"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
            ["unexpected_transport"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
        };

    public static ProviderCapabilityDiscoveryResult ToProviderFailure(
        ForgejoReadinessResult readiness,
        ProviderCapabilityDiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(request);

        (ProviderFailureCategory Category, string ReasonCode) mapped = readiness.FailureCondition switch
        {
            ForgejoApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed"),
            ForgejoApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required"),
            ForgejoApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient"),
            ForgejoApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing"),
            ForgejoApiFailureCondition.MissingRepository => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing"),
            ForgejoApiFailureCondition.MissingBranchOrPath => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing"),
            ForgejoApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict"),
            ForgejoApiFailureCondition.ExistingEquivalent => (ProviderFailureCategory.None, "forgejo_existing_equivalent"),
            ForgejoApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict"),
            ForgejoApiFailureCondition.RedirectCrossOrigin => (ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected"),
            ForgejoApiFailureCondition.RateLimit => (ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited"),
            ForgejoApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable"),
            ForgejoApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown"),
            ForgejoApiFailureCondition.CancellationDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown"),
            ForgejoApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response"),
            ForgejoApiFailureCondition.UnsupportedCapability => (ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported"),
            ForgejoApiFailureCondition.VersionIncompatible => (ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible"),
            ForgejoApiFailureCondition.SchemaDriftBreaking => (ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking"),
            ForgejoApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_unmapped_outcome"),
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
        ForgejoRepositoryCreationResult result,
        ProviderRepositoryCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        if (result.FailureCondition == ForgejoApiFailureCondition.ExistingEquivalent)
        {
            string safeTargetFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? value)
                ? value
                : string.Empty;
            return ProviderRepositoryCreationResult.Success(request, equivalentExisting: true, safeTargetFingerprint);
        }

        (ProviderFailureCategory Category, string ReasonCode) mapped = result.FailureCondition switch
        {
            ForgejoApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed"),
            ForgejoApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required"),
            ForgejoApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient"),
            ForgejoApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing"),
            ForgejoApiFailureCondition.MissingRepository => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing"),
            ForgejoApiFailureCondition.MissingBranchOrPath => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing"),
            ForgejoApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict"),
            ForgejoApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict"),
            ForgejoApiFailureCondition.RedirectCrossOrigin => (ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected"),
            ForgejoApiFailureCondition.RateLimit => (ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited"),
            ForgejoApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable"),
            ForgejoApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown"),
            ForgejoApiFailureCondition.CancellationDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown"),
            ForgejoApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response"),
            ForgejoApiFailureCondition.UnsupportedCapability => (ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported"),
            ForgejoApiFailureCondition.VersionIncompatible => (ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible"),
            ForgejoApiFailureCondition.SchemaDriftBreaking => (ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking"),
            ForgejoApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_unmapped_outcome"),
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
        ForgejoRepositoryBindingResult result,
        ProviderRepositoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        if (result.FailureCondition == ForgejoApiFailureCondition.ExistingEquivalent)
        {
            string safeTargetFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? value)
                ? value
                : string.Empty;
            return ProviderRepositoryBindingResult.Success(request, equivalentExisting: true, safeTargetFingerprint);
        }

        (ProviderFailureCategory Category, string ReasonCode) mapped = result.FailureCondition switch
        {
            ForgejoApiFailureCondition.ValidationFailure => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_validation_failed"),
            ForgejoApiFailureCondition.AuthenticationRequired => (ProviderFailureCategory.ProviderAuthenticationRequired, "forgejo_authentication_required"),
            ForgejoApiFailureCondition.PermissionInsufficient => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_permission_insufficient"),
            ForgejoApiFailureCondition.NotFoundOrHidden => (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_resource_hidden_or_missing"),
            ForgejoApiFailureCondition.MissingRepository => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_repository_missing"),
            ForgejoApiFailureCondition.MissingBranchOrPath => (ProviderFailureCategory.ProviderValidationFailed, "forgejo_branch_or_path_missing"),
            ForgejoApiFailureCondition.RepositoryConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_repository_conflict"),
            ForgejoApiFailureCondition.BranchProtectionConflict => (ProviderFailureCategory.ProviderConflict, "forgejo_branch_protection_conflict"),
            ForgejoApiFailureCondition.RedirectCrossOrigin => (ProviderFailureCategory.ProviderReadinessFailed, "forgejo_cross_origin_redirect_rejected"),
            ForgejoApiFailureCondition.RateLimit => (ProviderFailureCategory.ProviderRateLimited, "forgejo_rate_limited"),
            ForgejoApiFailureCondition.ServerUnavailable => (ProviderFailureCategory.ProviderUnavailable, "forgejo_server_unavailable"),
            ForgejoApiFailureCondition.TimeoutDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_outcome_unknown"),
            ForgejoApiFailureCondition.CancellationDuringMutation => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_mutation_cancellation_outcome_unknown"),
            ForgejoApiFailureCondition.MalformedResponse => (ProviderFailureCategory.ProviderFailureKnown, "forgejo_malformed_response"),
            ForgejoApiFailureCondition.UnsupportedCapability => (ProviderFailureCategory.UnsupportedProviderCapability, "forgejo_capability_unsupported"),
            ForgejoApiFailureCondition.VersionIncompatible => (ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible"),
            ForgejoApiFailureCondition.SchemaDriftBreaking => (ProviderFailureCategory.ReconciliationRequired, "forgejo_schema_drift_breaking"),
            ForgejoApiFailureCondition.UnexpectedTransportFailure => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_transport_outcome_unknown"),
            _ => (ProviderFailureCategory.UnknownProviderOutcome, "forgejo_unmapped_outcome"),
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
