using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public sealed class FoldersDomainServiceRequestHandler(
    IEnumerable<IDomainProcessor> processors,
    LayeredFolderAuthorizationService authorizer,
    ITenantContextAccessor tenantContext,
    IEventStoreClaimTransformEvidenceAccessor claimTransformEvidenceAccessor,
    IFolderCommandActionTokenMapper actionTokenMapper,
    ILayeredFolderAuthorizationResultAccessor? authorizationResultAccessor = null)
{
    private const string OrganizationBaselineScope = "organization_baseline";

    public async Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        FolderCommandActionMapping? mapping = actionTokenMapper.Map(request.Command);
        if (mapping is null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(
                BuildUnsupportedCommandDenial(request.Command));
        }

        string? operationScope = ResolveOperationScope(mapping.ScopeKind, request.Command);
        if (mapping.ScopeKind == FolderCommandOperationScopeKind.FolderAggregate && operationScope is null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(
                BuildMalformedScopeDenial(request.Command));
        }

        EventStoreClaimTransformEvidence claimTransform = claimTransformEvidenceAccessor.GetEvidence(mapping.ActionToken);

        LayeredFolderAuthorizationResult authorization = await authorizer.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                AuthoritativeTenantId: tenantContext.AuthoritativeTenantId,
                PrincipalId: tenantContext.PrincipalId ?? string.Empty,
                ActorSafeIdentifier: tenantContext.PrincipalId,
                ActionToken: mapping.ActionToken,
                OperationPolicy: LayeredFolderOperationPolicy.Mutation(),
                ClaimTransformEvidence: claimTransform,
                OperationScope: operationScope,
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

        ILayeredFolderAuthorizationResultAccessor? accessor = authorizationResultAccessor;
        try
        {
            if (accessor is not null)
            {
                accessor.Current = authorization;
            }

            DomainResult result = await processorList[0].ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
            return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
        }
        finally
        {
            if (accessor is not null)
            {
                accessor.Current = null;
            }
        }
    }

    private static string? ResolveOperationScope(FolderCommandOperationScopeKind kind, CommandEnvelope command)
        => kind switch
        {
            FolderCommandOperationScopeKind.OrganizationBaseline => OrganizationBaselineScope,
            FolderCommandOperationScopeKind.FolderAggregate => ValidateFolderAggregateId(command.AggregateId),
            _ => null,
        };

    private static string? ValidateFolderAggregateId(string? aggregateId)
    {
        if (string.IsNullOrWhiteSpace(aggregateId))
        {
            return null;
        }

        string trimmed = aggregateId.Trim();
        return trimmed.Length is > 0 and <= 128 ? trimmed : null;
    }

    private static LayeredFolderAuthorizationResult BuildUnsupportedCommandDenial(CommandEnvelope command)
        => LayeredFolderAuthorizationResult.Denied(
            new LayeredFolderAuthorizationDecisionSnapshot(
                TerminalLayer: AuthorizationLayer.FolderAcl,
                OutcomeCode: LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
                Retryable: false,
                FreshnessClass: "malformed",
                FreshnessWatermark: null,
                CorrelationId: command.CorrelationId,
                TaskId: null,
                ActorSafeIdentifier: "actor_present",
                OperationPolicyClass: "mutation",
                TimingBucket: "not_recorded",
                DecidedAt: DateTimeOffset.UtcNow),
            [AuthorizationLayer.JwtValidation, AuthorizationLayer.EventStoreClaimTransform, AuthorizationLayer.TenantAccessFreshness, AuthorizationLayer.FolderAcl]);

    private static LayeredFolderAuthorizationResult BuildMalformedScopeDenial(CommandEnvelope command)
        => LayeredFolderAuthorizationResult.Denied(
            new LayeredFolderAuthorizationDecisionSnapshot(
                TerminalLayer: AuthorizationLayer.FolderAcl,
                OutcomeCode: LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
                Retryable: false,
                FreshnessClass: "malformed",
                FreshnessWatermark: null,
                CorrelationId: command.CorrelationId,
                TaskId: null,
                ActorSafeIdentifier: "actor_present",
                OperationPolicyClass: "mutation",
                TimingBucket: "not_recorded",
                DecidedAt: DateTimeOffset.UtcNow),
            [AuthorizationLayer.JwtValidation, AuthorizationLayer.EventStoreClaimTransform, AuthorizationLayer.TenantAccessFreshness, AuthorizationLayer.FolderAcl]);
}
