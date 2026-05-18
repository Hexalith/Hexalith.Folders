using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Server;

public static class FoldersDomainServiceEndpoints
{
    public static IEndpointRouteBuilder MapFoldersDomainServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(FoldersServerModule.ProcessRoute, async (
            DomainServiceRequest request,
            FoldersDomainServiceRequestHandler handler,
            CancellationToken cancellationToken)
            => await handler.ProcessAsync(request, cancellationToken).ConfigureAwait(false));

        endpoints.MapPost(FoldersServerModule.ProjectRoute, (ProjectionRequest _) =>
            Results.Ok(Array.Empty<ProjectionResponse>()));

        return endpoints;
    }
}
