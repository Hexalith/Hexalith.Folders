namespace Hexalith.Folders.Providers.Abstractions;

public static class ProviderFailureCategoryExtensions
{
    public static string ToCategoryCode(this ProviderFailureCategory category)
        => category switch
        {
            ProviderFailureCategory.None => "none",
            ProviderFailureCategory.UnsupportedProviderCapability => "unsupported_provider_capability",
            ProviderFailureCategory.ProviderUnavailable => "provider_unavailable",
            ProviderFailureCategory.ProviderAuthenticationRequired => "provider_authentication_required",
            ProviderFailureCategory.ProviderConfigurationMissing => "provider_configuration_missing",
            ProviderFailureCategory.ProviderPermissionInsufficient => "provider_permission_insufficient",
            ProviderFailureCategory.ProviderRateLimited => "provider_rate_limited",
            ProviderFailureCategory.ProviderValidationFailed => "provider_validation_failed",
            ProviderFailureCategory.ProviderConflict => "provider_conflict",
            ProviderFailureCategory.ProviderReadinessFailed => "provider_readiness_failed",
            ProviderFailureCategory.ProviderFailureKnown => "provider_failure_known",
            ProviderFailureCategory.ProviderTransientFailure => "provider_transient_failure",
            ProviderFailureCategory.UnknownProviderOutcome => "unknown_provider_outcome",
            ProviderFailureCategory.ReconciliationRequired => "reconciliation_required",
            _ => "internal_error",
        };

    public static bool IsRetryableByDefault(this ProviderFailureCategory category)
        => category is ProviderFailureCategory.ProviderUnavailable
            or ProviderFailureCategory.ProviderRateLimited
            or ProviderFailureCategory.ProviderTransientFailure;
}
