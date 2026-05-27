using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using Newtonsoft.Json.Linq;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the success-envelope is truthful (AC #8): an <c>AcceptedCommand</c> surfaces
/// <c>correlationId</c>, <c>taskId</c>, <c>status</c>, and <c>idempotentReplay</c> without hiding a replay,
/// and the correlation used by the call is echoed at the envelope top level for caller correlation.
/// </summary>
public sealed class SuccessEnvelopeTests
{
    [Fact]
    public async Task AcceptedCommandSurfacesIdempotentReplayTruthfully()
    {
        // A 202 replay response: idempotentReplay=true must be surfaced, never papered over.
        string body = """
            {
              "acceptedAt": "2026-05-27T10:00:00Z",
              "correlationId": "server-corr-1",
              "taskId": "task-replay-1",
              "status": "accepted",
              "idempotentReplay": true
            }
            """;
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, body);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-replay-1", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        JObject envelope = TestSupport.Parse(result);
        envelope.Value<string>("correlationId").ShouldBe("corr-1"); // call correlation echoed at the top level
        TestSupport.Kind(result).ShouldBeNull(); // success, not a failure

        JObject inner = (JObject)envelope["result"]!;
        inner.Value<bool>("idempotentReplay").ShouldBeTrue();
        inner.Value<string>("status").ShouldBe("accepted");
        inner.Value<string>("taskId").ShouldBe("task-replay-1");
    }
}
