namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRateLimitEvidence(
    string Classification,
    bool Retryable,
    TimeSpan? RetryAfter,
    string HeaderPosture);
