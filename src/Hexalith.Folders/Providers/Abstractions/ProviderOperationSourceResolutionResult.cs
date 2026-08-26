namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationSourceResolutionResult<T>(
    bool IsSuccess,
    T? Source,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
    where T : class
{
    private static readonly IReadOnlyDictionary<string, ProviderFailureCategory?> AllowedReasonCodes =
        new Dictionary<string, ProviderFailureCategory?>(StringComparer.Ordinal)
    {
        ["operation_source_unavailable"] = null,
        ["provider_file_mutation_source_unconfigured"] = ProviderFailureCategory.ProviderConfigurationMissing,
        ["provider_commit_source_unconfigured"] = ProviderFailureCategory.ProviderConfigurationMissing,
        ["provider_operation_status_source_unconfigured"] = ProviderFailureCategory.ProviderConfigurationMissing,
        ["provider_unavailable"] = ProviderFailureCategory.ProviderUnavailable,
        ["provider_configuration_missing"] = ProviderFailureCategory.ProviderConfigurationMissing,
        ["provider_permission_insufficient"] = ProviderFailureCategory.ProviderPermissionInsufficient,
        ["provider_validation_failed"] = ProviderFailureCategory.ProviderValidationFailed,
        ["provider_conflict"] = ProviderFailureCategory.ProviderConflict,
        ["provider_rate_limited"] = ProviderFailureCategory.ProviderRateLimited,
        ["reconciliation_required"] = ProviderFailureCategory.ReconciliationRequired,
    };

    public TimeSpan? SafeRetryAfter
        => IsSafeFailureCategory(FailureCategory)
            && FailureCategory.IsRetryableByDefault()
            && RetryAfter is { } retryAfter
            && retryAfter > TimeSpan.Zero
            && retryAfter <= TimeSpan.FromHours(24)
                ? retryAfter
                : null;

    public ProviderFailureCategory GetSafeFailureCategory(ProviderFailureCategory fallback)
        => IsSafeFailureCategory(FailureCategory)
            ? FailureCategory
            : fallback;

    public string GetSafeReasonCode(string fallback)
    {
        ProviderFailureCategory safeCategory = GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable);
        return IsSafeFailureCategory(FailureCategory) && IsSafeReasonCode(ReasonCode, safeCategory)
            ? ReasonCode
            : fallback;
    }

    public static ProviderOperationSourceResolutionResult<T> Success(T source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(true, source, ProviderFailureCategory.None, "success", null);
    }

    public static ProviderOperationSourceResolutionResult<T> Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
    {
        bool hasSafeCategory = IsSafeFailureCategory(category);
        ProviderFailureCategory safeCategory = hasSafeCategory
                ? category
                : ProviderFailureCategory.ProviderUnavailable;
        string categoryCode = safeCategory.ToCategoryCode();
        return new(
            false,
            null,
            safeCategory,
            hasSafeCategory && IsSafeReasonCode(reasonCode, safeCategory) ? reasonCode : categoryCode,
            hasSafeCategory
                && safeCategory.IsRetryableByDefault()
                && retryAfter is { } duration
                && duration > TimeSpan.Zero
                && duration <= TimeSpan.FromHours(24)
                    ? duration
                    : null);
    }

    private static bool IsSafeFailureCategory(ProviderFailureCategory category)
        => Enum.IsDefined(category)
            && category is not ProviderFailureCategory.None and not ProviderFailureCategory.UnknownProviderOutcome;

    private static bool IsSafeReasonCode(string? value, ProviderFailureCategory category)
        => value is { Length: > 0 and <= 128 }
            && AllowedReasonCodes.TryGetValue(value, out ProviderFailureCategory? expectedCategory)
            && (expectedCategory is null || expectedCategory == category);
}
