using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

public sealed partial class GitHubProvider
{
    public async Task<ProviderFileMutationResult> StageFileChangesAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return FileMutationFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(
            request,
            credentialMode,
            out ProviderTargetEvidence? safeTargetEvidence,
            out string? targetFailure))
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderFileMutationResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? sourceResolution;
        try
        {
            sourceResolution = await _operationSourceResolver.ResolveFileMutationAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_file_mutation_source_unavailable");
        }

        if (sourceResolution is null)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_file_mutation_source_unavailable");
        }

        if (!sourceResolution.IsSuccess)
        {
            return FileMutationFailure(
                request,
                sourceResolution.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable),
                sourceResolution.GetSafeReasonCode("github_file_mutation_source_unavailable"),
                sourceResolution.SafeRetryAfter);
        }

        ProviderFileMutationResolvedSource? source = sourceResolution.Source;
        if (source is null)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_file_mutation_source_unavailable");
        }
        if (!TryValidateResolvedSource(request, source, out string? sourceFailure))
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                sourceFailure ?? "github_file_mutation_source_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                credentialMode,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return FileMutationFailure(
                request,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient client;
        try
        {
            client = await CreateClientAsync(
                request.ProviderBindingRef,
                request.CorrelationId,
                credentialMode,
                credential,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_client_creation_unavailable");
        }

        GitHubFileMutationResult? result;
        try
        {
            result = await client.StageFileChangesAsync(
                new GitHubFileMutationRequest(source.Target, source.Changes),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FileMutationUnknown(request, safeTargetFingerprint, "github_file_mutation_outcome_unknown");
        }
        catch (Exception)
        {
            return FileMutationUnknown(request, safeTargetFingerprint, "github_file_mutation_outcome_unknown");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result is null)
        {
            return FileMutationUnknown(request, safeTargetFingerprint, "github_file_mutation_outcome_unknown");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = GitHubFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            return mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? FileMutationUnknown(request, safeTargetFingerprint, mapped.ReasonCode)
                : FileMutationFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter);
        }

        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(result.TreeSha))
        {
            return FileMutationUnknown(request, safeTargetFingerprint, "github_mutation_evidence_ambiguous");
        }

        string treeSha = result.TreeSha!;
        string safeOutcomeFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "github-file-mutation-v1",
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            treeSha);
        string operationReference = GitHubProviderSafeOperationEvidence.Create(
            "github-staged-operation-v1",
            safeOutcomeFingerprint,
            request.CorrelationId);
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
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return CommitFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(
            request,
            credentialMode,
            out ProviderTargetEvidence? safeTargetEvidence,
            out string? targetFailure))
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderCommitResult? admissionResult = ReplayOrReject(request, safeTargetFingerprint);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? sourceResolution;
        try
        {
            sourceResolution = await _operationSourceResolver.ResolveCommitAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_commit_source_unavailable");
        }

        if (sourceResolution is null)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_commit_source_unavailable");
        }

        if (!sourceResolution.IsSuccess)
        {
            return CommitFailure(
                request,
                sourceResolution.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable),
                sourceResolution.GetSafeReasonCode("github_commit_source_unavailable"),
                sourceResolution.SafeRetryAfter);
        }

        ProviderCommitResolvedSource? source = sourceResolution.Source;
        if (source is null)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_commit_source_unavailable");
        }
        if (!TryValidateResolvedSource(source, out string? sourceFailure))
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                sourceFailure ?? "github_commit_source_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                credentialMode,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return CommitFailure(
                request,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient client;
        try
        {
            client = await CreateClientAsync(
                request.ProviderBindingRef,
                request.CorrelationId,
                credentialMode,
                credential,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return CommitFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_client_creation_unavailable");
        }

        GitHubCommitResult? result;
        try
        {
            result = await client.CommitAsync(
                new GitHubCommitRequest(source.Target, source.TreeSha, source.CommitMessage),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CommitUnknown(request, safeTargetFingerprint, "github_commit_outcome_unknown");
        }
        catch (Exception)
        {
            return CommitUnknown(request, safeTargetFingerprint, "github_commit_outcome_unknown");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result is null)
        {
            return CommitUnknown(request, safeTargetFingerprint, "github_commit_outcome_unknown");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = GitHubFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            return mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? CommitUnknown(request, safeTargetFingerprint, mapped.ReasonCode, result.CreatedCommitSha)
                : CommitFailure(request, mapped.Category, mapped.ReasonCode, result.RetryAfter);
        }

        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(result.CommitSha))
        {
            return CommitUnknown(request, safeTargetFingerprint, "github_mutation_evidence_ambiguous", result.CreatedCommitSha);
        }

        string commitSha = result.CommitSha!;
        string safeCommitFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "github-commit-v1",
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            commitSha);
        string operationReference = GitHubProviderSafeOperationEvidence.Create(
            "github-commit-operation-v1",
            safeCommitFingerprint,
            request.CorrelationId);
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
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } failure)
        {
            return StatusFailure(request, failure.Category, failure.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(
            request,
            credentialMode,
            out ProviderTargetEvidence? safeTargetEvidence,
            out string? targetFailure))
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? sourceResolution;
        try
        {
            sourceResolution = await _operationSourceResolver.ResolveStatusAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_operation_status_source_unavailable");
        }

        if (sourceResolution is null)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_operation_status_source_unavailable");
        }

        if (!sourceResolution.IsSuccess)
        {
            return StatusFailure(
                request,
                sourceResolution.GetSafeFailureCategory(ProviderFailureCategory.ProviderUnavailable),
                sourceResolution.GetSafeReasonCode("github_operation_status_source_unavailable"),
                sourceResolution.SafeRetryAfter);
        }

        ProviderOperationStatusResolvedSource? source = sourceResolution.Source;
        if (source is null)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_operation_status_source_unavailable");
        }
        if (!source.Target.TryValidate(out string? targetSourceFailure)
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(source.IntendedCommitSha))
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetSourceFailure ?? "github_operation_status_source_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                credentialMode,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return StatusFailure(
                request,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        IGitHubApiClient client;
        try
        {
            client = await CreateClientAsync(
                request.ProviderBindingRef,
                request.CorrelationId,
                credentialMode,
                credential,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_operation_cancelled_before_dispatch");
        }
        catch (Exception)
        {
            await credential.DisposeAsync().ConfigureAwait(false);
            return StatusFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_client_creation_unavailable");
        }

        GitHubOperationStatusResult? result;
        try
        {
            result = await client.GetOperationStatusAsync(
                new GitHubOperationStatusRequest(source.Target, source.IntendedCommitSha),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return StatusFailure(
                request,
                request.CheckNumber == 5
                    ? ProviderFailureCategory.ReconciliationRequired
                    : ProviderFailureCategory.ProviderUnavailable,
                request.CheckNumber == 5
                    ? "github_reconciliation_checks_exhausted"
                    : "github_status_evidence_unavailable");
        }
        catch (Exception)
        {
            return StatusFailure(
                request,
                request.CheckNumber == 5
                    ? ProviderFailureCategory.ReconciliationRequired
                    : ProviderFailureCategory.ProviderUnavailable,
                request.CheckNumber == 5
                    ? "github_reconciliation_checks_exhausted"
                    : "github_status_evidence_unavailable");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result is null)
        {
            return StatusFailure(
                request,
                request.CheckNumber == 5 ? ProviderFailureCategory.ReconciliationRequired : ProviderFailureCategory.ProviderUnavailable,
                request.CheckNumber == 5 ? "github_reconciliation_checks_exhausted" : "github_status_evidence_unavailable");
        }

        if (!result.IsSuccess)
        {
            (ProviderFailureCategory Category, string ReasonCode) mapped = GitHubFailureMapper.ToProviderOperationFailure(result.FailureCondition);
            ProviderFailureCategory category = request.CheckNumber == 5
                && mapped.Category is ProviderFailureCategory.ProviderUnavailable
                    or ProviderFailureCategory.ProviderRateLimited
                    or ProviderFailureCategory.ProviderTransientFailure
                    or ProviderFailureCategory.UnknownProviderOutcome
                        ? ProviderFailureCategory.ReconciliationRequired
                        : mapped.Category == ProviderFailureCategory.UnknownProviderOutcome
                            ? ProviderFailureCategory.ProviderUnavailable
                            : mapped.Category;
            string reasonCode = category == ProviderFailureCategory.ReconciliationRequired
                ? "github_reconciliation_checks_exhausted"
                : mapped.ReasonCode;
            return StatusFailure(request, category, reasonCode, result.RetryAfter);
        }

        if (!Enum.IsDefined(result.Status)
            || result.Status == ProviderOperationStatusKind.Unavailable
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(result.ObservedSha))
        {
            return StatusFailure(
                request,
                request.CheckNumber == 5 ? ProviderFailureCategory.ReconciliationRequired : ProviderFailureCategory.ProviderFailureKnown,
                request.CheckNumber == 5 ? "github_reconciliation_checks_exhausted" : "github_malformed_response");
        }

        if (request.CheckNumber == 5 && result.Status == ProviderOperationStatusKind.NotApplied)
        {
            return StatusFailure(
                request,
                ProviderFailureCategory.ReconciliationRequired,
                "github_reconciliation_checks_exhausted");
        }

        string safeObservedFingerprint = GitHubProviderSafeOperationEvidence.Create(
            "github-status-observation-v1",
            safeTargetEvidence.Metadata["safe_target_fingerprint"],
            result.ObservedSha);
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

        return new ProviderOperationStatusResult(
            IsSuccess: true,
            result.Status,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            result.Status == ProviderOperationStatusKind.Confirmed ? "confirmed" : "not_applied",
            "none",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            request.CheckNumber,
            safeObservedFingerprint,
            request.OperationReference);
    }

    private async Task<GitHubCredentialResolutionResult> ResolveCredentialAsync(
        string managedTenantId,
        string organizationId,
        string providerBindingRef,
        string credentialReferenceId,
        string authorizationFingerprint,
        string correlationId,
        ProviderCredentialMode credentialMode,
        CancellationToken cancellationToken)
        => await _credentialResolver.ResolveAsync(
            new GitHubCredentialResolutionRequest(
                managedTenantId,
                organizationId,
                providerBindingRef,
                credentialReferenceId,
                credentialMode,
                authorizationFingerprint,
                correlationId),
            cancellationToken).ConfigureAwait(false);

    private async ValueTask<IGitHubApiClient> CreateClientAsync(
        string providerBindingRef,
        string correlationId,
        ProviderCredentialMode credentialMode,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken)
        => await _apiClientFactory.CreateAsync(
            new GitHubApiClientRequest(
                GitHubProviderConstants.ProductHeader,
                GitHubProviderConstants.RestApiVersion,
                credentialMode,
                providerBindingRef,
                correlationId),
            credential,
            cancellationToken).ConfigureAwait(false);

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(
        ProviderFileMutationRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
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
            request.RefPolicyEvidence);
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
            || request.FilePolicyEvidence.MaximumFileBytes < 0
            || request.FilePolicyEvidence.MaximumChangeCount <= 0)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "github_file_policy_evidence_stale_or_malformed");
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsReplayEvidenceWellFormed(request.IdempotencyAdmission))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_mutation_intent_malformed");
        }

        if (!IsSafeOpaqueValue(request.ChangeSetReference)
            || !IsSafeFingerprint(request.SafeChangeSetFingerprint)
            || request.Changes is null
            || request.Changes.Count == 0
            || request.Changes.Count > request.FilePolicyEvidence.MaximumChangeCount)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_change_set_malformed");
        }

        for (int index = 0; index < request.Changes.Count; index++)
        {
            ProviderOrderedFileChange change = request.Changes[index];
            if (change is null
                || change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || !IsSafeOpaqueValue(change.PathReference)
                || !IsSafeFingerprint(change.SafePathFingerprint)
                || (change.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change
                    && (!IsSafeOpaqueValue(change.ContentReference)
                        || !IsSafeFingerprint(change.SafeContentFingerprint)))
                || (change.Kind == ProviderFileChangeKind.Remove
                    && (change.ContentReference is not null || change.SafeContentFingerprint is not null))
                || !IsAllowedByPolicy(change.Kind, request.FilePolicyEvidence))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "github_change_set_malformed");
            }
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(
        ProviderCommitRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
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
            request.RefPolicyEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!request.RefPolicyEvidence.AllowsCommit || !request.RefPolicyEvidence.AllowsNonForceUpdate)
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "github_commit_ref_policy_denied");
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsAdmissionWellFormed(request.IdempotencyAdmission)
            || !IsReplayEvidenceWellFormed(request.IdempotencyAdmission)
            || !IsSafeOpaqueValue(request.StagedChangeSetReference)
            || !IsSafeFingerprint(request.SafeStagedChangeSetFingerprint)
            || !IsSafeOpaqueValue(request.CommitMessageReference))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_commit_intent_malformed");
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(
        ProviderOperationStatusRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
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
            request.RefPolicyEvidence);
        if (common is not null)
        {
            return common;
        }

        if (request.IdempotencyKey is not null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "idempotency_key_not_allowed");
        }

        if (!IsSafeFingerprint(request.OperationReference)
            || !IsSafeFingerprint(request.SafeExpectedHeadFingerprint)
            || !IsSafeFingerprint(request.SafeIntendedCommitFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_status_evidence_malformed");
        }

        if (request.CheckNumber is < 1 or > 5
            || request.RequestedAt < request.ReconciliationStartedAt
            || request.RequestedAt - request.ReconciliationStartedAt > TimeSpan.FromMinutes(15))
        {
            return (ProviderFailureCategory.ReconciliationRequired, "github_reconciliation_budget_exhausted");
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
            || !IsSafeOpaqueValue(providerBindingRef)
            || !IsSafeOpaqueValue(credentialReferenceId)
            || !IsSafeOpaqueValue(repositoryBindingId)
            || !IsSafeOpaqueValue(correlationId)
            || targetEvidence is null
            || authorizationEvidence is null
            || lockEvidence is null
            || refPolicyEvidence is null)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_operation_evidence_malformed");
        }

        if (!IsFresh(authorizationEvidence.FreshnessClass)
            || !IsSafeOpaqueValue(authorizationEvidence.Fingerprint))
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

        if (!IsFresh(refPolicyEvidence.FreshnessClass)
            || !IsSafeFingerprint(refPolicyEvidence.Fingerprint))
        {
            return (ProviderFailureCategory.ReconciliationRequired, "ref_policy_evidence_stale_or_malformed");
        }

        return null;
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

    private static bool TryValidateResolvedSource(
        ProviderFileMutationRequest request,
        ProviderFileMutationResolvedSource source,
        out string? failureReason)
    {
        failureReason = null;
        if (source.Target is null
            || !source.Target.TryValidate(out failureReason)
            || source.Changes is null
            || source.Changes.Count != request.Changes.Count)
        {
            failureReason ??= "github_file_mutation_source_malformed";
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
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
                || resolved.Content.Length > request.FilePolicyEvidence.MaximumFileBytes
                || (resolved.Kind == ProviderFileChangeKind.Remove && !resolved.Content.IsEmpty))
            {
                failureReason = "github_file_mutation_source_malformed";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateResolvedSource(
        ProviderCommitResolvedSource source,
        out string? failureReason)
    {
        failureReason = null;
        if (source.Target is null
            || !source.Target.TryValidate(out failureReason)
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(source.TreeSha)
            || string.IsNullOrWhiteSpace(source.CommitMessage)
            || source.CommitMessage.Length > 65536
            || source.CommitMessage.Contains('\0', StringComparison.Ordinal))
        {
            failureReason ??= "github_commit_source_malformed";
            return false;
        }

        return true;
    }

    private static bool IsSafeGitPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && path.Length <= 4096
            && path[0] != '/'
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains("\\", StringComparison.Ordinal)
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

    private static ProviderFileMutationResult? ReplayOrReject(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => new ProviderFileMutationResult(
                IsSuccess: true,
                EquivalentReplay: true,
                ProviderFailureCategory.None,
                ProviderFailureCategory.None.ToCategoryCode(),
                "existing_equivalent",
                "none",
                Retryable: false,
                RetryAfter: null,
                request.CorrelationId,
                safeTargetFingerprint,
                request.IdempotencyAdmission.PriorSafeOutcomeFingerprint,
                GitHubProviderSafeOperationEvidence.Create(
                    "github-file-mutation-replay-v1",
                    request.IdempotencyAdmission.PriorSafeOutcomeFingerprint,
                    request.IdempotencyAdmission.IntentFingerprint),
                request.IdempotencyAdmission.PriorReconciliationReference),
            ProviderIdempotencyDisposition.Conflict => FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => FileMutationFailure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_key_expired"),
        };

    private static ProviderCommitResult? ReplayOrReject(
        ProviderCommitRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => new ProviderCommitResult(
                IsSuccess: true,
                EquivalentReplay: true,
                ProviderFailureCategory.None,
                ProviderFailureCategory.None.ToCategoryCode(),
                "existing_equivalent",
                "none",
                Retryable: false,
                RetryAfter: null,
                request.CorrelationId,
                safeTargetFingerprint,
                request.IdempotencyAdmission.PriorSafeOutcomeFingerprint,
                GitHubProviderSafeOperationEvidence.Create(
                    "github-commit-replay-v1",
                    request.IdempotencyAdmission.PriorSafeOutcomeFingerprint,
                    request.IdempotencyAdmission.IntentFingerprint),
                request.IdempotencyAdmission.PriorReconciliationReference),
            ProviderIdempotencyDisposition.Conflict => CommitFailure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => CommitFailure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_key_expired"),
        };

    private static ProviderFileMutationResult FileMutationUnknown(
        ProviderFileMutationRequest request,
        string safeTargetFingerprint,
        string reasonCode)
    {
        string reconciliationReference = GitHubProviderSafeOperationEvidence.Create(
            "github-file-mutation-reconciliation-v1",
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            request.CorrelationId);
        return new ProviderFileMutationResult(
            IsSuccess: false,
            EquivalentReplay: false,
            ProviderFailureCategory.UnknownProviderOutcome,
            ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
            reasonCode,
            "reconciliation_required_metadata_only",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            safeTargetFingerprint,
            SafeOutcomeFingerprint: null,
            OpaqueOperationReference: null,
            reconciliationReference);
    }

    private static ProviderFileMutationResult FileMutationFailure(
        ProviderFileMutationRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(
            IsSuccess: false,
            EquivalentReplay: false,
            category,
            category.ToCategoryCode(),
            reasonCode,
            $"{category.ToCategoryCode()}_remediation",
            category.IsRetryableByDefault(),
            retryAfter,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeOutcomeFingerprint: null,
            OpaqueOperationReference: null,
            ReconciliationReference: null);

    private static ProviderCommitResult CommitUnknown(
        ProviderCommitRequest request,
        string safeTargetFingerprint,
        string reasonCode,
        string? createdCommitSha = null)
    {
        string reconciliationReference = GitHubProviderSafeOperationEvidence.Create(
            "github-commit-reconciliation-v1",
            safeTargetFingerprint,
            request.IdempotencyAdmission.IntentFingerprint,
            ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha) ? createdCommitSha : null,
            request.CorrelationId);
        return new ProviderCommitResult(
            IsSuccess: false,
            EquivalentReplay: false,
            ProviderFailureCategory.UnknownProviderOutcome,
            ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
            reasonCode,
            "reconciliation_required_metadata_only",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            safeTargetFingerprint,
            SafeCommitFingerprint: null,
            OpaqueOperationReference: null,
            reconciliationReference);
    }

    private static ProviderCommitResult CommitFailure(
        ProviderCommitRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(
            IsSuccess: false,
            EquivalentReplay: false,
            category,
            category.ToCategoryCode(),
            reasonCode,
            $"{category.ToCategoryCode()}_remediation",
            category.IsRetryableByDefault(),
            retryAfter,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeCommitFingerprint: null,
            OpaqueOperationReference: null,
            ReconciliationReference: null);

    private static ProviderOperationStatusResult StatusFailure(
        ProviderOperationStatusRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(
            IsSuccess: false,
            ProviderOperationStatusKind.Unavailable,
            category,
            category.ToCategoryCode(),
            reasonCode,
            category == ProviderFailureCategory.ReconciliationRequired
                ? "reconciliation_required_metadata_only"
                : $"{category.ToCategoryCode()}_remediation",
            category.IsRetryableByDefault(),
            retryAfter,
            request.CorrelationId,
            request.CheckNumber,
            SafeObservedFingerprint: null,
            IsSafeFingerprint(request.OperationReference) ? request.OperationReference : null);
}
