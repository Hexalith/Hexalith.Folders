using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class WorkspaceFileMutationService(
    LayeredFolderAuthorizationService authorizationService,
    IFolderRepository repository,
    IWorkspacePathPolicyEvidenceProvider pathPolicyEvidenceProvider,
    TimeProvider? timeProvider = null,
    IWorkspaceFileContentStore? contentStore = null,
    IWorkspaceFileDeleteOperationStore? deleteOperationStore = null,
    IFolderFileReferenceBindingAuthority? fileReferenceBindingAuthority = null)
{
    public const string ActionToken = "mutate_files";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkspacePathPolicyEvidenceProvider _pathPolicyEvidenceProvider =
        pathPolicyEvidenceProvider ?? throw new ArgumentNullException(nameof(pathPolicyEvidenceProvider));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IWorkspaceFileContentStore _contentStore = contentStore ?? new UnavailableWorkspaceFileContentStore();
    private readonly IWorkspaceFileDeleteOperationStore _deleteOperationStore =
        deleteOperationStore ?? new UnavailableWorkspaceFileDeleteOperationStore();

    public async Task<FolderResult> MutateAsync(
        WorkspaceFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string?> clientTenantValues = WithPayloadTenant(
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
            request.ClientControlledPrincipalValues)
        {
            OwnerFileId = request.FileOperationKind == "add"
                ? Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()
                : request.OwnerFileId,
            ProjectFileReferenceId = request.ProjectFileReferenceId,
            ContentReceiptId = request.ContentReceiptId,
            ExpectedFileVersion = request.ExpectedFileVersion,
            ExpectedLockFence = request.ExpectedLockFence,
        };


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
        string target = WorkspacePathPolicyValidator.ComputeMutationTargetDigest(command.WorkspaceId, command.PathMetadata);
        if (command.FileOperationKind != "add" && command.OwnerFileId is null)
        {
            string? fileId = state.ContentPublications.GetValueOrDefault(command.IdempotencyKey)?.FileId
                ?? state.FileIdentities.Values.SingleOrDefault(file => file.Active && file.MutationTargetDigest == target)?.OwnerFileId;
            command = command with { OwnerFileId = fileId };
            validation = FolderCommandValidator.Validate(command);
        }
        long currentFileVersion = state.ContentPublications.Values.Where(binding => binding.FileId == command.OwnerFileId)
            .Select(binding => binding.Version).DefaultIfEmpty(0).Max();
        if (!state.IdempotencyFingerprints.ContainsKey(command.IdempotencyKey)
            && (command.ExpectedFileVersion is not null && command.FileOperationKind == "add"
                    && state.FileIdentities.Values.Any(file => file.Active && file.MutationTargetDigest == target)
                || command.ExpectedLockFence is { } fence && fence != state.WorkspaceLockFence
                || command.ExpectedFileVersion is { } version && version != currentFileVersion))
            return FolderResult.Rejected(command, FolderResultCode.AppendConflict);
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
                pathPolicy.PathPolicyClass!) { PathMetadata = command.PathMetadata },
            cancellationToken).ConfigureAwait(false);

        if (evidence.Decision != WorkspacePathPolicyEvidenceDecision.NoEscape)
        {
            return FolderResult.Rejected(
                command,
                evidence.Decision == WorkspacePathPolicyEvidenceDecision.Unavailable
                    ? FolderResultCode.PolicyEvidenceUnavailable
                    : FolderResultCode.PathPolicyDenied);
        }

        LayeredFolderAuthorizationResult finalAuthorization = await _authorizationService
            .ReauthorizeTaskMutationAsync(authorizationContext, cancellationToken).ConfigureAwait(false);
        if (!finalAuthorization.IsAllowed)
        {
            return FolderResult.Rejected(command, MapAuthorization(finalAuthorization.Decision.OutcomeCode));
        }

        // Projects owns this namespace. A native File mutation establishes the owner incarnation,
        // but a supplied Projects reference still requires independent current Projects authority.
        if (command.ProjectFileReferenceId is not null
            && (!state.FileIdentities.TryGetValue(command.OwnerFileId!, out FolderFileIdentity? existingFile)
                || existingFile.ProjectFileReferenceId != command.ProjectFileReferenceId))
        {
            if (fileReferenceBindingAuthority is null || !await fileReferenceBindingAuthority.AuthorizeAsync(
                new(command.ManagedTenantId, command.FolderId, command.OwnerFileId!, command.ProjectFileReferenceId,
                    existingFile?.NativeMutationId), cancellationToken).ConfigureAwait(false))
                return FolderResult.Rejected(command, FolderResultCode.PolicyEvidenceUnavailable);

            // Binding authority awaits another owner. Refresh Folder permission after that I/O
            // before staging content or appending a native mapping event.
            finalAuthorization = await _authorizationService.ReauthorizeTaskMutationAsync(
                authorizationContext, cancellationToken).ConfigureAwait(false);
            if (!finalAuthorization.IsAllowed)
                return FolderResult.Rejected(command, MapAuthorization(finalAuthorization.Decision.OutcomeCode));
        }

        WorkspaceContentBinding? binding = null;
        long expectedHead = FolderActorSequenceScope.TryGet(command.ManagedTenantId, command.FolderId, out long actorHead)
            ? actorHead : state.IdempotencyFingerprints.Count;
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
                    command.ObservedByteLength!.Value)
                {
                    PrincipalId = allowed.PrincipalId ?? request.PrincipalId,
                    FileId = command.OwnerFileId, FileVersion = currentFileVersion + 1,
                    ExpectedVersion = expectedHead, LockFence = state.WorkspaceLockFence,
                    ReceiptId = command.ContentReceiptId, PathMetadata = command.PathMetadata,
                    PolicyVersion = evidence.PolicyVersion, PolicyDigest = evidence.PolicyDigest,
                    RequestedLockFence = command.ExpectedLockFence, RequestedFileVersion = command.ExpectedFileVersion,
                },
                cancellationToken).ConfigureAwait(false);

            if (!contentResult.Accepted)
            {
                return FolderResult.Rejected(command, contentResult.FailureCode);
            }
            binding = contentResult.Binding;
        }
        else if (command.FileOperationKind is "remove")
        {
            WorkspaceFileDeleteOperationStoreResult deleteResult = await _deleteOperationStore.StageAsync(
                new WorkspaceFileDeleteOperationStoreRequest(
                    command.ManagedTenantId,
                    command.FolderId,
                    command.WorkspaceId,
                    command.TaskId,
                    command.OperationId,
                    command.FileOperationKind,
                    command.TransportOperation,
                    pathPolicy.PathMetadataDigest!,
                    pathPolicy.PathPolicyClass!)
                {
                    PrincipalId = allowed.PrincipalId ?? request.PrincipalId,
                    FileId = command.OwnerFileId, FileVersion = currentFileVersion + 1,
                    ExpectedVersion = expectedHead, LockFence = state.WorkspaceLockFence,
                    PathMetadata = command.PathMetadata, PolicyVersion = evidence.PolicyVersion, PolicyDigest = evidence.PolicyDigest,
                    RequestedLockFence = command.ExpectedLockFence, RequestedFileVersion = command.ExpectedFileVersion,
                },
                cancellationToken).ConfigureAwait(false);

            if (!deleteResult.Accepted)
            {
                return FolderResult.Rejected(command, deleteResult.FailureCode);
            }
            binding = deleteResult.Binding;
        }
        if (binding is not null)
        {
            // Receipt verification/preparation awaits the SDK. Reconfirm current authority and the
            // exact pinned path policy after that I/O, before admitting the metadata decision.
            finalAuthorization = await _authorizationService.ReauthorizeTaskMutationAsync(
                authorizationContext, cancellationToken).ConfigureAwait(false);
            if (!finalAuthorization.IsAllowed) return FolderResult.Rejected(command, MapAuthorization(finalAuthorization.Decision.OutcomeCode));
            var finalEvidence = await _pathPolicyEvidenceProvider.GetEvidenceAsync(new(
                command.ManagedTenantId, command.FolderId, command.WorkspaceId, command.TaskId, command.OperationId,
                pathPolicy.PathMetadataDigest!, pathPolicy.PathPolicyClass!) { PathMetadata = command.PathMetadata }, cancellationToken).ConfigureAwait(false);
            if (finalEvidence.Decision != WorkspacePathPolicyEvidenceDecision.NoEscape
                || finalEvidence.PolicyVersion != evidence.PolicyVersion || finalEvidence.PolicyDigest != evidence.PolicyDigest)
                return FolderResult.Rejected(command, FolderResultCode.PolicyEvidenceUnavailable);
            aggregateResult = FolderResult.Accepted(command,
                aggregateResult.Events.Select(item => item is WorkspaceFileMutationAccepted accepted
                    ? accepted with { ContentBinding = binding } : item).ToArray());
        }

        FolderState finalState = _repository.Load(streamName);
        if (finalState.WorkspaceLockFence != state.WorkspaceLockFence || finalState.IsTaskClosed(command.WorkspaceId, command.TaskId))
            return FolderResult.Rejected(command, FolderResultCode.LockNotOwned);
        FolderResult finalDecision = FolderAggregate.Handle(finalState, command, _timeProvider.GetUtcNow());
        if (finalDecision.Events.Count == 0) return finalDecision;

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
