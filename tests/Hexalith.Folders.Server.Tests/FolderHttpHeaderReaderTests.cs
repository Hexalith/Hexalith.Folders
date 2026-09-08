using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderHttpHeaderReaderTests
{
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
