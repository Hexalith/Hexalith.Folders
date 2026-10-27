using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FoldersNativeGatewayBearerTests
{
    [Theory]
    [InlineData("authorized")]
    [InlineData("unmarked")]
    [InlineData("payload")]
    [InlineData("task")]
    [InlineData("command")]
    [InlineData("path")]
    public async Task ActualGatewayClientForwardsOnlyTheExactIndependentlyAuthorizedNativeSubmission(string scenario)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "human-a")], "Bearer")) };
        context.Request.Method = "POST"; context.Request.Path = "/api/v1/folders/folder-a/workspaces/workspace-a/native/task-lease";
        context.Request.Headers.Authorization = "Bearer dedicated-human";
        var authorized = new SubmitCommandRequest("message-a", "tenant-a", "folders", "folder-a", FoldersServerModule.NativeTaskCommandType,
            JsonSerializer.SerializeToElement(new { workspaceId = "workspace-a", taskId = "task-a", expectedHead = 1, request = new { operation = "acquire", requestedLeaseSeconds = 300 } }),
            "correlation-a", new Dictionary<string, string> { ["taskId"] = "task-a" }, "key-a");
        if (scenario != "unmarked") context.Items[WorkspaceNativeEndpoints.AuthorizedSubmissionItem] = authorized;
        var sent = scenario switch
        {
            "payload" => authorized with { Payload = JsonSerializer.SerializeToElement(new { workspaceId = "foreign" }) },
            "task" => authorized with { Extensions = new Dictionary<string, string> { ["taskId"] = "foreign" } },
            "command" => authorized with { CommandType = FoldersServerModule.ArchiveFolderCommandType },
            _ => authorized,
        };
        using var capture = new BearerCapture();
        using var forwarding = new FoldersGatewayHumanBearerHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = capture };
        using var http = new HttpClient(forwarding) { BaseAddress = new Uri("https://gateway.invalid/") };
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions { CommandPath = scenario == "path" ? "other" : "api/v1/commands" }));
        if (scenario == "authorized")
        {
            _ = await gateway.SubmitCommandAsync(sent, TestContext.Current.CancellationToken).ConfigureAwait(true);
            capture.Bearer.ShouldBe("dedicated-human"); capture.Count.ShouldBe(1);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => gateway.SubmitCommandAsync(sent, TestContext.Current.CancellationToken)).ConfigureAwait(true);
            capture.Count.ShouldBe(0);
        }
    }
    private sealed class BearerCapture : HttpMessageHandler
    {
        internal string? Bearer; internal int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Count++; Bearer = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(new { correlationId = "correlation-a", messageId = "message-a" }) });
        }
    }
}
