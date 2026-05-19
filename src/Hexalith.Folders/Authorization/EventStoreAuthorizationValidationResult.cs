namespace Hexalith.Folders.Authorization;

public sealed record EventStoreAuthorizationValidationResult(
    EventStoreAuthorizationValidationStatus Status,
    string OutcomeCode,
    string? FreshnessWatermark,
    string FreshnessClass,
    bool Retryable)
{
    public static EventStoreAuthorizationValidationResult Allowed(string? freshnessWatermark)
        => new(
            EventStoreAuthorizationValidationStatus.Allowed,
            LayeredAuthorizationOutcomeCodes.Allowed,
            freshnessWatermark,
            "fresh",
            Retryable: false);

    public static EventStoreAuthorizationValidationResult Denied()
        => new(
            EventStoreAuthorizationValidationStatus.Denied,
            LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied,
            FreshnessWatermark: null,
            FreshnessClass: "fresh",
            Retryable: false);

    public static EventStoreAuthorizationValidationResult Unavailable()
        => new(
            EventStoreAuthorizationValidationStatus.Unavailable,
            LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied,
            FreshnessWatermark: null,
            FreshnessClass: "unavailable",
            Retryable: true);

    public static EventStoreAuthorizationValidationResult Malformed()
        => new(
            EventStoreAuthorizationValidationStatus.Malformed,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
            FreshnessWatermark: null,
            FreshnessClass: "malformed",
            Retryable: false);
}
