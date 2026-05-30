using System.Security.Cryptography;
using System.Text;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Workers.RepositoryProvisioning;

public sealed class RepositoryProvisioningProcessManager
{
    private readonly IFolderRepository _repository;
    private readonly IProviderCapabilityResolver _providerResolver;
    private readonly TimeProvider _timeProvider;

    public RepositoryProvisioningProcessManager(
        IFolderRepository repository,
        IProviderCapabilityResolver providerResolver,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RepositoryProvisioningResult> HandleAsync(
        RepositoryBindingRequested requested,
        RepositoryProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ContextMatchesRequested(requested, context))
        {
            return Result(RepositoryProvisioningResultCode.ContextMismatch, "repository_provisioning_context_mismatch", requested, null);
        }

        FolderStreamName streamName = _repository.CreateStreamName(requested.ManagedTenantId, requested.FolderId);
        FolderState state = _repository.Load(streamName);
        if (IsAlreadyProcessed(state, requested))
        {
            return Result(RepositoryProvisioningResultCode.AlreadyProcessed, "repository_provisioning_already_processed", requested, null);
        }

        if (!IsExpectedRequestedState(state, requested))
        {
            return Result(RepositoryProvisioningResultCode.StateUnavailable, "repository_binding_request_unavailable", requested, null);
        }

        IGitProvider? provider = await _providerResolver.ResolveAsync(
            context.ProviderFamily,
            context.ProviderKey,
            cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return AppendOutcome(
                streamName,
                requested,
                RepositoryBindingFailedEvent(
                    requested,
                    ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode()),
                RepositoryProvisioningResultCode.Failed);
        }

        ProviderRepositoryCreationRequest providerRequest = new(
            requested.ManagedTenantId,
            requested.OrganizationId,
            requested.ProviderBindingRef,
            context.CredentialReferenceId,
            requested.RepositoryBindingId,
            context.ProviderFamily,
            context.ProviderKey,
            context.TargetEvidence,
            context.CredentialModeRequirements,
            context.AuthorizationEvidence,
            requested.CorrelationId,
            requested.IdempotencyKey);

        ProviderRepositoryCreationResult providerResult;
        try
        {
            providerResult = await provider.CreateRepositoryAsync(
                providerRequest,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            providerResult = ProviderRepositoryCreationResult.Failure(
                providerRequest,
                ProviderFailureCategory.UnknownProviderOutcome,
                ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode());
        }
        catch (Exception)
        {
            providerResult = ProviderRepositoryCreationResult.Failure(
                providerRequest,
                ProviderFailureCategory.UnknownProviderOutcome,
                ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode());
        }

        (IFolderEvent Outcome, RepositoryProvisioningResultCode Code) mapped = MapProviderResult(requested, providerResult);
        return AppendOutcome(streamName, requested, mapped.Outcome, mapped.Code);
    }

    private RepositoryProvisioningResult AppendOutcome(
        FolderStreamName streamName,
        RepositoryBindingRequested requested,
        IFolderEvent outcome,
        RepositoryProvisioningResultCode successCode)
    {
        FolderAppendOutcome append = _repository.AppendIfFingerprintAbsent(
            streamName,
            outcome.IdempotencyKey,
            outcome.IdempotencyFingerprint,
            [outcome]);

        return append switch
        {
            FolderAppendOutcome.Appended => Result(successCode, "repository_provisioning_outcome_recorded", requested, append),
            FolderAppendOutcome.FingerprintMatched => Result(RepositoryProvisioningResultCode.AlreadyProcessed, "repository_provisioning_already_processed", requested, append),
            _ => Result(RepositoryProvisioningResultCode.AppendConflict, "repository_provisioning_append_conflict", requested, append),
        };
    }

    private (IFolderEvent Outcome, RepositoryProvisioningResultCode Code) MapProviderResult(
        RepositoryBindingRequested requested,
        ProviderRepositoryCreationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return (RepositoryBoundEvent(requested), RepositoryProvisioningResultCode.Bound);
        }

        if (result.FailureCategory == ProviderFailureCategory.ReconciliationRequired)
        {
            return (
                ProviderOutcomeUnknownEvent(
                    requested,
                    reconciliationRequired: true,
                    result.CategoryCode),
                RepositoryProvisioningResultCode.ReconciliationRequired);
        }

        if (result.FailureCategory == ProviderFailureCategory.UnknownProviderOutcome)
        {
            return (
                ProviderOutcomeUnknownEvent(
                    requested,
                    reconciliationRequired: false,
                    result.CategoryCode),
                RepositoryProvisioningResultCode.UnknownProviderOutcome);
        }

        return (RepositoryBindingFailedEvent(requested, result.CategoryCode), RepositoryProvisioningResultCode.Failed);
    }

    private RepositoryBound RepositoryBoundEvent(RepositoryBindingRequested requested)
    {
        string idempotencyKey = OutcomeIdempotencyKey(requested);
        return new RepositoryBound(
            requested.ManagedTenantId,
            requested.OrganizationId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            requested.CorrelationId,
            requested.TaskId,
            idempotencyKey,
            OutcomeFingerprint("repository_bound", requested, ProviderFailureCategory.None.ToCategoryCode()),
            _timeProvider.GetUtcNow());
    }

    private RepositoryBindingFailed RepositoryBindingFailedEvent(
        RepositoryBindingRequested requested,
        string failureCategory)
    {
        string idempotencyKey = OutcomeIdempotencyKey(requested);
        return new RepositoryBindingFailed(
            requested.ManagedTenantId,
            requested.OrganizationId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            failureCategory,
            requested.CorrelationId,
            requested.TaskId,
            idempotencyKey,
            OutcomeFingerprint("repository_binding_failed", requested, failureCategory),
            _timeProvider.GetUtcNow());
    }

    private ProviderOutcomeUnknown ProviderOutcomeUnknownEvent(
        RepositoryBindingRequested requested,
        bool reconciliationRequired,
        string outcomeCategory)
    {
        string idempotencyKey = OutcomeIdempotencyKey(requested);
        return new ProviderOutcomeUnknown(
            requested.ManagedTenantId,
            requested.OrganizationId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            reconciliationRequired,
            outcomeCategory,
            requested.CorrelationId,
            requested.TaskId,
            idempotencyKey,
            OutcomeFingerprint(
                reconciliationRequired ? "repository_reconciliation_required" : "provider_outcome_unknown",
                requested,
                outcomeCategory),
            _timeProvider.GetUtcNow());
    }

    private static RepositoryProvisioningResult Result(
        RepositoryProvisioningResultCode code,
        string reasonCode,
        RepositoryBindingRequested requested,
        FolderAppendOutcome? appendOutcome)
        => new(
            code,
            reasonCode,
            requested.ManagedTenantId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            appendOutcome);

    private static bool ContextMatchesRequested(RepositoryBindingRequested requested, RepositoryProvisioningContext context)
        => string.Equals(context.ManagedTenantId, requested.ManagedTenantId, StringComparison.Ordinal)
            && string.Equals(context.OrganizationId, requested.OrganizationId, StringComparison.Ordinal)
            && string.Equals(context.ProviderBindingRef, requested.ProviderBindingRef, StringComparison.Ordinal);

    private static bool IsExpectedRequestedState(FolderState state, RepositoryBindingRequested requested)
        => state.IsCreated
            && state.RepositoryBindingState == FolderRepositoryBindingState.BindingRequested
            && string.Equals(state.RepositoryBindingId, requested.RepositoryBindingId, StringComparison.Ordinal)
            && string.Equals(state.ProviderBindingRef, requested.ProviderBindingRef, StringComparison.Ordinal);

    private static bool IsAlreadyProcessed(FolderState state, RepositoryBindingRequested requested)
        => state.IsCreated
            && state.RepositoryBindingState is FolderRepositoryBindingState.Bound
                or FolderRepositoryBindingState.Failed
                or FolderRepositoryBindingState.UnknownProviderOutcome
                or FolderRepositoryBindingState.ReconciliationRequired
            && string.Equals(state.RepositoryBindingId, requested.RepositoryBindingId, StringComparison.Ordinal)
            && string.Equals(state.ProviderBindingRef, requested.ProviderBindingRef, StringComparison.Ordinal);

    private static string OutcomeIdempotencyKey(RepositoryBindingRequested requested)
        => $"provisioning-{Sha256(
            requested.ManagedTenantId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            requested.CorrelationId,
            requested.IdempotencyKey)[..32]}";

    private static string OutcomeFingerprint(
        string outcomeType,
        RepositoryBindingRequested requested,
        string category)
        => Sha256(
            "repository_provisioning_outcome_v1",
            outcomeType,
            requested.ManagedTenantId,
            requested.OrganizationId,
            requested.FolderId,
            requested.RepositoryBindingId,
            requested.ProviderBindingRef,
            requested.CorrelationId,
            requested.TaskId,
            requested.IdempotencyKey,
            category);

    private static string Sha256(params string?[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string? value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
