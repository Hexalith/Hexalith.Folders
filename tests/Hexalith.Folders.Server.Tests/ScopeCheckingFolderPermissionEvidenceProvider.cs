using Hexalith.Folders.Authorization;
using Hexalith.EventStore.Client.Streams;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Exercises the production sequence reader while the calling EventStore actor is locked.</summary>
internal sealed class ScopeCheckingFolderPermissionEvidenceProvider(IFolderPermissionEvidenceProvider inner)
    : IFolderPermissionEvidenceProvider
{
    private readonly DaprOrganizationStreamSequenceReader _reader = new(
        SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => throw new InvalidOperationException("The sequence reader called the locked actor.")));

    /// <summary>Gets the heads supplied by the request handler's invocation scopes.</summary>
    internal List<long> ObservedHeads { get; } = [];

    /// <inheritdoc/>
    public async Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationScope != "organization_baseline" || request.ActionToken == "manage_organization_acl")
        {
            ObservedHeads.Add(await _reader.GetCurrentSequenceAsync(request.ManagedTenantId,
                request.OperationScope == "organization_baseline" ? request.ManagedTenantId : request.OperationScope!,
                cancellationToken).ConfigureAwait(false));
        }

        return await inner.GetEvidenceAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
