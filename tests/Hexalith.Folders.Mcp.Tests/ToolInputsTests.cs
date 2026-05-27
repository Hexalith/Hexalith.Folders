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
/// typed <see cref="ReadConsistencyClass"/> (and unknown/blank values to <see langword="null"/>, introducing
/// no new request semantics), and a supplied freshness threads through to the wire <c>X-Hexalith-Freshness</c>
/// header while an omitted one sends no header.
/// </summary>
public sealed class ToolInputsTests
{
    [Theory]
    [InlineData("snapshot_per_task", ReadConsistencyClass.Snapshot_per_task)]
    [InlineData("read_your_writes", ReadConsistencyClass.Read_your_writes)]
    [InlineData("eventually_consistent", ReadConsistencyClass.Eventually_consistent)]
    public void ParsesKnownFreshnessTokens(string token, ReadConsistencyClass expected)
        => ToolInputs.ParseFreshness(token).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not_a_real_class")]
    public void UnknownOrBlankFreshnessMapsToNull(string? token)
        => ToolInputs.ParseFreshness(token).ShouldBeNull();

    [Fact]
    public async Task SuppliedFreshnessIsSentOnTheWire()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, """{ "lifecycleState": "active" }""");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        await FolderTools.GetFolderLifecycleStatus(
            pipeline, folderId: "f", correlationId: "corr-1", freshness: "read_your_writes", cancellationToken: TestContext.Current.CancellationToken);

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
}
