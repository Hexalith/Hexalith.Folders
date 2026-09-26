using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Client.Serialization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// F15 guard: every hard-coded per-call freshness literal on the console pages must be the exact value that the
/// generated v2 contract accepts for that operation, the same check the CLI and MCP adapters run through
/// <see cref="OperationFreshness.TryParse"/>. A contract change to an operation's freshness fails here.
/// </summary>
public sealed class PageReadFreshnessContractTests
{
    public static TheoryData<string, string, ReadConsistencyClass> PageReadFreshness => new()
    {
        { "GetEffectivePermissions", "read_your_writes", ReadConsistencyClass.Read_your_writes },
        { "GetFolderLifecycleStatus", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "ListAuditTrail", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "ListOperationTimeline", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetProviderStatusDiagnostics", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetProviderBinding", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetRepositoryBinding", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetSyncStatusDiagnostics", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetProviderOutcome", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetWorkspaceStatus", "read_your_writes", ReadConsistencyClass.Read_your_writes },
        { "GetWorkspaceLock", "read_your_writes", ReadConsistencyClass.Read_your_writes },
        { "GetDirtyStateDiagnostics", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetCommitEvidence", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetWorkspaceCleanupStatus", "read_your_writes", ReadConsistencyClass.Read_your_writes },
        { "ListFolderFiles", "snapshot_per_task", ReadConsistencyClass.Snapshot_per_task },
        { "GetProviderSupportEvidence", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
        { "GetFolderIndexingStatus", "eventually_consistent", ReadConsistencyClass.Eventually_consistent },
    };

    [Theory]
    [MemberData(nameof(PageReadFreshness))]
    public void PageFreshnessLiteralIsTheValueTheContractAcceptsForTheOperation(
        string operationId,
        string wireValue,
        ReadConsistencyClass pageLiteral)
    {
        OperationFreshness.TryParse(operationId, wireValue, out ReadConsistencyClass? parsed)
            .ShouldBeTrue($"{operationId} must accept {wireValue}");
        parsed.ShouldBe(pageLiteral);
    }
}
