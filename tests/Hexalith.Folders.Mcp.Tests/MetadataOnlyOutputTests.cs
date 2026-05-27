using System.Net;
using System.Threading.Tasks;

using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the metadata-only invariant on tool output: content-bearing fields are dropped at any depth, so
/// even an authorized <c>read-file-range</c> result cannot leak file bytes, and the bearer token never
/// appears in any output channel.
/// </summary>
public sealed class MetadataOnlyOutputTests
{
    private const string LeakedContentMarker = "LEAKED_FILE_BYTES_MARKER";

    [Fact]
    public async Task AuthorizedReadFileRangeDropsContentBytes()
    {
        // A 200 OK FileRangeReadResult carrying authorized content in contentBytes plus benign metadata.
        string body = $$"""
            {
              "path": { "normalizedPath": "docs/readme.md", "displayName": "readme.md" },
              "range": { "startOffset": 0, "endOffset": 16, "actualBytes": 16, "partial": false },
              "contentBytes": "{{LeakedContentMarker}}",
              "freshness": { "readConsistency": "read_your_writes", "stale": false }
            }
            """;
        TestSupport.CapturingHandler handler = new(HttpStatusCode.OK, body);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await ContextTools.ReadFileRange(
            pipeline, folderId: "f", workspaceId: "w", taskId: "task-1", correlationId: "corr-range", requestJson: "{}", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotContain(LeakedContentMarker);
        result.ShouldNotContain("contentBytes");
        // Benign metadata is still present (proves the result was serialized, not suppressed wholesale).
        result.ShouldContain("readme.md");
        result.ShouldContain("corr-range");
    }

    [Fact]
    public async Task TokenNeverAppearsInSuccessOutput()
    {
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler), token: TestSupport.Token);

        string result = await FolderTools.CreateFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        result.ShouldNotContain(TestSupport.Token);
    }
}
