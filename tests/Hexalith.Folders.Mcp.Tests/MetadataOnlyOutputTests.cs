using System.Net;
using System.Text;
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

    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.PartialContent, true)]
    public async Task AuthorizedReadFileRangeDropsContentBytes(HttpStatusCode status, bool partial)
    {
        // A 200 OK FileRangeReadResult carrying authorized content in contentBytes plus benign metadata.
        string encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(LeakedContentMarker));
        int contentLength = Encoding.UTF8.GetByteCount(LeakedContentMarker);
        string body = $$"""
            {
              "path": { "normalizedPath": "docs/readme.md", "displayName": "readme.md", "pathPolicyClass": "content_allowed", "unicodeNormalization": "NFC" },
              "range": { "startOffset": 0, "endOffset": {{contentLength + (partial ? 1 : 0)}}, "actualBytes": {{contentLength}}, "partial": {{partial.ToString().ToLowerInvariant()}} },
              "contentBytes": "{{encodedContent}}",
              "limits": { "queryFamily": "range", "configuredLimit": 262144, "actualCount": 1, "actualBytes": {{contentLength}}, "elapsedMilliseconds": 1, "isTruncated": false, "truncatedReason": "not_truncated" },
              "freshness": { "readConsistency": "read_your_writes", "observedAt": "2026-09-14T00:00:00Z", "projectionWatermark": "watermark_01HZY7Z6N7J4Q2X8", "stale": false }
            }
            """;
        TestSupport.CapturingHandler handler = new(status, body);
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await ContextTools.ReadFileRange(
            pipeline,
            folderId: "f",
            workspaceId: "w",
            taskId: "task-1",
            correlationId: "corr-range",
            requestJson: $$"""
                {"requestSchemaVersion":"v1","path":{"normalizedPath":"docs/readme.md","displayName":"readme.md","pathPolicyClass":"content_allowed","unicodeNormalization":"NFC"},"startOffset":0,"endOffset":{{contentLength}}}
                """,
            cancellationToken: TestContext.Current.CancellationToken);

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
