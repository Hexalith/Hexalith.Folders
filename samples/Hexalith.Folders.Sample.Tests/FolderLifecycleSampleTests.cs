using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Sample;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json.Linq;

using Shouldly;

using Xunit;

using GeneratedFoldersClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Sample.Tests;

public sealed class FolderLifecycleSampleTests
{
    private static readonly Uri BaseAddress = new("https://folders.example/");

    [Fact]
    public void SampleHostRegistersTypedClientAndBearerHandler()
    {
        using ServiceProvider provider = FoldersSampleHost.BuildServiceProvider(BaseAddress);

        provider.GetRequiredService<IClient>().ShouldBeOfType<GeneratedFoldersClient>();
        provider.GetService<BearerTokenHandler>().ShouldNotBeNull();
    }

    [Fact]
    public async Task LifecycleDrivesCanonicalSequenceUsingUploadHelper()
    {
        RecordingHandler handler = new();
        IClient client = new GeneratedFoldersClient(new HttpClient(handler) { BaseAddress = BaseAddress });

        List<string> log = [];
        FolderLifecycleSample sample = new(client, log.Add);

        await sample.RunAsync(
            new FolderLifecycleInputs { TaskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001" },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        // Canonical ordering of the mutating/query path segments the golden flow visits.
        string[] orderedPaths = handler.Requests.Select(r => r.Path).ToArray();
        IndexOfSuffix(orderedPaths, "/provider-readiness/validations").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/provider-bindings/provider_binding_01HZY7Z6N7J4Q2X8Y9V0PBR001"));
        IndexOfSuffix(orderedPaths, "/folders/repository-backed").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/provider-readiness/validations"));
        IndexOfSuffix(orderedPaths, "/preparation").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/folders/repository-backed"));
        IndexOfSuffix(orderedPaths, "/lock").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/preparation"));
        IndexOfSuffix(orderedPaths, "/files/add").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/lock"));
        IndexOfSuffix(orderedPaths, "/commits").ShouldBeGreaterThan(IndexOfSuffix(orderedPaths, "/files/add"));

        // The file step used the upload helper, producing an inline FileMutationRequest.
        RecordedRequest addFile = handler.Requests.Single(r => r.Path.EndsWith("/files/add", StringComparison.Ordinal));
        addFile.Headers["Idempotency-Key"].ShouldNotBeNullOrWhiteSpace();
        addFile.Headers["X-Correlation-Id"].ShouldNotBeNullOrWhiteSpace();
        addFile.Headers["X-Hexalith-Task-Id"].ShouldBe("task_01HZY7Z6N7J4Q2X8Y9V0TSK001");

        JObject body = JObject.Parse(addFile.Body!);
        body["transportOperation"]!.Value<string>().ShouldBe("PutFileInline");
        body["inlineContent"]!["contentBytes"].ShouldNotBeNull();

        log.ShouldContain("canonical lifecycle complete.");
    }

    [Fact]
    public async Task ExplicitCorrelationIdPropagatesToEveryRequest()
    {
        RecordingHandler handler = new();
        IClient client = new GeneratedFoldersClient(new HttpClient(handler) { BaseAddress = BaseAddress });

        FolderLifecycleSample sample = new(client, _ => { });

        await sample.RunAsync(
            new FolderLifecycleInputs
            {
                TaskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001",
                CorrelationId = "corr_explicit_01HZY7Z6N7J4Q2X8Y9V0COR777",
            },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Requests.ShouldNotBeEmpty();
        handler.Requests.ShouldAllBe(r => r.Headers["X-Correlation-Id"] == "corr_explicit_01HZY7Z6N7J4Q2X8Y9V0COR777");
    }

    [Fact]
    public async Task RegisteredCorrelationProviderSuppliesCorrelationIdWhenNoneIsExplicit()
    {
        RecordingHandler handler = new();
        IClient client = new GeneratedFoldersClient(new HttpClient(handler) { BaseAddress = BaseAddress });

        FolderLifecycleSample sample = new(client, _ => { }, new StubCorrelationIdProvider("corr_provider_01HZY7Z6N7J4Q2X8Y9V0COR888"));

        await sample.RunAsync(
            new FolderLifecycleInputs { TaskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001", CorrelationId = null },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Requests.ShouldNotBeEmpty();
        handler.Requests.ShouldAllBe(r => r.Headers["X-Correlation-Id"] == "corr_provider_01HZY7Z6N7J4Q2X8Y9V0COR888");
    }

    [Fact]
    public async Task LifecycleFailsClosedWhenTaskIdMissingBeforeAnyRequest()
    {
        RecordingHandler handler = new();
        IClient client = new GeneratedFoldersClient(new HttpClient(handler) { BaseAddress = BaseAddress });

        FolderLifecycleSample sample = new(client, _ => { });

        // Task ID is required and never SDK-generated; absence must fail before any wire call.
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => sample.RunAsync(new FolderLifecycleInputs { TaskId = "   " }, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task BearerTokenHandlerAttachesBearerAuthorizationWhenTokenPresent()
    {
        CapturingInnerHandler inner = new();
        BearerTokenHandler handler = new(_ => ValueTask.FromResult<string?>("synthetic-bearer-token")) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Get, BaseAddress);
        _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        inner.Request!.Headers.Authorization.ShouldNotBeNull();
        inner.Request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        inner.Request.Headers.Authorization.Parameter.ShouldBe("synthetic-bearer-token");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BearerTokenHandlerOmitsAuthorizationWhenTokenBlank(string? token)
    {
        CapturingInnerHandler inner = new();
        BearerTokenHandler handler = new(_ => ValueTask.FromResult(token)) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Get, BaseAddress);
        _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        inner.Request!.Headers.Authorization.ShouldBeNull();
    }

    private static int IndexOfSuffix(string[] paths, string suffix)
    {
        for (int index = 0; index < paths.Length; index++)
        {
            if (paths[index].EndsWith(suffix, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record RecordedRequest(string Method, string Path, IReadOnlyDictionary<string, string?> Headers, string? Body);

    private sealed class StubCorrelationIdProvider(string correlationId) : ICorrelationIdProvider
    {
        public string? GetCorrelationId() => correlationId;
    }

    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private const string AcceptedJson =
            """{"acceptedAt":"2026-05-27T12:00:00+00:00","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","taskId":"task_01HZY7Z6N7J4Q2X8Y9V0TSK001","status":"accepted","idempotentReplay":false}""";

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            Dictionary<string, string?> headers = new(StringComparer.Ordinal);
            foreach (string name in new[] { "Idempotency-Key", "X-Correlation-Id", "X-Hexalith-Task-Id" })
            {
                headers[name] = request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
            }

            Requests.Add(new RecordedRequest(request.Method.Method, path, headers, body));

            // Queries and the (non-mutating) provider-readiness validation answer 200; commands answer 202.
            bool isQueryOrReadiness =
                request.Method == HttpMethod.Get ||
                path.EndsWith("/provider-readiness/validations", StringComparison.Ordinal);

            HttpStatusCode status = isQueryOrReadiness ? HttpStatusCode.OK : HttpStatusCode.Accepted;
            string json = isQueryOrReadiness ? "{}" : AcceptedJson;

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }
}
