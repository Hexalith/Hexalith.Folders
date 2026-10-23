using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.AdapterCore;
using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Tests.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>Exercises the bearer destination boundary without sending network requests.</summary>
public sealed class BearerTransportTests
{
    [Theory]
    [InlineData("https://remote.invalid/")]
    [InlineData("https://localhost.evil.invalid/")]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST:8080/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.1.2.3:8080/")]
    [InlineData("http://127.255.255.255/")]
    [InlineData("http://[::1]:8080/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://127.1/")]
    [InlineData("http://2130706433/")]
    public async Task SafeDestinationsAttachTheTokenAndSend(string destination)
    {
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, destination);

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        request.Headers.Authorization?.ToString().ShouldBe("Bearer synthetic-token");
        inner.Request.ShouldBeSameAs(request);
    }

    [Theory]
    [InlineData("http://remote.invalid/private?secret=hidden")]
    [InlineData("http://localhost.evil.invalid/")]
    [InlineData("http://localhost./")]
    [InlineData("http://127.0.0.1.evil.invalid/")]
    [InlineData("http://128.0.0.1/")]
    [InlineData("http://[::2]/")]
    [InlineData("http://[::ffff:127.1.2.3]/")]
    [InlineData("http://[::ffff:128.0.0.1]/")]
    [InlineData("http://[::ffff:126.255.255.255]/")]
    [InlineData("http://2147483648/")]
    [InlineData("http://126.255.255.255/")]
    [InlineData("ftp://localhost/private")]
    [InlineData("/relative")]
    [InlineData(null)]
    public async Task UnsafeDestinationsRejectBeforeCredentialAcquisitionAttachmentAndInnerSend(string? destination)
    {
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, destination is null ? null : new Uri(destination, UriKind.RelativeOrAbsolute));

        UnsafeBearerTransportException exception = await Should.ThrowAsync<UnsafeBearerTransportException>(
            () => invoker.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Bearer transport requires HTTPS or HTTP loopback.");
        exception.ToString().ShouldNotContain("synthetic-token");
        exception.ToString().ShouldNotContain("hidden");
        request.Headers.Authorization.ShouldBeNull();
        inner.Request.ShouldBeNull();
    }

    [Fact]
    public async Task UnsafeAbsoluteRequestCannotOverrideASafeBase()
    {
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpClient client = new(bearer) { BaseAddress = new Uri("https://safe.invalid/") };

        await Should.ThrowAsync<UnsafeBearerTransportException>(
            () => client.GetAsync("http://unsafe.invalid/?hidden=value", TestContext.Current.CancellationToken));
        inner.Request.ShouldBeNull();
    }

    [Fact]
    public async Task CancellationStopsBeforeTokenAcquisitionAndSend()
    {
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://safe.invalid/");
        using CancellationTokenSource source = new();
        source.Cancel();

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => invoker.SendAsync(request, source.Token));

        exception.CancellationToken.ShouldBe(source.Token);
        request.Headers.Authorization.ShouldBeNull();
        inner.Request.ShouldBeNull();
    }

    [Fact]
    public void ProductionPrimaryTransportDisablesAutomaticRedirects()
    {
        using HttpClientHandler handler = BearerTransportPolicy.CreatePrimaryHandler();

        handler.AllowAutoRedirect.ShouldBeFalse();
    }

    [Fact]
    public void ProductionClientFactoryUsesTheRedirectDisabledPrimaryTransport()
    {
        Client.Generated.IClient client = FoldersClientFactory.Create(new Uri("https://safe.invalid/"), "synthetic-token");
        System.Reflection.FieldInfo clientField = client.GetType().GetField("_httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        using HttpClient httpClient = (HttpClient)clientField.GetValue(client)!;
        System.Reflection.FieldInfo handlerField = typeof(HttpMessageInvoker).GetField("_handler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        HttpMessageHandler handler = (HttpMessageHandler)handlerField.GetValue(httpClient)!;
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        handler.ShouldBeOfType<HttpClientHandler>().AllowAutoRedirect.ShouldBeFalse();
        ((HttpClientHandler)handler).Proxy.ShouldBeOfType<LoopbackBypassProxy>();
    }

    [Theory]
    [InlineData("http://remote.invalid/private?hidden=value")]
    [InlineData("http://localhost.evil.invalid/")]
    [InlineData("file:///private/hidden")]
    [InlineData("relative")]
    [InlineData(null)]
    public async Task UnsafeConfigurationFailsBeforeResolvingCredentials(string? destination)
    {
        TestCliConsole console = new();
        Commands.CommandPipeline pipeline = new(new CliDependencies
        {
            Console = console,
            Credentials = new Credentials.CredentialResolver(_ => throw new InvalidOperationException("Credential read"), "/unused-file"),
            ClientFactory = (_, _) => throw new InvalidOperationException("Client created"),
        });
        GlobalOptions global = new(destination, "synthetic-token", "correlation-safe", OutputMode.Json);

        int exit = await pipeline.ExecuteQueryAsync(global, null, false, false,
            (_, _, _) => throw new InvalidOperationException("SDK invoked"), TestContext.Current.CancellationToken);

        exit.ShouldBe(64);
        console.StdErr.ShouldContain("client_configuration_error");
        console.StdErr.ShouldContain("Bearer transport requires HTTPS or HTTP loopback.");
        console.StdErr.ShouldContain("correlation-safe");
        console.StdErr.ShouldNotContain("hidden");
        console.StdErr.ShouldNotContain("synthetic-token");
    }

    [Fact]
    public async Task PerRequestRejectionUsesTheConfigurationProjectionAndRetainsCorrelation()
    {
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpClient httpClient = new(bearer) { BaseAddress = new Uri("http://unsafe-destination.invalid/private-marker/") };
        Client.Generated.IClient client = new Client.Generated.Client(httpClient);
        TestCliConsole console = new();
        Commands.CommandPipeline pipeline = new(new CliDependencies
        {
            Console = console,
            Credentials = new Credentials.CredentialResolver(_ => "synthetic-token", "/unused-file"),
            ClientFactory = (_, _) => client,
        });
        GlobalOptions global = new("https://safe.invalid/", null, "correlation-rejected", OutputMode.Json);

        int exit = await pipeline.ExecuteQueryAsync(global, null, false, false,
            async (sdk, source, cancellation) => await sdk.GetFolderLifecycleStatusAsync("folder-1", source.CorrelationId, null, cancellation).ConfigureAwait(false),
            TestContext.Current.CancellationToken);

        exit.ShouldBe(64);
        console.StdErr.ShouldContain("client_configuration_error");
        console.StdErr.ShouldContain("correlation-rejected");
        console.StdErr.ShouldContain(BearerTransportPolicy.RejectionMessage);
        console.StdErr.ShouldNotContain("unsafe-destination.invalid");
        console.StdErr.ShouldNotContain("private-marker");
        console.StdErr.ShouldNotContain("synthetic-token");
        inner.SendCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("http://LOCALHOST:8080/", true)]
    [InlineData("http://127.1/", true)]
    [InlineData("http://2130706433/", true)]
    [InlineData("http://[::1]/", true)]
    [InlineData("http://[::ffff:127.0.0.1]/", true)]
    [InlineData("https://remote.invalid/", false)]
    [InlineData("https://localhost/", false)]
    [InlineData("http://remote.invalid/", false)]
    public void ProxyBypassesHttpLoopbackAndDelegatesOrdinaryProxyBehavior(string destination, bool bypass)
    {
        Uri address = new(destination);
        Uri remoteProxy = new("http://configured-proxy.invalid:8080/");
        IWebProxy configured = Substitute.For<IWebProxy>();
        configured.GetProxy(Arg.Any<Uri>()).Returns(remoteProxy);
        configured.IsBypassed(Arg.Any<Uri>()).Returns(false);
        LoopbackBypassProxy proxy = new(configured);

        proxy.IsBypassed(address).ShouldBe(bypass);
        proxy.GetProxy(address).ShouldBe(bypass ? address : remoteProxy);
        if (bypass)
        {
            configured.DidNotReceive().IsBypassed(address);
            configured.DidNotReceive().GetProxy(address);
        }
        else
        {
            configured.Received(1).IsBypassed(address);
            configured.Received(1).GetProxy(address);
        }

        ICredentials credentials = new NetworkCredential("synthetic-user", "synthetic-password");
        proxy.Credentials = credentials;
        configured.Credentials.ShouldBeSameAs(credentials);
        proxy.Credentials.ShouldBeSameAs(credentials);
    }

    [Fact]
    public async Task RegisteredProductionPipelineDoesNotLogRejectedDestinationOrCredentials()
    {
        ILogger logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ILoggerProvider sink = Substitute.For<ILoggerProvider>();
        sink.CreateLogger(Arg.Any<string>()).Returns(logger);
        ServiceCollection services = new();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(sink));
        CapturingHttpHandler inner = new(HttpStatusCode.OK, "{}");
        IHttpClientBuilder registration = FoldersClientFactory.Register(services, new Uri("https://safe.invalid/"), "synthetic-token");
        registration.ConfigurePrimaryHttpMessageHandler(() => inner);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(registration.Name);
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("metadata-probe").LogInformation("metadata-only marker");

        await Should.ThrowAsync<UnsafeBearerTransportException>(() => client.GetAsync(
            "http://unsafe-destination.invalid/private-marker?hidden=value", TestContext.Current.CancellationToken));

        string recorded = string.Join(Environment.NewLine, logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name is "Log" or "BeginScope")
            .Select(call => string.Join(" ", call.GetArguments().Select(argument => argument?.ToString()))));
        recorded.ShouldContain("metadata-only marker");
        recorded.ShouldNotContain("unsafe-destination.invalid");
        recorded.ShouldNotContain("private-marker");
        recorded.ShouldNotContain("hidden");
        recorded.ShouldNotContain("synthetic-token");
        inner.SendCount.ShouldBe(0);
    }

    [Fact]
    public async Task RedirectResponseRetainsTheExistingInternalErrorProjectionAndOneInitialRequest()
    {
        CapturingHttpHandler inner = new(HttpStatusCode.Redirect, "{}", new Uri("http://redirect.invalid/private-marker"));
        BearerTokenHandler bearer = new("synthetic-token") { InnerHandler = inner };
        using HttpClient httpClient = new(bearer) { BaseAddress = new Uri("https://safe.invalid/") };
        Client.Generated.IClient client = new Client.Generated.Client(httpClient);
        TestCliConsole console = new();
        Commands.CommandPipeline pipeline = new(new CliDependencies
        {
            Console = console,
            Credentials = new Credentials.CredentialResolver(_ => "synthetic-token", "/unused-file"),
            ClientFactory = (_, _) => client,
        });
        GlobalOptions global = new("https://safe.invalid/", null, "correlation-redirect", OutputMode.Json);

        int exit = await pipeline.ExecuteQueryAsync(global, null, false, false,
            async (sdk, source, cancellation) => await sdk.GetFolderLifecycleStatusAsync("folder-1", source.CorrelationId, null, cancellation).ConfigureAwait(false),
            TestContext.Current.CancellationToken);

        exit.ShouldBe(1);
        console.StdErr.ShouldContain("internal_error");
        console.StdErr.ShouldContain("correlation-redirect");
        console.StdErr.ShouldNotContain("redirect.invalid");
        console.StdErr.ShouldNotContain("private-marker");
        console.StdErr.ShouldNotContain("synthetic-token");
        inner.SendCount.ShouldBe(1);
        inner.Request!.RequestUri!.Host.ShouldBe("safe.invalid");
    }

}
