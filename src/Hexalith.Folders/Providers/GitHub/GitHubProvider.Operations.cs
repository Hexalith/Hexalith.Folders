using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

public sealed partial class GitHubProvider
{
    private const int MaximumChangeCount = 100;
    private const int MaximumFileBytes = 1024 * 1024;
    private const long MaximumAggregateContentBytes = 10L * 1024 * 1024;
    private static readonly TimeSpan OutcomeRecordingTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconciliationWindow = TimeSpan.FromMinutes(15);
    private static readonly HashSet<string> AllowedOperationReasonCodes = new(StringComparer.Ordinal)
    {
        "existing_equivalent",
        "github_authentication_required",
        "github_branch_protection_conflict",
        "github_client_creation_unavailable",
        "github_change_set_malformed",
        "github_commit_intent_malformed",
        "github_commit_outcome_unknown",
        "github_commit_ref_policy_denied",
        "github_commit_source_malformed",
        "github_commit_source_unavailable",
        "github_content_policy_invalid",
        "github_credential_resolution_unavailable",
        "github_evidence_temporarily_unavailable",
        "github_file_mutation_outcome_unknown",
        "github_file_mutation_ref_policy_denied",
        "github_file_mutation_source_malformed",
        "github_file_mutation_source_unavailable",
        "github_file_policy_evidence_stale_or_malformed",
        "github_malformed_response",
        "github_mutation_evidence_ambiguous",
        "github_mutation_intent_malformed",
        "github_mutation_outcome_unknown",
        "github_operation_cancelled_before_dispatch",
        "github_operation_outcome_store_unavailable",
        "github_operation_pending",
        "github_operation_reservation_invalidated",
        "github_operation_status_source_malformed",
        "github_operation_status_source_unavailable",
        "github_outcome_recording_failed",
        "github_path_policy_invalid",
        "github_permission_insufficient",
        "github_primary_rate_limited",
        "github_reconciliation_budget_exhausted",
        "github_reconciliation_checks_exhausted",
        "github_ref_head_conflict",
        "github_resource_hidden_or_missing",
        "github_response_limit_exceeded",
        "github_secondary_rate_limited",
        "github_server_unavailable",
        "github_status_evidence_conflicting",
        "github_status_evidence_malformed",
        "github_status_evidence_unavailable",
        "github_transport_outcome_unknown",
        "github_validation_failed",
        "idempotency_conflict",
        "idempotency_key_expired",
        "idempotency_key_not_allowed",
        "provider_commit_source_unconfigured",
        "provider_file_mutation_source_unconfigured",
        "provider_operation_outcome_store_unconfigured",
        "provider_operation_status_source_unconfigured",
        "provider_validation_failed",
        "reconciliation_required",
        "success",
    };

    private static readonly HashSet<string> AllowedOperationRemediationCodes = new(StringComparer.Ordinal)
    {
        "none",
        "provider_authentication_required_remediation",
        "provider_configuration_missing_remediation",
        "provider_conflict_remediation",
        "provider_failure_known_remediation",
        "provider_permission_insufficient_remediation",
        "provider_rate_limited_remediation",
        "provider_transient_failure_remediation",
        "provider_unavailable_remediation",
        "provider_validation_failed_remediation",
        "reconciliation_required_metadata_only",
        "reconciliation_required_remediation",
        "unknown_provider_outcome_remediation",
        "unsupported_provider_capability_remediation",
    };

    public async Task<ProviderFileMutationResult> StageFileChangesAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return FileMutationFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out ProviderCredentialMode credentialMode, out string? credentialFailure))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderValidationFailed, SafeReason(targetFailure, "provider_validation_failed"));
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderFileMutationResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? sourceResolution = await ResolveFileMutationSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "github_file_mutation_source_unavailable");
        }

        ProviderFileMutationResolvedSource source = sourceResolution.Source;
        if (!TryValidateResolvedSource(request, source, out string? sourceFailure))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "github_file_mutation_source_malformed");
        }

        ProviderOperationReservationResult? reservation = await ReserveAsync(
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
        GitHubCredentialResolutionResult credentialResult = await ResolveCredentialSafelyAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            cancellationToken).ConfigureAwait(false);
        if (!credentialResult.IsSuccess)
        {
            await FinalizeNoDispatchAsync(operationReference, generation, credentialResult.FailureCategory, credentialResult.ReasonCode).ConfigureAwait(false);
            return FileMutationFailure(request, credentialResult.FailureCategory, SafeReason(credentialResult.ReasonCode, "github_credential_resolution_unavailable"), credentialResult.RetryAfter, operationReference);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient? client = await CreateClientSafelyAsync(request.ProviderBindingRef, request.CorrelationId, credentialMode, credential, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            await FinalizeNoDispatchAsync(operationReference, generation, ProviderFailureCategory.ProviderUnavailable, "github_client_creation_unavailable").ConfigureAwait(false);
            return FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_client_creation_unavailable", operationReference: operationReference);
        }

        GitHubFileMutationResult? result;
        try
        {
            result = await client.StageFileChangesAsync(
                new GitHubFileMutationRequest(
                    source.Target,
                    source.Changes,
                    token => ValidateReservationAsync(operationReference, generation, request.IdempotencyAdmission.IntentFingerprint, token)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result is null)
        {
            await RecordUnknownAsync(operationReference, generation, "github_file_mutation_outcome_unknown").ConfigureAwait(false);
            return FileMutationUnknown(request, safeTargetFingerprint, operationReference, "github_file_mutation_outcome_unknown");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = GitHubFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            if (result.FailureCondition is GitHubApiFailureCondition.CancellationBeforeDispatch or GitHubApiFailureCondition.ReservationInvalidated)
            {
                await FinalizeNoDispatchAsync(operationReference, generation, mapped.Category, mapped.ReasonCode).ConfigureAwait(false);
                return FileMutationFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference);
            }

            if (mapped.Category == ProviderFailureCategory.UnknownProviderOutcome)
            {
                await RecordUnknownAsync(operationReference, generation, mapped.ReasonCode).ConfigureAwait(false);
                return FileMutationUnknown(request, safeTargetFingerprint, operationReference, mapped.ReasonCode);
            }

            string safeFailureFingerprint = CreateFailureFingerprint(
                "hxf-github:v1:mutation-failure",
                request.AuthorizationEvidence.Fingerprint,
                operationReference,
                safeTargetFingerprint,
                request.IdempotencyAdmission.IntentFingerprint,
                mapped.Category,
                mapped.ReasonCode);
            await RecordKnownFailureAsync(operationReference, generation, mapped.Category, mapped.ReasonCode, safeFailureFingerprint, result.RetryAfter).ConfigureAwait(false);
            return FileMutationFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference, safeTargetFingerprint: safeTargetFingerprint, safeOutcomeFingerprint: safeFailureFingerprint);
        }

        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(result.TreeSha))
        {
            await RecordUnknownAsync(operationReference, generation, "github_mutation_evidence_ambiguous").ConfigureAwait(false);
            return FileMutationUnknown(request, safeTargetFingerprint, operationReference, "github_mutation_evidence_ambiguous");
        }

        string safeOutcomeFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:mutation-outcome",
            request.AuthorizationEvidence.Fingerprint,
            operationReference,
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            result.TreeSha);
        bool recorded = await RecordAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.StagedTree,
            result.TreeSha,
            safeOutcomeFingerprint,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);
        if (!recorded)
        {
            return FileMutationUnknown(request, safeTargetFingerprint, operationReference, "github_outcome_recording_failed");
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
            return CommitFailure(request, ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return CommitFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out ProviderCredentialMode credentialMode, out string? credentialFailure))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderValidationFailed, SafeReason(targetFailure, "provider_validation_failed"));
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderCommitResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? sourceResolution = await ResolveCommitSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "github_commit_source_unavailable");
        }

        ProviderCommitResolvedSource source = sourceResolution.Source;
        if (!TryValidateResolvedSource(request, source, out string? sourceFailure))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "github_commit_source_malformed");
        }

        ProviderOperationReservationResult? reservation = await ReserveAsync(
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
        GitHubCredentialResolutionResult credentialResult = await ResolveCredentialSafelyAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            cancellationToken).ConfigureAwait(false);
        if (!credentialResult.IsSuccess)
        {
            await FinalizeNoDispatchAsync(operationReference, generation, credentialResult.FailureCategory, credentialResult.ReasonCode).ConfigureAwait(false);
            return CommitFailure(request, credentialResult.FailureCategory, SafeReason(credentialResult.ReasonCode, "github_credential_resolution_unavailable"), credentialResult.RetryAfter, operationReference);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient? client = await CreateClientSafelyAsync(request.ProviderBindingRef, request.CorrelationId, credentialMode, credential, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            await FinalizeNoDispatchAsync(operationReference, generation, ProviderFailureCategory.ProviderUnavailable, "github_client_creation_unavailable").ConfigureAwait(false);
            return CommitFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_client_creation_unavailable", operationReference: operationReference);
        }

        GitHubCommitResult? result;
        try
        {
            result = await client.CommitAsync(
                new GitHubCommitRequest(
                    source.Target,
                    source.TreeSha,
                    source.CommitMessage,
                    token => ValidateReservationAsync(operationReference, generation, request.IdempotencyAdmission.IntentFingerprint, token),
                    commitSha => RecordCreatedCommitAsync(operationReference, generation, commitSha)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result is null)
        {
            await RecordUnknownAsync(operationReference, generation, "github_commit_outcome_unknown").ConfigureAwait(false);
            return CommitUnknown(request, safeTargetFingerprint, operationReference, "github_commit_outcome_unknown");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = GitHubFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            if (result.FailureCondition is GitHubApiFailureCondition.CancellationBeforeDispatch or GitHubApiFailureCondition.ReservationInvalidated)
            {
                await FinalizeNoDispatchAsync(operationReference, generation, mapped.Category, mapped.ReasonCode).ConfigureAwait(false);
                return CommitFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference);
            }

            if (mapped.Category == ProviderFailureCategory.UnknownProviderOutcome)
            {
                await RecordUnknownAsync(operationReference, generation, mapped.ReasonCode).ConfigureAwait(false);
                return CommitUnknown(request, safeTargetFingerprint, operationReference, mapped.ReasonCode);
            }

            string safeFailureFingerprint = CreateFailureFingerprint(
                "hxf-github:v1:commit-failure",
                request.AuthorizationEvidence.Fingerprint,
                operationReference,
                safeTargetFingerprint,
                request.IdempotencyAdmission.IntentFingerprint,
                mapped.Category,
                mapped.ReasonCode);
            await RecordKnownFailureAsync(operationReference, generation, mapped.Category, mapped.ReasonCode, safeFailureFingerprint, result.RetryAfter, result.CreatedCommitSha).ConfigureAwait(false);
            return CommitFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter, operationReference, safeTargetFingerprint: safeTargetFingerprint, safeOutcomeFingerprint: safeFailureFingerprint);
        }

        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(result.CommitSha))
        {
            await RecordUnknownAsync(operationReference, generation, "github_mutation_evidence_ambiguous").ConfigureAwait(false);
            return CommitUnknown(request, safeTargetFingerprint, operationReference, "github_mutation_evidence_ambiguous");
        }

        string safeCommitFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:commit-outcome",
            request.AuthorizationEvidence.Fingerprint,
            operationReference,
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            result.CommitSha);
        bool recorded = await RecordAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.RefUpdateConfirmed,
            result.CommitSha,
            safeCommitFingerprint,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);
        if (!recorded)
        {
            return CommitUnknown(request, safeTargetFingerprint, operationReference, "github_outcome_recording_failed");
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
            return StatusFailure(request, ProviderFailureCategory.ProviderTransientFailure, "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return StatusFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(request.CredentialModeRequirements, out ProviderCredentialMode credentialMode, out string? credentialFailure))
        {
            return StatusFailure(request, ProviderFailureCategory.ProviderValidationFailed, credentialFailure ?? "provider_validation_failed");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return StatusFailure(request, ProviderFailureCategory.ProviderValidationFailed, SafeReason(targetFailure, "provider_validation_failed"));
        }

        ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? sourceResolution = await ResolveStatusSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (sourceResolution is null || !sourceResolution.IsSuccess || sourceResolution.Source is null)
        {
            return SourceFailure(request, sourceResolution, "github_operation_status_source_unavailable");
        }

        ProviderOperationStatusResolvedSource source = sourceResolution.Source;
        if (!TryValidateResolvedSource(request, source, out string? sourceFailure))
        {
            return StatusFailure(request, ProviderFailureCategory.ProviderValidationFailed, sourceFailure ?? "github_operation_status_source_malformed");
        }

        GitHubCredentialResolutionResult credentialResult = await ResolveCredentialSafelyAsync(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            request.CredentialReferenceId,
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            credentialMode,
            cancellationToken).ConfigureAwait(false);
        if (!credentialResult.IsSuccess)
        {
            return StatusFailure(request, credentialResult.FailureCategory, SafeReason(credentialResult.ReasonCode, "github_status_evidence_unavailable"), credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient? client = await CreateClientSafelyAsync(request.ProviderBindingRef, request.CorrelationId, credentialMode, credential, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return StatusFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_status_evidence_unavailable");
        }

        GitHubOperationStatusResult? result;
        try
        {
            result = await client.GetOperationStatusAsync(
                new GitHubOperationStatusRequest(source.Target, source.IntendedCommitSha),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = null;
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        bool exhausted = request.CheckNumber == 5 || _timeProvider.GetUtcNow() - request.ReconciliationStartedAt >= ReconciliationWindow;
        if (result is null || !result.IsSuccess)
        {
            TimeSpan? retryAfter = result?.RetryAfter;
            return exhausted
                ? StatusFailure(request, ProviderFailureCategory.ReconciliationRequired, "github_reconciliation_checks_exhausted")
                : StatusUnavailable(request, retryAfter);
        }

        if (!Enum.IsDefined(result.Status) || result.Status == ProviderOperationStatusKind.Unavailable)
        {
            return exhausted
                ? StatusFailure(request, ProviderFailureCategory.ReconciliationRequired, "github_reconciliation_checks_exhausted")
                : StatusUnavailable(request, result.RetryAfter);
        }

        string safeObservedFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:status-observation",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            safeTargetEvidence.Metadata["safe_target_fingerprint"],
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
                "github_status_evidence_conflicting",
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
            return StatusFailure(request, ProviderFailureCategory.ReconciliationRequired, "github_reconciliation_checks_exhausted", safeObservedFingerprint: safeObservedFingerprint);
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

    private async Task<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>?> ResolveFileMutationSourceAsync(ProviderFileMutationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveFileMutationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>?> ResolveCommitSourceAsync(ProviderCommitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveCommitAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>?> ResolveStatusSourceAsync(ProviderOperationStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _operationSourceResolver.ResolveStatusAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ProviderOperationReservationResult?> ReserveAsync(
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
        catch (Exception)
        {
            return null;
        }
    }

    private async ValueTask<bool> ValidateReservationAsync(string operationReference, long generation, string intentFingerprint, CancellationToken cancellationToken)
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
        catch (Exception)
        {
            return false;
        }
    }

    private async ValueTask<bool> RecordCreatedCommitAsync(string operationReference, long generation, string commitSha)
        => await RecordAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.CreatedCommit,
            commitSha,
            SafeOutcomeFingerprint: null,
            ProviderFailureCategory.None,
            "success")).ConfigureAwait(false);

    private async ValueTask<bool> RecordAsync(ProviderOperationOutcomeRecord record)
    {
        try
        {
            using CancellationTokenSource timeout = new(OutcomeRecordingTimeout);
            return await _operationOutcomeStore.RecordAsync(record, timeout.Token).ConfigureAwait(false) == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task RecordKnownFailureAsync(
        string operationReference,
        long generation,
        ProviderFailureCategory category,
        string reasonCode,
        string safeOutcomeFingerprint,
        TimeSpan? retryAfter,
        string? privateObjectId = null)
        => _ = await RecordAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.KnownTerminalFailure,
            ProviderGitOperationResolvedTarget.IsGitObjectId(privateObjectId) ? privateObjectId : null,
            safeOutcomeFingerprint,
            category,
            SafeReason(reasonCode, category.ToCategoryCode()),
            SafeRemediation(null, category),
            category.IsRetryableByDefault(),
            SafeRetryAfter(retryAfter))).ConfigureAwait(false);

    private async Task RecordUnknownAsync(string operationReference, long generation, string reasonCode)
        => _ = await RecordAsync(new ProviderOperationOutcomeRecord(
            operationReference,
            generation,
            ProviderOperationOutcomeKind.Unknown,
            PrivateObjectId: null,
            SafeOutcomeFingerprint: null,
            ProviderFailureCategory.UnknownProviderOutcome,
            SafeReason(reasonCode, "github_mutation_outcome_unknown"),
            "reconciliation_required_metadata_only",
            Retryable: false,
            RetryAfter: null,
            ReconciliationReference: operationReference)).ConfigureAwait(false);

    private async Task FinalizeNoDispatchAsync(string operationReference, long generation, ProviderFailureCategory category, string reasonCode)
    {
        try
        {
            using CancellationTokenSource timeout = new(OutcomeRecordingTimeout);
            await _operationOutcomeStore.FinalizeNoDispatchAsync(new ProviderOperationOutcomeRecord(
                operationReference,
                generation,
                ProviderOperationOutcomeKind.NoDispatch,
                PrivateObjectId: null,
                SafeOutcomeFingerprint: null,
                category,
                SafeReason(reasonCode, category.ToCategoryCode()),
                SafeRemediation(null, category),
                category.IsRetryableByDefault()), timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private async Task<GitHubCredentialResolutionResult> ResolveCredentialSafelyAsync(
        string managedTenantId,
        string organizationId,
        string providerBindingRef,
        string credentialReferenceId,
        string authorizationFingerprint,
        string correlationId,
        ProviderCredentialMode credentialMode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _credentialResolver.ResolveAsync(new GitHubCredentialResolutionRequest(
                managedTenantId,
                organizationId,
                providerBindingRef,
                credentialReferenceId,
                credentialMode,
                authorizationFingerprint,
                correlationId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return GitHubCredentialResolutionResult.Failure(ProviderFailureCategory.ProviderUnavailable, "github_credential_resolution_unavailable");
        }
    }

    private async ValueTask<IGitHubApiClient?> CreateClientSafelyAsync(
        string providerBindingRef,
        string correlationId,
        ProviderCredentialMode credentialMode,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClientFactory.CreateAsync(new GitHubApiClientRequest(
                GitHubProviderConstants.ProductHeader,
                GitHubProviderConstants.RestApiVersion,
                credentialMode,
                providerBindingRef,
                correlationId), credential, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(ProviderFileMutationRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily, request.ProviderKey, request.ManagedTenantId, request.OrganizationId, request.FolderId,
            request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId,
            request.CorrelationId, request.TargetEvidence, request.AuthorizationEvidence, request.LockEvidence, request.RefPolicyEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!request.RefPolicyEvidence.AllowsFileMutation)
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "github_file_mutation_ref_policy_denied");
        }

        if (request.FilePolicyEvidence is null
            || !IsFresh(request.FilePolicyEvidence.FreshnessClass)
            || !IsSafeFingerprint(request.FilePolicyEvidence.Fingerprint)
            || request.FilePolicyEvidence.MaximumFileBytes is <= 0 or > MaximumFileBytes
            || request.FilePolicyEvidence.MaximumChangeCount is <= 0 or > MaximumChangeCount)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "github_file_policy_evidence_stale_or_malformed");
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsOperationAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeOpaqueReference(request.ChangeSetReference)
            || !IsSafeFingerprint(request.SafeChangeSetFingerprint)
            || request.Changes is null
            || request.Changes.Count is < 1 or > MaximumChangeCount
            || request.Changes.Count > request.FilePolicyEvidence.MaximumChangeCount)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_change_set_malformed");
        }

        HashSet<string> opaquePaths = new(StringComparer.Ordinal);
        for (int index = 0; index < request.Changes.Count; index++)
        {
            ProviderOrderedFileChange change = request.Changes[index];
            if (change is null
                || change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || !IsSafeOpaqueReference(change.PathReference)
                || !opaquePaths.Add(change.PathReference)
                || !IsSafeFingerprint(change.SafePathFingerprint)
                || (change.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    && (!IsSafeOpaqueReference(change.ContentReference) || !IsSafeFingerprint(change.SafeContentFingerprint)))
                || (change.Kind == ProviderFileChangeKind.Remove && (change.ContentReference is not null || change.SafeContentFingerprint is not null))
                || !IsAllowedByPolicy(change.Kind, request.FilePolicyEvidence))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "github_change_set_malformed");
            }
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(ProviderCommitRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily, request.ProviderKey, request.ManagedTenantId, request.OrganizationId, request.FolderId,
            request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId,
            request.CorrelationId, request.TargetEvidence, request.AuthorizationEvidence, request.LockEvidence, request.RefPolicyEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!request.RefPolicyEvidence.AllowsCommit || !request.RefPolicyEvidence.AllowsNonForceUpdate)
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "github_commit_ref_policy_denied");
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsOperationAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeOpaqueReference(request.StagedChangeSetReference)
            || !IsSafeFingerprint(request.SafeStagedChangeSetFingerprint)
            || !IsSafeOpaqueReference(request.CommitMessageReference)
            || !IsSafeFingerprint(request.SafeCommitMessageFingerprint)
            || !IsSafeFingerprint(request.SafeExpectedHeadFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_commit_intent_malformed");
        }

        return null;
    }

    private (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(ProviderOperationStatusRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily, request.ProviderKey, request.ManagedTenantId, request.OrganizationId, request.FolderId,
            request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId,
            request.CorrelationId, request.TargetEvidence, request.AuthorizationEvidence, request.LockEvidence, request.RefPolicyEvidence);
        if (common is not null)
        {
            return common;
        }

        if (request.IdempotencyKey is not null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "idempotency_key_not_allowed");
        }

        if (!IsSafeOpaqueReference(request.OperationReference)
            || !IsSafeFingerprint(request.SafeResolvedTargetFingerprint)
            || !IsSafeFingerprint(request.SafeFullRefFingerprint)
            || !IsSafeFingerprint(request.SafeExpectedHeadFingerprint)
            || !IsSafeFingerprint(request.SafeIntendedCommitFingerprint)
            || !IsSafeFingerprint(request.SafeCheckWindowFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_status_evidence_malformed");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (request.CheckNumber is < 1 or > 5
            || request.ReconciliationStartedAt > now
            || request.RequestedAt < request.ReconciliationStartedAt
            || request.RequestedAt > now.AddMinutes(1)
            || now - request.ReconciliationStartedAt >= ReconciliationWindow)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "github_reconciliation_budget_exhausted");
        }

        if (!GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeCheckWindowFingerprint, GitHubOperationSourceBindings.CheckWindow(request)))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_status_evidence_malformed");
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateOperationBoundary(
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
        ProviderRefPolicyEvidence refPolicyEvidence)
    {
        try
        {
            if (!string.Equals(ProviderIdentityIdentifier.Normalize(providerFamily), GitHubProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(ProviderIdentityIdentifier.Normalize(providerKey), GitHubProviderConstants.ProviderKey, StringComparison.Ordinal))
            {
                return (ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family");
            }
        }
        catch (ArgumentException)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed");
        }

        if (!IsSafeOpaqueValue(managedTenantId)
            || !IsSafeOpaqueValue(organizationId)
            || !IsSafeOpaqueValue(folderId)
            || !IsSafeOpaqueValue(delegatedTaskId)
            || !IsSafeOpaqueReference(providerBindingRef)
            || !IsSafeOpaqueReference(credentialReferenceId)
            || !IsSafeOpaqueReference(repositoryBindingId)
            || !IsCanonicalUlid(correlationId)
            || targetEvidence is null
            || authorizationEvidence is null
            || lockEvidence is null
            || refPolicyEvidence is null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_operation_evidence_malformed");
        }

        if (!IsFresh(authorizationEvidence.FreshnessClass) || !IsSafeOpaqueValue(authorizationEvidence.Fingerprint))
        {
            return (ProviderFailureCategory.ReconciliationRequired, "authorization_evidence_stale");
        }

        if (targetEvidence.IsStale)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale");
        }

        if (!IsFresh(lockEvidence.FreshnessClass)
            || !IsSafeFingerprint(lockEvidence.Fingerprint)
            || !lockEvidence.IsOwnedByDelegatedTask
            || lockEvidence.IsRevoked)
        {
            return (ProviderFailureCategory.ProviderConflict, "canonical_lock_evidence_invalid");
        }

        if (!IsFresh(refPolicyEvidence.FreshnessClass) || !IsSafeFingerprint(refPolicyEvidence.Fingerprint))
        {
            return (ProviderFailureCategory.ReconciliationRequired, "ref_policy_evidence_stale_or_malformed");
        }

        return null;
    }

    private static bool TryValidateResolvedSource(ProviderFileMutationRequest request, ProviderFileMutationResolvedSource source, out string? failureReason)
    {
        failureReason = "github_file_mutation_source_malformed";
        if (source.Target is null
            || !source.Target.TryValidate(out _)
            || source.Changes is null
            || source.Changes.Count != request.Changes.Count
            || !GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeResolvedTargetFingerprint, GitHubOperationSourceBindings.ResolvedTarget(request, source.Target)))
        {
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        long aggregateBytes = 0;
        for (int index = 0; index < source.Changes.Count; index++)
        {
            ProviderResolvedFileChange resolved = source.Changes[index];
            ProviderOrderedFileChange declared = request.Changes[index];
            if (resolved is null
                || resolved.Sequence != index
                || resolved.Sequence != declared.Sequence
                || resolved.Kind != declared.Kind
                || resolved.ContentType != declared.ContentType
                || !IsSafeGitPath(resolved.Path)
                || !paths.Add(resolved.Path)
                || HasAncestorConflict(paths, resolved.Path)
                || resolved.Content.Length > request.FilePolicyEvidence.MaximumFileBytes
                || resolved.Content.Length > MaximumFileBytes
                || (resolved.Kind == ProviderFileChangeKind.Remove && !resolved.Content.IsEmpty)
                || !GitHubProviderSafeOperationEvidence.FixedTimeEquals(declared.SafePathFingerprint, GitHubOperationSourceBindings.Path(request, declared, resolved.Path))
                || (resolved.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    && !GitHubProviderSafeOperationEvidence.FixedTimeEquals(declared.SafeContentFingerprint, GitHubOperationSourceBindings.Content(request, declared, resolved.Content))))
            {
                return false;
            }

            if (aggregateBytes > MaximumAggregateContentBytes - resolved.Content.Length)
            {
                return false;
            }

            aggregateBytes += resolved.Content.Length;
        }

        return GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeChangeSetFingerprint, GitHubOperationSourceBindings.ChangeSet(request, source.Changes));
    }

    private static bool TryValidateResolvedSource(ProviderCommitRequest request, ProviderCommitResolvedSource source, out string? failureReason)
    {
        failureReason = "github_commit_source_malformed";
        return source.Target is not null
            && source.Target.TryValidate(out _)
            && ProviderGitOperationResolvedTarget.IsGitObjectId(source.TreeSha)
            && !string.IsNullOrWhiteSpace(source.CommitMessage)
            && source.CommitMessage.Length <= 65536
            && !source.CommitMessage.Contains('\0', StringComparison.Ordinal)
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeResolvedTargetFingerprint, GitHubOperationSourceBindings.ResolvedTarget(request, source.Target))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeStagedChangeSetFingerprint, GitHubOperationSourceBindings.StagedTree(request, source.TreeSha))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeCommitMessageFingerprint, GitHubOperationSourceBindings.CommitMessage(request, source.CommitMessage))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeExpectedHeadFingerprint, GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha));
    }

    private static bool TryValidateResolvedSource(ProviderOperationStatusRequest request, ProviderOperationStatusResolvedSource source, out string? failureReason)
    {
        failureReason = "github_operation_status_source_malformed";
        return source.Target is not null
            && source.Target.TryValidate(out _)
            && ProviderGitOperationResolvedTarget.IsGitObjectId(source.IntendedCommitSha)
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeResolvedTargetFingerprint, GitHubOperationSourceBindings.ResolvedTarget(request, source.Target))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeFullRefFingerprint, GitHubOperationSourceBindings.FullRef(request, source.Target.FullRef))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeExpectedHeadFingerprint, GitHubOperationSourceBindings.ExpectedHead(request, source.Target.ExpectedHeadSha))
            && GitHubProviderSafeOperationEvidence.FixedTimeEquals(request.SafeIntendedCommitFingerprint, GitHubOperationSourceBindings.IntendedCommit(request, source.IntendedCommitSha));
    }

    private static ProviderFileMutationResult? ReplayOrReject(ProviderFileMutationRequest request, string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
        };

    private static ProviderCommitResult? ReplayOrReject(ProviderCommitRequest request, string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
        };

    private static ProviderFileMutationResult Replay(ProviderFileMutationRequest request, string safeTargetFingerprint, ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => new ProviderFileMutationResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, null),
            ProviderPriorOutcomeDisposition.Unknown => new ProviderFileMutationResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(admission.PriorReasonCode, "github_mutation_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, admission.PriorReconciliationReference),
            _ => FileMutationFailure(request, admission.PriorFailureCategory, admission.PriorReasonCode!, admission.PriorRetryAfter, admission.PriorOperationReference, true, admission.PriorRemediationCode, admission.PriorRetryable, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint),
        };

    private static ProviderCommitResult Replay(ProviderCommitRequest request, string safeTargetFingerprint, ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => new ProviderCommitResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, null),
            ProviderPriorOutcomeDisposition.Unknown => new ProviderCommitResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(admission.PriorReasonCode, "github_commit_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint, admission.PriorOperationReference, admission.PriorReconciliationReference),
            _ => CommitFailure(request, admission.PriorFailureCategory, admission.PriorReasonCode!, admission.PriorRetryAfter, admission.PriorOperationReference, true, admission.PriorRemediationCode, admission.PriorRetryable, safeTargetFingerprint, admission.PriorSafeOutcomeFingerprint),
        };

    private static ProviderFileMutationResult? MapReservation(ProviderFileMutationRequest request, string safeTargetFingerprint, ProviderOperationReservationResult? reservation)
    {
        if (reservation is null || reservation.Disposition == ProviderOperationReservationDisposition.Unavailable)
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderConfigurationMissing, SafeReason(reservation?.ReasonCode, "github_operation_outcome_store_unavailable"));
        }

        if (!IsReservationWellFormed(reservation))
        {
            return FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_operation_outcome_store_unavailable");
        }

        if (reservation.Disposition == ProviderOperationReservationDisposition.Acquired
            && IsSafeOpaqueReference(reservation.OperationReference)
            && reservation.Generation > 0)
        {
            return null;
        }

        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Pending => FileMutationUnknown(request, safeTargetFingerprint, reservation.OperationReference!, "github_operation_pending"),
            ProviderOperationReservationDisposition.ReplaySuccess => new ProviderFileMutationResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, null),
            ProviderOperationReservationDisposition.ReplayUnknown => new ProviderFileMutationResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(reservation.ReasonCode, "github_mutation_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, reservation.ReconciliationReference ?? reservation.OperationReference),
            ProviderOperationReservationDisposition.ReplayKnownFailure => FileMutationFailure(request, reservation.FailureCategory, reservation.ReasonCode!, reservation.RetryAfter, reservation.OperationReference, true, reservation.RemediationCode, reservation.Retryable, safeTargetFingerprint, reservation.SafeOutcomeFingerprint),
            ProviderOperationReservationDisposition.Conflict => FileMutationFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict", operationReference: reservation.OperationReference),
            _ => FileMutationFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_operation_outcome_store_unavailable"),
        };
    }

    private static ProviderCommitResult? MapReservation(ProviderCommitRequest request, string safeTargetFingerprint, ProviderOperationReservationResult? reservation)
    {
        if (reservation is null || reservation.Disposition == ProviderOperationReservationDisposition.Unavailable)
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderConfigurationMissing, SafeReason(reservation?.ReasonCode, "github_operation_outcome_store_unavailable"));
        }

        if (!IsReservationWellFormed(reservation))
        {
            return CommitFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_operation_outcome_store_unavailable");
        }

        if (reservation.Disposition == ProviderOperationReservationDisposition.Acquired
            && IsSafeOpaqueReference(reservation.OperationReference)
            && reservation.Generation > 0)
        {
            return null;
        }

        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Pending => CommitUnknown(request, safeTargetFingerprint, reservation.OperationReference!, "github_operation_pending"),
            ProviderOperationReservationDisposition.ReplaySuccess => new ProviderCommitResult(true, true, ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode(), "existing_equivalent", "none", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, null),
            ProviderOperationReservationDisposition.ReplayUnknown => new ProviderCommitResult(false, true, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(reservation.ReasonCode, "github_commit_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, reservation.SafeOutcomeFingerprint, reservation.OperationReference, reservation.ReconciliationReference ?? reservation.OperationReference),
            ProviderOperationReservationDisposition.ReplayKnownFailure => CommitFailure(request, reservation.FailureCategory, reservation.ReasonCode!, reservation.RetryAfter, reservation.OperationReference, true, reservation.RemediationCode, reservation.Retryable, safeTargetFingerprint, reservation.SafeOutcomeFingerprint),
            ProviderOperationReservationDisposition.Conflict => CommitFailure(request, ProviderFailureCategory.ProviderConflict, "idempotency_conflict", operationReference: reservation.OperationReference),
            _ => CommitFailure(request, ProviderFailureCategory.ProviderUnavailable, "github_operation_outcome_store_unavailable"),
        };
    }

    private static ProviderFileMutationResult SourceFailure(ProviderFileMutationRequest request, ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? resolution, string fallback)
        => FileMutationFailure(request, resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable, resolution?.GetSafeReasonCode(fallback) ?? fallback, resolution?.SafeRetryAfter);

    private static ProviderCommitResult SourceFailure(ProviderCommitRequest request, ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? resolution, string fallback)
        => CommitFailure(request, resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable, resolution?.GetSafeReasonCode(fallback) ?? fallback, resolution?.SafeRetryAfter);

    private static ProviderOperationStatusResult SourceFailure(ProviderOperationStatusRequest request, ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? resolution, string fallback)
        => StatusFailure(request, resolution?.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable) ?? ProviderFailureCategory.ProviderUnavailable, resolution?.GetSafeReasonCode(fallback) ?? fallback, resolution?.SafeRetryAfter);

    private static ProviderFileMutationResult FileMutationUnknown(ProviderFileMutationRequest request, string safeTargetFingerprint, string operationReference, string reasonCode)
        => new(false, false, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(reasonCode, "github_mutation_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, IsSafeOpaqueReference(operationReference) ? operationReference : null, IsSafeOpaqueReference(operationReference) ? operationReference : null);

    private static ProviderCommitResult CommitUnknown(ProviderCommitRequest request, string safeTargetFingerprint, string operationReference, string reasonCode)
        => new(false, false, ProviderFailureCategory.UnknownProviderOutcome, ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(), SafeReason(reasonCode, "github_commit_outcome_unknown"), "reconciliation_required_metadata_only", false, null, request.CorrelationId, safeTargetFingerprint, null, IsSafeOpaqueReference(operationReference) ? operationReference : null, IsSafeOpaqueReference(operationReference) ? operationReference : null);

    private static ProviderFileMutationResult FileMutationFailure(ProviderFileMutationRequest request, ProviderFailureCategory category, string reasonCode, TimeSpan? retryAfter = null, string? operationReference = null, bool equivalentReplay = false, string? remediationCode = null, bool? retryable = null, string? safeTargetFingerprint = null, string? safeOutcomeFingerprint = null)
        => new(false, equivalentReplay, category, category.ToCategoryCode(), SafeReason(reasonCode, category.ToCategoryCode()), SafeRemediation(remediationCode, category), retryable ?? category.IsRetryableByDefault(), SafeRetryAfter(retryAfter), request.CorrelationId, IsSafeFingerprint(safeTargetFingerprint) ? safeTargetFingerprint : null, IsSafeFingerprint(safeOutcomeFingerprint) ? safeOutcomeFingerprint : null, IsSafeOpaqueReference(operationReference) ? operationReference : null, null);

    private static ProviderCommitResult CommitFailure(ProviderCommitRequest request, ProviderFailureCategory category, string reasonCode, TimeSpan? retryAfter = null, string? operationReference = null, bool equivalentReplay = false, string? remediationCode = null, bool? retryable = null, string? safeTargetFingerprint = null, string? safeOutcomeFingerprint = null)
        => new(false, equivalentReplay, category, category.ToCategoryCode(), SafeReason(reasonCode, category.ToCategoryCode()), SafeRemediation(remediationCode, category), retryable ?? category.IsRetryableByDefault(), SafeRetryAfter(retryAfter), request.CorrelationId, IsSafeFingerprint(safeTargetFingerprint) ? safeTargetFingerprint : null, IsSafeFingerprint(safeOutcomeFingerprint) ? safeOutcomeFingerprint : null, IsSafeOpaqueReference(operationReference) ? operationReference : null, null);

    private static ProviderOperationStatusResult StatusUnavailable(ProviderOperationStatusRequest request, TimeSpan? retryAfter)
        => new(false, ProviderOperationStatusKind.Unavailable, ProviderFailureCategory.ProviderUnavailable, ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(), "github_status_evidence_unavailable", ProviderFailureCategory.ProviderUnavailable.ToCategoryCode() + "_remediation", true, SafeRetryAfter(retryAfter), request.CorrelationId, request.CheckNumber, null, request.OperationReference);

    private static ProviderOperationStatusResult StatusFailure(ProviderOperationStatusRequest request, ProviderFailureCategory category, string reasonCode, TimeSpan? retryAfter = null, string? safeObservedFingerprint = null)
        => new(false, ProviderOperationStatusKind.Unavailable, category, category.ToCategoryCode(), SafeReason(reasonCode, category.ToCategoryCode()), SafeRemediation(null, category), category.IsRetryableByDefault(), SafeRetryAfter(retryAfter), request.CorrelationId, request.CheckNumber, safeObservedFingerprint, IsSafeOpaqueReference(request.OperationReference) ? request.OperationReference : null);

    private static bool IsOperationAdmissionWellFormed(ProviderIdempotencyAdmission? admission)
    {
        if (admission is null || !Enum.IsDefined(admission.Disposition) || !IsSafeOpaqueValue(admission.IntentFingerprint))
        {
            return false;
        }

        if (admission.Disposition != ProviderIdempotencyDisposition.EquivalentReplay)
        {
            return true;
        }

        if (admission.PriorOutcomeDisposition is null
            || !Enum.IsDefined(admission.PriorOutcomeDisposition.Value)
            || !IsSafeOpaqueReference(admission.PriorOperationReference))
        {
            return false;
        }

        return admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint) && admission.PriorReconciliationReference is null,
            ProviderPriorOutcomeDisposition.Unknown => IsSafeOpaqueReference(admission.PriorReconciliationReference),
            ProviderPriorOutcomeDisposition.KnownFailure => admission.PriorFailureCategory != ProviderFailureCategory.None
                && Enum.IsDefined(admission.PriorFailureCategory)
                && IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && AllowedOperationReasonCodes.Contains(admission.PriorReasonCode ?? string.Empty)
                && (admission.PriorRemediationCode is null || AllowedOperationRemediationCodes.Contains(admission.PriorRemediationCode))
                && (admission.PriorRetryAfter is null || SafeRetryAfter(admission.PriorRetryAfter) == admission.PriorRetryAfter),
            _ => false,
        };
    }

    private static bool IsReservationWellFormed(ProviderOperationReservationResult reservation)
    {
        if (!Enum.IsDefined(reservation.Disposition))
        {
            return false;
        }

        bool hasOperationIdentity = IsSafeOpaqueReference(reservation.OperationReference);
        return reservation.Disposition switch
        {
            ProviderOperationReservationDisposition.Acquired or ProviderOperationReservationDisposition.Pending
                => hasOperationIdentity && reservation.Generation > 0,
            ProviderOperationReservationDisposition.ReplaySuccess
                => hasOperationIdentity && IsSafeFingerprint(reservation.SafeOutcomeFingerprint),
            ProviderOperationReservationDisposition.ReplayUnknown
                => hasOperationIdentity && IsSafeOpaqueReference(reservation.ReconciliationReference),
            ProviderOperationReservationDisposition.ReplayKnownFailure
                => hasOperationIdentity
                    && reservation.FailureCategory != ProviderFailureCategory.None
                    && Enum.IsDefined(reservation.FailureCategory)
                    && IsSafeFingerprint(reservation.SafeOutcomeFingerprint)
                    && AllowedOperationReasonCodes.Contains(reservation.ReasonCode ?? string.Empty)
                    && (reservation.RemediationCode is null || AllowedOperationRemediationCodes.Contains(reservation.RemediationCode))
                    && (reservation.RetryAfter is null || SafeRetryAfter(reservation.RetryAfter) == reservation.RetryAfter),
            ProviderOperationReservationDisposition.Conflict
                => reservation.OperationReference is null || hasOperationIdentity,
            _ => false,
        };
    }

    private static bool IsFresh(string? freshnessClass)
        => string.Equals(freshnessClass, "fresh", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedByPolicy(ProviderFileChangeKind kind, ProviderFilePolicyEvidence policy)
        => kind switch
        {
            ProviderFileChangeKind.Add => policy.AllowsAdd,
            ProviderFileChangeKind.Change => policy.AllowsChange,
            ProviderFileChangeKind.Remove => policy.AllowsRemove,
            _ => false,
        };

    private static bool IsSafeGitPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && path.Length <= 4096
            && path[0] != '/'
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains("\\", StringComparison.Ordinal)
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

    private static bool HasAncestorConflict(HashSet<string> paths, string candidate)
        => paths.Any(path => !string.Equals(path, candidate, StringComparison.Ordinal)
            && (path.StartsWith(candidate + "/", StringComparison.Ordinal)
                || candidate.StartsWith(path + "/", StringComparison.Ordinal)));

    private static bool IsSafeOpaqueReference(string? value)
        => value is { Length: > 0 and <= 128 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static bool IsCanonicalUlid(string? value)
        => value is { Length: 26 }
            && value[0] is >= '0' and <= '7'
            && value.All(static character => character is >= '0' and <= '9'
                || character is >= 'A' and <= 'H'
                || character is >= 'J' and <= 'K'
                || character is >= 'M' and <= 'N'
                || character is >= 'P' and <= 'T'
                || character is >= 'V' and <= 'Z');

    private static string SafeReason(string? reasonCode, string fallback)
        => reasonCode is not null && AllowedOperationReasonCodes.Contains(reasonCode) ? reasonCode : fallback;

    private static string SafeRemediation(string? remediationCode, ProviderFailureCategory category)
        => remediationCode is not null && AllowedOperationRemediationCodes.Contains(remediationCode)
                ? remediationCode
                : category == ProviderFailureCategory.ReconciliationRequired || category == ProviderFailureCategory.UnknownProviderOutcome
                    ? "reconciliation_required_metadata_only"
                    : category.ToCategoryCode() + "_remediation";

    private static TimeSpan? SafeRetryAfter(TimeSpan? retryAfter)
        => retryAfter is { } value && value > TimeSpan.Zero && value <= TimeSpan.FromHours(24) ? value : null;

    private static string CreateFailureFingerprint(
        string domain,
        string authorizationFingerprint,
        string operationReference,
        string safeTargetFingerprint,
        string intentFingerprint,
        ProviderFailureCategory category,
        string reasonCode)
        => GitHubProviderSafeOperationEvidence.Create(
            domain,
            authorizationFingerprint,
            operationReference,
            safeTargetFingerprint,
            intentFingerprint,
            category.ToCategoryCode(),
            SafeReason(reasonCode, category.ToCategoryCode()));
}
