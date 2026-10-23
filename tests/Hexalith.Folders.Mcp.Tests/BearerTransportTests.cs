using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.AdapterCore;
using Hexalith.Folders.Mcp.Composition;
using Hexalith.Folders.Mcp.Configuration;
using Hexalith.Folders.Mcp.Credentials;
using Hexalith.Folders.Mcp.Tooling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

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
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new(TestSupport.Resolver("synthetic-token")) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, destination);

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        request.Headers.Authorization?.ToString().ShouldBe("Bearer synthetic-token");
        inner.Requests.ShouldHaveSingleItem();
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
        int reads = 0;
        McpCredentialResolver credentials = new(new FoldersMcpAuthOptions(), _ => { reads++; return "synthetic-token"; }, _ => throw new InvalidOperationException("File read"));
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new(credentials) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, destination is null ? null : new Uri(destination, UriKind.RelativeOrAbsolute));

        UnsafeBearerTransportException exception = await Should.ThrowAsync<UnsafeBearerTransportException>(
            () => invoker.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Bearer transport requires HTTPS or HTTP loopback.");
        exception.ToString().ShouldNotContain("synthetic-token");
        exception.ToString().ShouldNotContain("hidden");
        request.Headers.Authorization.ShouldBeNull();
        reads.ShouldBe(0);
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnsafeAbsoluteRequestCannotOverrideASafeBase()
    {
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new(TestSupport.Resolver("synthetic-token")) { InnerHandler = inner };
        using HttpClient client = new(bearer) { BaseAddress = new Uri("https://safe.invalid/") };

        await Should.ThrowAsync<UnsafeBearerTransportException>(
            () => client.GetAsync("http://unsafe.invalid/?hidden=value", TestContext.Current.CancellationToken));
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationStopsBeforeTokenAcquisitionAndSend()
    {
        McpCredentialResolver credentials = new(new FoldersMcpAuthOptions(), _ => throw new InvalidOperationException("Token read after cancellation"));
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new(credentials) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(bearer);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://safe.invalid/");
        using CancellationTokenSource source = new();
        source.Cancel();

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => invoker.SendAsync(request, source.Token));

        exception.CancellationToken.ShouldBe(source.Token);
        request.Headers.Authorization.ShouldBeNull();
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void ProductionPrimaryTransportDisablesAutomaticRedirects()
    {
        ServiceCollection services = new();
        services.AddSingleton(TestSupport.Resolver("synthetic-token"));
        IHttpClientBuilder registration = FoldersClientRegistration.Register(services, new Uri("https://safe.invalid/"));
        using ServiceProvider provider = services.BuildServiceProvider();
        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(registration.Name);
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        handler.ShouldBeOfType<HttpClientHandler>().AllowAutoRedirect.ShouldBeFalse();
        ((HttpClientHandler)handler).Proxy.ShouldBeOfType<LoopbackBypassProxy>();
    }

    [Fact]
    public async Task PerCallRejectionIsUsageErrorWithCorrelationAndNoDiagnosticLeak()
    {
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        BearerTokenHandler bearer = new(TestSupport.Resolver("synthetic-token")) { InnerHandler = inner };
        using HttpClient client = new(bearer) { BaseAddress = new Uri("https://safe.invalid/") };
        ToolPipeline pipeline = TestSupport.Pipeline(NSubstitute.Substitute.For<Client.Generated.IClient>());

        string result = await pipeline.ExecuteQueryAsync(null, false, "correlation-rejected", async (_, _, cancellation) =>
        {
            using HttpResponseMessage response = await client.GetAsync("http://unsafe.invalid/?hidden=value", cancellation).ConfigureAwait(false);
            return null;
        }, TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("usage_error");
        TestSupport.CorrelationId(result).ShouldBe("correlation-rejected");
        result.ShouldContain(BearerTransportPolicy.RejectionMessage);
        result.ShouldNotContain("hidden");
        result.ShouldNotContain("unsafe.invalid");
        result.ShouldNotContain("synthetic-token");
        inner.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://remote.invalid/private?hidden=value")]
    [InlineData("http://localhost.evil.invalid/")]
    [InlineData("ftp://localhost/")]
    [InlineData("relative")]
    [InlineData("")]
    public async Task UnsafeStartupConfigurationExitsOneWithConstantDiagnostic(string destination)
    {
        System.Diagnostics.ProcessStartInfo start = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(typeof(BearerTokenHandler).Assembly.Location);
        start.Environment["HEXALITH_FOLDERS_BASE_ADDRESS"] = destination;
        start.Environment["HEXALITH_TOKEN"] = "synthetic-token";
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            process.ExitCode.ShouldBe(1);
            (await output.ConfigureAwait(true)).ShouldBeEmpty();
            (await error.ConfigureAwait(true)).Trim().ShouldBe(BearerTransportPolicy.RejectionMessage);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token);
            }
        }
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
        services.AddSingleton(TestSupport.Resolver("synthetic-token"));
        TestSupport.CapturingHandler inner = new(HttpStatusCode.OK, "{}");
        IHttpClientBuilder registration = FoldersClientRegistration.Register(services, new Uri("https://safe.invalid/"));
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
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RedirectResponseRetainsTheExistingInternalErrorProjectionAndOneInitialRequest()
    {
        TestSupport.CapturingHandler inner = new(HttpStatusCode.Redirect, "{}", location: new Uri("http://redirect.invalid/private-marker"));
        BearerTokenHandler bearer = new(TestSupport.Resolver("synthetic-token")) { InnerHandler = inner };
        using HttpClient httpClient = new(bearer) { BaseAddress = new Uri("https://safe.invalid/") };
        ToolPipeline pipeline = TestSupport.Pipeline(new Client.Generated.Client(httpClient));

        string result = await pipeline.ExecuteQueryAsync(null, false, "correlation-redirect",
            async (sdk, source, cancellation) => await sdk.GetFolderLifecycleStatusAsync("folder-1", source.CorrelationId, null, cancellation).ConfigureAwait(false),
            TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe("internal_error");
        TestSupport.CorrelationId(result).ShouldBe("correlation-redirect");
        result.ShouldNotContain("redirect.invalid");
        result.ShouldNotContain("private-marker");
        result.ShouldNotContain("synthetic-token");
        inner.Requests.ShouldHaveSingleItem().Uri!.Host.ShouldBe("safe.invalid");
    }

}
