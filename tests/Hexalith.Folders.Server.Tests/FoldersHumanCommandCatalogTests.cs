using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Checks the existing human transport against actual catalog eligibility without replacing downstream authorization.</summary>
public sealed class FoldersHumanCommandCatalogTests
{
    [Theory]
    [InlineData("ui")]
    [InlineData("agent")]
    public void EveryExistingCommandRetainsItsSupportedHumanEligibility(string surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        string[] publishedCommands = typeof(FoldersServerModule).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("CommandType", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!).Order(StringComparer.Ordinal).ToArray();
        var routes = CompiledRouteCatalogDeclarations.Read([typeof(FoldersRuntimeCatalogDeclarations).Assembly]);
        var operations = CompiledRouteCatalogDeclarations.ReadOperations([typeof(FoldersRuntimeCatalogDeclarations).Assembly]);
        var commands = operations.Where(operation => operation.Kind == "command").ToArray();
        commands.Select(operation => operation.MessageType).Order(StringComparer.Ordinal).ShouldBe(publishedCommands);
        commands.Length.ShouldBe(18);
        ClaimsPrincipal human = Human(surface);
        EventStoreHttpHostSecurityExtensions.IsHuman(human).ShouldBeTrue();
        foreach (var operation in commands)
        {
            operation.Ingress.ShouldBe("public");
            operation.Eligibility.ShouldBe("agent-eligible");
            operation.Digest.ShouldBe(RouteCatalogCodec.ComputeOperationContractDigest(operation));
            RouteCatalogSurfacePolicy.Allows(operation, human).ShouldBeTrue();
            RouteCatalogSurfacePolicy.Allows(operation, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(RouteCatalogSurfacePolicy.ClaimType, surface)]))).ShouldBeFalse();
            RouteCatalogSurfacePolicy.Allows(operation, new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "human-a")], "Bearer")))
                .ShouldBeFalse();
        }
        var internalOperations = operations.Where(operation => routes.Single(route => route.RouteId == operation.RouteId).Kind != "command").ToArray();
        internalOperations.Length.ShouldBe(5);
        foreach (var operation in internalOperations)
        {
            operation.Ingress.ShouldBe("internal");
            RouteCatalogSurfacePolicy.Allows(operation, human, verifiedOriginatingProof: true).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(FoldersServerModule.CreateFolderCommandType, "/api/v1/folders")]
    [InlineData(FoldersServerModule.CommitWorkspaceCommandType, "/api/v1/folders/folder-a/workspaces/workspace-a/commits")]
    [InlineData(FoldersServerModule.NativeTaskCommandType, "/api/v1/folders/folder-a/workspaces/workspace-a/native/task-lease")]
    [InlineData(FoldersServerModule.AuthorizeContentErasureCommandType, "/api/v1/folders/folder-a/workspaces/workspace-a/content/disposition")]
    public async Task ProductionForwarderAndActualOperationPolicyAgreeOnTheExistingHumanCaller(string commandType, string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        FolderGatewayTransportCaptureHandler capture = new();
        builder.Services.AddEventStoreGatewayClient().ConfigurePrimaryHttpMessageHandler(() => capture);
        await using WebApplication app = builder.Build();
        var context = new DefaultHttpContext { User = Human("ui") };
        context.Request.Method = "POST";
        context.Request.Path = route;
        context.Request.Headers.Authorization = "Bearer validated-human-token";
        var command = new SubmitCommandRequest("message-a", "tenant-a", "folders", "folder-a", commandType,
            JsonSerializer.SerializeToElement(new { workspaceId = "workspace-a", taskId = "task-a" }), "correlation-a",
            new Dictionary<string, string> { ["taskId"] = "task-a" }, "key-a");
        if (commandType == FoldersServerModule.NativeTaskCommandType)
            context.Items[WorkspaceNativeEndpoints.AuthorizedSubmissionItem] = command;
        if (commandType == FoldersServerModule.AuthorizeContentErasureCommandType)
            context.Items["Folders:ContentRetentionAuthorized"] = true;
        app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        // The existing independently authorized transport marks are supplied above; this check
        // qualifies credential forwarding and catalog eligibility, not a new domain grant.
        (await app.Services.GetRequiredService<IEventStoreGatewayClient>()
            .SubmitCommandAsync(command, TestContext.Current.CancellationToken).ConfigureAwait(true)).MessageId.ShouldBe("message-a");
        capture.Target!.AbsolutePath.ShouldBe("/api/v1/commands");
        capture.Authorization.ShouldBe("Bearer validated-human-token");
        var operation = FoldersRuntimeCatalogDeclarations.Operations.Single(operation => operation.MessageType == commandType);
        RouteCatalogSurfacePolicy.Allows(operation, context.User).ShouldBeTrue();
    }

    [Fact]
    public async Task HumanCommandMetadataDoesNotMapPublicV2Routes()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        FoldersApiRouting.ResolveMode(builder.Configuration).ShouldBe(FoldersApiRoutingMode.V1Only);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        string[] before = EndpointPatterns(app);
        _ = CompiledRouteCatalogDeclarations.ReadOperations([typeof(FoldersRuntimeCatalogDeclarations).Assembly]);
        EndpointPatterns(app).ShouldBe(before);
        before.ShouldNotContain(pattern => pattern.StartsWith("/api/v2", StringComparison.OrdinalIgnoreCase));
        FoldersRuntimeCatalogDeclarations.Routes.ShouldAllBe(route => route.ContractVersion == "v1");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync("/api/v2/folders/folder-a/lifecycle-status", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private static ClaimsPrincipal Human(string surface)
        => new(new ClaimsIdentity([new Claim("sub", "human-a"), new Claim(RouteCatalogSurfacePolicy.ClaimType, surface)], "Bearer"));

    private static string[] EndpointPatterns(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
}
