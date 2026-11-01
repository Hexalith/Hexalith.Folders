using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Observes an authorized ACL handler read over one exact projected folder.</summary>
/// <param name="projection">The existing projected folder and ACL state.</param>
/// <param name="context">The actual authenticated request context.</param>
internal sealed class HostModeFolderAclReadModel(FolderPermissionProjection projection, IHttpContextAccessor context) : IFolderAclReadModel
{
    /// <summary>Gets the number of actual handler reads.</summary>
    internal int ReadCount { get; private set; }

    /// <summary>Gets the historical request path observed inside the handler.</summary>
    internal string? RequestPath { get; private set; }

    /// <summary>Gets the actual selected endpoint name.</summary>
    internal string? EndpointName { get; private set; }

    /// <inheritdoc/>
    public Task<FolderPermissionProjection?> GetCurrentAsync(string tenantId, string folderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        RequestPath = context.HttpContext?.Request.Path.Value;
        EndpointName = context.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        return Task.FromResult<FolderPermissionProjection?>(tenantId == projection.TenantId && folderId == projection.FolderId
            ? projection : null);
    }
}
