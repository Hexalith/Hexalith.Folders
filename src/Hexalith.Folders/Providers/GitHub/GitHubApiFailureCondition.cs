namespace Hexalith.Folders.Providers.GitHub;

internal enum GitHubApiFailureCondition
{
    ValidationFailure,
    AuthenticationRequired,
    PermissionInsufficient,
    NotFoundOrHidden,
    ExistingEquivalent,
    RepositoryConflict,
    DefaultBranchConflict,
    MissingBranchOrRef,
    UnsupportedRefOperation,
    ContentsPermissionInsufficient,
    AdministrationPermissionInsufficient,
    BranchProtectionConflict,
    RefMoved,
    RefUpdateConflict,
    PrimaryRateLimit,
    SecondaryRateLimit,
    ServerUnavailable,
    CancellationBeforeDispatch,
    TimeoutDuringObservation,
    TimeoutDuringMutation,
    AmbiguousMutationResponse,
    MalformedResponse,
    UnexpectedTransportFailure,
}
