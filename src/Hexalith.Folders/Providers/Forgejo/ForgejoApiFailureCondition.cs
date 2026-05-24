namespace Hexalith.Folders.Providers.Forgejo;

internal enum ForgejoApiFailureCondition
{
    ValidationFailure,
    AuthenticationRequired,
    PermissionInsufficient,
    NotFoundOrHidden,
    MissingRepository,
    MissingBranchOrPath,
    RepositoryConflict,
    ExistingEquivalent,
    BranchProtectionConflict,
    RedirectCrossOrigin,
    RateLimit,
    ServerUnavailable,
    TimeoutDuringMutation,
    CancellationDuringMutation,
    MalformedResponse,
    UnsupportedCapability,
    VersionIncompatible,
    SchemaDriftBreaking,
    UnexpectedTransportFailure,
}
