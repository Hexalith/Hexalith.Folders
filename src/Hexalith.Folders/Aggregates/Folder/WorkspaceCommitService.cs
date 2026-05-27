using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class WorkspaceCommitService(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceCommitReadinessValidator readinessValidator,
    IFolderRepository repository,
    IWorkspaceCommitExecutor commitExecutor,
    TimeProvider? timeProvider = null)
{
    public const string ActionToken = "commit";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IWorkspaceCommitReadinessValidator _readinessValidator =
        readinessValidator ?? throw new ArgumentNullException(nameof(readinessValidator));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkspaceCommitExecutor _commitExecutor = commitExecutor ?? throw new ArgumentNullException(nameof(commitExecutor));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<FolderResult> CommitAsync(
        WorkspaceCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string?> clientTenantValues = WithPayloadTenant(
            request.ClientControlledTenantValues,
            request.PayloadTenantId);

        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
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
                request.ClientControlledPrincipalValues),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return FolderResult.Rejected(
                MapAuthorization(authorization.Decision.OutcomeCode),
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

        CommitWorkspace command = new(
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            request.FolderId,
            request.RequestSchemaVersion,
            request.WorkspaceId,
            request.OperationId,
            request.AuthorMetadataReference,
            request.BranchRefTarget,
            request.CommitMessageClassification,
            request.ChangedPathMetadataDigest,
            allowed.ActorSafeIdentifier,
            request.CorrelationId,
            request.TaskId,
            request.IdempotencyKey,
            request.PayloadTenantId,
            clientTenantValues,
            request.ClientControlledPrincipalValues);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        FolderStreamName streamName = _repository.CreateStreamName(command.ManagedTenantId, command.FolderId);
        FolderState state = _repository.Load(streamName);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        FolderResult preflight = FolderAggregate.ValidateCommitPreconditions(state, command, now);
        if (preflight.Code != FolderResultCode.Accepted)
        {
            return preflight;
        }

        FolderIdempotencyLookupResult lookup = _repository.TryGetIdempotencyFingerprint(
            streamName,
            command.IdempotencyKey,
            out string? priorFingerprint);

        if (lookup == FolderIdempotencyLookupResult.Found)
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(command, FolderResultCode.IdempotencyUnavailable);
        }

        ProviderReadinessValidationResult readiness = await _readinessValidator.ValidateAsync(
            new ProviderReadinessValidationRequest(
                command.ManagedTenantId,
                request.PrincipalId,
                state.ProviderBindingRef ?? string.Empty,
                ProviderReadinessRequestedCapability.CommitStatus,
                command.CorrelationId,
                request.ClaimTransformEvidence,
                clientTenantValues),
            cancellationToken).ConfigureAwait(false);

        if (!IsReady(readiness))
        {
            return FolderResult.Rejected(command, MapReadiness(readiness));
        }

        WorkspaceCommitExecutionResult execution = await _commitExecutor.CommitAsync(
            new WorkspaceCommitExecutionRequest(
                command.ManagedTenantId,
                command.FolderId,
                command.WorkspaceId,
                command.OperationId,
                command.CorrelationId,
                command.TaskId,
                command.AuthorMetadataReference,
                command.BranchRefTarget,
                command.CommitMessageClassification,
                command.ChangedPathMetadataDigest),
            cancellationToken).ConfigureAwait(false);

        FolderResult aggregateResult = FolderAggregate.Handle(state, command, execution, now);
        if (aggregateResult.Events.Count == 0)
        {
            return aggregateResult;
        }

        FolderAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint!,
            aggregateResult.Events);

        return outcome switch
        {
            FolderAppendOutcome.Appended => aggregateResult,
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

    private FolderResult ResolveAppendConflict(FolderStreamName streamName, CommitWorkspace command)
    {
        FolderState refreshed = _repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.ValidateCommitPreconditions(refreshed, command, _timeProvider.GetUtcNow());
        return refreshedResult.Code == FolderResultCode.IdempotentReplay
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    private static IReadOnlyDictionary<string, string?> WithPayloadTenant(
        IReadOnlyDictionary<string, string?> values,
        string? payloadTenantId)
    {
        Dictionary<string, string?> merged = new(values, StringComparer.Ordinal);
        if (payloadTenantId is not null)
        {
            merged["payload_tenant_id"] = payloadTenantId;
        }

        return merged;
    }

    private static FolderResultCode MapAuthorization(string outcomeCode)
        => outcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => FolderResultCode.MissingAuthoritativeTenant,
            LayeredAuthorizationOutcomeCodes.ClaimTransformDenied => FolderResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale => FolderResultCode.StaleProjection,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable => FolderResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.FolderAclDenied or LayeredAuthorizationOutcomeCodes.SafeNotFound => FolderResultCode.FolderAclDenied,
            LayeredAuthorizationOutcomeCodes.FolderAclStale => FolderResultCode.StaleProjection,
            LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => FolderResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied => FolderResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied => FolderResultCode.PolicyEvidenceUnavailable,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => FolderResultCode.MalformedEvidence,
            _ => FolderResultCode.TenantAccessDenied,
        };

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
