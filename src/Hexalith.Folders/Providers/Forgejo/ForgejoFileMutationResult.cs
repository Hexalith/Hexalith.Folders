namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoFileMutationResult(
    bool IsSuccess,
    ForgejoApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? TreeSha)
{
    public static ForgejoFileMutationResult Success(string treeSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(treeSha);
        return new(true, default, null, treeSha);
    }

    public static ForgejoFileMutationResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, condition, retryAfter, null);

    public override string ToString() => nameof(ForgejoFileMutationResult);
}
