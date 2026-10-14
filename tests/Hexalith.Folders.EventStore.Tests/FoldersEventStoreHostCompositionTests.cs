using System.Text.Json;

using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Guards the real command host's safe-denial registration and handler ordering.</summary>
public sealed class FoldersEventStoreHostCompositionTests
{
    /// <summary>Executes the entry point's ordered handler chain for every trusted denial status.</summary>
    /// <param name="status">The closed downstream denial status.</param>
    /// <param name="code">The public gateway category.</param>
    [Theory]
    [InlineData(401, "authentication_failure")]
    [InlineData(403, "authorization_denied")]
    [InlineData(404, "not_found_to_caller")]
    [InlineData(503, "read_model_unavailable")]
    public async Task CommandHostHandlesSafeDenialBeforeItsGlobalFallback(int status, string code)
    {
        using FoldersEventStoreHostFactory factory = new();
        IExceptionHandler[] handlers = [.. factory.Services.GetServices<IExceptionHandler>()];
        handlers.First().ShouldBeOfType<FoldersDomainServiceSafeDenialExceptionHandler>();
        handlers.ShouldContain(static handler => handler is GlobalExceptionHandler);

        using MemoryStream body = new();
        DefaultHttpContext context = new() { RequestServices = factory.Services };
        context.Response.Body = body;
        bool handled = false;
        foreach (IExceptionHandler handler in handlers)
        {
            if (await handler.TryHandleAsync(context, new DomainServiceSafeDenialException(status),
                TestContext.Current.CancellationToken).ConfigureAwait(true))
            {
                handled = true;
                break;
            }
        }

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(status);
        body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(body,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        document.RootElement.GetProperty("code").GetString().ShouldBe(code);
        document.RootElement.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
        document.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();
    }
}
