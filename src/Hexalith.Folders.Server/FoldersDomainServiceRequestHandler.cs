using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public sealed class FoldersDomainServiceRequestHandler(
    IEnumerable<IDomainProcessor> processors,
    LayeredFolderAuthorizationService authorizer,
    ITenantContextAccessor tenantContext)
{
    public async Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string actionToken = ActionTokenFor(request.Command);
        LayeredFolderAuthorizationResult authorization = await authorizer.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                AuthoritativeTenantId: tenantContext.AuthoritativeTenantId,
                PrincipalId: tenantContext.PrincipalId ?? string.Empty,
                ActorSafeIdentifier: tenantContext.PrincipalId,
                ActionToken: actionToken,
                OperationPolicy: LayeredFolderOperationPolicy.Mutation(),
                ClaimTransformEvidence: EventStoreClaimTransformEvidence.Allowed(
                    request.Command.TenantId,
                    request.Command.UserId,
                    [actionToken, "commands:*"]),
                OperationScope: request.Command.AggregateId,
                CorrelationId: request.Command.CorrelationId,
                TaskId: null,
                ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["eventstore_envelope_tenant"] = request.Command.TenantId,
                },
                ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["eventstore_envelope_user"] = request.Command.UserId,
                }),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(authorization);
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

    private static string ActionTokenFor(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.CommandType.Contains("CreateFolder", StringComparison.Ordinal)
            ? "create_folder"
            : "read_metadata";
    }
}
