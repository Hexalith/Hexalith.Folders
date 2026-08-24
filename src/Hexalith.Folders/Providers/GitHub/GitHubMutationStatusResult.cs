namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubMutationStatusResult(
    GitHubMutationStatusDisposition Disposition,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static GitHubMutationStatusResult Available(GitHubMutationStatusDisposition disposition)
        => disposition is GitHubMutationStatusDisposition.Confirmed
            or GitHubMutationStatusDisposition.NotApplied
            or GitHubMutationStatusDisposition.Conflicting
                ? new(disposition, null, null)
                : Unavailable(GitHubApiFailureCondition.ValidationFailure);

    public static GitHubMutationStatusResult Unavailable(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(GitHubMutationStatusDisposition.Unavailable, condition, retryAfter);
}
