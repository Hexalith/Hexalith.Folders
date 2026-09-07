using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoOperationStatusResult(
    bool IsSuccess,
    ProviderOperationStatusKind Status,
    ForgejoApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? ObservedSha,
    string? ObservedFullRef,
    string? ObservedObjectType)
{
    public static ForgejoOperationStatusResult Observed(
        ProviderOperationStatusKind status,
        string observedSha,
        string observedFullRef,
        string observedObjectType = "commit")
        => new(true, status, default, null, observedSha, observedFullRef, observedObjectType);

    public static ForgejoOperationStatusResult Conflicting(
        string? observedSha,
        string? observedFullRef,
        string? observedObjectType)
        => new(true, ProviderOperationStatusKind.Conflicting, default, null, observedSha, observedFullRef, observedObjectType);

    public static ForgejoOperationStatusResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, ProviderOperationStatusKind.Unavailable, condition, retryAfter, null, null, null);

    public override string ToString() => nameof(ForgejoOperationStatusResult);
}
