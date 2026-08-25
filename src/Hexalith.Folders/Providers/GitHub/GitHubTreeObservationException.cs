namespace Hexalith.Folders.Providers.GitHub;

/// <summary>
/// Carries metadata-safe tree-observation failure evidence through the private transport.
/// </summary>
internal sealed class GitHubTreeObservationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTreeObservationException"/> class.
    /// </summary>
    /// <param name="condition">The safe provider failure condition.</param>
    /// <param name="retryAfter">The bounded retry evidence.</param>
    public GitHubTreeObservationException(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
    {
        Condition = condition;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the safe provider failure condition.
    /// </summary>
    public GitHubApiFailureCondition Condition { get; }

    /// <summary>
    /// Gets the bounded retry evidence.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
