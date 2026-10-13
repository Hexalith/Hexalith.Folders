using System.Text.Json;

using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class FoldersDomainServiceSafeDenialExceptionHandlerTests
{
    [Theory]
    [InlineData(401, "authentication_failure", "Authentication required", false)]
    [InlineData(403, "authorization_denied", "Authorization denied", false)]
    [InlineData(404, "not_found_to_caller", "Resource not available", false)]
    [InlineData(503, "read_model_unavailable", "Read model unavailable", true)]
    public async Task SafeDenialProblemShouldKeepStatusTitleAndCategoryConsistent(
        int status, string code, string title, bool retryable)
    {
        using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using MemoryStream body = new();
        DefaultHttpContext context = new() { RequestServices = services };
        context.Response.Body = body;

        (await new FoldersDomainServiceSafeDenialExceptionHandler().TryHandleAsync(context,
            new DomainServiceSafeDenialException(status), TestContext.Current.CancellationToken)).ShouldBeTrue();

        context.Response.StatusCode.ShouldBe(status);
        body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(body,
            cancellationToken: TestContext.Current.CancellationToken);
        JsonElement problem = document.RootElement;
        problem.GetProperty("title").GetString().ShouldBe(title);
        problem.GetProperty("type").GetString().ShouldBe($"https://hexalith.io/problems/{code}");
        problem.GetProperty("category").GetString().ShouldBe(code);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("retryable").GetBoolean().ShouldBe(retryable);
        problem.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
        problem.TryGetProperty("detail", out _).ShouldBeFalse();
    }
}
