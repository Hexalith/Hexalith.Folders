using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.Folders.Server.Authorization;

public interface IFolderCommandActionTokenMapper
{
    FolderCommandActionMapping? Map(CommandEnvelope command);
}

public sealed record FolderCommandActionMapping(string ActionToken, FolderCommandOperationScopeKind ScopeKind);

public enum FolderCommandOperationScopeKind
{
    OrganizationBaseline,
    FolderAggregate,
}
