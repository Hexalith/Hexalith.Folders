using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

public sealed partial class ForgejoProvider
{
    private const int MaximumOperationChangeCount = 100;
    private const int MaximumOperationFileBytes = 1024 * 1024;
    private const long MaximumOperationAggregateContentBytes = 10L * 1024 * 1024;
    private const int MaximumOperationPathCharacters = 500;
    private const int MaximumOperationBranchCharacters = 100;
    private static readonly TimeSpan OperationOutcomeRecordingTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OperationReconciliationWindow = TimeSpan.FromMinutes(15);
    private static readonly HashSet<string> AllowedOperationReasonCodes = new(StringComparer.Ordinal)
    {
        "authorization_evidence_malformed",
        "authorization_evidence_stale",
        "canonical_lock_evidence_invalid",
        "existing_equivalent",
        "forgejo_administration_permission_insufficient",
        "forgejo_authentication_required",
        "forgejo_branch_protection_conflict",
        "forgejo_capability_unsupported",
        "forgejo_change_set_malformed",
        "forgejo_base_url_invalid",
        "forgejo_base_url_token_query_rejected",
        "forgejo_base_url_userinfo_rejected",
        "forgejo_client_creation_unavailable",
        "forgejo_commit_intent_malformed",
        "forgejo_commit_outcome_unknown",
        "forgejo_commit_ref_policy_denied",
        "forgejo_commit_source_malformed",
        "forgejo_commit_source_unavailable",
        "forgejo_credential_resolution_unavailable",
        "forgejo_credential_resolver_unconfigured",
        "forgejo_cross_origin_redirect_rejected",
        "forgejo_file_mutation_outcome_unknown",
        "forgejo_file_mutation_ref_policy_denied",
        "forgejo_file_mutation_source_malformed",
        "forgejo_file_mutation_source_unavailable",
        "forgejo_file_policy_evidence_stale_or_malformed",
        "forgejo_mutation_evidence_ambiguous",
        "missing_forgejo_credential_mode",
        "ambiguous_forgejo_credential_mode",
        "forgejo_operation_cancelled_before_dispatch",
        "forgejo_operation_outcome_store_unavailable",
        "forgejo_operation_pending",
        "forgejo_operation_reservation_invalidated",
        "forgejo_operation_status_source_malformed",
        "forgejo_operation_status_source_unavailable",
        "forgejo_outcome_recording_failed",
        "forgejo_permission_insufficient",
        "forgejo_rate_limited",
        "forgejo_response_limit_exceeded",
        "forgejo_object_format_unsupported",
        "forgejo_smart_http_unsupported",
        "forgejo_native_runtime_unavailable",
        "forgejo_transfer_limit_exceeded",
        "forgejo_temporary_disk_limit_exceeded",
        "forgejo_temporary_repository_cleanup_failed",
        "forgejo_operation_timed_out",
        "forgejo_remote_policy_rejected",
        "forgejo_remote_rejected",
        "forgejo_operation_evidence_malformed",
        "forgejo_operation_scope_mismatch",
        "forgejo_reconciliation_budget_exhausted",
        "forgejo_reconciliation_checks_exhausted",
        "forgejo_ref_head_conflict",
        "forgejo_repository_archived",
        "forgejo_resource_hidden_or_missing",
        "forgejo_server_unavailable",
        "forgejo_status_evidence_conflicting",
        "forgejo_status_evidence_malformed",
        "forgejo_status_evidence_unavailable",
        "forgejo_transport_outcome_unknown",
        "forgejo_validation_failed",
        "forgejo_version_incompatible",
        "idempotency_conflict",
        "idempotency_key_expired",
        "idempotency_key_not_allowed",
        "provider_commit_source_unconfigured",
        "provider_credential_reference_denied",
        "provider_credential_reference_missing",
        "provider_credential_secret_malformed",
        "provider_credential_store_unavailable",
        "provider_file_mutation_source_unconfigured",
        "provider_operation_outcome_store_unconfigured",
        "provider_operation_status_source_unconfigured",
        "provider_validation_failed",
        "provider_identity_malformed",
        "ref_policy_evidence_stale_or_malformed",
        "reconciliation_required",
        "success",
        "target_evidence_malformed",
        "target_evidence_stale",
        "unsafe_forgejo_target_metadata",
        "unsupported_forgejo_credential_mode",
        "unsupported_provider_family",
    };

    public async Task<ProviderFileMutationResult> StageFileChangesAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch");
        }

        if (!TrySnapshotDeclaredRequest(request, out ProviderFileMutationRequest requestSnapshot))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_change_set_malformed");
        }

        request = requestSnapshot;
        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateOperationBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return FileMutationFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!TryPrepareOperationTarget(request, out ProviderCredentialMode credentialMode, out Uri? baseUri, out string? version, out ProviderTargetEvidence? safeTargetEvidence, out failure))
        {
            return FileMutationFailure(request, failure.Category, failure.ReasonCode);
        }

        string safeTargetFingerprint = safeTargetEvidence!.Metadata["safe_target_fingerprint"];
        ProviderFileMutationResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? sourceResolution =
            await ResolveFileMutationSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "forgejo_file_mutation_source_unavailable");
        }

        string? sourceFailure = null;
        if (!TrySnapshotResolvedSource(sourceResolution.Source, out ProviderFileMutationResolvedSource source)
            || !TryValidateResolvedSource(request, source, out sourceFailure))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "forgejo_file_mutation_source_malformed");
        }

        ProviderOperationReservationResult? reservation = await ReserveOperationAsync(
            ProviderOperationCatalog.FileMutationSupport,
            request.IdempotencyAdmission,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            cancellationToken).ConfigureAwait(false);
        ProviderFileMutationResult? reservationResult = MapReservation(request, safeTargetFingerprint, reservation);
        if (reservationResult is not null)
        {
            return reservationResult;
        }

        string operationReference = reservation!.OperationReference!;
        long generation = reservation.Generation;
        (ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter) access =
            await ResolveOperationAccessAsync(request, credentialMode, baseUri!, cancellationToken).ConfigureAwait(false);
        if (access.Credential is null || access.Client is null)
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
            bool finalized = await FinalizeNoDispatchAsync(operationReference, generation, access.FailureCategory, access.ReasonCode, access.RetryAfter).ConfigureAwait(false);
            return finalized
                ? FileMutationFailure(request, access.FailureCategory, access.ReasonCode, access.RetryAfter, operationReference)
                : FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "forgejo_outcome_recording_failed", operationReference: operationReference);
        }

        ForgejoFileMutationResult? result;
        try
        {
            result = await access.Client.StageFileChangesAsync(
                new ForgejoFileMutationRequest(
                    source.Target,
                    source.Changes,
                    version!,
                    token => ValidateReservationAsync(operationReference, generation, request.IdempotencyAdmission.IntentFingerprint, token)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
        }

        if (result is null || !IsFileMutationResultWellFormed(result))
        {
            bool finalized = await FinalizeNoDispatchAsync(
                operationReference,
                generation,
                ProviderFailureCategory.ProviderUnavailable,
                "forgejo_file_mutation_outcome_unknown").ConfigureAwait(false);
            return finalized
                ? FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "forgejo_file_mutation_outcome_unknown", operationReference: operationReference)
                : FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "forgejo_outcome_recording_failed", operationReference: operationReference);
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = ForgejoFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            bool finalized = await FinalizeNoDispatchAsync(operationReference, generation, mapped.Category, mapped.ReasonCode, result.RetryAfter).ConfigureAwait(false);
            return finalized
                ? FileMutationFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference)
                : FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "forgejo_outcome_recording_failed", operationReference: operationReference);
        }

        string safeOutcomeFingerprint = ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:staged-outcome",
            request.AuthorizationEvidence.Fingerprint,
            operationReference,
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            request.SafeChangeSetFingerprint,
            result.TreeSha);
        bool recorded = await RecordOperationAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.StagedChangeSet,
            PrivateObjectId: result.TreeSha,
            safeOutcomeFingerprint,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);
        if (!recorded)
        {
            await RecordUnknownAsync(operationReference, generation, "forgejo_outcome_recording_failed").ConfigureAwait(false);
            return FileMutationUnknown(request, safeTargetFingerprint, operationReference, "forgejo_outcome_recording_failed");
        }

        return new ProviderFileMutationResult(
            IsSuccess: true,
            EquivalentReplay: false,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            safeTargetFingerprint,
            safeOutcomeFingerprint,
            operationReference,
            ReconciliationReference: null);
    }

    public async Task<ProviderCommitResult> CommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateOperationBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return CommitFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!TryPrepareOperationTarget(request, out ProviderCredentialMode credentialMode, out Uri? baseUri, out string? version, out ProviderTargetEvidence? safeTargetEvidence, out failure))
        {
            return CommitFailure(request, failure.Category, failure.ReasonCode);
        }

        string safeTargetFingerprint = safeTargetEvidence!.Metadata["safe_target_fingerprint"];
        ProviderCommitResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? sourceResolution =
            await ResolveCommitSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "forgejo_commit_source_unavailable");
        }

        string? sourceFailure = null;
        if (!TrySnapshotResolvedSource(sourceResolution.Source, out ProviderCommitResolvedSource source)
            || !TryValidateResolvedSource(request, source, out sourceFailure))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "forgejo_commit_source_malformed");
        }

        ProviderOperationReservationResult? reservation = await ReserveOperationAsync(
            ProviderOperationCatalog.CommitSupport,
            request.IdempotencyAdmission,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            cancellationToken).ConfigureAwait(false);
        ProviderCommitResult? reservationResult = MapReservation(request, safeTargetFingerprint, reservation);
        if (reservationResult is not null)
        {
            return reservationResult;
        }

        string operationReference = reservation!.OperationReference!;
        long generation = reservation.Generation;
        (ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter) access =
            await ResolveOperationAccessAsync(request, credentialMode, baseUri!, cancellationToken).ConfigureAwait(false);
        if (access.Credential is null || access.Client is null)
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
            bool finalized = await FinalizeNoDispatchAsync(operationReference, generation, access.FailureCategory, access.ReasonCode, access.RetryAfter).ConfigureAwait(false);
            return finalized
                ? CommitFailure(request, access.FailureCategory, access.ReasonCode, access.RetryAfter, operationReference)
                : CommitFailure(request, ProviderFailureCategory.ProviderUnavailable, "forgejo_outcome_recording_failed", operationReference: operationReference);
        }

        ForgejoCommitResult? result;
        try
        {
            result = await access.Client.CommitAsync(
                new ForgejoCommitRequest(
                    source.Target,
                    source.StagedChanges!,
                    source.TreeSha,
                    NormalizeCommitMessage(source.CommitMessage),
                    version!,
                    token => ValidateReservationAsync(operationReference, generation, request.IdempotencyAdmission.IntentFingerprint, token),
                    commitSha => RecordCreatedCommitAsync(
                        request,
                        source,
                        safeTargetFingerprint,
                        operationReference,
                        generation,
                        commitSha)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
        }

        if (result is null || !IsCommitResultWellFormed(result, source))
        {
            await RecordUnknownAsync(operationReference, generation, "forgejo_commit_outcome_unknown").ConfigureAwait(false);
            return CommitUnknown(request, safeTargetFingerprint, operationReference, "forgejo_commit_outcome_unknown");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = ForgejoFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            if (result.FailureCondition is ForgejoApiFailureCondition.CancellationBeforeDispatch
                or ForgejoApiFailureCondition.ReservationInvalidated)
            {
                bool finalized = await FinalizeNoDispatchAsync(
                    operationReference,
                    generation,
                    mapped.Category,
                    mapped.ReasonCode,
                    result.RetryAfter).ConfigureAwait(false);
                return finalized
                    ? CommitFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference)
                    : CommitUnknown(request, safeTargetFingerprint, operationReference, "forgejo_outcome_recording_failed");
            }

            if (mapped.Category == ProviderFailureCategory.UnknownProviderOutcome)
            {
                await RecordUnknownAsync(operationReference, generation, mapped.ReasonCode, result.ObservedCommitSha).ConfigureAwait(false);
                return CommitUnknown(request, safeTargetFingerprint, operationReference, mapped.ReasonCode);
            }

            string safeFailureFingerprint = CreateFailureFingerprint(
                "hxf-forgejo:v1:commit-failure",
                request.AuthorizationEvidence.Fingerprint,
                operationReference,
                safeTargetFingerprint,
                request.IdempotencyAdmission.IntentFingerprint,
                mapped.Category,
                mapped.ReasonCode);
            bool knownFailureRecorded = await RecordKnownFailureAsync(
                operationReference,
                generation,
                mapped.Category,
                mapped.ReasonCode,
                safeFailureFingerprint,
                result.RetryAfter,
                result.ObservedCommitSha).ConfigureAwait(false);
            if (!knownFailureRecorded)
            {
                await RecordUnknownAsync(operationReference, generation, "forgejo_outcome_recording_failed", result.ObservedCommitSha).ConfigureAwait(false);
                return CommitUnknown(request, safeTargetFingerprint, operationReference, "forgejo_outcome_recording_failed");
            }

            return CommitFailure(
                request,
                mapped.Category,
                mapped.ReasonCode,
                result.RetryAfter,
                operationReference,
                safeTargetFingerprint: safeTargetFingerprint,
                safeOutcomeFingerprint: safeFailureFingerprint);
        }

        string safeCommitFingerprint = ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:commit-outcome",
            request.AuthorizationEvidence.Fingerprint,
            operationReference,
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            result.CommitSha);
        bool recorded = await RecordOperationAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.RefUpdateConfirmed,
            result.CommitSha,
            safeCommitFingerprint,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);
        if (!recorded)
        {
            await RecordUnknownAsync(operationReference, generation, "forgejo_outcome_recording_failed", result.CommitSha).ConfigureAwait(false);
            return CommitUnknown(request, safeTargetFingerprint, operationReference, "forgejo_outcome_recording_failed");
        }

        return new ProviderCommitResult(
            IsSuccess: true,
            EquivalentReplay: false,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            safeTargetFingerprint,
            safeCommitFingerprint,
            operationReference,
            ReconciliationReference: null);
    }

    public async Task<ProviderOperationStatusResult> GetOperationStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return StatusFailure(request, ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateOperationBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return StatusFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!TryPrepareOperationTarget(request, out ProviderCredentialMode credentialMode, out Uri? baseUri, out string? version, out ProviderTargetEvidence? safeTargetEvidence, out failure))
        {
            return StatusFailure(request, failure.Category, failure.ReasonCode);
        }

        ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? sourceResolution =
            await ResolveStatusSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "forgejo_operation_status_source_unavailable");
        }

        string? sourceFailure = null;
        if (!TrySnapshotResolvedSource(sourceResolution.Source, out ProviderOperationStatusResolvedSource source)
            || !TryValidateResolvedSource(request, source, out sourceFailure))
        {
            return StatusFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "forgejo_operation_status_source_malformed");
        }

        (ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter) access =
            await ResolveOperationAccessAsync(request, credentialMode, baseUri!, cancellationToken).ConfigureAwait(false);
        if (access.Credential is null || access.Client is null)
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
            return StatusFailure(request, access.FailureCategory, access.ReasonCode, access.RetryAfter);
        }

        ForgejoOperationStatusResult? result;
        try
        {
            result = await access.Client.GetOperationStatusAsync(
                new ForgejoOperationStatusRequest(
                    source.Target,
                    source.IntendedCommitSha,
                    source.StagedChanges!,
                    NormalizeCommitMessage(source.CommitMessage!),
                    version!),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await DisposeOperationResourcesSafelyAsync(access.Client, access.Credential).ConfigureAwait(false);
        }

        bool exhausted = request.CheckNumber == 5
            || _timeProvider.GetUtcNow() - request.ReconciliationStartedAt >= OperationReconciliationWindow;
        if (result is null || !IsStatusResultWellFormed(result, source) || !result.IsSuccess)
        {
            if (result is null || !IsStatusResultWellFormed(result, source))
            {
                return exhausted
                    ? StatusFailure(request, ProviderFailureCategory.ReconciliationRequired, "forgejo_reconciliation_checks_exhausted")
                    : StatusUnavailable(request, result?.RetryAfter);
            }

            (ProviderFailureCategory Category, string ReasonCode) mapped =
                ForgejoFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            if (exhausted && mapped.Category is ProviderFailureCategory.ProviderUnavailable
                or ProviderFailureCategory.ProviderRateLimited
                or ProviderFailureCategory.ProviderTransientFailure)
            {
                return StatusFailure(request, ProviderFailureCategory.ReconciliationRequired, "forgejo_reconciliation_checks_exhausted");
            }

            return StatusFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter);
        }

        string safeObservedFingerprint = ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:status-observation",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            safeTargetEvidence!.Metadata["safe_target_fingerprint"],
            result.ObservedFullRef,
            result.ObservedObjectType,
            result.ObservedSha,
            result.Status.ToString());
        if (result.Status == ProviderOperationStatusKind.Conflicting)
        {
            return new ProviderOperationStatusResult(
                IsSuccess: false,
                ProviderOperationStatusKind.Conflicting,
                ProviderFailureCategory.ReconciliationRequired,
                ProviderFailureCategory.ReconciliationRequired.ToCategoryCode(),
                "forgejo_status_evidence_conflicting",
                "reconciliation_required_metadata_only",
                Retryable: false,
                RetryAfter: null,
                request.CorrelationId,
                request.CheckNumber,
                safeObservedFingerprint,
                request.OperationReference);
        }

        if (result.Status == ProviderOperationStatusKind.NotApplied && exhausted)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ReconciliationRequired,
                "forgejo_reconciliation_checks_exhausted",
                safeObservedFingerprint: safeObservedFingerprint);
        }

        return new ProviderOperationStatusResult(
            IsSuccess: true,
            result.Status,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            result.Status == ProviderOperationStatusKind.Confirmed ? "confirmed" : "not_applied",
            "none",
            Retryable: result.Status == ProviderOperationStatusKind.NotApplied,
            RetryAfter: null,
            request.CorrelationId,
            request.CheckNumber,
            safeObservedFingerprint,
            request.OperationReference);
    }

    private static bool TryPrepareOperationTarget(
        ProviderFileMutationRequest request,
        out ProviderCredentialMode credentialMode,
        out Uri? baseUri,
        out string? version,
        out ProviderTargetEvidence? safeTargetEvidence,
        out (ProviderFailureCategory Category, string ReasonCode) failure)
    {
        credentialMode = default;
        baseUri = null;
        version = null;
        safeTargetEvidence = null;
        failure = default;
        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out credentialMode, out string? credentialFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
            return false;
        }

        if (!TryGetOperationAuthority(request.TargetEvidence, out baseUri, out version, out failure))
        {
            return false;
        }

        if (!ForgejoSafeTargetFingerprint.TryCreate(request, credentialMode, baseUri!, version!, out ProviderTargetEvidence evidence, out string? targetFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, SafeOperationReason(targetFailure, "provider_validation_failed"));
            return false;
        }

        safeTargetEvidence = evidence;
        return true;
    }

    private static bool TryPrepareOperationTarget(
        ProviderCommitRequest request,
        out ProviderCredentialMode credentialMode,
        out Uri? baseUri,
        out string? version,
        out ProviderTargetEvidence? safeTargetEvidence,
        out (ProviderFailureCategory Category, string ReasonCode) failure)
    {
        credentialMode = default;
        baseUri = null;
        version = null;
        safeTargetEvidence = null;
        failure = default;
        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out credentialMode, out string? credentialFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
            return false;
        }

        if (!TryGetOperationAuthority(request.TargetEvidence, out baseUri, out version, out failure))
        {
            return false;
        }

        if (!ForgejoSafeTargetFingerprint.TryCreate(request, credentialMode, baseUri!, version!, out ProviderTargetEvidence evidence, out string? targetFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, SafeOperationReason(targetFailure, "provider_validation_failed"));
            return false;
        }

        safeTargetEvidence = evidence;
        return true;
    }

    private static bool TryPrepareOperationTarget(
        ProviderOperationStatusRequest request,
        out ProviderCredentialMode credentialMode,
        out Uri? baseUri,
        out string? version,
        out ProviderTargetEvidence? safeTargetEvidence,
        out (ProviderFailureCategory Category, string ReasonCode) failure)
    {
        credentialMode = default;
        baseUri = null;
        version = null;
        safeTargetEvidence = null;
        failure = default;
        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out credentialMode, out string? credentialFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
            return false;
        }

        if (!TryGetOperationAuthority(request.TargetEvidence, out baseUri, out version, out failure))
        {
            return false;
        }

        if (!ForgejoSafeTargetFingerprint.TryCreate(request, credentialMode, baseUri!, version!, out ProviderTargetEvidence evidence, out string? targetFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, SafeOperationReason(targetFailure, "provider_validation_failed"));
            return false;
        }

        safeTargetEvidence = evidence;
        return true;
    }

    private static bool TryGetOperationAuthority(
        ProviderTargetEvidence targetEvidence,
        out Uri? baseUri,
        out string? version,
        out (ProviderFailureCategory Category, string ReasonCode) failure)
    {
        baseUri = null;
        version = null;
        failure = default;
        if (!ForgejoAuthorizedBaseUrl.TryCanonicalize(
            targetEvidence.Metadata.TryGetValue("authorized_base_url", out string? baseUrl) ? baseUrl : null,
            out Uri canonicalBaseUri,
            out string? baseUrlFailure))
        {
            failure = (ProviderFailureCategory.ProviderValidationFailed, SafeOperationReason(baseUrlFailure, "provider_validation_failed"));
            return false;
        }

        if (!ForgejoSupportedVersionCatalog.TryFind(targetEvidence.ProductVersion, out ForgejoSupportedVersionEntry supportedVersion))
        {
            failure = (ProviderFailureCategory.ReconciliationRequired, "forgejo_version_incompatible");
            return false;
        }

        baseUri = canonicalBaseUri;
        version = supportedVersion.Version;
        return true;
    }

    private (ProviderFailureCategory Category, string ReasonCode)? ValidateOperationBoundary(ProviderFileMutationRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateCommonOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.OrganizationId,
            request.FolderId,
            request.DelegatedTaskId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.TargetEvidence,
            request.AuthorizationEvidence,
            request.LockEvidence,
            request.RefPolicyEvidence,
            ProviderOperationCatalog.FileMutationSupport);
        if (common is not null)
        {
            return common;
        }

        if (!request.RefPolicyEvidence.AllowsFileMutation)
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_file_mutation_ref_policy_denied");
        }

        if (!IsCurrentEvidence(request.FilePolicyEvidence?.CapturedAt, request.FilePolicyEvidence?.FreshnessClass)
            || !IsSafeFingerprint(request.FilePolicyEvidence?.Fingerprint)
            || request.FilePolicyEvidence!.MaximumFileBytes is <= 0 or > MaximumOperationFileBytes
            || request.FilePolicyEvidence.MaximumChangeCount is <= 0 or > MaximumOperationChangeCount)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "forgejo_file_policy_evidence_stale_or_malformed");
        }

        if (!IsSafeOperationReference(request.IdempotencyKey)
            || !IsOperationAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeOperationReference(request.ChangeSetReference)
            || !IsSafeFingerprint(request.SafeChangeSetFingerprint)
            || request.Changes is null
            || request.Changes.Count is < 1 or > MaximumOperationChangeCount
            || request.Changes.Count > request.FilePolicyEvidence.MaximumChangeCount)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "forgejo_change_set_malformed");
        }

        HashSet<string> pathReferences = new(StringComparer.Ordinal);
        for (int index = 0; index < request.Changes.Count; index++)
        {
            ProviderOrderedFileChange? change = request.Changes[index];
            if (change is null
                || change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || !IsSafeOperationReference(change.PathReference)
                || !pathReferences.Add(change.PathReference)
                || !IsSafeFingerprint(change.SafePathFingerprint)
                || change.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    && (!IsSafeOperationReference(change.ContentReference) || !IsSafeFingerprint(change.SafeContentFingerprint))
                || change.Kind == ProviderFileChangeKind.Remove
                    && (change.ContentReference is not null || change.SafeContentFingerprint is not null)
                || !IsAllowedOperationChange(change.Kind, request.FilePolicyEvidence))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "forgejo_change_set_malformed");
            }
        }

        return null;
    }

    private (ProviderFailureCategory Category, string ReasonCode)? ValidateOperationBoundary(ProviderCommitRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateCommonOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.OrganizationId,
            request.FolderId,
            request.DelegatedTaskId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.TargetEvidence,
            request.AuthorizationEvidence,
            request.LockEvidence,
            request.RefPolicyEvidence,
            ProviderOperationCatalog.CommitSupport);
        if (common is not null)
        {
            return common;
        }

        if (!request.RefPolicyEvidence.AllowsCommit || !request.RefPolicyEvidence.AllowsNonForceUpdate)
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "forgejo_commit_ref_policy_denied");
        }

        return !IsSafeOperationReference(request.IdempotencyKey)
            || !IsOperationAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeOperationReference(request.StagedChangeSetReference)
            || !IsSafeFingerprint(request.SafeStagedChangeSetFingerprint)
            || !IsSafeOperationReference(request.CommitMessageReference)
            || !IsSafeFingerprint(request.SafeCommitMessageFingerprint)
            || !IsSafeFingerprint(request.SafeExpectedHeadFingerprint)
                ? (ProviderFailureCategory.ProviderValidationFailed, "forgejo_commit_intent_malformed")
                : null;
    }

    private (ProviderFailureCategory Category, string ReasonCode)? ValidateOperationBoundary(ProviderOperationStatusRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateCommonOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.OrganizationId,
            request.FolderId,
            request.DelegatedTaskId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.TargetEvidence,
            request.AuthorizationEvidence,
            request.LockEvidence,
            request.RefPolicyEvidence,
            ProviderOperationCatalog.StatusQuery);
        if (common is not null)
        {
            return common;
        }

        if (request.IdempotencyKey is not null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "idempotency_key_not_allowed");
        }

        if (!IsSafeOperationReference(request.OperationReference)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeFingerprint(request.SafeFullRefFingerprint)
            || !IsSafeFingerprint(request.SafeExpectedHeadFingerprint)
            || !IsSafeFingerprint(request.SafeIntendedCommitFingerprint)
            || !IsSafeFingerprint(request.SafeCheckWindowFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "forgejo_status_evidence_malformed");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (request.CheckNumber is < 1 or > 5
            || request.ReconciliationStartedAt > now
            || request.RequestedAt < request.ReconciliationStartedAt
            || request.RequestedAt > now.AddMinutes(1)
            || now - request.ReconciliationStartedAt >= OperationReconciliationWindow)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "forgejo_reconciliation_budget_exhausted");
        }

        return ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
            request.SafeCheckWindowFingerprint,
            ForgejoOperationSourceBindings.CheckWindow(request))
                ? null
                : (ProviderFailureCategory.ProviderValidationFailed, "forgejo_status_evidence_malformed");
    }

    private (ProviderFailureCategory Category, string ReasonCode)? ValidateCommonOperationBoundary(
        string providerFamily,
        string providerKey,
        string managedTenantId,
        string organizationId,
        string folderId,
        string delegatedTaskId,
        string providerBindingRef,
        string credentialReferenceId,
        string repositoryBindingId,
        string correlationId,
        ProviderTargetEvidence targetEvidence,
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        ProviderOperationLockEvidence lockEvidence,
        ProviderRefPolicyEvidence refPolicyEvidence,
        string operationScope)
    {
        try
        {
            if (!string.Equals(ProviderIdentityIdentifier.Normalize(providerFamily), ForgejoProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(ProviderIdentityIdentifier.Normalize(providerKey), ForgejoProviderConstants.ProviderKey, StringComparison.Ordinal))
            {
                return (ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family");
            }
        }
        catch (ArgumentException)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed");
        }

        if (!IsSafeOpaqueValue(managedTenantId)
            || IsReservedTenant(managedTenantId)
            || !IsSafeOpaqueValue(organizationId)
            || !IsSafeOpaqueValue(folderId)
            || !IsSafeOpaqueValue(delegatedTaskId)
            || !IsSafeOperationReference(providerBindingRef)
            || !IsSafeOperationReference(credentialReferenceId)
            || !IsSafeOperationReference(repositoryBindingId)
            || !IsSafeOpaqueValue(correlationId)
            || targetEvidence is null
            || authorizationEvidence is null
            || lockEvidence is null
            || refPolicyEvidence is null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed");
        }

        string? evidenceFailure = ValidateOperationEvidence(authorizationEvidence, targetEvidence, operationScope);
        if (evidenceFailure is not null)
        {
            return evidenceFailure is "authorization_evidence_stale" or "target_evidence_stale"
                ? (ProviderFailureCategory.ReconciliationRequired, evidenceFailure)
                : (ProviderFailureCategory.ProviderValidationFailed, evidenceFailure);
        }

        if (!IsCurrentEvidence(lockEvidence.CapturedAt, lockEvidence.FreshnessClass)
            || !IsSafeFingerprint(lockEvidence.Fingerprint)
            || !lockEvidence.IsOwnedByDelegatedTask
            || lockEvidence.IsRevoked)
        {
            return (ProviderFailureCategory.ProviderConflict, "canonical_lock_evidence_invalid");
        }

        if (!IsCurrentEvidence(refPolicyEvidence.CapturedAt, refPolicyEvidence.FreshnessClass)
            || !IsSafeFingerprint(refPolicyEvidence.Fingerprint))
        {
            return (ProviderFailureCategory.ReconciliationRequired, "ref_policy_evidence_stale_or_malformed");
        }

        return null;
    }

    private bool IsCurrentEvidence(DateTimeOffset? capturedAt, string? freshnessClass)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return capturedAt is { } timestamp
            && timestamp != default
            && timestamp <= now
            && now - timestamp <= MaximumAuthorizationAge
            && string.Equals(freshnessClass, "fresh", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySnapshotDeclaredRequest(
        ProviderFileMutationRequest request,
        out ProviderFileMutationRequest snapshot)
    {
        snapshot = request;
        try
        {
            if (request.Changes is null || request.Changes.Count is < 1 or > MaximumOperationChangeCount)
            {
                return false;
            }

            ProviderOrderedFileChange[] changes = new ProviderOrderedFileChange[request.Changes.Count];
            for (int index = 0; index < changes.Length; index++)
            {
                ProviderOrderedFileChange? change = request.Changes[index];
                if (change is null)
                {
                    return false;
                }

                changes[index] = change with { };
            }

            snapshot = request with { Changes = Array.AsReadOnly(changes) };
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TrySnapshotResolvedSource(
        ProviderFileMutationResolvedSource source,
        out ProviderFileMutationResolvedSource snapshot)
    {
        snapshot = null!;
        if (!TrySnapshotChanges(source.Changes, out IReadOnlyList<ProviderResolvedFileChange>? changes))
        {
            return false;
        }

        snapshot = new ProviderFileMutationResolvedSource(source.Target, changes!);
        return true;
    }

    private static bool TrySnapshotResolvedSource(
        ProviderCommitResolvedSource source,
        out ProviderCommitResolvedSource snapshot)
    {
        snapshot = null!;
        if (!TrySnapshotChanges(source.StagedChanges, out IReadOnlyList<ProviderResolvedFileChange>? changes))
        {
            return false;
        }

        snapshot = source with { StagedChanges = changes };
        return true;
    }

    private static bool TrySnapshotResolvedSource(
        ProviderOperationStatusResolvedSource source,
        out ProviderOperationStatusResolvedSource snapshot)
    {
        snapshot = null!;
        if (!TrySnapshotChanges(source.StagedChanges, out IReadOnlyList<ProviderResolvedFileChange>? changes))
        {
            return false;
        }

        snapshot = source with { StagedChanges = changes };
        return true;
    }

    private static bool TrySnapshotChanges(
        IReadOnlyList<ProviderResolvedFileChange>? source,
        out IReadOnlyList<ProviderResolvedFileChange>? snapshot)
    {
        snapshot = null;
        try
        {
            if (source is null || source.Count is < 1 or > MaximumOperationChangeCount)
            {
                return false;
            }

            ProviderResolvedFileChange[] changes = new ProviderResolvedFileChange[source.Count];
            long aggregateBytes = 0;
            for (int index = 0; index < source.Count; index++)
            {
                ProviderResolvedFileChange? change = source[index];
                if (change is null
                    || change.Content.Length > MaximumOperationFileBytes
                    || aggregateBytes > MaximumOperationAggregateContentBytes - change.Content.Length)
                {
                    return false;
                }

                aggregateBytes += change.Content.Length;
                changes[index] = change with { Content = change.Content.ToArray() };
            }

            snapshot = Array.AsReadOnly(changes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryValidateResolvedSource(
        ProviderFileMutationRequest request,
        ProviderFileMutationResolvedSource source,
        out string? failureReason)
    {
        failureReason = "forgejo_file_mutation_source_malformed";
        if (source.Target is null
            || !source.Target.TryValidate(out _)
            || !IsOperationTargetWithinBounds(source.Target)
            || source.Changes.Count != request.Changes.Count
            || !TryValidateResolvedChanges(source.Changes)
            || !HaveConsistentObjectIdWidths(source.Target, source.Changes)
            || !ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeResolvedTargetFingerprint,
                ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target)))
        {
            return false;
        }

        for (int index = 0; index < source.Changes.Count; index++)
        {
            ProviderResolvedFileChange resolved = source.Changes[index];
            ProviderOrderedFileChange declared = request.Changes[index];
            if (resolved.Sequence != declared.Sequence
                || resolved.Kind != declared.Kind
                || resolved.ContentType != declared.ContentType
                || resolved.Content.Length > request.FilePolicyEvidence.MaximumFileBytes
                || !ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                    declared.SafePathFingerprint,
                    ForgejoOperationSourceBindings.Path(request, declared, resolved.Path))
                || resolved.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    && !ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                        declared.SafeContentFingerprint,
                        ForgejoOperationSourceBindings.Content(request, declared, resolved.Content)))
            {
                return false;
            }
        }

        return ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
            request.SafeChangeSetFingerprint,
            ForgejoOperationSourceBindings.ChangeSet(request, source.Changes));
    }

    private static bool TryValidateResolvedSource(
        ProviderCommitRequest request,
        ProviderCommitResolvedSource source,
        out string? failureReason)
    {
        failureReason = "forgejo_commit_source_malformed";
        if (source.Target is null
            || !source.Target.TryValidate(out _)
            || !IsOperationTargetWithinBounds(source.Target)
            || !TryValidateResolvedChanges(source.StagedChanges)
            || !HaveConsistentObjectIdWidths(source.Target, source.StagedChanges!)
            || !TryNormalizeCommitMessage(source.CommitMessage, out string normalizedMessage))
        {
            return false;
        }

        return ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeResolvedTargetFingerprint,
                ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeStagedChangeSetFingerprint,
                ForgejoOperationSourceBindings.StagedChanges(request, source.TreeSha, source.StagedChanges!))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeCommitMessageFingerprint,
                ForgejoOperationSourceBindings.CommitMessage(request, normalizedMessage))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeExpectedHeadFingerprint,
                ForgejoOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha));
    }

    private static bool TryValidateResolvedSource(
        ProviderOperationStatusRequest request,
        ProviderOperationStatusResolvedSource source,
        out string? failureReason)
    {
        failureReason = "forgejo_operation_status_source_malformed";
        if (source.Target is null
            || !source.Target.TryValidate(out _)
            || !IsOperationTargetWithinBounds(source.Target)
            || source.IntendedCommitSha is not null
                && (!ProviderGitOperationResolvedTarget.IsGitObjectId(source.IntendedCommitSha)
                    || source.IntendedCommitSha.Length != source.Target.ExpectedHeadSha.Length
                    || string.Equals(source.IntendedCommitSha, source.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
            || !TryValidateResolvedChanges(source.StagedChanges)
            || !HaveConsistentObjectIdWidths(source.Target, source.StagedChanges!)
            || !TryNormalizeCommitMessage(source.CommitMessage, out string normalizedMessage))
        {
            return false;
        }

        return ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeResolvedTargetFingerprint,
                ForgejoOperationSourceBindings.ResolvedTarget(request, source.Target))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeFullRefFingerprint,
                ForgejoOperationSourceBindings.FullRef(request, source.Target.FullRef))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeExpectedHeadFingerprint,
                ForgejoOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha))
            && ForgejoProviderSafeOperationEvidence.FixedTimeEquals(
                request.SafeIntendedCommitFingerprint,
                ForgejoOperationSourceBindings.IntendedCommit(
                    request,
                    source.IntendedCommitSha,
                    source.StagedChanges!,
                    normalizedMessage));
    }

    private static bool TryValidateResolvedChanges(IReadOnlyList<ProviderResolvedFileChange>? changes)
    {
        if (changes is null || changes.Count is < 1 or > MaximumOperationChangeCount)
        {
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        long aggregateBytes = 0;
        for (int index = 0; index < changes.Count; index++)
        {
            ProviderResolvedFileChange? change = changes[index];
            if (change is null
                || change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || !IsSafeGitPath(change.Path)
                || !paths.Add(change.Path)
                || HasAncestorConflict(paths, change.Path)
                || change.Content.Length > MaximumOperationFileBytes
                || aggregateBytes > MaximumOperationAggregateContentBytes - change.Content.Length
                || change.Kind == ProviderFileChangeKind.Add && change.SourceObjectId is not null
                || change.Kind is ProviderFileChangeKind.Change or ProviderFileChangeKind.Remove
                    && !ProviderGitOperationResolvedTarget.IsGitObjectId(change.SourceObjectId)
                || change.Kind == ProviderFileChangeKind.Remove && !change.Content.IsEmpty)
            {
                return false;
            }

            aggregateBytes += change.Content.Length;
        }

        return true;
    }

    private async Task<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>?> ResolveFileMutationSourceAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveFileMutationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>?> ResolveCommitSourceAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveCommitAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>?> ResolveStatusSourceAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveStatusAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<(ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter)> ResolveOperationAccessAsync(
        ProviderFileMutationRequest request,
        ProviderCredentialMode credentialMode,
        Uri baseUri,
        CancellationToken cancellationToken)
        => await ResolveOperationAccessAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            baseUri,
            cancellationToken).ConfigureAwait(false);

    private async Task<(ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter)> ResolveOperationAccessAsync(
        ProviderCommitRequest request,
        ProviderCredentialMode credentialMode,
        Uri baseUri,
        CancellationToken cancellationToken)
        => await ResolveOperationAccessAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            baseUri,
            cancellationToken).ConfigureAwait(false);

    private async Task<(ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter)> ResolveOperationAccessAsync(
        ProviderOperationStatusRequest request,
        ProviderCredentialMode credentialMode,
        Uri baseUri,
        CancellationToken cancellationToken)
        => await ResolveOperationAccessAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            baseUri,
            cancellationToken).ConfigureAwait(false);

    private async Task<(ForgejoCredentialLease? Credential, IForgejoApiClient? Client, ProviderFailureCategory FailureCategory, string ReasonCode, TimeSpan? RetryAfter)> ResolveOperationAccessAsync(
        string managedTenantId,
        string organizationId,
        string providerBindingRef,
        string credentialReferenceId,
        string authorizationFingerprint,
        string correlationId,
        ProviderCredentialMode credentialMode,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        ForgejoCredentialResolutionResult? credentialResult;
        try
        {
            credentialResult = await _credentialResolver.ResolveAsync(new ForgejoCredentialResolutionRequest(
                managedTenantId,
                organizationId,
                providerBindingRef,
                credentialReferenceId,
                credentialMode,
                authorizationFingerprint,
                correlationId), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return (null, null, ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch", null);
        }
        catch (Exception)
        {
            return (null, null, ProviderFailureCategory.ProviderUnavailable, "forgejo_credential_resolution_unavailable", null);
        }

        if (!IsCredentialResolutionResultWellFormed(credentialResult))
        {
            await DisposeCredentialSafelyAsync(credentialResult?.Credential).ConfigureAwait(false);
            return (null, null, ProviderFailureCategory.ProviderUnavailable, "forgejo_credential_resolution_unavailable", null);
        }

        if (!credentialResult.IsSuccess)
        {
            return (
                null,
                null,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        ForgejoCredentialLease credential = credentialResult.Credential!;
        try
        {
            IForgejoApiClient client = await _apiClientFactory.CreateAsync(
                new ForgejoApiClientRequest(
                    ForgejoProviderConstants.ProductHeader,
                    baseUri,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    credentialMode,
                    providerBindingRef,
                    correlationId),
                credential,
                cancellationToken).ConfigureAwait(false);
            return client is null
                ? (credential, null, ProviderFailureCategory.ProviderUnavailable, "forgejo_client_creation_unavailable", null)
                : (credential, client, ProviderFailureCategory.None, "success", null);
        }
        catch (OperationCanceledException)
        {
            return (credential, null, ProviderFailureCategory.ProviderTransientFailure, "forgejo_operation_cancelled_before_dispatch", null);
        }
        catch (Exception)
        {
            return (credential, null, ProviderFailureCategory.ProviderUnavailable, "forgejo_client_creation_unavailable", null);
        }
    }

    private async Task<ProviderOperationReservationResult?> ReserveOperationAsync(
        string operationKind,
        ProviderIdempotencyAdmission admission,
        string authorizationFingerprint,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _operationOutcomeStore.ReserveAsync(
                new ProviderOperationReservationRequest(operationKind, admission.IntentFingerprint, authorizationFingerprint, correlationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsCredentialResolutionResultWellFormed(ForgejoCredentialResolutionResult? result)
    {
        if (result is null)
        {
            return false;
        }

        if (result.IsSuccess)
        {
            return result.Credential is { AccessToken.Length: > 0 }
                && result.FailureCategory == ProviderFailureCategory.None
                && string.Equals(result.ReasonCode, "success", StringComparison.Ordinal)
                && result.RetryAfter is null;
        }

        return result.Credential is null
            && IsCoherentKnownFailureTuple(
                result.FailureCategory,
                result.ReasonCode,
                SafeOperationRemediation(null, result.FailureCategory),
                result.FailureCategory.IsRetryableByDefault(),
                result.RetryAfter);
    }

    private async ValueTask<bool> ValidateReservationAsync(
        string operationReference,
        long generation,
        string intentFingerprint,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            return await _operationOutcomeStore.ValidateAsync(
                new ProviderOperationReservationValidationRequest(operationReference, generation, intentFingerprint),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async ValueTask<bool> RecordCreatedCommitAsync(
        ProviderCommitRequest request,
        ProviderCommitResolvedSource source,
        string safeTargetFingerprint,
        string operationReference,
        long generation,
        string commitSha)
    {
        string safeFingerprint = ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:created-commit",
            request.AuthorizationEvidence.Fingerprint,
            operationReference,
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            request.SafeStagedChangeSetFingerprint,
            NormalizeCommitMessage(source.CommitMessage),
            source.Target.ExpectedHeadSha,
            commitSha);
        return await RecordOperationAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.CreatedCommit,
            commitSha,
            safeFingerprint,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);
    }

    private async ValueTask<bool> RecordOperationAsync(ProviderOperationOutcomeRecord record)
    {
        try
        {
            using CancellationTokenSource timeout = new(OperationOutcomeRecordingTimeout);
            return await _operationOutcomeStore.RecordAsync(record, timeout.Token).ConfigureAwait(false) == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> RecordKnownFailureAsync(
        string operationReference,
        long generation,
        ProviderFailureCategory category,
        string reasonCode,
        string safeOutcomeFingerprint,
        TimeSpan? retryAfter,
        string? privateObjectId = null)
        => await RecordOperationAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.KnownTerminalFailure,
            ProviderGitOperationResolvedTarget.IsGitObjectId(privateObjectId) ? privateObjectId : null,
            safeOutcomeFingerprint,
            category,
            SafeOperationReason(reasonCode, category.ToCategoryCode()),
            SafeOperationRemediation(null, category),
            category.IsRetryableByDefault(),
            SafeOperationRetryAfter(category, retryAfter))).ConfigureAwait(false);

    private async Task<bool> RecordUnknownAsync(
        string operationReference,
        long generation,
        string reasonCode,
        string? privateObjectId = null)
        => await RecordOperationAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.Unknown,
            ProviderGitOperationResolvedTarget.IsGitObjectId(privateObjectId) ? privateObjectId : null,
            SafeOutcomeFingerprint: null,
            ProviderFailureCategory.UnknownProviderOutcome,
            SafeOperationReason(reasonCode, "forgejo_commit_outcome_unknown"),
            "reconciliation_required_metadata_only",
            Retryable: false,
            RetryAfter: null,
            ReconciliationReference: operationReference)).ConfigureAwait(false);

    private async Task<bool> FinalizeNoDispatchAsync(
        string operationReference,
        long generation,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
    {
        try
        {
            ProviderFailureCategory safeCategory = Enum.IsDefined(category)
                && category is not ProviderFailureCategory.None and not ProviderFailureCategory.UnknownProviderOutcome
                    ? category
                    : ProviderFailureCategory.ProviderUnavailable;
            string safeReasonCode = KnownFailureReasonCategory(reasonCode) == safeCategory
                ? reasonCode
                : KnownFailureFallbackReason(safeCategory);
            using CancellationTokenSource timeout = new(OperationOutcomeRecordingTimeout);
            return await _operationOutcomeStore.FinalizeNoDispatchAsync(new ProviderOperationOutcomeRecord(
                operationReference,
                generation,
                ProviderOperationOutcomeKind.NoDispatch,
                PrivateObjectId: null,
                SafeOutcomeFingerprint: null,
                safeCategory,
                safeReasonCode,
                SafeOperationRemediation(null, safeCategory),
                safeCategory.IsRetryableByDefault(),
                SafeOperationRetryAfter(safeCategory, retryAfter)), timeout.Token).ConfigureAwait(false) == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async ValueTask DisposeOperationResourcesSafelyAsync(
        IForgejoApiClient? client,
        ForgejoCredentialLease? credential)
    {
        if (client is not null)
        {
            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        await DisposeCredentialSafelyAsync(credential).ConfigureAwait(false);
    }

    private static async ValueTask DisposeCredentialSafelyAsync(ForgejoCredentialLease? credential)
    {
        if (credential is null)
        {
            return;
        }

        try
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static ProviderFileMutationResult? ReplayOrReject(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh or ProviderIdempotencyDisposition.Execute => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
        };

    private static ProviderCommitResult? ReplayOrReject(
        ProviderCommitRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh or ProviderIdempotencyDisposition.Execute => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
        };

    private static ProviderFileMutationResult Replay(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => new ProviderFileMutationResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, null),
            ProviderPriorOutcomeDisposition.Unknown => new ProviderFileMutationResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), "forgejo_file_mutation_outcome_unknown", "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, admission.PriorOperationReference, admission.PriorReconciliationReference),
            _ => FileMutationFailure(request, admission.PriorFailureCategory, admission.PriorReasonCode!, admission.PriorRetryAfter, admission.PriorOperationReference, true, admission.PriorRemediationCode, admission.PriorRetryable, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint),
        };

    private static ProviderCommitResult Replay(
        ProviderCommitRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => new ProviderCommitResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, null),
            ProviderPriorOutcomeDisposition.Unknown => new ProviderCommitResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), "forgejo_commit_outcome_unknown", "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, admission.PriorOperationReference, admission.PriorReconciliationReference),
            _ => CommitFailure(request, admission.PriorFailureCategory, admission.PriorReasonCode!, admission.PriorRetryAfter, admission.PriorOperationReference, true, admission.PriorRemediationCode, admission.PriorRetryable, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint),
        };

    private static ProviderFileMutationResult? MapReservation(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint,
        ProviderOperationReservationResult? reservation)
    {
        if (reservation is null || !IsReservationWellFormed(reservation))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderConfigurationMissing, "forgejo_operation_outcome_store_unavailable");
        }

        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Acquired => null,
            ProviderOperationReservationDisposition.Pending => FileMutationUnknown(request, safeTargetFingerprint, reservation.OperationReference!, "forgejo_operation_pending"),
            ProviderOperationReservationDisposition.ReplaySuccess => new ProviderFileMutationResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, null),
            ProviderOperationReservationDisposition.ReplayUnknown => new ProviderFileMutationResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), "forgejo_file_mutation_outcome_unknown", "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, reservation.OperationReference, reservation.ReconciliationReference),
            ProviderOperationReservationDisposition.ReplayKnownFailure => FileMutationFailure(request, reservation.FailureCategory, reservation.ReasonCode!, reservation.RetryAfter, reservation.OperationReference, true, reservation.RemediationCode, reservation.Retryable, safeTargetFingerprint, reservation.SafeOutcomeFingerprint),
            ProviderOperationReservationDisposition.Conflict => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => FileMutationFailure(request, reservation.FailureCategory, reservation.ReasonCode ?? "forgejo_operation_outcome_store_unavailable"),
        };
    }

    private static ProviderCommitResult? MapReservation(
        ProviderCommitRequest request,
        string safeTargetFingerprint,
        ProviderOperationReservationResult? reservation)
    {
        if (reservation is null || !IsReservationWellFormed(reservation))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderConfigurationMissing, "forgejo_operation_outcome_store_unavailable");
        }

        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Acquired => null,
            ProviderOperationReservationDisposition.Pending => CommitUnknown(request, safeTargetFingerprint, reservation.OperationReference!, "forgejo_operation_pending"),
            ProviderOperationReservationDisposition.ReplaySuccess => new ProviderCommitResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, null),
            ProviderOperationReservationDisposition.ReplayUnknown => new ProviderCommitResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), "forgejo_commit_outcome_unknown", "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, reservation.OperationReference, reservation.ReconciliationReference),
            ProviderOperationReservationDisposition.ReplayKnownFailure => CommitFailure(request, reservation.FailureCategory, reservation.ReasonCode!, reservation.RetryAfter, reservation.OperationReference, true, reservation.RemediationCode, reservation.Retryable, safeTargetFingerprint, reservation.SafeOutcomeFingerprint),
            ProviderOperationReservationDisposition.Conflict => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => CommitFailure(request, reservation.FailureCategory, reservation.ReasonCode ?? "forgejo_operation_outcome_store_unavailable"),
        };
    }

    private static bool IsOperationAdmissionWellFormed(ProviderIdempotencyAdmission? admission)
    {
        if (admission is null || !Enum.IsDefined(admission.Disposition) || !IsSafeOpaqueValue(admission.IntentFingerprint))
        {
            return false;
        }

        if (admission.Disposition != ProviderIdempotencyDisposition.EquivalentReplay)
        {
            return HasNoPriorOutcomeFields(admission);
        }

        if (admission.PriorOutcomeDisposition is null
            || !Enum.IsDefined(admission.PriorOutcomeDisposition.Value)
            || !IsSafeOperationReference(admission.PriorOperationReference))
        {
            return false;
        }

        return admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && admission.PriorCanonicalRepositoryId is null
                && admission.PriorReconciliationReference is null
                && admission.PriorFailureCategory == ProviderFailureCategory.None
                && admission.PriorReasonCode is null
                && admission.PriorRemediationCode is null
                && !admission.PriorRetryable
                && admission.PriorRetryAfter is null,
            ProviderPriorOutcomeDisposition.Unknown => admission.PriorSafeOutcomeFingerprint is null
                && admission.PriorCanonicalRepositoryId is null
                && IsSafeOperationReference(admission.PriorReconciliationReference)
                && admission.PriorFailureCategory == ProviderFailureCategory.None
                && admission.PriorReasonCode is null
                && admission.PriorRemediationCode is null
                && !admission.PriorRetryable
                && admission.PriorRetryAfter is null,
            ProviderPriorOutcomeDisposition.KnownFailure => IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && admission.PriorCanonicalRepositoryId is null
                && admission.PriorReconciliationReference is null
                && IsCoherentKnownFailureTuple(
                    admission.PriorFailureCategory,
                    admission.PriorReasonCode,
                    admission.PriorRemediationCode,
                    admission.PriorRetryable,
                    admission.PriorRetryAfter),
            _ => false,
        };
    }

    private static bool IsReservationWellFormed(ProviderOperationReservationResult reservation)
    {
        if (!Enum.IsDefined(reservation.Disposition))
        {
            return false;
        }

        bool hasOperationReference = IsSafeOperationReference(reservation.OperationReference);
        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Acquired or ProviderOperationReservationDisposition.Pending
                => hasOperationReference && reservation.Generation > 0 && HasNoReservationOutcome(reservation),
            ProviderOperationReservationDisposition.ReplaySuccess
                => hasOperationReference && reservation.Generation == 0 && IsSafeFingerprint(reservation.SafeOutcomeFingerprint)
                    && reservation.ReconciliationReference is null && reservation.FailureCategory == ProviderFailureCategory.None
                    && reservation.ReasonCode is null && reservation.RemediationCode is null && !reservation.Retryable && reservation.RetryAfter is null,
            ProviderOperationReservationDisposition.ReplayUnknown
                => hasOperationReference && reservation.Generation == 0 && reservation.SafeOutcomeFingerprint is null
                    && IsSafeOperationReference(reservation.ReconciliationReference) && reservation.FailureCategory == ProviderFailureCategory.None
                    && reservation.ReasonCode is null && reservation.RemediationCode is null && !reservation.Retryable && reservation.RetryAfter is null,
            ProviderOperationReservationDisposition.ReplayKnownFailure
                => hasOperationReference && reservation.Generation == 0 && IsSafeFingerprint(reservation.SafeOutcomeFingerprint)
                    && reservation.ReconciliationReference is null
                    && IsCoherentKnownFailureTuple(
                        reservation.FailureCategory,
                        reservation.ReasonCode,
                        reservation.RemediationCode,
                        reservation.Retryable,
                        reservation.RetryAfter),
            ProviderOperationReservationDisposition.Conflict
                => reservation.OperationReference is null && reservation.Generation == 0 && reservation.SafeOutcomeFingerprint is null
                    && reservation.ReconciliationReference is null && reservation.FailureCategory == ProviderFailureCategory.ProviderConflict
                    && string.Equals(reservation.ReasonCode, "idempotency_conflict", StringComparison.Ordinal)
                    && reservation.RemediationCode is null && !reservation.Retryable && reservation.RetryAfter is null,
            ProviderOperationReservationDisposition.Unavailable
                => reservation.OperationReference is null && reservation.Generation == 0 && reservation.SafeOutcomeFingerprint is null
                    && reservation.ReconciliationReference is null
                    && reservation.FailureCategory is ProviderFailureCategory.ProviderConfigurationMissing or ProviderFailureCategory.ProviderUnavailable
                    && AllowedOperationReasonCodes.Contains(reservation.ReasonCode ?? string.Empty)
                    && reservation.RemediationCode is null && !reservation.Retryable && reservation.RetryAfter is null,
            _ => false,
        };
    }

    private static bool HasNoReservationOutcome(ProviderOperationReservationResult reservation)
        => reservation.SafeOutcomeFingerprint is null
            && reservation.ReconciliationReference is null
            && reservation.FailureCategory == ProviderFailureCategory.None
            && reservation.ReasonCode is null
            && reservation.RemediationCode is null
            && !reservation.Retryable
            && reservation.RetryAfter is null;

    private static bool IsFileMutationResultWellFormed(ForgejoFileMutationResult result)
        => result.IsSuccess
            ? result.FailureCondition == default
                && result.RetryAfter is null
                && result.TreeSha is { Length: 40 }
                && ProviderGitOperationResolvedTarget.IsGitObjectId(result.TreeSha)
            : Enum.IsDefined(result.FailureCondition)
                && result.FailureCondition != default
                && result.TreeSha is null
                && (result.RetryAfter is null || result.RetryAfter >= TimeSpan.Zero && result.RetryAfter <= TimeSpan.FromHours(24));

    private static bool IsCommitResultWellFormed(ForgejoCommitResult result, ProviderCommitResolvedSource source)
    {
        if (result.IsSuccess)
        {
            return result.FailureCondition == default
                && result.RetryAfter is null
                && ProviderGitOperationResolvedTarget.IsGitObjectId(result.CommitSha)
                && string.Equals(result.CommitSha, result.ObservedCommitSha, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(result.CommitSha, source.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase);
        }

        return Enum.IsDefined(result.FailureCondition)
            && result.FailureCondition != default
            && result.CommitSha is null
            && (result.ObservedCommitSha is null || ProviderGitOperationResolvedTarget.IsGitObjectId(result.ObservedCommitSha))
            && (result.RetryAfter is null || result.RetryAfter >= TimeSpan.Zero && result.RetryAfter <= TimeSpan.FromHours(24));
    }

    private static bool IsStatusResultWellFormed(
        ForgejoOperationStatusResult result,
        ProviderOperationStatusResolvedSource source)
    {
        if (!result.IsSuccess)
        {
            return result.Status == ProviderOperationStatusKind.Unavailable
                && Enum.IsDefined(result.FailureCondition)
                && result.FailureCondition != default
                && result.ObservedSha is null
                && result.ObservedFullRef is null
                && result.ObservedObjectType is null;
        }

        if (result.FailureCondition != default
            || result.RetryAfter is not null
            || !Enum.IsDefined(result.Status)
            || result.Status == ProviderOperationStatusKind.Unavailable)
        {
            return false;
        }

        if (result.Status == ProviderOperationStatusKind.NotApplied)
        {
            return string.Equals(result.ObservedFullRef, source.Target.FullRef, StringComparison.Ordinal)
                && string.Equals(result.ObservedObjectType, "commit", StringComparison.Ordinal)
                && string.Equals(result.ObservedSha, source.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase);
        }

        if (result.Status == ProviderOperationStatusKind.Confirmed)
        {
            return string.Equals(result.ObservedFullRef, source.Target.FullRef, StringComparison.Ordinal)
                && string.Equals(result.ObservedObjectType, "commit", StringComparison.Ordinal)
                && ProviderGitOperationResolvedTarget.IsGitObjectId(result.ObservedSha)
                && !string.Equals(result.ObservedSha, source.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase)
                && (source.IntendedCommitSha is null
                    || string.Equals(result.ObservedSha, source.IntendedCommitSha, StringComparison.OrdinalIgnoreCase));
        }

        return result.Status == ProviderOperationStatusKind.Conflicting
            && (result.ObservedFullRef is null || string.Equals(result.ObservedFullRef, source.Target.FullRef, StringComparison.Ordinal))
            && (result.ObservedObjectType is null || string.Equals(result.ObservedObjectType, "commit", StringComparison.Ordinal))
            && (result.ObservedSha is null
                || ProviderGitOperationResolvedTarget.IsGitObjectId(result.ObservedSha)
                    && !string.Equals(result.ObservedSha, source.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase)
                    && (source.IntendedCommitSha is null
                        || !string.Equals(result.ObservedSha, source.IntendedCommitSha, StringComparison.OrdinalIgnoreCase)));
    }

    private static ProviderFileMutationResult SourceFailure(
        ProviderFileMutationRequest request,
        ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? resolution,
        string fallback)
        => FileMutationFailure(
            request,
            resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable,
            resolution?.GetSafeReasonCode(fallback) ?? fallback,
            resolution?.SafeRetryAfter);

    private static ProviderCommitResult SourceFailure(
        ProviderCommitRequest request,
        ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? resolution,
        string fallback)
        => CommitFailure(
            request,
            resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable,
            resolution?.GetSafeReasonCode(fallback) ?? fallback,
            resolution?.SafeRetryAfter);

    private static ProviderOperationStatusResult SourceFailure(
        ProviderOperationStatusRequest request,
        ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? resolution,
        string fallback)
        => StatusFailure(
            request,
            resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable,
            resolution?.GetSafeReasonCode(fallback) ?? fallback,
            resolution?.SafeRetryAfter);

    private static ProviderFileMutationResult FileMutationUnknown(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint,
        string operationReference,
        string reasonCode)
        => new(false, false, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeOperationReason(reasonCode, "forgejo_file_mutation_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, IsSafeOperationReference(operationReference) ? operationReference : null, IsSafeOperationReference(operationReference) ? operationReference : null);

    private static ProviderCommitResult CommitUnknown(
        ProviderCommitRequest request,
        string safeTargetFingerprint,
        string operationReference,
        string reasonCode)
        => new(false, false, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeOperationReason(reasonCode, "forgejo_commit_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, IsSafeOperationReference(operationReference) ? operationReference : null, IsSafeOperationReference(operationReference) ? operationReference : null);

    private static ProviderFileMutationResult FileMutationFailure(
        ProviderFileMutationRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? operationReference = null,
        bool equivalentReplay = false,
        string? remediationCode = null,
        bool? retryable = null,
        string? safeTargetFingerprint = null,
        string? safeOutcomeFingerprint = null)
        => new(false, equivalentReplay, category, category.ToCategoryCode(), SafeOperationReason(reasonCode, category.ToCategoryCode()), SafeOperationRemediation(remediationCode, category), retryable ?? category.IsRetryableByDefault(), SafeOperationRetryAfter(category, retryAfter), request.CorrelationId, IsSafeFingerprint(safeTargetFingerprint) ? safeTargetFingerprint : null, IsSafeFingerprint(safeOutcomeFingerprint) ? safeOutcomeFingerprint : null, IsSafeOperationReference(operationReference) ? operationReference : null, null);

    private static ProviderCommitResult CommitFailure(
        ProviderCommitRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? operationReference = null,
        bool equivalentReplay = false,
        string? remediationCode = null,
        bool? retryable = null,
        string? safeTargetFingerprint = null,
        string? safeOutcomeFingerprint = null)
        => new(false, equivalentReplay, category, category.ToCategoryCode(), SafeOperationReason(reasonCode, category.ToCategoryCode()), SafeOperationRemediation(remediationCode, category), retryable ?? category.IsRetryableByDefault(), SafeOperationRetryAfter(category, retryAfter), request.CorrelationId, IsSafeFingerprint(safeTargetFingerprint) ? safeTargetFingerprint : null, IsSafeFingerprint(safeOutcomeFingerprint) ? safeOutcomeFingerprint : null, IsSafeOperationReference(operationReference) ? operationReference : null, null);

    private static ProviderOperationStatusResult StatusUnavailable(
        ProviderOperationStatusRequest request,
        TimeSpan? retryAfter)
        => new(false, ProviderOperationStatusKind.Unavailable, ProviderFailureCategory.ProviderUnavailable, ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(), "forgejo_status_evidence_unavailable", "provider_unavailable_remediation", true, SafeOperationRetryAfter(ProviderFailureCategory.ProviderUnavailable, retryAfter), request.CorrelationId, request.CheckNumber, null, request.OperationReference);

    private static ProviderOperationStatusResult StatusFailure(
        ProviderOperationStatusRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? safeObservedFingerprint = null)
        => new(false, ProviderOperationStatusKind.Unavailable, category, category.ToCategoryCode(), SafeOperationReason(reasonCode, category.ToCategoryCode()), SafeOperationRemediation(null, category), category.IsRetryableByDefault(), SafeOperationRetryAfter(category, retryAfter), request.CorrelationId, request.CheckNumber, IsSafeFingerprint(safeObservedFingerprint) ? safeObservedFingerprint : null, IsSafeOperationReference(request.OperationReference) ? request.OperationReference : null);

    private static bool IsAllowedOperationChange(ProviderFileChangeKind kind, ProviderFilePolicyEvidence policy)
        => kind switch
        {
            ProviderFileChangeKind.Add => policy.AllowsAdd,
            ProviderFileChangeKind.Change => policy.AllowsChange,
            ProviderFileChangeKind.Remove => policy.AllowsRemove,
            _ => false,
        };

    private static bool IsSafeGitPath(string? path)
        => path is { Length: > 0 and <= MaximumOperationPathCharacters }
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(path)
            && path[0] != '/'
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains("\\", StringComparison.Ordinal)
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

    private static bool HasAncestorConflict(HashSet<string> paths, string candidate)
        => paths.Any(path => !string.Equals(path, candidate, StringComparison.Ordinal)
            && (path.StartsWith(candidate + "/", StringComparison.Ordinal)
                || candidate.StartsWith(path + "/", StringComparison.Ordinal)));

    private static bool IsOperationTargetWithinBounds(ProviderGitOperationResolvedTarget target)
        => target.RefName["heads/".Length..].Length <= MaximumOperationBranchCharacters;

    private static bool HaveConsistentObjectIdWidths(
        ProviderGitOperationResolvedTarget target,
        IReadOnlyList<ProviderResolvedFileChange> changes)
        => changes.All(change => change.Kind == ProviderFileChangeKind.Add
            || change.SourceObjectId?.Length == target.ExpectedHeadSha.Length);

    private static bool IsSafeOperationReference(string? value)
        => value is { Length: > 0 and <= 128 }
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static string NormalizeCommitMessage(string value) => value.Trim();

    private static bool TryNormalizeCommitMessage(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 65536
            || !ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            || value.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        normalized = value.Trim();
        return normalized.Length > 0;
    }

    private static string SafeOperationReason(string? reasonCode, string fallback)
        => reasonCode is not null && AllowedOperationReasonCodes.Contains(reasonCode) ? reasonCode : fallback;

    private static string SafeOperationRemediation(string? remediationCode, ProviderFailureCategory category)
        => IsAllowedOperationRemediation(remediationCode)
            ? remediationCode!
            : category is ProviderFailureCategory.ReconciliationRequired or ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{category.ToCategoryCode()}_remediation";

    private static bool IsAllowedOperationRemediation(string? remediationCode)
        => remediationCode is "none"
            or "provider_authentication_required_remediation"
            or "provider_configuration_missing_remediation"
            or "provider_conflict_remediation"
            or "provider_failure_known_remediation"
            or "provider_permission_insufficient_remediation"
            or "provider_rate_limited_remediation"
            or "provider_transient_failure_remediation"
            or "provider_unavailable_remediation"
            or "provider_validation_failed_remediation"
            or "reconciliation_required_metadata_only"
            or "reconciliation_required_remediation"
            or "unknown_provider_outcome_remediation"
            or "unsupported_provider_capability_remediation";

    private static TimeSpan? SafeOperationRetryAfter(
        ProviderFailureCategory category,
        TimeSpan? retryAfter)
        => category.IsRetryableByDefault()
            && retryAfter is { } value
            && value > TimeSpan.Zero
            && value <= TimeSpan.FromHours(24)
                ? value
                : null;

    private static bool IsCoherentKnownFailureTuple(
        ProviderFailureCategory category,
        string? reasonCode,
        string? remediationCode,
        bool retryable,
        TimeSpan? retryAfter)
        => Enum.IsDefined(category)
            && category is not ProviderFailureCategory.None and not ProviderFailureCategory.UnknownProviderOutcome
            && KnownFailureReasonCategory(reasonCode) == category
            && string.Equals(remediationCode, SafeOperationRemediation(null, category), StringComparison.Ordinal)
            && retryable == category.IsRetryableByDefault()
            && (retryAfter is null || SafeOperationRetryAfter(category, retryAfter) == retryAfter);

    private static ProviderFailureCategory? KnownFailureReasonCategory(string? reasonCode)
        => reasonCode switch
        {
            "forgejo_validation_failed" or "provider_credential_secret_malformed"
                => ProviderFailureCategory.ProviderValidationFailed,
            "forgejo_operation_cancelled_before_dispatch" => ProviderFailureCategory.ProviderTransientFailure,
            "forgejo_operation_reservation_invalidated" => ProviderFailureCategory.ProviderConflict,
            "forgejo_authentication_required" => ProviderFailureCategory.ProviderAuthenticationRequired,
            "forgejo_permission_insufficient" or "forgejo_administration_permission_insufficient"
                or "forgejo_remote_policy_rejected"
                or "forgejo_resource_hidden_or_missing" or "provider_credential_reference_denied"
                => ProviderFailureCategory.ProviderPermissionInsufficient,
            "forgejo_branch_protection_conflict" or "forgejo_ref_head_conflict"
                or "forgejo_repository_archived" or "idempotency_conflict" or "idempotency_key_expired"
                or "canonical_lock_evidence_invalid" => ProviderFailureCategory.ProviderConflict,
            "forgejo_capability_unsupported" or "unsupported_forgejo_credential_mode" or "unsupported_provider_family"
                or "forgejo_object_format_unsupported" or "forgejo_smart_http_unsupported"
                or "forgejo_native_runtime_unavailable"
                => ProviderFailureCategory.UnsupportedProviderCapability,
            "forgejo_cross_origin_redirect_rejected" => ProviderFailureCategory.ProviderReadinessFailed,
            "forgejo_rate_limited" => ProviderFailureCategory.ProviderRateLimited,
            "forgejo_response_limit_exceeded" or "forgejo_transfer_limit_exceeded"
                or "forgejo_temporary_disk_limit_exceeded" or "forgejo_temporary_repository_cleanup_failed"
                or "forgejo_remote_rejected"
                => ProviderFailureCategory.ProviderFailureKnown,
            "forgejo_operation_timed_out" => ProviderFailureCategory.ProviderTransientFailure,
            "forgejo_server_unavailable" or "forgejo_client_creation_unavailable"
                or "forgejo_credential_resolution_unavailable" or "forgejo_file_mutation_source_unavailable"
                or "forgejo_commit_source_unavailable" or "forgejo_operation_status_source_unavailable"
                or "forgejo_status_evidence_unavailable" or "provider_credential_store_unavailable"
                => ProviderFailureCategory.ProviderUnavailable,
            "forgejo_credential_resolver_unconfigured" or "provider_credential_reference_missing"
                or "provider_file_mutation_source_unconfigured" or "provider_commit_source_unconfigured"
                or "provider_operation_status_source_unconfigured" or "provider_operation_outcome_store_unconfigured"
                or "forgejo_operation_outcome_store_unavailable"
                => ProviderFailureCategory.ProviderConfigurationMissing,
            "forgejo_version_incompatible" or "forgejo_reconciliation_budget_exhausted"
                or "forgejo_reconciliation_checks_exhausted" or "reconciliation_required"
                or "authorization_evidence_stale" or "target_evidence_stale"
                or "forgejo_file_policy_evidence_stale_or_malformed" or "ref_policy_evidence_stale_or_malformed"
                => ProviderFailureCategory.ReconciliationRequired,
            _ => null,
        };

    private static string KnownFailureFallbackReason(ProviderFailureCategory category)
        => category switch
        {
            ProviderFailureCategory.ProviderValidationFailed => "forgejo_validation_failed",
            ProviderFailureCategory.ProviderTransientFailure => "forgejo_operation_cancelled_before_dispatch",
            ProviderFailureCategory.ProviderConflict => "forgejo_ref_head_conflict",
            ProviderFailureCategory.ProviderAuthenticationRequired => "forgejo_authentication_required",
            ProviderFailureCategory.ProviderPermissionInsufficient => "forgejo_permission_insufficient",
            ProviderFailureCategory.UnsupportedProviderCapability => "forgejo_capability_unsupported",
            ProviderFailureCategory.ProviderReadinessFailed => "forgejo_cross_origin_redirect_rejected",
            ProviderFailureCategory.ProviderRateLimited => "forgejo_rate_limited",
            ProviderFailureCategory.ProviderConfigurationMissing => "forgejo_operation_outcome_store_unavailable",
            ProviderFailureCategory.ReconciliationRequired => "reconciliation_required",
            _ => "forgejo_server_unavailable",
        };

    private static string CreateFailureFingerprint(
        string domain,
        string authorizationFingerprint,
        string operationReference,
        string safeTargetFingerprint,
        string intentFingerprint,
        ProviderFailureCategory category,
        string reasonCode)
        => ForgejoProviderSafeOperationEvidence.Create(
            domain,
            authorizationFingerprint,
            operationReference,
            safeTargetFingerprint,
            intentFingerprint,
            category.ToCategoryCode(),
            SafeOperationReason(reasonCode, category.ToCategoryCode()));
}
