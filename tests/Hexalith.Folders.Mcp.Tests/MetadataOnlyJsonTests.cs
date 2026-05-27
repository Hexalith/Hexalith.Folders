using Hexalith.Folders.Mcp.Infrastructure;

using Newtonsoft.Json.Linq;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Directly verifies the centralized metadata-only serializer contract (AC #8): every content-bearing wire
/// field (<c>contentBytes</c>, <c>inlineContent</c>, <c>streamDescriptor</c>) is dropped regardless of nesting
/// depth, benign metadata survives, and output is camelCase to match the SDK/wire <c>ProblemDetails</c> shape.
/// The <c>read-file-range</c> end-to-end leak guard lives in <see cref="MetadataOnlyOutputTests"/>; this test
/// pins the filter itself so a future result type that declares any forbidden field cannot leak through it.
/// </summary>
public sealed class MetadataOnlyJsonTests
{
    [Fact]
    public void DropsEveryForbiddenContentFieldAtAnyNestingDepth()
    {
        object payload = new
        {
            ContentBytes = "TOP_LEVEL_LEAK",
            DisplayName = "readme.md",
            Nested = new
            {
                InlineContent = "NESTED_INLINE_LEAK",
                StreamDescriptor = "NESTED_STREAM_LEAK",
                Deeper = new
                {
                    ContentBytes = "DEEP_LEAK",
                    NormalizedPath = "docs/readme.md",
                },
            },
        };

        string json = MetadataOnlyJson.Serialize(payload);

        json.ShouldNotContain("TOP_LEVEL_LEAK");
        json.ShouldNotContain("NESTED_INLINE_LEAK");
        json.ShouldNotContain("NESTED_STREAM_LEAK");
        json.ShouldNotContain("DEEP_LEAK");
        json.ShouldNotContain("contentBytes");
        json.ShouldNotContain("inlineContent");
        json.ShouldNotContain("streamDescriptor");

        // Benign metadata at every depth is preserved (proves the payload was serialized, not suppressed).
        json.ShouldContain("readme.md");
        json.ShouldContain("docs/readme.md");
    }

    [Fact]
    public void SerializesPropertyNamesAsCamelCaseForWireParity()
    {
        string json = MetadataOnlyJson.Serialize(new { CorrelationId = "corr-1", DisplayName = "x" });

        JObject parsed = JObject.Parse(json);
        parsed.ContainsKey("correlationId").ShouldBeTrue();
        parsed.ContainsKey("displayName").ShouldBeTrue();
        parsed.ContainsKey("CorrelationId").ShouldBeFalse();
    }
}
