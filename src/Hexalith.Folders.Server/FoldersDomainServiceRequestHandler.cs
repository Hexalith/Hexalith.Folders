using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public sealed class FoldersDomainServiceRequestHandler(
    IEnumerable<IDomainProcessor> processors,
    TenantAccessAuthorizer authorizer)
{
    public async Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        TenantAccessAuthorizationResult authorization = await authorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext(
                request.Command.TenantId,
                request.Command.UserId,
                request.Command.TenantId),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed)
        {
            return Results.Json(authorization, statusCode: StatusCodes.Status403Forbidden);
        }

        foreach (IDomainProcessor processor in processors)
        {
            DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
            return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
        }

        return Results.Problem(
            title: "No Folders domain processor is registered.",
            statusCode: StatusCodes.Status501NotImplemented);
    }
}
