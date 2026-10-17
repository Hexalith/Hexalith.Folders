using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FoldersGatewayTransportTests
{
    [Theory]
    [InlineData(null, "http://eventstore/api/v1/commands")]
    [InlineData("http://127.0.0.1:39081/", "http://127.0.0.1:39081/api/v1/commands")]
    public async Task ProductionComposedGatewayRelaysPublicHumanBearer(string? gateway, string expected)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        if (gateway is not null) builder.Configuration["EventStore:Gateway:BaseAddress"] = gateway;
        FolderGatewayTransportCaptureHandler capture = new();
        builder.Services.AddEventStoreGatewayClient().ConfigurePrimaryHttpMessageHandler(() => capture);
        await using WebApplication app = builder.Build();
        app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = PublicContext("/api/v1/folders", authenticated: true);

        SubmitCommandResponse response = await app.Services.GetRequiredService<IEventStoreGatewayClient>()
            .SubmitCommandAsync(Command(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.MessageId.ShouldBe("message-a");
        capture.Target!.ToString().ShouldBe(expected);
        capture.Authorization.ShouldBe("Bearer validated-human-token");
    }

    [Theory]
    [InlineData("/process", true)]
    [InlineData("/api/v1/unknown", true)]
    [InlineData("/api/v1/folders", false)]
    public async Task WrongRouteOrUnauthenticatedCallerCannotReachGateway(string path, bool authenticated)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        FolderGatewayTransportCaptureHandler capture = new();
        builder.Services.AddEventStoreGatewayClient().ConfigurePrimaryHttpMessageHandler(() => capture);
        await using WebApplication app = builder.Build();
        app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = PublicContext(path, authenticated);

        await Should.ThrowAsync<InvalidOperationException>(() => app.Services.GetRequiredService<IEventStoreGatewayClient>()
            .SubmitCommandAsync(Command(), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        capture.Target.ShouldBeNull();
    }

    private static DefaultHttpContext PublicContext(string path, bool authenticated)
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Headers.Authorization = "Bearer validated-human-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-a")], authenticated ? "Bearer" : null));
        return context;
    }

    private static SubmitCommandRequest Command() => new("message-a", "tenant-a", "folders", "folder-a", "CreateFolder",
        JsonSerializer.SerializeToElement(new { }), CorrelationId: "correlation-a");
}
