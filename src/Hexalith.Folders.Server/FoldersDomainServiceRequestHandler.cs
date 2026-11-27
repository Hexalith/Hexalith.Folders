using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Server.Provisioning;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public sealed class FoldersDomainServiceRequestHandler(
    IEnumerable<IDomainProcessor> processors,
    LayeredFolderAuthorizationService authorizer,
    ITenantContextAccessor tenantContext,
    IEventStoreClaimTransformEvidenceAccessor claimTransformEvidenceAccessor,
    IFolderCommandActionTokenMapper actionTokenMapper,
    ILayeredFolderAuthorizationResultAccessor authorizationResultAccessor,
    HumanAuthorityReceiptVerifier? humanReceiptVerifier = null,
    IConfiguration? configuration = null)
{
    private const string OrganizationBaselineScope = FolderStreamName.ReservedOrganizationPermissionScope;

    public Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken cancellationToken = default)
        => ProcessAsync(request, provisioning: null, cancellationToken);

    internal async Task<IResult> ProcessAsync(DomainServiceRequest request, FolderProvisioningService? provisioning,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (FolderProvisioningService.MutationsFrozen(configuration))
        {
            return new AuthorizationDenialTransportResult(Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        }

        if (request.Command.CommandType == FolderProvisioningContract.CommandType)
        {
            return provisioning is null
                ? Results.NotFound()
                : await provisioning.ProcessAsync(request, cancellationToken).ConfigureAwait(false);
        }

        IDisposable? humanAuthorityScope = null;
        if (humanReceiptVerifier is not null
            && !humanReceiptVerifier.TryEnter(request, out humanAuthorityScope))
        {
            return new AuthorizationDenialTransportResult(Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "authentication_failure",
                    ["code"] = "authentication_failure",
                    ["retryable"] = false,
                    ["clientAction"] = "authenticate",
                    ["details"] = new Dictionary<string, object?>
                    {
                        ["visibility"] = "metadata_only",
                    },
                }));
        }

        using IDisposable? verifiedHumanScope = humanAuthorityScope;
        if (!TryValidateProcessEnvelope(request.Command, out string? taskId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "validation_error",
                    ["code"] = "validation_error",
                    ["retryable"] = false,
                    ["clientAction"] = "correct_request",
                    ["details"] = new Dictionary<string, object?>
                    {
                        ["visibility"] = "metadata_only",
                    },
                });
        }

        FolderCommandActionMapping? mapping = actionTokenMapper.Map(request.Command);
        if (mapping is null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(
                BuildUnsupportedCommandDenial(request.Command), trustedEventStoreInvocation: true);
        }

        string? operationScope = ResolveOperationScope(mapping.ScopeKind, request.Command);
        if (mapping.ScopeKind == FolderCommandOperationScopeKind.FolderAggregate && operationScope is null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(
                BuildMalformedScopeDenial(request.Command), trustedEventStoreInvocation: true);
        }

        if (!IsAdmittedActor(request.Command))
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(
                BuildMalformedScopeDenial(request.Command), trustedEventStoreInvocation: true);
        }

        EventStoreClaimTransformEvidence claimTransform = claimTransformEvidenceAccessor.GetEvidence(mapping.ActionToken);

        LayeredFolderAuthorizationContext authorizationContext = new(
                AuthoritativeTenantId: tenantContext.AuthoritativeTenantId,
                PrincipalId: tenantContext.PrincipalId ?? string.Empty,
                ActorSafeIdentifier: tenantContext.PrincipalId,
                ActionToken: mapping.ActionToken,
                OperationPolicy: LayeredFolderOperationPolicy.Mutation(),
                ClaimTransformEvidence: claimTransform,
                OperationScope: operationScope,
                CorrelationId: request.Command.CorrelationId,
                TaskId: taskId,
                ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["eventstore_envelope_tenant"] = request.Command.TenantId,
                },
                ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["eventstore_envelope_user"] = request.Command.UserId,
                });

        bool initializingAcl = string.Equals(request.Command.CommandType,
            FoldersServerModule.InitializeOrganizationAclBaselineCommandType, StringComparison.Ordinal);
        if (initializingAcl
            && !string.Equals(request.Command.AggregateId, tenantContext.AuthoritativeTenantId, StringComparison.Ordinal))
        {
            return SafeAclBootstrapDenial(StatusCodes.Status403Forbidden, "permission_denied");
        }
        bool mutatingAcl = string.Equals(request.Command.CommandType,
            FoldersServerModule.GrantOrganizationAclPrincipalCommandType, StringComparison.Ordinal)
            || string.Equals(request.Command.CommandType,
                FoldersServerModule.RevokeOrganizationAclPrincipalCommandType, StringComparison.Ordinal);
        IDisposable? actorSequenceScope = null;
        if (mutatingAcl)
        {
            DomainServiceCurrentState? actorState;
            try
            {
                actorState = DomainStateReplay.SnapshotAware(request.CurrentState);
            }
            catch (System.Text.Json.JsonException)
            {
                return SafeAclBootstrapDenial(StatusCodes.Status503ServiceUnavailable, "authorization_unavailable");
            }

            if (actorState is not null && actorState.CurrentSequence < 0)
            {
                return SafeAclBootstrapDenial(StatusCodes.Status503ServiceUnavailable, "authorization_unavailable");
            }

            actorSequenceScope = OrganizationActorSequenceScope.Enter(request.Command.TenantId,
                request.Command.AggregateId, actorState?.CurrentSequence ?? 0);
        }
        else if (mapping.ScopeKind == FolderCommandOperationScopeKind.FolderAggregate)
        {
            DomainServiceCurrentState? actorState;
            try
            {
                actorState = DomainStateReplay.SnapshotAware(request.CurrentState);
            }
            catch (System.Text.Json.JsonException)
            {
                return SafeAclBootstrapDenial(StatusCodes.Status503ServiceUnavailable, "authorization_unavailable");
            }

            if (actorState is not null && actorState.CurrentSequence < 0)
            {
                return SafeAclBootstrapDenial(StatusCodes.Status503ServiceUnavailable, "authorization_unavailable");
            }

            actorSequenceScope = FolderActorSequenceScope.Enter(request.Command.TenantId,
                request.Command.AggregateId, actorState?.CurrentSequence ?? 0);
        }

        using IDisposable? verifiedActorSequenceScope = actorSequenceScope;
        LayeredFolderAuthorizationResult authorization = initializingAcl
            ? await authorizer.ReauthorizeOrganizationAclMutationAsync(authorizationContext, initializing: true, cancellationToken).ConfigureAwait(false)
            : await authorizer.AuthorizeAsync(authorizationContext, cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(authorization, trustedEventStoreInvocation: true);
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

        // Begin the scope before entering the try block — if BeginScope throws (e.g. on a
        // null authorization payload or because a prior scope was not torn down), the
        // finally block must not attempt to EndScope a scope that never began.
        authorizationResultAccessor.BeginScope(authorization);
        try
        {
            IDomainProcessor processor = processorList[0];
            DomainResult result = processor is IAsyncDomainProcessor asyncProcessor
                ? await asyncProcessor.ProcessAsync(request.Command, request.CurrentState, cancellationToken).ConfigureAwait(false)
                : await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
            return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
        }
        catch (OrganizationAclAuthorizationDeniedException denial)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(denial.Authorization, trustedEventStoreInvocation: true);
        }
        finally
        {
            authorizationResultAccessor.EndScope();
        }
    }

    private static IResult SafeAclBootstrapDenial(int status, string code)
        => new AuthorizationDenialTransportResult(Results.Problem(statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["category"] = status == StatusCodes.Status503ServiceUnavailable ? "authorization_unavailable" : "authorization_denied",
            ["code"] = code,
            ["retryable"] = status == StatusCodes.Status503ServiceUnavailable,
            ["clientAction"] = status == StatusCodes.Status503ServiceUnavailable ? "retry" : "request_access",
            ["details"] = new Dictionary<string, object?> { ["visibility"] = "metadata_only" },
        }));

    private static string? ResolveOperationScope(FolderCommandOperationScopeKind kind, CommandEnvelope command)
        => kind switch
        {
            FolderCommandOperationScopeKind.OrganizationBaseline => OrganizationBaselineScope,
            FolderCommandOperationScopeKind.FolderAggregate => ValidateFolderAggregateId(command.AggregateId),
            _ => null,
        };

    /// <summary>Admits commands only to their disjoint EventStore actor namespace before state lookup.</summary>
    internal static bool IsAdmittedActor(CommandEnvelope command)
        => command.CommandType switch
        {
            FoldersServerModule.ConfigureProviderBindingCommandType => ProviderBindingActorId.IsReserved(command.AggregateId)
                && !string.Equals(command.AggregateId, command.TenantId, StringComparison.Ordinal),
            FoldersServerModule.InitializeOrganizationAclBaselineCommandType
                or FoldersServerModule.GrantOrganizationAclPrincipalCommandType
                or FoldersServerModule.RevokeOrganizationAclPrincipalCommandType =>
                    string.Equals(command.AggregateId, command.TenantId, StringComparison.Ordinal)
                    && !ProviderBindingActorId.IsReserved(command.TenantId)
                    && !string.Equals(command.TenantId, OrganizationBaselineScope, StringComparison.Ordinal),
            _ => FolderStreamName.TryCreate(command.TenantId, command.AggregateId, out _, out _),
        };

    private static string? ValidateFolderAggregateId(string? aggregateId)
        => FolderCanonicalSegmentIdentifier.IsValid(aggregateId) ? aggregateId : null;

    private static bool TryValidateProcessEnvelope(CommandEnvelope command, out string? taskId)
    {
        taskId = null;
        if (!FolderCanonicalSegmentIdentifier.IsValid(command.MessageId)
            || !FolderCanonicalSegmentIdentifier.IsValid(command.CorrelationId)
            || !FolderCanonicalSegmentIdentifier.IsValid(command.TenantId)
            || !FolderCanonicalSegmentIdentifier.IsValid(command.AggregateId)
            || !FolderCanonicalSegmentIdentifier.IsValid(command.UserId)
            || command.Extensions is null
            || !command.Extensions.TryGetValue("taskId", out string? extensionTaskId)
            || !FolderCanonicalSegmentIdentifier.IsValid(extensionTaskId))
        {
            return false;
        }

        taskId = extensionTaskId;
        return true;
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
                TaskId: TryReadCanonicalTaskId(command),
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
                TaskId: TryReadCanonicalTaskId(command),
                ActorSafeIdentifier: "actor_present",
                OperationPolicyClass: "mutation",
                TimingBucket: "not_recorded",
                DecidedAt: DateTimeOffset.UtcNow),
            [AuthorizationLayer.JwtValidation, AuthorizationLayer.EventStoreClaimTransform, AuthorizationLayer.TenantAccessFreshness, AuthorizationLayer.FolderAcl]);

    private static string? TryReadCanonicalTaskId(CommandEnvelope command)
        => command.Extensions is not null
        && command.Extensions.TryGetValue("taskId", out string? taskId)
        && FolderCanonicalSegmentIdentifier.IsValid(taskId)
            ? taskId
            : null;
}
