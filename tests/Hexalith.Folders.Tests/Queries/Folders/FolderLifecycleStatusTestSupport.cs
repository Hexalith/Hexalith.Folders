using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Tests.Queries.Folders;

internal static class FolderLifecycleStatusTestSupport
{
    internal static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    internal const string TenantWatermark = "tenant_a_watermark_v0007";

    internal const string AuthorizationWatermark = "auth_folder_watermark_v0011";

    internal const string LifecycleWatermark = "lifecycle_folder_watermark_v0023";

    internal static FolderLifecycleStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        IFolderLifecycleStatusReadModel lifecycleReadModel,
        RecordingFolderPermissionEvidenceProvider? folderEvidence = null,
        RecordingEventStoreAuthorizationValidator? validator = null,
        RecordingDaprPolicyEvidenceProvider? dapr = null)
    {
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            folderEvidence ?? new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed(AuthorizationWatermark)),
            validator ?? new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            dapr ?? new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            new FixedUtcClock(Now));

        return new FolderLifecycleStatusQueryHandler(
            authorization,
            lifecycleReadModel,
            new FixedUtcClock(Now));
    }

    internal static FolderLifecycleStatusQuery Query(
        string folderId = "folder-a",
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        string? taskId = "task-a",
        string? correlationId = "corr-a",
        IReadOnlyDictionary<string, string?>? clientTenantValues = null,
        EventStoreClaimTransformEvidence? claimTransformEvidence = null)
        => new(
            folderId,
            tenantId,
            principalId,
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, ["read_metadata"]),
            correlationId,
            taskId,
            clientTenantValues,
            ClientControlledPrincipalValues: null);

    internal static FolderLifecycleStatusReadModelSnapshot ActiveUnbound(
        string tenantId = "tenant-a",
        string folderId = "folder-a",
        FolderLifecycleEvidenceScope? evidenceScope = null,
        IReadOnlyList<string>? diagnosticSentinels = null)
        => Snapshot(
            tenantId,
            folderId,
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unbound,
            repositoryBindingId: null,
            providerBindingRef: null,
            evidenceScope,
            diagnosticSentinels);

    internal static FolderLifecycleStatusReadModelSnapshot ActiveBound(
        string repositoryBindingId = "repository_binding_opaque_001",
        string providerBindingRef = "provider_binding_opaque_001")
        => Snapshot(
            "tenant-a",
            "folder-a",
            FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Bound,
            repositoryBindingId,
            providerBindingRef);

    internal static FolderLifecycleStatusReadModelSnapshot Snapshot(
        string tenantId,
        string folderId,
        FolderLifecycleProjectionState lifecycleState,
        FolderRepositoryBindingStatus bindingStatus,
        string? repositoryBindingId = null,
        string? providerBindingRef = null,
        FolderLifecycleEvidenceScope? evidenceScope = null,
        IReadOnlyList<string>? diagnosticSentinels = null)
        => new(
            ManagedTenantId: tenantId,
            FolderId: folderId,
            LifecycleState: lifecycleState,
            BindingStatus: bindingStatus,
            RepositoryBindingId: repositoryBindingId,
            ProviderBindingRef: providerBindingRef,
            Freshness: Freshness(),
            EvidenceScope: evidenceScope ?? EvidenceScope(),
            DiagnosticSentinels: diagnosticSentinels ?? []);

    internal static FolderLifecycleFreshness Freshness(
        bool stale = false,
        string? projectionWatermark = LifecycleWatermark,
        string? reasonCode = null)
        => new(
            ReadConsistency: "eventually_consistent",
            ObservedAt: Now,
            ProjectionWatermark: projectionWatermark,
            Stale: stale,
            ReasonCode: reasonCode);

    internal static FolderLifecycleEvidenceScope EvidenceScope(
        string tenantId = "tenant-a",
        string principalId = "user-a",
        string actionToken = "read_metadata",
        string? taskId = "task-a",
        string? correlationId = "corr-a",
        string? authorizationWatermark = AuthorizationWatermark)
        => new(
            tenantId,
            principalId,
            actionToken,
            taskId,
            correlationId,
            authorizationWatermark);

    internal static FolderTenantAccessProjection TenantProjection(
        string tenantId = "tenant-a",
        params string[] principals)
    {
        Dictionary<string, FolderTenantPrincipalEvidence> principalEvidence = principals
            .ToDictionary(
                static principal => principal,
                static principal => new FolderTenantPrincipalEvidence(principal, "Member"),
                StringComparer.Ordinal);

        return new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = true,
            Principals = principalEvidence,
            Watermark = 7,
            LastEventTimestamp = Now.AddMinutes(-1),
            ProjectionWatermark = TenantWatermark,
        };
    }
}

internal sealed class CountingLifecycleStatusReadModel(FolderLifecycleStatusReadModelResult result)
    : IFolderLifecycleStatusReadModel
{
    public int Requests { get; private set; }

    public FolderLifecycleStatusReadModelRequest? LastRequest { get; private set; }

    public Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        LastRequest = request;
        return Task.FromResult(result);
    }
}

internal sealed class CountingTenantAccessProjectionStore(FolderTenantAccessProjection? projection = null)
    : IFolderTenantAccessProjectionStore
{
    public int Gets { get; private set; }

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        Gets++;
        return Task.FromResult(projection);
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult result) : IFolderPermissionEvidenceProvider
{
    public List<FolderPermissionEvidenceRequest> Requests { get; } = [];

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}

internal sealed class RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult result) : IEventStoreAuthorizationValidator
{
    public List<EventStoreAuthorizationValidationRequest> Requests { get; } = [];

    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}

internal sealed class RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult result) : IDaprPolicyEvidenceProvider
{
    public List<DaprPolicyEvidenceRequest> Requests { get; } = [];

    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
        DaprPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}
