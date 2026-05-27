using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the credential→wire path through the production <c>BearerTokenHandler</c>: the resolved token is
/// attached as a <c>Bearer</c> <c>Authorization</c> header on the outgoing SDK request (AC #7), the token
/// text never appears in the tool result, and when no token resolves the pipeline short-circuits with
/// <c>credential_missing</c> so no request — and therefore no <c>Authorization</c> header — ever reaches the
/// handler.
/// </summary>
public sealed class BearerTokenWireTests
{
    [Fact]
    public async Task ResolvedTokenIsAttachedAsBearerAuthorizationHeader()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.BearerClient(handler, TestSupport.Token));

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].Authorization.ShouldBe($"Bearer {TestSupport.Token}");
        // The token must never leak into the tool output, even though it travels on the wire.
        result.ShouldNotContain(TestSupport.Token);
    }

    [Fact]
    public async Task IdempotencyKeyIsAttachedToTheWireForMutations()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.BearerClient(handler, TestSupport.Token));

        await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-wire-9", taskId: "task-1", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        // The caller-sourced idempotency key reaches the wire unchanged (never MCP-generated, never altered).
        handler.Requests[0].IdempotencyKey.ShouldBe("idem-wire-9");
    }

    [Fact]
    public async Task MissingTokenShortCircuitsBeforeAnyRequestReachesTheHandler()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.BearerClient(handler, token: null), token: null);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-no-token", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("credential_missing");
        handler.Requests.ShouldBeEmpty();
    }
}
