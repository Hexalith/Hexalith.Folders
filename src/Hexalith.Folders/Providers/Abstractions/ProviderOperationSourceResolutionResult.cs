namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationSourceResolutionResult<T>(
    bool IsSuccess,
    T? Source,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
    where T : class
{
    private static readonly HashSet<string> AllowedReasonCodes = new(StringComparer.Ordinal)
    {
        "operation_source_unavailable",
        "provider_file_mutation_source_unconfigured",
        "provider_commit_source_unconfigured",
        "provider_operation_status_source_unconfigured",
        "provider_unavailable",
        "provider_configuration_missing",
        "provider_permission_insufficient",
        "provider_validation_failed",
        "provider_conflict",
        "provider_rate_limited",
        "reconciliation_required",
    };

    public TimeSpan? SafeRetryAfter
        => RetryAfter is { } retryAfter
            && retryAfter > TimeSpan.Zero
            && retryAfter <= TimeSpan.FromHours(24)
                ? retryAfter
                : null;

    public ProviderFailureCategory GetSafeFailureCategory(ProviderFailureCategory fallback)
        => Enum.IsDefined(FailureCategory) && FailureCategory != ProviderFailureCategory.None
            ? FailureCategory
            : fallback;

    public string GetSafeReasonCode(string fallback)
        => IsSafeReasonCode(ReasonCode) ? ReasonCode : fallback;

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
        string categoryCode = category.ToCategoryCode();
        return new(
            false,
            null,
            category,
            IsSafeReasonCode(reasonCode) ? reasonCode : categoryCode,
            retryAfter is { } duration
                && duration > TimeSpan.Zero
                && duration <= TimeSpan.FromHours(24)
                    ? duration
                    : null);
    }

    private static bool IsSafeReasonCode(string? value)
        => value is { Length: > 0 and <= 128 }
            && AllowedReasonCodes.Contains(value);
}
