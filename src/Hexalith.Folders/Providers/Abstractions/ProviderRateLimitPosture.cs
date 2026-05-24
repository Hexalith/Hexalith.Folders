namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderRateLimitPosture(
    string Classification,
    bool Retryable,
    TimeSpan? RetryAfter,
    IReadOnlyDictionary<string, string> Metadata);
