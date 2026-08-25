namespace Hexalith.Folders.Providers.Abstractions;

internal enum ProviderOperationOutcomeKind
{
    NoDispatch,
    StagedTree,
    CreatedCommit,
    RefUpdateConfirmed,
    KnownTerminalFailure,
    Unknown,
}
