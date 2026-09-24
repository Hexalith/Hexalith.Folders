using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies query read-consistency sourcing: <c>ParseFreshness</c> maps each canonical freshness token to its
/// typed <see cref="ReadConsistencyClass"/> (while rejecting supplied unknown or blank values), and a supplied freshness threads through to the wire <c>X-Hexalith-Freshness</c>
/// header while an omitted one sends no header.
/// </summary>
public sealed class ToolInputsTests
{
    [Theory]
    [InlineData("ListFolderFiles", "snapshot_per_task", ReadConsistencyClass.Snapshot_per_task)]
    [InlineData("GetEffectivePermissions", "read_your_writes", ReadConsistencyClass.Read_your_writes)]
    [InlineData("GetFolderLifecycleStatus", "eventually_consistent", ReadConsistencyClass.Eventually_consistent)]
    public void ParsesOperationAcceptedFreshnessTokens(string operationId, string token, ReadConsistencyClass expected)
        => ToolInputs.ParseFreshness(token, operationId).ShouldBe(expected);

    [Fact]
    public void OmittedFreshnessMapsToNull()
        => ToolInputs.ParseFreshness(null, "GetFolderLifecycleStatus").ShouldBeNull();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not_a_real_class")]
    public void SuppliedUnknownOrBlankFreshnessIsAUsageError(string token)
        => Should.Throw<McpUsageException>(() => ToolInputs.ParseFreshness(token, "GetFolderLifecycleStatus"));

    [Theory]
    [InlineData("ListFolderFiles", "eventually_consistent")]
    [InlineData("GetEffectivePermissions", "snapshot_per_task")]
    [InlineData("GetFolderLifecycleStatus", "read_your_writes")]
    public void KnownFreshnessForAnotherOperationIsAUsageError(string operationId, string token)
        => Should.Throw<McpUsageException>(() => ToolInputs.ParseFreshness(token, operationId));

    [Fact]
    public async Task SuppliedFreshnessIsSentOnTheWire()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, """{ "lifecycleState": "active" }""");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "corr-1", freshness: "eventually_consistent", cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].Freshness.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OmittedFreshnessSendsNoFreshnessHeader()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, """{ "lifecycleState": "active" }""");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "corr-1", freshness: null, cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests[0].Freshness.ShouldBeNull();
    }

    [Fact]
    public async Task EffectivePermissionsPassesOptionalTaskContextOnTheWire()
    {
        const string taskId = "task-effective-permissions-mcp";
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        _ = await FolderTools.GetEffectivePermissions(
            pipeline,
            folderId: "folder_000000001",
            taskId,
            correlationId: "correlation-effective-permissions-mcp",
            freshness: "read_your_writes",
            cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].TaskId.ShouldBe(taskId);
    }

    [Fact]
    public async Task EffectivePermissionsForwardsNullWhenTaskContextIsOmitted()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        _ = await FolderTools.GetEffectivePermissions(
            pipeline,
            folderId: "folder_000000001",
            taskId: null,
            correlationId: "correlation-effective-permissions-without-task-mcp",
            freshness: "read_your_writes",
            cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].TaskId.ShouldBeNull();
    }

    [Fact]
    public async Task EffectivePermissionsRejectsExplicitlyBlankOptionalTaskContextBeforeDispatch()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.GetEffectivePermissions(
            pipeline,
            folderId: "folder_000000001",
            taskId: " ",
            correlationId: "correlation-effective-permissions-mcp",
            freshness: "read_your_writes",
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldContain("usage_error");
        handler.Requests.ShouldBeEmpty();
    }
}
