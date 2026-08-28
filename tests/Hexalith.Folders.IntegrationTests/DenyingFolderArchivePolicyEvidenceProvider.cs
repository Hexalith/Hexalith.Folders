using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>
/// Records metadata-only archive policy observations and returns a scoped denial.
/// </summary>
internal sealed class DenyingFolderArchivePolicyEvidenceProvider : IFolderArchivePolicyEvidenceProvider
{
    private int _calls;

    /// <summary>Gets the number of policy evaluations.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets the last observed managed tenant identifier.</summary>
    public string? LastManagedTenantId { get; private set; }

    /// <summary>Gets the last observed organization identifier.</summary>
    public string? LastOrganizationId { get; private set; }

    /// <summary>Gets the last observed folder identifier.</summary>
    public string? LastFolderId { get; private set; }

    /// <inheritdoc/>
    /// <remarks><c>IDomainProcessor.ProcessAsync</c> carries no token, so <c>FolderDomainProcessor</c> hands
    /// this provider <see cref="CancellationToken.None"/>; the guard below is defensive only.</remarks>
    public Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _calls);
        LastManagedTenantId = command.ManagedTenantId;
        LastOrganizationId = command.OrganizationId;
        LastFolderId = command.FolderId;

        return Task.FromResult(FolderArchivePolicyEvidence.Denied(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            "v1-test-denied"));
    }
}
