using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

public sealed partial class GitHubProvider
{
    private const int MaximumFileChanges = 100;
    private const long MaximumFileBytes = 1_048_576;
    private const int MaximumStatusChecks = 5;
    private static readonly TimeSpan MaximumStatusWindow = TimeSpan.FromMinutes(15);

    public async Task<ProviderFileChangeSetResult> StageFileChangesAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } denied)
        {
            return ProviderFileChangeSetResult.Failure(request, denied.Category, denied.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return ProviderFileChangeSetResult.Failure(
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
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderFileChangeSetResult? replay = ReplayOrReject(request, safeTargetFingerprint);
        if (replay is not null)
        {
            return replay;
        }

        ProviderGitChangeSetResolutionResult resolution;
        try
        {
            resolution = await _gitOperationResolver.ResolveChangeSetAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_file_change_set_source_cancelled");
        }
        catch (Exception)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_file_change_set_source_unavailable");
        }

        if (!resolution.IsSuccess)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                resolution.FailureCategory,
                SafeReasonCode(resolution.FailureCategory, resolution.ReasonCode),
                resolution.RetryAfter);
        }

        ProviderGitChangeSetResolvedInput input = resolution.Input.ShouldNotBeNullForProvider();
        if (!TryValidateResolvedInput(request, input, safeTargetFingerprint, out string? inputFailure))
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                inputFailure ?? "resolved_provider_change_set_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_credential_resolution_cancelled");
        }
        catch (Exception)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                credentialResult.FailureCategory,
                SafeReasonCode(credentialResult.FailureCategory, credentialResult.ReasonCode),
                credentialResult.RetryAfter);
        }

        string reconciliationReference = GitHubSafeTargetFingerprint.ComputeReconciliationReference(
            safeTargetFingerprint,
            request.ChangeSetReference,
            request.IdempotencyAdmission.IntentFingerprint);
        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubFileChangeSetResult result;
        try
        {
            IGitHubApiClient client;
            try
            {
                client = await CreateClientAsync(
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId,
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderFileChangeSetResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderTransientFailure,
                    "github_client_creation_cancelled");
            }
            catch (Exception)
            {
                return ProviderFileChangeSetResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "github_client_creation_unavailable");
            }

            try
            {
                result = await client.StageFileChangesAsync(
                    new GitHubFileChangeSetRequest(
                        input.Target,
                        input.ExpectedHeadSha,
                        input.Changes,
                        safeTargetFingerprint,
                        reconciliationReference),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderFileChangeSetResult.Failure(
                    request,
                    ProviderFailureCategory.UnknownProviderOutcome,
                    "github_file_mutation_outcome_unknown",
                    reconciliationReference: reconciliationReference);
            }
            catch (Exception)
            {
                return ProviderFileChangeSetResult.Failure(
                    request,
                    ProviderFailureCategory.UnknownProviderOutcome,
                    "github_file_mutation_outcome_unknown",
                    reconciliationReference: reconciliationReference);
            }
        }
        finally
        {
            await DisposeCredentialSafelyAsync(credential).ConfigureAwait(false);
        }

        if (!result.IsSuccess)
        {
            return MapFailure(request, result, reconciliationReference);
        }

        if (!TryGitSha(result.StagedTreeSha, out string? stagedTreeSha))
        {
            return ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "github_file_mutation_evidence_ambiguous",
                reconciliationReference: reconciliationReference);
        }

        string stagedFingerprint = GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
            request.OperationEvidence.RepositoryBindingFingerprint,
            request.ChangeSetReference,
            stagedTreeSha);
        return ProviderFileChangeSetResult.Success(request, safeTargetFingerprint, stagedFingerprint);
    }

    public async Task<ProviderCommitResult> CommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } denied)
        {
            return ProviderCommitResult.Failure(request, denied.Category, denied.ReasonCode);
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return ProviderCommitResult.Failure(
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
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderCommitResult? replay = ReplayOrReject(request, safeTargetFingerprint);
        if (replay is not null)
        {
            return replay;
        }

        ProviderGitCommitResolutionResult resolution;
        try
        {
            resolution = await _gitOperationResolver.ResolveCommitAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_commit_source_cancelled");
        }
        catch (Exception)
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_commit_source_unavailable");
        }

        if (!resolution.IsSuccess)
        {
            return ProviderCommitResult.Failure(
                request,
                resolution.FailureCategory,
                SafeReasonCode(resolution.FailureCategory, resolution.ReasonCode),
                resolution.RetryAfter);
        }

        ProviderGitCommitResolvedInput input = resolution.Input.ShouldNotBeNullForProvider();
        if (!TryValidateResolvedInput(request, input, safeTargetFingerprint, out string? inputFailure))
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                inputFailure ?? "resolved_provider_commit_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_credential_resolution_cancelled");
        }
        catch (Exception)
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return ProviderCommitResult.Failure(
                request,
                credentialResult.FailureCategory,
                SafeReasonCode(credentialResult.FailureCategory, credentialResult.ReasonCode),
                credentialResult.RetryAfter);
        }

        string reconciliationReference = GitHubSafeTargetFingerprint.ComputeReconciliationReference(
            safeTargetFingerprint,
            request.StagedChangeSetReference,
            request.IdempotencyAdmission.IntentFingerprint);
        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubCommitResult result;
        try
        {
            IGitHubApiClient client;
            try
            {
                client = await CreateClientAsync(
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId,
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderCommitResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderTransientFailure,
                    "github_client_creation_cancelled");
            }
            catch (Exception)
            {
                return ProviderCommitResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "github_client_creation_unavailable");
            }

            try
            {
                result = await client.CommitAsync(
                    new GitHubCommitRequest(
                        input.Target,
                        input.ExpectedHeadSha,
                        input.StagedTreeSha,
                        input.CommitMessage,
                        safeTargetFingerprint,
                        reconciliationReference),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderCommitResult.Failure(
                    request,
                    ProviderFailureCategory.UnknownProviderOutcome,
                    "github_commit_outcome_unknown",
                    reconciliationReference: reconciliationReference);
            }
            catch (Exception)
            {
                return ProviderCommitResult.Failure(
                    request,
                    ProviderFailureCategory.UnknownProviderOutcome,
                    "github_commit_outcome_unknown",
                    reconciliationReference: reconciliationReference);
            }
        }
        finally
        {
            await DisposeCredentialSafelyAsync(credential).ConfigureAwait(false);
        }

        if (!result.IsSuccess)
        {
            return MapFailure(request, result, reconciliationReference);
        }

        if (!TryGitSha(result.CommitSha, out string? commitSha))
        {
            return ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "github_commit_evidence_ambiguous",
                reconciliationReference: reconciliationReference);
        }

        string commitFingerprint = GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
            request.OperationEvidence.RepositoryBindingFingerprint,
            request.StagedChangeSetReference,
            commitSha);
        return ProviderCommitResult.Success(request, safeTargetFingerprint, commitFingerprint);
    }

    public async Task<ProviderMutationStatusResult> GetMutationStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        (ProviderFailureCategory Category, string ReasonCode)? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is { } denied)
        {
            return ProviderMutationStatusResult.Unavailable(request, denied.Category, denied.ReasonCode);
        }

        if (request.CheckNumber > MaximumStatusChecks
            || request.RequestedAt - request.UnknownOutcomeObservedAt > MaximumStatusWindow)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ReconciliationRequired,
                "github_status_reconciliation_exhausted");
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return ProviderMutationStatusResult.Unavailable(
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
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
        }

        string safeTargetFingerprint = safeTargetEvidence.Metadata["safe_target_fingerprint"];
        ProviderGitStatusResolutionResult resolution;
        try
        {
            resolution = await _gitOperationResolver.ResolveStatusAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_status_source_cancelled");
        }
        catch (Exception)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_status_source_unavailable");
        }

        if (!resolution.IsSuccess)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                resolution.FailureCategory,
                SafeReasonCode(resolution.FailureCategory, resolution.ReasonCode),
                resolution.RetryAfter);
        }

        ProviderGitStatusResolvedInput input = resolution.Input.ShouldNotBeNullForProvider();
        if (!TryValidateResolvedInput(request, input, safeTargetFingerprint, out string? inputFailure))
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                inputFailure ?? "resolved_provider_status_malformed");
        }

        GitHubCredentialResolutionResult credentialResult;
        try
        {
            credentialResult = await ResolveCredentialAsync(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderTransientFailure,
                "github_credential_resolution_cancelled");
        }
        catch (Exception)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "github_credential_resolution_unavailable");
        }

        if (!credentialResult.IsSuccess)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                credentialResult.FailureCategory,
                SafeReasonCode(credentialResult.FailureCategory, credentialResult.ReasonCode),
                credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubMutationStatusResult result;
        try
        {
            IGitHubApiClient client;
            try
            {
                client = await CreateClientAsync(
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId,
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderMutationStatusResult.Unavailable(
                    request,
                    ProviderFailureCategory.ProviderTransientFailure,
                    "github_client_creation_cancelled");
            }
            catch (Exception)
            {
                return ProviderMutationStatusResult.Unavailable(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "github_client_creation_unavailable");
            }

            try
            {
                result = await client.GetMutationStatusAsync(
                    new GitHubMutationStatusRequest(input.Target, input.ExpectedHeadSha, input.ExpectedCommitSha),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ProviderMutationStatusResult.Unavailable(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "github_status_unavailable");
            }
            catch (Exception)
            {
                return ProviderMutationStatusResult.Unavailable(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "github_status_unavailable");
            }
        }
        finally
        {
            await DisposeCredentialSafelyAsync(credential).ConfigureAwait(false);
        }

        if (result.Disposition == GitHubMutationStatusDisposition.Unavailable)
        {
            (ProviderFailureCategory Category, string ReasonCode) failure = GitHubFailureMapper.MapCondition(result.FailureCondition);
            ProviderFailureCategory safeCategory = failure.Category == ProviderFailureCategory.UnknownProviderOutcome
                ? ProviderFailureCategory.ProviderUnavailable
                : failure.Category;
            return ProviderMutationStatusResult.Unavailable(
                request,
                safeCategory,
                safeCategory == failure.Category ? failure.ReasonCode : "github_status_unavailable",
                result.RetryAfter);
        }

        ProviderMutationStatusDisposition? disposition = result.Disposition switch
        {
            GitHubMutationStatusDisposition.Confirmed => ProviderMutationStatusDisposition.Confirmed,
            GitHubMutationStatusDisposition.NotApplied => ProviderMutationStatusDisposition.NotApplied,
            GitHubMutationStatusDisposition.Conflicting => ProviderMutationStatusDisposition.Conflicting,
            _ => null,
        };
        if (disposition is null)
        {
            return ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "github_status_disposition_invalid");
        }

        return ProviderMutationStatusResult.Available(
            request,
            disposition.Value,
            disposition == ProviderMutationStatusDisposition.Confirmed
                ? request.SafeExpectedCommitFingerprint
                : null);
    }

    private async ValueTask<GitHubCredentialResolutionResult> ResolveCredentialAsync(
        string managedTenantId,
        string organizationId,
        string providerBindingRef,
        string credentialReferenceId,
        ProviderCredentialMode credentialMode,
        string authorizationFingerprint,
        string correlationId,
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
        ProviderCredentialMode credentialMode,
        string providerBindingRef,
        string correlationId,
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

    private static async ValueTask DisposeCredentialSafelyAsync(GitHubCredentialLease credential)
    {
        try
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Disposal is best effort after the access token has already been cleared by the lease.
        }
    }

    private static ProviderFileChangeSetResult? ReplayOrReject(
        ProviderFileChangeSetRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => ProviderFileChangeSetResult.Success(
                request,
                safeTargetFingerprint,
                request.IdempotencyAdmission.PriorSafeOutcomeFingerprint!,
                equivalentReplay: true,
                request.IdempotencyAdmission.PriorReconciliationReference),
            ProviderIdempotencyDisposition.Conflict => ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => ProviderFileChangeSetResult.Failure(
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
            ProviderIdempotencyDisposition.EquivalentReplay => ProviderCommitResult.Success(
                request,
                safeTargetFingerprint,
                request.IdempotencyAdmission.PriorSafeOutcomeFingerprint!,
                equivalentReplay: true,
                request.IdempotencyAdmission.PriorReconciliationReference),
            ProviderIdempotencyDisposition.Conflict => ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_key_expired"),
        };

    private static ProviderFileChangeSetResult MapFailure(
        ProviderFileChangeSetRequest request,
        GitHubFileChangeSetResult result,
        string reconciliationReference)
    {
        (ProviderFailureCategory Category, string ReasonCode) failure = GitHubFailureMapper.MapCondition(result.FailureCondition);
        return ProviderFileChangeSetResult.Failure(
            request,
            failure.Category,
            failure.ReasonCode,
            result.RetryAfter,
            failure.Category == ProviderFailureCategory.UnknownProviderOutcome ? reconciliationReference : null);
    }

    private static ProviderCommitResult MapFailure(
        ProviderCommitRequest request,
        GitHubCommitResult result,
        string reconciliationReference)
    {
        (ProviderFailureCategory Category, string ReasonCode) failure = GitHubFailureMapper.MapCondition(result.FailureCondition);
        string? safeExpectedCommitFingerprint = failure.Category == ProviderFailureCategory.UnknownProviderOutcome
            && TryGitSha(result.CommitSha, out string? commitSha)
                ? GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
                    request.OperationEvidence.RepositoryBindingFingerprint,
                    request.StagedChangeSetReference,
                    commitSha)
                : null;
        return ProviderCommitResult.Failure(
            request,
            failure.Category,
            failure.ReasonCode,
            result.RetryAfter,
            failure.Category == ProviderFailureCategory.UnknownProviderOutcome ? reconciliationReference : null,
            safeExpectedCommitFingerprint);
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(
        ProviderFileChangeSetRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.CredentialModeRequirements,
            request.AuthorizationEvidence,
            request.OperationEvidence,
            request.TargetEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsSafeOpaqueValue(request.ChangeSetReference)
            || request.IdempotencyAdmission is null
            || !Enum.IsDefined(request.IdempotencyAdmission.Disposition)
            || !IsSafeOpaqueValue(request.IdempotencyAdmission.IntentFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_mutation_intent_malformed");
        }

        if (request.IdempotencyAdmission.Disposition == ProviderIdempotencyDisposition.EquivalentReplay
            && (!IsSafeFingerprint(request.IdempotencyAdmission.PriorSafeOutcomeFingerprint)
                || (request.IdempotencyAdmission.PriorReconciliationReference is not null
                    && !IsSafeFingerprint(request.IdempotencyAdmission.PriorReconciliationReference))))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_replay_evidence_malformed");
        }

        if (request.Changes is null || request.Changes.Count is 0 or > MaximumFileChanges)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_file_change_count_invalid");
        }

        HashSet<string> operationReferences = new(StringComparer.Ordinal);
        foreach (ProviderFileChange change in request.Changes)
        {
            if (change is null
                || !operationReferences.Add(change.OperationReference)
                || !IsSafeOpaqueValue(change.OperationReference)
                || !IsSafeOpaqueValue(change.PathReference)
                || !Enum.IsDefined(change.Kind))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "github_file_change_metadata_invalid");
            }

            bool writesContent = change.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change;
            if (writesContent
                && (!IsSafeOpaqueValue(change.ContentReference)
                    || change.ByteLength < 0
                    || change.ByteLength > MaximumFileBytes
                    || !IsSafeMediaType(change.MediaType)))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "github_file_change_policy_invalid");
            }

            if (!writesContent
                && (!string.IsNullOrEmpty(change.ContentReference)
                    || change.ByteLength != 0
                    || !string.IsNullOrEmpty(change.MediaType)))
            {
                return (ProviderFailureCategory.ProviderValidationFailed, "github_file_remove_policy_invalid");
            }
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(ProviderCommitRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.CredentialModeRequirements,
            request.AuthorizationEvidence,
            request.OperationEvidence,
            request.TargetEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!IsSafeOpaqueValue(request.IdempotencyKey)
            || !IsSafeOpaqueValue(request.StagedChangeSetReference)
            || !IsSafeFingerprint(request.SafeStagedChangeSetFingerprint)
            || !IsSafeOpaqueValue(request.CommitMessageReference)
            || request.IdempotencyAdmission is null
            || !Enum.IsDefined(request.IdempotencyAdmission.Disposition)
            || !IsSafeOpaqueValue(request.IdempotencyAdmission.IntentFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_commit_intent_malformed");
        }

        if (request.IdempotencyAdmission.Disposition == ProviderIdempotencyDisposition.EquivalentReplay
            && (!IsSafeFingerprint(request.IdempotencyAdmission.PriorSafeOutcomeFingerprint)
                || (request.IdempotencyAdmission.PriorReconciliationReference is not null
                    && !IsSafeFingerprint(request.IdempotencyAdmission.PriorReconciliationReference))))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_replay_evidence_malformed");
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateBoundary(
        ProviderMutationStatusRequest request)
    {
        (ProviderFailureCategory Category, string ReasonCode)? common = ValidateOperationBoundary(
            request.ProviderFamily,
            request.ProviderKey,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.CorrelationId,
            request.CredentialModeRequirements,
            request.AuthorizationEvidence,
            request.OperationEvidence,
            request.TargetEvidence);
        if (common is not null)
        {
            return common;
        }

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "status_idempotency_key_forbidden");
        }

        if (!IsSafeOpaqueValue(request.OperationReference)
            || !IsSafeFingerprint(request.ReconciliationReference)
            || !IsSafeFingerprint(request.SafeExpectedCommitFingerprint)
            || request.CheckNumber < 1
            || request.RequestedAt < request.UnknownOutcomeObservedAt)
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_status_evidence_malformed");
        }

        return null;
    }

    private static (ProviderFailureCategory Category, string ReasonCode)? ValidateOperationBoundary(
        string providerFamily,
        string providerKey,
        string managedTenantId,
        string folderId,
        string organizationId,
        string credentialReferenceId,
        string providerBindingRef,
        string repositoryBindingId,
        string correlationId,
        IReadOnlyList<ProviderCredentialMode>? credentialModeRequirements,
        ProviderAuthorizationEvidenceSnapshot? authorizationEvidence,
        ProviderOperationEvidenceSnapshot? operationEvidence,
        ProviderTargetEvidence? targetEvidence)
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
            || !IsSafeOpaqueValue(folderId)
            || !IsSafeOpaqueValue(organizationId)
            || !IsSafeOpaqueValue(credentialReferenceId)
            || !IsSafeOpaqueValue(providerBindingRef)
            || !IsSafeOpaqueValue(repositoryBindingId)
            || !IsSafeOpaqueValue(correlationId)
            || credentialModeRequirements is null
            || credentialModeRequirements.Count == 0
            || authorizationEvidence is null
            || operationEvidence is null
            || targetEvidence is null
            || targetEvidence.Metadata is null
            || targetEvidence.Metadata.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "github_operation_scope_malformed");
        }

        if (!string.Equals(authorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(operationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase)
            || targetEvidence.IsStale)
        {
            return (ProviderFailureCategory.ReconciliationRequired, "provider_operation_evidence_stale");
        }

        if (!string.Equals(operationEvidence.AuthorizedManagedTenantId, managedTenantId, StringComparison.Ordinal)
            || !string.Equals(operationEvidence.AuthorizedFolderId, folderId, StringComparison.Ordinal)
            || !string.Equals(operationEvidence.AuthorizedOrganizationId, organizationId, StringComparison.Ordinal)
            || !string.Equals(operationEvidence.AuthorizedCredentialReferenceId, credentialReferenceId, StringComparison.Ordinal)
            || !string.Equals(operationEvidence.AuthorizedRepositoryBindingId, repositoryBindingId, StringComparison.Ordinal))
        {
            return (ProviderFailureCategory.ProviderPermissionInsufficient, "provider_operation_scope_denied");
        }

        if (!IsSafeOpaqueValue(authorizationEvidence.Fingerprint)
            || !IsSafeOpaqueValue(operationEvidence.AuthorizedManagedTenantId)
            || !IsSafeOpaqueValue(operationEvidence.AuthorizedFolderId)
            || !IsSafeOpaqueValue(operationEvidence.AuthorizedOrganizationId)
            || !IsSafeOpaqueValue(operationEvidence.AuthorizedCredentialReferenceId)
            || !IsSafeOpaqueValue(operationEvidence.AuthorizedRepositoryBindingId)
            || !IsSafeOpaqueValue(operationEvidence.DelegatedTaskFingerprint)
            || !IsSafeOpaqueValue(operationEvidence.RepositoryBindingFingerprint)
            || !IsSafeOpaqueValue(operationEvidence.RefPolicyFingerprint)
            || !IsSafeOpaqueValue(operationEvidence.CanonicalLockFingerprint)
            || !IsSafeFingerprint(operationEvidence.ExpectedHeadFingerprint))
        {
            return (ProviderFailureCategory.ProviderValidationFailed, "provider_operation_evidence_malformed");
        }

        return null;
    }

    private static bool TryValidateResolvedInput(
        ProviderFileChangeSetRequest request,
        ProviderGitChangeSetResolvedInput input,
        string safeTargetFingerprint,
        out string? failureReason)
    {
        failureReason = null;
        if (!TryValidateResolvedTarget(input.Target)
            || !TryGitSha(input.ExpectedHeadSha, out string? expectedHead)
            || !string.Equals(
                request.OperationEvidence.ExpectedHeadFingerprint,
                GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
                    safeTargetFingerprint,
                    request.ChangeSetReference,
                    expectedHead),
                StringComparison.Ordinal)
            || input.Changes is null
            || input.Changes.Count != request.Changes.Count)
        {
            failureReason = "resolved_provider_change_set_malformed";
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        for (int index = 0; index < request.Changes.Count; index++)
        {
            ProviderFileChange declared = request.Changes[index];
            ProviderGitResolvedFileChange resolved = input.Changes[index];
            if (!string.Equals(declared.OperationReference, resolved.OperationReference, StringComparison.Ordinal)
                || declared.Kind != resolved.Kind
                || !IsSafeGitPath(resolved.Path)
                || !paths.Add(resolved.Path))
            {
                failureReason = "resolved_provider_change_order_mismatch";
                return false;
            }

            if (declared.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change)
            {
                if (resolved.Content is null || resolved.Content.LongLength != declared.ByteLength)
                {
                    failureReason = "resolved_provider_content_mismatch";
                    return false;
                }
            }
            else if (resolved.Content is not null)
            {
                failureReason = "resolved_provider_remove_content_present";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateResolvedInput(
        ProviderCommitRequest request,
        ProviderGitCommitResolvedInput input,
        string safeTargetFingerprint,
        out string? failureReason)
    {
        failureReason = null;
        if (!TryValidateResolvedTarget(input.Target)
            || !TryGitSha(input.ExpectedHeadSha, out string? expectedHead)
            || !TryGitSha(input.StagedTreeSha, out string? stagedTree)
            || !string.Equals(
                request.OperationEvidence.ExpectedHeadFingerprint,
                GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
                    safeTargetFingerprint,
                    request.StagedChangeSetReference,
                    expectedHead),
                StringComparison.Ordinal)
            || !string.Equals(
                request.SafeStagedChangeSetFingerprint,
                GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
                    request.OperationEvidence.RepositoryBindingFingerprint,
                    request.StagedChangeSetReference,
                    stagedTree),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(input.CommitMessage)
            || input.CommitMessage.Length > 4096
            || input.CommitMessage.Contains('\0'))
        {
            failureReason = "resolved_provider_commit_malformed";
            return false;
        }

        return true;
    }

    private static bool TryValidateResolvedInput(
        ProviderMutationStatusRequest request,
        ProviderGitStatusResolvedInput input,
        string safeTargetFingerprint,
        out string? failureReason)
    {
        failureReason = null;
        if (!TryValidateResolvedTarget(input.Target)
            || !TryGitSha(input.ExpectedHeadSha, out string? expectedHead)
            || !TryGitSha(input.ExpectedCommitSha, out string? expectedCommit)
            || string.Equals(expectedHead, expectedCommit, StringComparison.Ordinal)
            || !string.Equals(
                request.OperationEvidence.ExpectedHeadFingerprint,
                GitHubSafeTargetFingerprint.ComputeExpectedHeadFingerprint(
                    safeTargetFingerprint,
                    request.OperationReference,
                    expectedHead),
                StringComparison.Ordinal)
            || !string.Equals(
                request.SafeExpectedCommitFingerprint,
                GitHubSafeTargetFingerprint.ComputeProviderObjectFingerprint(
                    request.OperationEvidence.RepositoryBindingFingerprint,
                    request.OperationReference,
                    expectedCommit),
                StringComparison.Ordinal))
        {
            failureReason = "resolved_provider_status_malformed";
            return false;
        }

        if (request.CheckNumber != input.AuthoritativeCheckNumber
            || request.UnknownOutcomeObservedAt != input.AuthoritativeUnknownOutcomeObservedAt
            || request.RequestedAt != input.AuthoritativeRequestedAt)
        {
            failureReason = "resolved_provider_status_budget_mismatch";
            return false;
        }

        return true;
    }

    private static bool TryValidateResolvedTarget(ProviderRepositoryResolvedTarget target)
        => target is not null
            && target.TryValidate(out _)
            && target.SelectedRefKind == ProviderRepositoryRefKind.Branch;

    private static bool TryGitSha(string? value, out string sha)
    {
        sha = value ?? string.Empty;
        return value is { Length: 40 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
    }

    private static bool IsSafeGitPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && path.Length <= 1024
            && !path.StartsWith("/", StringComparison.Ordinal)
            && !path.Contains('\\')
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

    private static bool IsSafeOpaqueValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 512
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    private static bool IsSafeFingerprint(string? value)
        => value is { Length: 64 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    private static bool IsSafeMediaType(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && !value.Any(char.IsControl)
            && value.Contains('/');

    private static string SafeReasonCode(ProviderFailureCategory category, string? reasonCode)
        => reasonCode is { Length: > 0 and <= 128 }
            && reasonCode.All(static character => character is >= 'a' and <= 'z' || char.IsAsciiDigit(character) || character == '_')
                ? reasonCode
                : category.ToCategoryCode();
}
