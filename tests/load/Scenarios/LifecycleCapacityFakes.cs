using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.LoadTests.Scenarios;

internal sealed class StaticFolderPermissionEvidenceProvider(string organizationId) : IFolderPermissionEvidenceProvider
{
    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(FolderPermissionEvidenceResult.Allowed(
            $"{request.ManagedTenantId}:folder-permission:7",
            organizationId: organizationId));
}

internal sealed class StaticEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator:7"));
}

internal sealed class StaticDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
{
    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
}

internal sealed class ReadyWorkspaceReadinessValidator(
    LifecycleCapacityIteration iteration,
    DateTimeOffset now) :
    IWorkspacePreparationReadinessValidator,
    IWorkspaceCommitReadinessValidator
{
    public Task<ProviderReadinessValidationResult> ValidateAsync(
        ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ProviderReadinessValidationResult(
            ProviderReadinessResultCode.Allowed,
            "ready",
            "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: "none",
            CorrelationId: request.CorrelationId ?? iteration.PrepareCorrelationId,
            ProviderReference: request.ProviderBindingRef,
            ProviderBindingRef: request.ProviderBindingRef,
            CapabilityProfileRef: "repository-profile-0001",
            Evidence: null,
            new ProviderReadinessFreshness("snapshot_per_task", now, $"{iteration.TenantId}:7", Stale: false),
            ProviderFailureCategory.None,
            "none"));
}

internal sealed class SafePathPolicyEvidenceProvider : IWorkspacePathPolicyEvidenceProvider
{
    public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
        WorkspacePathPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.NoEscape));
}

internal sealed class MetadataOnlyContentStore(
    LifecycleCapacityRunRecorder recorder,
    LifecycleCapacityIteration iteration) : IWorkspaceFileContentStore
{
    public Task<WorkspaceFileContentStoreResult> StageAsync(
        WorkspaceFileContentStoreRequest request,
        CancellationToken cancellationToken = default)
    {
        recorder.RecordOperation(iteration, request.OperationId, iteration.MutationIdempotencyKey);
        return Task.FromResult(WorkspaceFileContentStoreResult.Succeeded);
    }
}

internal sealed class MetadataOnlyDeleteOperationStore : IWorkspaceFileDeleteOperationStore
{
    public Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(
        WorkspaceFileDeleteOperationStoreRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(WorkspaceFileDeleteOperationStoreResult.Succeeded);
}

internal sealed class SyntheticCommitExecutor(LifecycleCapacityIteration iteration) : IWorkspaceCommitExecutor
{
    public Task<WorkspaceCommitExecutionResult> CommitAsync(
        WorkspaceCommitExecutionRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(WorkspaceCommitExecutionResult.Succeeded(iteration.CommitReference));
}
