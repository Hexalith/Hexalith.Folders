namespace Hexalith.Folders.Providers.GitHub;

internal enum GitHubApiFailureCondition
{
    ValidationFailure,
    AuthenticationRequired,
    PermissionInsufficient,
    NotFoundOrHidden,
    ExistingEquivalent,
    RepositoryConflict,
    BranchProtectionConflict,
    PrimaryRateLimit,
    SecondaryRateLimit,
    ServerUnavailable,
    TimeoutDuringMutation,
    MalformedResponse,
    UnexpectedTransportFailure,
}
