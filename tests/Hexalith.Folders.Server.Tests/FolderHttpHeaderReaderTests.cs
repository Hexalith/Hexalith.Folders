using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderHttpHeaderReaderTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("   ", false, true)]
    [InlineData("key_1", true, true)]
    public void ReadIdempotencyCheckShouldRespectTheRouteHeaderPolicy(string? value, bool usable, bool present)
    {
        DefaultHttpContext context = new();
        if (value is not null)
        {
            context.Request.Headers["Idempotency-Key"] = value;
        }

        FolderHttpHeaderReader.HasReadIdempotencyKey(context).ShouldBe(usable);
        FolderHttpHeaderReader.HasReadIdempotencyKey(context, includeEmpty: true).ShouldBe(present);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("  eventually_consistent  ", false)]
    [InlineData("Eventually_Consistent", true)]
    [InlineData("read_your_writes", true)]
    public void ReadFreshnessCheckShouldCompareTheTrimmedClassUsingOrdinalCase(string? value, bool unsupported)
    {
        DefaultHttpContext context = new();
        if (value is not null)
        {
            context.Request.Headers["X-Hexalith-Freshness"] = value;
        }

        FolderHttpHeaderReader.HasUnsupportedReadFreshness(context, "eventually_consistent").ShouldBe(unsupported);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("   ", "Folder_1", true)]
    [InlineData("  Diagnostic_1  ", "Diagnostic_1", true)]
    [InlineData("bad\nvalue", null, false)]
    [InlineData("secret_1", null, false)]
    public void DiagnosticCorrelationCheckShouldUseTheFirstNonblankValue(string? first, string? expected, bool accepted)
    {
        DefaultHttpContext context = new();
        if (first is not null)
        {
            context.Request.Headers["X-Correlation-Id"] = new StringValues([first, "Folder_1"]);
        }

        FolderHttpHeaderReader.TryReadDiagnosticCorrelation(context, out string? correlationId).ShouldBe(accepted);
        correlationId.ShouldBe(expected);
    }

    [Theory]
    [InlineData("cursor_1\n", "cursor_1")]
    [InlineData("cursor_1\r\n", "cursor_1")]
    [InlineData("cur\nsor_1", null)]
    public void DecodedQueryShouldPreserveOuterTrimmingAndRejectEmbeddedLf(string raw, string? expected)
    {
        DefaultHttpContext context = new();
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["cursor"] = raw,
        });
        FolderHttpHeaderReader.ReadQuery(context, "cursor").ShouldBe(expected);
    }

    [Fact]
    public void ReadHeaderShouldReturnFirstNonEmptySafeValue()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Append("X-Correlation-Id", StringValues.Empty);
        httpContext.Request.Headers.Append("X-Correlation-Id", "   ");
        httpContext.Request.Headers.Append("X-Correlation-Id", " folder_1 ");

        FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id").ShouldBe("folder_1");
    }

    [Fact]
    public void ReadQueryShouldReturnFirstNonEmptySafeValue()
    {
        DefaultHttpContext httpContext = new()
        {
            Request =
            {
                Query = new QueryCollection(new Dictionary<string, StringValues>(StringComparer.Ordinal)
                {
                    ["tenantId"] = new StringValues(["", "  workspace_1  "]),
                }),
            },
        };

        FolderHttpHeaderReader.ReadQuery(httpContext, "tenantId").ShouldBe("workspace_1");
    }

    [Theory]
    [InlineData("bad\rvalue")]
    [InlineData("bad\nvalue")]
    [InlineData("bad\u0001value")]
    public void ReadHeaderShouldRejectControlCharactersWithoutEchoingThem(string unsafeValue)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Correlation-Id"] = unsafeValue;

        string? value = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");

        value.ShouldBeNull();
        value.ShouldNotBe(unsafeValue);
    }

    [Fact]
    public void ReadHeaderShouldSkipUnsafeValuesAndKeepLaterSafeValue()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Append("X-Hexalith-Task-Id", "injected\r\nX-Other: 1");
        httpContext.Request.Headers.Append("X-Hexalith-Task-Id", "task_1");

        FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id").ShouldBe("task_1");
    }
}
