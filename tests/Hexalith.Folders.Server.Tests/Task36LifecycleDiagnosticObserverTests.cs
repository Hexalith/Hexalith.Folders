using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;
using Task36FoldObserver;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies actual framework lifecycle boundaries and payload-free advisory observation.</summary>
[Collection("Task36 fold observation")]
public sealed class Task36LifecycleDiagnosticObserverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealFrameworkLifecycleShouldRetainOnlyClosedMetadataWithoutResolvingTransientServices(bool concurrent)
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-observer-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.Configure<HostOptions>(options => options.ServicesStopConcurrently = concurrent);
        int constructed = 0;
        builder.Services.AddTransient<IHostedService>(_ =>
        {
            constructed++;
            LifecycleDiagnosticTestService service = new();
            service.Release.SetResult();
            return service;
        });
        AddObserver(builder, path);
        WebApplication app = builder.Build();
        LifecycleDiagnosticObserver observer = app.Services.GetRequiredService<LifecycleDiagnosticObserver>();
        try
        {
            app.MapGet("/{**path}", () => Results.NoContent());
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpClient client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Private", "private-header-sentinel");
            using HttpResponseMessage response = await client.GetAsync("/private-path-sentinel", TestContext.Current.CancellationToken);
            response.IsSuccessStatusCode.ShouldBeTrue();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            constructed.ShouldBe(1); // The host's one transient instance; observer never resolves another.
            JsonElement[] rows = Rows(path);
            string[] phases = rows.Select(row => row.GetProperty("phase").GetString()!).ToArray();
            foreach (string phase in new[] { "observer_registered", "host_start", "application_stopping", "host_stop", "application_stopped" })
                phases.ShouldContain(phase);
            Array.IndexOf(phases, "host_stop").ShouldBeLessThan(Array.IndexOf(phases, "application_stopped"));
            JsonElement config = rows.Single(row => row.GetProperty("phase").GetString() == "host_configuration");
            config.GetProperty("stopConcurrently").GetBoolean().ShouldBe(concurrent);
            config.GetProperty("serverRegisteredLast").GetBoolean().ShouldBeTrue();
            config.GetProperty("nonSingletonRegistrationCount").GetInt32().ShouldBe(1);
            config.GetProperty("serviceRegistrationOrder").EnumerateArray().Last().GetString().ShouldBe("web_server");
            foreach (JsonElement row in rows)
            {
                row.GetProperty("processId").GetInt32().ShouldBe(Environment.ProcessId);
                row.GetProperty("utc").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.MinValue);
                if (row.TryGetProperty("inFlightRequests", out JsonElement count)
                    && !row.GetProperty("phase").GetString()!.StartsWith("request_activity_", StringComparison.Ordinal))
                    count.GetInt64().ShouldBe(row.GetProperty("phase").GetString() == "request_start" ? 1 : 0);
            }
            File.ReadAllText(path).ShouldNotContain("private-");
            if (!OperatingSystem.IsWindows())
                File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ObserverShouldStayActiveAfterItsStopAndDistinguishServerStopFromLaterHostedStop()
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-pending-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        LifecycleDiagnosticTestService blocker = new();
        builder.Services.AddSingleton<IHostedService>(blocker);
        AddObserver(builder, path);
        WebApplication app = builder.Build();
        LifecycleDiagnosticObserver observer = app.Services.GetRequiredService<LifecycleDiagnosticObserver>();
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
            Task stop = app.StopAsync(TestContext.Current.CancellationToken);
            await blocker.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            stop.IsCompleted.ShouldBeFalse();
            using CancellationTokenSource inspection = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            inspection.CancelAfter(TimeSpan.FromSeconds(5));
            while (!File.Exists(path) || !Rows(path).Any(row => row.GetProperty("phase").GetString() == "stopping_snapshot"))
                await Task.Delay(20, inspection.Token);
            JsonElement snapshot = Rows(path).Last(row => row.GetProperty("phase").GetString() == "stopping_snapshot");
            snapshot.GetProperty("hostStopSeen").GetBoolean().ShouldBeTrue();
            snapshot.GetProperty("applicationStoppedSeen").GetBoolean().ShouldBeFalse();
            snapshot.GetProperty("droppedRecords").GetInt64().ShouldBe(0);
            snapshot.GetProperty("sinkFailed").GetBoolean().ShouldBeFalse();
            blocker.Release.SetResult();
            await stop.ConfigureAwait(true);
        }
        finally
        {
            blocker.Release.TrySetResult();
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SinkAndOptionalResolutionFailuresShouldRemainAdvisoryWithClosedEventIds(bool resolutionThrows)
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-advisory-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        ServiceCollection services = new();
        if (resolutionThrows)
            services.AddSingleton<IHostApplicationLifetime>(_ => throw new InvalidOperationException("private-provider-message"));
        using ServiceProvider provider = services.BuildServiceProvider();
        LifecycleDiagnosticObserver observer = LifecycleDiagnosticObserver.Create(provider, services,
            resolutionThrows ? path : Path.Combine(directory, "missing", "sink.jsonl"));
        try
        {
            await observer.StartAsync(TestContext.Current.CancellationToken);
            observer.ObserveEvent(3);
            observer.ObserveEvent(4);
            observer.ObserveEvent(999);
            observer.ObserveEvent(2);
            using CancellationTokenSource canceled = new();
            canceled.Cancel();
            observer.StopAsync(canceled.Token).IsCompletedSuccessfully.ShouldBeTrue();
            observer.Dispose();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (resolutionThrows)
            {
                string text = File.ReadAllText(path);
                text.ShouldNotContain("private-provider-message");
                Rows(path).ShouldAllBe(row => new[] { "setup_unavailable", "host_configuration", "observer_registered", "host_stop", "request_start", "request_end" }
                    .Contains(row.GetProperty("phase").GetString()));
                Rows(path).ShouldContain(row => row.GetProperty("phase").GetString() == "setup_unavailable");
            }
        }
        finally
        {
            observer.Dispose();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AddObserver(WebApplicationBuilder builder, string path)
    {
        builder.Services.AddSingleton(provider => LifecycleDiagnosticObserver.Create(provider, builder.Services, path));
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LifecycleDiagnosticObserver>());
    }

    private static JsonElement[] Rows(string path)
    {
        string text = File.ReadAllText(path);
        return text[..(text.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
    }
}
