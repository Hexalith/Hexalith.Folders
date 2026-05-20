using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

public sealed class LayeredAuthBackedFolderArchiveAclEvidenceProvider(
    ILayeredFolderAuthorizationResultAccessor authorizationAccessor) : IFolderArchiveAclEvidenceProvider
{
    private readonly ILayeredFolderAuthorizationResultAccessor _authorizationAccessor =
        authorizationAccessor ?? throw new ArgumentNullException(nameof(authorizationAccessor));

    public Task<FolderArchiveAclEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        FolderArchiveAclEvidence evidence = allowed is not null
            && string.Equals(allowed.ActionToken, FolderArchiveAclEvidence.ArchiveAction, StringComparison.Ordinal)
            && string.Equals(allowed.OperationScope, command.FolderId, StringComparison.Ordinal)
            && string.Equals(allowed.AuthoritativeTenantId, command.ManagedTenantId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(allowed.OrganizationId)
                ? FolderArchiveAclEvidence.Allowed(
                    allowed.AuthoritativeTenantId,
                    allowed.OrganizationId,
                    command.FolderId,
                    allowed.ActorSafeIdentifier)
                : FolderArchiveAclEvidence.Denied(
                    command.ManagedTenantId,
                    command.OrganizationId,
                    command.FolderId,
                    command.ActorPrincipalId);

        return Task.FromResult(evidence);
    }
}
