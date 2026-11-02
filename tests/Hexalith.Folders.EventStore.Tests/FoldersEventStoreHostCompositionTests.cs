using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Guards the real command host's safe-denial registration and handler ordering.</summary>
public sealed class FoldersEventStoreHostCompositionTests
{
    /// <summary>Executes the actual gateway pipeline with its public inventories and distinct human/channel credentials.</summary>
    [Fact]
    public async Task CommandHostEnrollsHttpSecurityAndSeparatesHumanDocumentationFromSidecarCallbacks()
    {
        using FoldersEventStoreHostFactory factory = new(enableSignalR: true);
        using HttpClient client = factory.CreateClient();
        factory.Services.GetServices<IHostedService>().ShouldContain(static service => service.GetType().Name == "EventStoreHttpHostSecurityStartupValidator");
        factory.Services.GetServices<IHostedService>().ShouldContain(static service => service.GetType().Name == "EventStoreAuthorizationStartupValidator");
        Endpoint[] endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.ToArray();
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, factory.Services).ConfigureAwait(true)).ShouldBeEmpty();
        EventStoreDomainServiceEndpointInventory.Validate(endpoints, null).ShouldBeEmpty();

        foreach (string route in new[] { "/swagger/index.html", "/openapi/v1.json" })
        {
            using HttpResponseMessage anonymous = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
            anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            using HttpRequestMessage channel = new(HttpMethod.Get, route);
            channel.Headers.Add(DaprAppChannelToken.HeaderName, FoldersEventStoreHostFactory.ChannelToken);
            using HttpResponseMessage channelResponse = await client.SendAsync(channel, TestContext.Current.CancellationToken).ConfigureAwait(true);
            channelResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        foreach (string route in new[] { "/swagger/index.html", "/hubs/projection-changes/negotiate?negotiateVersion=1" })
        {
            using HttpRequestMessage human = new(route.Contains("negotiate", StringComparison.Ordinal) ? HttpMethod.Post : HttpMethod.Get, route);
            human.Headers.Authorization = new("Bearer", HumanToken());
            using HttpResponseMessage response = await client.SendAsync(human, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, route);
        }

        foreach (string credential in new[] { "absent", "channel", "workload", "mixed" })
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/hubs/projection-changes/negotiate?negotiateVersion=1");
            if (credential is "channel" or "mixed") request.Headers.Add(DaprAppChannelToken.HeaderName, FoldersEventStoreHostFactory.ChannelToken);
            if (credential is "workload" or "mixed") request.Headers.Authorization = new("Bearer", HumanToken(workload: credential == "workload"));
            if (credential == "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, HumanToken(workload: true));
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        foreach (string credential in new[] { "absent", "wrong", "human", "channel" })
        {
            using HttpRequestMessage discovery = new(HttpMethod.Get, "/dapr/subscribe");
            if (credential is "wrong" or "channel")
                discovery.Headers.Add(DaprAppChannelToken.HeaderName, credential == "channel" ? FoldersEventStoreHostFactory.ChannelToken : "wrong-channel");
            if (credential == "human") discovery.Headers.Authorization = new("Bearer", HumanToken());
            using HttpResponseMessage response = await client.SendAsync(discovery, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(credential == "channel" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, credential);
        }
    }

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

    private static string HumanToken(bool workload = false)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "test-issuer",
            Audience = "eventstore",
            Subject = new ClaimsIdentity(workload
                ? [new Claim("sub", "gateway-composition-workload"), new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "caller")]
                : [new Claim("sub", "gateway-composition-human")]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-with-at-least-thirty-two-bytes")), SecurityAlgorithms.HmacSha256),
        });
}
