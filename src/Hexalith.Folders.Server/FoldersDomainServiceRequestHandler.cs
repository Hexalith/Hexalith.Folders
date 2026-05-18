using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public sealed class FoldersDomainServiceRequestHandler(
    IEnumerable<IDomainProcessor> processors,
    TenantAccessAuthorizer authorizer,
    ITenantContextAccessor tenantContext)
{
    public async Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        TenantAccessAuthorizationResult authorization = await authorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext(
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId ?? string.Empty,
                request.Command.TenantId),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed)
        {
            return Results.Problem(
                type: $"https://hexalith.dev/errors/folders/{authorization.Code}",
                title: "Tenant access denied.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "authorization",
                    ["code"] = authorization.Code,
                    ["retryable"] = authorization.Outcome is TenantAccessOutcome.StaleProjection
                        or TenantAccessOutcome.UnavailableProjection,
                });
        }

        List<IDomainProcessor> processorList = [.. processors];
        if (processorList.Count == 0)
        {
            return Results.Problem(
                title: "No Folders domain processor is registered.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        if (processorList.Count > 1)
        {
            return Results.Problem(
                title: "Multiple Folders domain processors are registered; the dispatcher requires exactly one.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        DomainResult result = await processorList[0].ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
        return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
    }
}
