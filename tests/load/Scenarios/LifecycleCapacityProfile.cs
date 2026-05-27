using System.Globalization;

namespace Hexalith.Folders.LoadTests.Scenarios;

public sealed record LifecycleCapacityProfile(
    string Name,
    int TenantCount,
    int FoldersPerTenant,
    int WorkspacesPerTenant,
    int TasksPerWorkspace,
    int OperationsPerTask,
    int InjectRate,
    TimeSpan InjectInterval,
    TimeSpan Duration,
    bool ExplicitReplay)
{
    public static LifecycleCapacityProfile Quick { get; } = new(
        "quick",
        TenantCount: 2,
        FoldersPerTenant: 1,
        WorkspacesPerTenant: 1,
        TasksPerWorkspace: 1,
        OperationsPerTask: 1,
        InjectRate: 1,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        ExplicitReplay: false);

    public static LifecycleCapacityProfile FromName(string name)
        => string.Equals(name, Quick.Name, StringComparison.OrdinalIgnoreCase)
            ? Quick
            : throw new ArgumentException($"Unsupported lifecycle capacity profile '{name}'.", nameof(name));

    public LifecycleCapacityIteration CreateIteration(long invocationNumber)
    {
        if (invocationNumber < 1)
        {
            invocationNumber = 1;
        }

        int tenantOrdinal = Ordinal(invocationNumber, TenantCount, 1);
        int folderOrdinal = Ordinal(invocationNumber / TenantCount, FoldersPerTenant, 1);
        int workspaceOrdinal = Ordinal(invocationNumber / (TenantCount * FoldersPerTenant), WorkspacesPerTenant, 1);
        int taskOrdinal = Ordinal(invocationNumber / (TenantCount * FoldersPerTenant * WorkspacesPerTenant), TasksPerWorkspace, 1);
        int operationOrdinal = checked((int)invocationNumber);

        string tenantId = SafeOrdinalId.Create("tenant", tenantOrdinal);
        string folderId = SafeOrdinalId.Create("folder", folderOrdinal);
        string workspaceId = SafeOrdinalId.Create("workspace", workspaceOrdinal);
        string taskId = SafeOrdinalId.Create("task", taskOrdinal);

        return new LifecycleCapacityIteration(
            tenantId,
            "organization-0001",
            folderId,
            workspaceId,
            taskId,
            PrincipalId: "principal-0001",
            RepositoryBindingId: "repository-binding-0001",
            ProviderBindingRef: "provider-binding-0001",
            BranchRefPolicyRef: "branch-ref-policy-0001",
            WorkspacePolicyRef: "workspace-policy-0001",
            PrepareCorrelationId: SafeOrdinalId.Create("correlation", operationOrdinal * 10 + 1),
            LockCorrelationId: SafeOrdinalId.Create("correlation", operationOrdinal * 10 + 2),
            MutationCorrelationId: SafeOrdinalId.Create("correlation", operationOrdinal * 10 + 3),
            CommitCorrelationId: SafeOrdinalId.Create("correlation", operationOrdinal * 10 + 4),
            PrepareIdempotencyKey: SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 1),
            PreparedOutcomeIdempotencyKey: SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 2),
            LockIdempotencyKey: SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 3),
            MutationIdempotencyKey: SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 4),
            CommitIdempotencyKey: ExplicitReplay
                ? SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 4)
                : SafeOrdinalId.Create("idempotency", operationOrdinal * 10 + 5),
            PrepareOperationId: SafeOrdinalId.Create("operation", operationOrdinal * 10 + 1),
            LockOperationId: SafeOrdinalId.Create("operation", operationOrdinal * 10 + 2),
            MutationOperationId: SafeOrdinalId.Create("operation", operationOrdinal * 10 + 3),
            CommitOperationId: SafeOrdinalId.Create("operation", operationOrdinal * 10 + 4),
            RelativePath: $"docs/{SafeOrdinalId.Create("operation", operationOrdinal)}.md",
            ContentHashReference: $"hashref_capacity_{operationOrdinal.ToString("D6", CultureInfo.InvariantCulture)}",
            ChangedPathMetadataDigest: $"digest_capacity_{operationOrdinal.ToString("D6", CultureInfo.InvariantCulture)}",
            CommitReference: $"commitref_capacity_{operationOrdinal.ToString("D6", CultureInfo.InvariantCulture)}");
    }

    public IReadOnlyDictionary<string, object> Dimensions()
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["tenant_count"] = TenantCount,
            ["folders_per_tenant"] = FoldersPerTenant,
            ["workspaces_per_tenant"] = WorkspacesPerTenant,
            ["tasks_per_workspace"] = TasksPerWorkspace,
            ["operations_per_task"] = OperationsPerTask,
        };

    public IReadOnlyDictionary<string, object> LoadSimulation()
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["kind"] = "inject",
            ["rate"] = InjectRate,
            ["interval_seconds"] = InjectInterval.TotalSeconds,
            ["duration_seconds"] = Duration.TotalSeconds,
            ["warmup"] = "disabled",
        };

    private static int Ordinal(long value, int modulo, int offset)
        => checked((int)(Math.Abs(value) % modulo) + offset);
}
