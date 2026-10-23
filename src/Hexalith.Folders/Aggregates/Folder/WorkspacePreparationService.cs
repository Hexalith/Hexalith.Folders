using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class WorkspacePreparationService(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspacePreparationReadinessValidator readinessValidator,
    IFolderRepository repository,
    TimeProvider? timeProvider = null)
{
    public const string ActionToken = "prepare_workspace";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IWorkspacePreparationReadinessValidator _readinessValidator =
        readinessValidator ?? throw new ArgumentNullException(nameof(readinessValidator));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<FolderResult> PrepareAsync(
        WorkspacePreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string?> clientTenantValues = ClientTenantValues.WithPayloadTenant(
            request.ClientControlledTenantValues,
            request.PayloadTenantId);

        LayeredFolderAuthorizationContext authorizationContext = new(
                request.AuthoritativeTenantId,
                request.PrincipalId,
                ActorSafeIdentifier: request.PrincipalId,
                ActionToken,
                LayeredFolderOperationPolicy.Mutation(),
                request.ClaimTransformEvidence,
                OperationScope: request.FolderId,
                request.CorrelationId,
                request.TaskId,
                clientTenantValues,
                request.ClientControlledPrincipalValues);
        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            authorizationContext,
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return FolderResult.Rejected(
                FolderAuthorizationResultMapper.Map(authorization.Decision.OutcomeCode),
                managedTenantId: null,
                organizationId: null,
                folderId: null,
                request.PrincipalId,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        if (string.IsNullOrWhiteSpace(allowed.OrganizationId))
        {
            return FolderResult.Rejected(
                FolderResultCode.MalformedEvidence,
                allowed.AuthoritativeTenantId,
                organizationId: null,
                request.FolderId,
                request.PrincipalId,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        PrepareWorkspace command = new(
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            request.FolderId,
            request.RequestSchemaVersion,
            request.WorkspaceId,
            request.RepositoryBindingId,
            request.BranchRefPolicyRef,
            request.WorkspacePolicyRef,
            allowed.ActorSafeIdentifier,
            request.CorrelationId,
            request.TaskId,
            request.IdempotencyKey,
            request.PayloadTenantId);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        FolderStreamName streamName = _repository.CreateStreamName(command.ManagedTenantId, command.FolderId);
        FolderState state = _repository.Load(streamName);
        FolderResult aggregateResult = FolderAggregate.Handle(state, command, _timeProvider.GetUtcNow());
        if (aggregateResult.Events.Count == 0)
        {
            return aggregateResult;
        }

        ProviderReadinessValidationResult readiness = await _readinessValidator.ValidateAsync(
            new ProviderReadinessValidationRequest(
                command.ManagedTenantId,
                request.PrincipalId,
                state.ProviderBindingRef ?? string.Empty,
                ProviderReadinessRequestedCapability.WorkspacePreparation,
                command.CorrelationId,
                request.ClaimTransformEvidence,
                clientTenantValues)
            {
                BoundFolderId = state.FolderId,
                BoundRepositoryBindingId = state.RepositoryBindingId,
                BoundExternalRepositoryRefFingerprint = state.ExternalRepositoryRefFingerprint,
                BoundBranchRefPolicyRef = state.BranchRefPolicyRef,
            },
            cancellationToken).ConfigureAwait(false);

        if (!IsReady(readiness))
        {
            FolderResultCode readinessCode = MapReadiness(readiness);
            if (readinessCode is FolderResultCode.UnknownProviderOutcome or FolderResultCode.ReconciliationRequired)
            {
                LayeredFolderAuthorizationResult unknownOutcomeAuthorization = await _authorizationService
                    .ReauthorizeTaskMutationAsync(authorizationContext, cancellationToken).ConfigureAwait(false);
                if (!unknownOutcomeAuthorization.IsAllowed)
                {
                    return FolderResult.Rejected(command, FolderAuthorizationResultMapper.Map(unknownOutcomeAuthorization.Decision.OutcomeCode));
                }

                return AppendUnknownWorkspaceOutcome(
                    streamName,
                    command,
                    validation.IdempotencyFingerprint!,
                    readinessCode);
            }

            return FolderResult.Rejected(command, readinessCode);
        }

        LayeredFolderAuthorizationResult finalAuthorization = await _authorizationService
            .ReauthorizeTaskMutationAsync(authorizationContext, cancellationToken).ConfigureAwait(false);
        if (!finalAuthorization.IsAllowed)
        {
            return FolderResult.Rejected(command, FolderAuthorizationResultMapper.Map(finalAuthorization.Decision.OutcomeCode));
        }

        // Requested remains an intent on replay. Only this successfully observed metadata
        // readiness gate records the existing outcome that makes the workspace lockable.
        // This outcome does not claim repository content has been cloned or materialized.
        IReadOnlyList<IFolderEvent> preparedEvents = [.. aggregateResult.Events,
            new FolderWorkspaceLifecycleEventRecorded(command.ManagedTenantId, command.OrganizationId,
                command.FolderId, command.WorkspaceId, FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                DirtyResolution: null, OperationId: command.WorkspaceId, command.CorrelationId,
                command.TaskId, command.IdempotencyKey, validation.IdempotencyFingerprint!,
                _timeProvider.GetUtcNow(), command.ActorPrincipalId)];
        FolderAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint!,
            preparedEvents);

        return outcome switch
        {
            FolderAppendOutcome.Appended => aggregateResult with { Events = preparedEvents },
            FolderAppendOutcome.FingerprintMatched => FolderResult.Rejected(command, FolderResultCode.IdempotentReplay),
            FolderAppendOutcome.FingerprintConflict => FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict => ResolveAppendConflict(streamName, command),
            _ => FolderResult.Rejected(command, FolderResultCode.MalformedEvidence),
        };
    }

    private FolderResult ResolveAppendConflict(FolderStreamName streamName, PrepareWorkspace command)
    {
        FolderState refreshed = _repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command, _timeProvider.GetUtcNow());
        return refreshedResult.Events.Count == 0
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    private FolderResult AppendUnknownWorkspaceOutcome(
        FolderStreamName streamName,
        PrepareWorkspace command,
        string idempotencyFingerprint,
        FolderResultCode resultCode)
    {
        FolderWorkspaceLifecycleEventRecorded unknownOutcome = new(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.WorkspaceId,
            FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown,
            DirtyResolution: null,
            OperationId: command.WorkspaceId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            idempotencyFingerprint,
            _timeProvider.GetUtcNow(),
            command.ActorPrincipalId);

        FolderAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            command.IdempotencyKey,
            idempotencyFingerprint,
            [unknownOutcome]);

        return outcome switch
        {
            FolderAppendOutcome.Appended => FolderResult.Rejected(command, resultCode) with { Events = [unknownOutcome] },
            FolderAppendOutcome.FingerprintMatched => FolderResult.Rejected(command, FolderResultCode.IdempotentReplay),
            FolderAppendOutcome.FingerprintConflict => FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict => ResolveAppendConflict(streamName, command),
            _ => FolderResult.Rejected(command, FolderResultCode.MalformedEvidence),
        };
    }

    private static bool IsReady(ProviderReadinessValidationResult readiness)
        => readiness.Code == ProviderReadinessResultCode.Allowed
        && string.Equals(readiness.Status, "ready", StringComparison.Ordinal)
        && readiness.FailureCategory == ProviderFailureCategory.None;

    private static FolderResultCode MapReadiness(ProviderReadinessValidationResult readiness)
    {
        if (readiness.Code is ProviderReadinessResultCode.ProjectionStale)
        {
            return FolderResultCode.StaleProjection;
        }

        if (readiness.Code is ProviderReadinessResultCode.ProjectionUnavailable or ProviderReadinessResultCode.ReadModelUnavailable)
        {
            return FolderResultCode.UnavailableProjection;
        }

        if (readiness.Code is ProviderReadinessResultCode.AuthenticationRequired or ProviderReadinessResultCode.AuthorizationDenied)
        {
            return FolderResultCode.TenantAccessDenied;
        }

        return readiness.FailureCategory switch
        {
            ProviderFailureCategory.UnsupportedProviderCapability => FolderResultCode.UnsupportedProviderCapability,
            ProviderFailureCategory.UnknownProviderOutcome => FolderResultCode.UnknownProviderOutcome,
            ProviderFailureCategory.ReconciliationRequired => FolderResultCode.ReconciliationRequired,
            ProviderFailureCategory.ProviderRateLimited => FolderResultCode.ProviderRateLimited,
            ProviderFailureCategory.ProviderUnavailable
                or ProviderFailureCategory.ProviderTransientFailure => FolderResultCode.ProviderUnavailable,
            ProviderFailureCategory.ProviderAuthenticationRequired
                or ProviderFailureCategory.ProviderPermissionInsufficient => FolderResultCode.ProviderPermissionInsufficient,
            ProviderFailureCategory.ProviderConflict => FolderResultCode.RepositoryConflict,
            _ => FolderResultCode.ProviderReadinessFailed,
        };
    }
}
