using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class WorkspaceFileMutationService(
    LayeredFolderAuthorizationService authorizationService,
    IFolderRepository repository,
    IWorkspacePathPolicyEvidenceProvider pathPolicyEvidenceProvider,
    TimeProvider? timeProvider = null,
    IWorkspaceFileContentStore? contentStore = null)
{
    public const string ActionToken = "mutate_files";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkspacePathPolicyEvidenceProvider _pathPolicyEvidenceProvider =
        pathPolicyEvidenceProvider ?? throw new ArgumentNullException(nameof(pathPolicyEvidenceProvider));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IWorkspaceFileContentStore _contentStore = contentStore ?? new UnavailableWorkspaceFileContentStore();

    public async Task<FolderResult> MutateAsync(
        WorkspaceFileMutationRequest request,
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

        MutateWorkspaceFile command = new(
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            request.FolderId,
            request.RequestSchemaVersion,
            request.WorkspaceId,
            request.OperationId,
            request.FileOperationKind,
            request.TransportOperation,
            request.PathMetadata,
            request.ContentHashReference,
            request.ByteLength,
            request.MediaType,
            request.TransportEvidenceKind,
            request.ObservedByteLength,
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

        WorkspacePathPolicyResult pathPolicy = WorkspacePathPolicyValidator.Validate(command.PathMetadata);
        if (!pathPolicy.IsAccepted)
        {
            return FolderResult.Rejected(command, FolderResultCode.PathPolicyDenied);
        }

        FolderStreamName streamName = _repository.CreateStreamName(command.ManagedTenantId, command.FolderId);
        FolderState state = _repository.Load(streamName);
        FolderResult aggregateResult = FolderAggregate.Handle(state, command, _timeProvider.GetUtcNow());
        if (aggregateResult.Events.Count == 0)
        {
            return aggregateResult;
        }

        WorkspacePathPolicyEvidenceResult evidence = await _pathPolicyEvidenceProvider.GetEvidenceAsync(
            new WorkspacePathPolicyEvidenceRequest(
                command.ManagedTenantId,
                command.FolderId,
                command.WorkspaceId,
                command.TaskId,
                command.OperationId,
                pathPolicy.PathMetadataDigest!,
                pathPolicy.PathPolicyClass!),
            cancellationToken).ConfigureAwait(false);

        if (evidence.Decision != WorkspacePathPolicyEvidenceDecision.NoEscape)
        {
            return FolderResult.Rejected(
                command,
                evidence.Decision == WorkspacePathPolicyEvidenceDecision.Unavailable
                    ? FolderResultCode.PolicyEvidenceUnavailable
                    : FolderResultCode.PathPolicyDenied);
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

        if (command.FileOperationKind is "add" or "change")
        {
            WorkspaceFileContentStoreResult contentResult = await _contentStore.StageAsync(
                new WorkspaceFileContentStoreRequest(
                    command.ManagedTenantId,
                    command.FolderId,
                    command.WorkspaceId,
                    command.TaskId,
                    command.OperationId,
                    command.FileOperationKind,
                    command.TransportOperation,
                    command.ContentHashReference!,
                    command.ByteLength!.Value,
                    command.MediaType!,
                    command.TransportEvidenceKind!,
                    command.ObservedByteLength!.Value),
                cancellationToken).ConfigureAwait(false);

            if (!contentResult.Accepted)
            {
                return FolderResult.Rejected(command, FolderResultCode.FileOperationFailed);
            }
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

    private FolderResult ResolveAppendConflict(FolderStreamName streamName, MutateWorkspaceFile command)
    {
        FolderState refreshed = _repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command, _timeProvider.GetUtcNow());
        return refreshedResult.Events.Count == 0
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
}
