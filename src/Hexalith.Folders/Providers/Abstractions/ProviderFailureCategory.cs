namespace Hexalith.Folders.Providers.Abstractions;

public enum ProviderFailureCategory
{
    None,
    UnsupportedProviderCapability,
    ProviderUnavailable,
    ProviderAuthenticationRequired,
    ProviderConfigurationMissing,
    ProviderPermissionInsufficient,
    ProviderRateLimited,
    ProviderValidationFailed,
    ProviderConflict,
    ProviderReadinessFailed,
    ProviderFailureKnown,
    ProviderTransientFailure,
    UnknownProviderOutcome,
    ReconciliationRequired,
    InternalError,
}
