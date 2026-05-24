namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRateLimitEvidence(
    string Classification,
    bool Retryable,
    TimeSpan? RetryAfter);

