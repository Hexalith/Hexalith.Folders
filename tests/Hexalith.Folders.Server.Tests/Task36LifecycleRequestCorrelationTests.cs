using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;
using Task36FoldObserver;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies actual framework opaque request correlation and bounded refusal without request data.</summary>
[Collection("Task36 fold observation")]
public sealed class Task36LifecycleRequestCorrelationTests
{
    [Fact]
    public async Task RealHeldFrameworkRequestShouldKeepTheSameOpaqueIdentityThroughStoppingAndDrain()
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-correlation-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        string sentinel = "private-" + Guid.NewGuid().ToString("N");
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(provider => LifecycleDiagnosticObserver.Create(provider, builder.Services, path));
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LifecycleDiagnosticObserver>());
        WebApplication app = builder.Build();
        LifecycleDiagnosticObserver observer = app.Services.GetRequiredService<LifecycleDiagnosticObserver>();
        TaskCompletionSource<(string Trace, string Span)> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            app.MapPost("/{**path}", async () =>
            {
                Activity activity = Activity.Current.ShouldNotBeNull();
                activity.SetTag(sentinel, sentinel);
                activity.AddBaggage(sentinel, sentinel);
                activity.TraceStateString = "private=" + sentinel;
                entered.TrySetResult((activity.TraceId.ToHexString(), activity.SpanId.ToHexString()));
                await release.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            });
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Add("X-Private", sentinel);
            using StringContent body = new(sentinel);
            Task<HttpResponseMessage> request = client.PostAsync("/" + sentinel + "?private=" + sentinel, body, TestContext.Current.CancellationToken);
            (string trace, string span) = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Task stop = app.StopAsync(TestContext.Current.CancellationToken);
            JsonElement snapshot = await WaitForPhaseAsync(path, "stopping_snapshot");
            stop.IsCompleted.ShouldBeFalse();
            snapshot.GetProperty("inFlightRequests").GetInt64().ShouldBe(1);
            snapshot.GetProperty("hostStopSeen").GetBoolean().ShouldBeFalse();
            JsonElement active = snapshot.GetProperty("requestActivityCorrelation").GetProperty("activeRequests").EnumerateArray().ShouldHaveSingleItem();
            active.GetProperty("traceId").GetString().ShouldBe(trace);
            active.GetProperty("spanId").GetString().ShouldBe(span);
            active.GetProperty("elapsedMilliseconds").GetDouble().ShouldBeGreaterThan(0);

            release.SetResult();
            using HttpResponseMessage response = await request.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            response.IsSuccessStatusCode.ShouldBeTrue();
            await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            JsonElement[] rows = Rows(path);
            JsonElement started = rows.Single(row => row.GetProperty("phase").GetString() == "request_activity_start").GetProperty("correlation");
            JsonElement ended = rows.Single(row => row.GetProperty("phase").GetString() == "request_activity_end").GetProperty("correlation");
            started.GetProperty("status").GetString().ShouldBe("tracked");
            ended.GetProperty("status").GetString().ShouldBe("matched");
            ended.GetProperty("reason").GetString().ShouldBe("trace_span");
            foreach (JsonElement correlation in new[] { started, active, ended })
            {
                correlation.GetProperty("traceId").GetString().ShouldBe(trace);
                correlation.GetProperty("spanId").GetString().ShouldBe(span);
                correlation.GetProperty("requestOrdinal").GetInt64().ShouldBe(started.GetProperty("requestOrdinal").GetInt64());
                foreach (string key in new[] { "frameworkActivityId", "relatedActivityId" })
                    if (correlation.GetProperty(key).ValueKind != JsonValueKind.Null)
                        Guid.TryParse(correlation.GetProperty(key).GetString(), out _).ShouldBeTrue();
            }
            JsonElement stopped = rows.Single(row => row.GetProperty("phase").GetString() == "application_stopped");
            stopped.GetProperty("inFlightRequests").GetInt64().ShouldBe(0);
            stopped.GetProperty("requestActivityCorrelation").GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
            stopped.GetProperty("droppedRecords").GetInt64().ShouldBe(0);
            File.ReadAllText(path).ShouldNotContain(sentinel);
            File.ReadAllText(path).ShouldNotContain("tracestate", Case.Insensitive);
            Console.WriteLine($"PASS framework existing request Activity: trace={trace}; span={span}; matched_start_snapshot_end=True; drained=True; private_sentinels_absent=True.");
        }
        finally
        {
            release.TrySetResult();
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ObservationMustNotCreateARequestActivityWhenExistingCreationIsDisabled()
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-no-activity-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(provider => LifecycleDiagnosticObserver.Create(provider, builder.Services, path));
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LifecycleDiagnosticObserver>());
        WebApplication app = builder.Build();
        LifecycleDiagnosticObserver observer = app.Services.GetRequiredService<LifecycleDiagnosticObserver>();
        try
        {
            app.MapGet("/", () =>
            {
                Activity.Current.ShouldBeNull();
                return Results.NoContent();
            });
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
            using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);
            response.IsSuccessStatusCode.ShouldBeTrue();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            JsonElement[] rows = Rows(path);
            rows.Count(row => row.GetProperty("phase").GetString() == "request_start").ShouldBe(1);
            rows.Count(row => row.GetProperty("phase").GetString() == "request_end").ShouldBe(1);
            rows.ShouldNotContain(row => row.GetProperty("phase").GetString()!.StartsWith("request_activity_", StringComparison.Ordinal));
            rows.Single(row => row.GetProperty("phase").GetString() == "application_stopped")
                .GetProperty("inFlightRequests").GetInt64().ShouldBe(0);
            Console.WriteLine("PASS framework request Activity remains absent with opaque observation installed and independent creation disabled.");
        }
        finally
        {
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn", false)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", true)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", true)]
    [InlineData("Microsoft.AspNetCore.Hosting.BeginRequest", false)]
    public void LateNotificationPredicateMustNotEnableActivityCreation(string name, bool enabled)
        => LifecycleActivityObservation.IsEnabled(name).ShouldBe(enabled);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateNotificationsMustIgnoreValuesAndIsolateCallbackFailureAndDisposal(bool throwFromCallback)
    {
        int calls = 0;
        Activity? before = Activity.Current;
        using DiagnosticListener listener = new("Microsoft.AspNetCore");
        using LifecycleActivityObservation observation = new(_ =>
        {
            calls++;
            if (throwFromCallback) throw new InvalidOperationException("private-" + Guid.NewGuid().ToString("N"));
        });
        listener.IsEnabled("Microsoft.AspNetCore.Hosting.HttpRequestIn").ShouldBeFalse();
        listener.IsEnabled("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start").ShouldBeTrue();
        listener.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", new object());
        calls.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(before);
        observation.Dispose();
        listener.IsEnabled("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start").ShouldBeFalse();
        listener.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", new object());
        calls.ShouldBe(1);
    }

    [Fact]
    public void MissingAndRelatedOnlyIdsShouldStayUnavailableWhileAnExactFrameworkIdCanMatch()
    {
        Activity? previous = Activity.Current;
        Activity.Current = null;
        try
        {
            LifecycleRequestCorrelation tracker = new();
            Element(tracker.Observe(true, 1, Guid.Empty, Guid.NewGuid())).GetProperty("reason").GetString().ShouldBe("identity_unavailable");
            Element(tracker.Observe(false, 0, Guid.NewGuid(), Guid.Empty)).GetProperty("status").GetString().ShouldBe("unavailable");
            Guid id = Guid.NewGuid();
            JsonElement start = Element(tracker.Observe(true, 1, id, Guid.NewGuid()));
            JsonElement end = Element(tracker.Observe(false, 0, id, Guid.Empty));
            end.GetProperty("status").GetString().ShouldBe("matched");
            end.GetProperty("reason").GetString().ShouldBe("framework_activity");
            end.GetProperty("requestOrdinal").GetInt64().ShouldBe(start.GetProperty("requestOrdinal").GetInt64());
            Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
        }
        finally { Activity.Current = previous; }
    }

    [Fact]
    public void ConflictingExistingSpansMustNeverJoinThroughAnOtherwiseEqualFrameworkId()
    {
        LifecycleRequestCorrelation tracker = new();
        Guid id = Guid.NewGuid();
        using Activity first = new Activity("first").SetIdFormat(ActivityIdFormat.W3C).Start();
        JsonElement start = Element(tracker.Observe(true, 1, id, Guid.Empty));
        using (Activity second = new Activity("second").SetIdFormat(ActivityIdFormat.W3C).Start())
            Element(tracker.Observe(false, 1, id, Guid.Empty)).GetProperty("status").GetString().ShouldBe("unmatched");
        JsonElement end = Element(tracker.Observe(false, 0, id, Guid.Empty));
        end.GetProperty("status").GetString().ShouldBe("unavailable");
        Element(tracker.Snapshot()).GetProperty("status").GetString().ShouldBe("available");
        Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public void OverflowShouldBoundIdentitiesAndRefuseJoinsUntilTheObservedRequestsDrain()
    {
        Activity? previous = Activity.Current;
        Activity.Current = null;
        try
        {
            LifecycleRequestCorrelation tracker = new();
            Guid[] ids = Enumerable.Range(0, LifecycleRequestCorrelation.MaximumTrackedRequests + 1).Select(_ => Guid.NewGuid()).ToArray();
            for (int index = 0; index < ids.Length - 1; index++)
                Element(tracker.Observe(true, index + 1, ids[index], Guid.Empty)).GetProperty("status").GetString().ShouldBe("tracked");
            Element(tracker.Snapshot()).GetProperty("activeRequests").GetArrayLength().ShouldBe(LifecycleRequestCorrelation.MaximumTrackedRequests);
            Element(tracker.Observe(true, ids.Length, ids[^1], Guid.Empty)).GetProperty("reason").GetString().ShouldBe("tracking_capacity");
            Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
            for (int index = ids.Length - 1; index >= 0; index--)
                Element(tracker.Observe(false, index, ids[index], Guid.Empty)).GetProperty("status").GetString().ShouldBe("unavailable");
            Guid next = Guid.NewGuid();
            Element(tracker.Observe(true, 1, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("tracked");
            Element(tracker.Observe(false, 0, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("matched");
        }
        finally { Activity.Current = previous; }
    }

    [Fact]
    public void DuplicateIdentityShouldReportAmbiguityInsteadOfChoosingARequest()
    {
        LifecycleRequestCorrelation tracker = new();
        Guid id = Guid.NewGuid();
        tracker.Observe(true, 1, id, Guid.Empty);
        tracker.Observe(true, 2, id, Guid.Empty);
        JsonElement end = Element(tracker.Observe(false, 1, id, Guid.Empty));
        end.GetProperty("status").GetString().ShouldBe("unmatched");
        end.GetProperty("reason").GetString().ShouldBe("ambiguous_identity");
        Element(tracker.Snapshot()).GetProperty("status").GetString().ShouldBe("unavailable");
        Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnmatchedOrUnidentifiedEndAtConfirmedDrainClearsPhantomRequestsAndRecovers(bool unavailableIdentity)
    {
        Activity? previous = Activity.Current;
        Activity.Current = null;
        try
        {
            LifecycleRequestCorrelation tracker = new();
            tracker.Observe(true, 1, Guid.NewGuid(), Guid.Empty);
            JsonElement ended = Element(tracker.Observe(false, 0, unavailableIdentity ? Guid.Empty : Guid.NewGuid(), Guid.Empty));
            ended.GetProperty("status").GetString().ShouldBe(unavailableIdentity ? "unavailable" : "unmatched");
            Element(tracker.Snapshot()).GetProperty("status").GetString().ShouldBe("available");
            Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
            Guid next = Guid.NewGuid();
            Element(tracker.Observe(true, 1, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("tracked");
            Element(tracker.Observe(false, 0, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("matched");
        }
        finally { Activity.Current = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualMonitorContentionMustNeverWaitAndPoisonsSnapshotsUntilConfirmedDrain(bool drainWhileBusy)
    {
        LifecycleRequestCorrelation tracker = new();
        Guid original = Guid.NewGuid();
        tracker.Observe(true, 1, original, Guid.Empty);
        object gate = typeof(LifecycleRequestCorrelation).GetField("_gate", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(tracker)!;
        using ManualResetEventSlim held = new();
        using ManualResetEventSlim release = new();
        Task holder = Task.Run(() =>
        {
            lock (gate)
            {
                held.Set();
                release.Wait(TestContext.Current.CancellationToken);
            }
        }, TestContext.Current.CancellationToken);
        try
        {
            held.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();
            Stopwatch elapsed = Stopwatch.StartNew();
            Element(tracker.Observe(false, drainWhileBusy ? 0 : 1, original, Guid.Empty))
                .GetProperty("reason").GetString().ShouldBe("observation_busy");
            Element(tracker.Snapshot()).GetProperty("reason").GetString().ShouldBe("observation_busy");
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
            holder.IsCompleted.ShouldBeFalse();
        }
        finally { release.Set(); }
        await holder.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (!drainWhileBusy)
        {
            JsonElement stoppingSnapshot = Element(tracker.Snapshot());
            stoppingSnapshot.GetProperty("status").GetString().ShouldBe("unavailable");
            stoppingSnapshot.GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
            Element(tracker.Observe(true, 2, Guid.NewGuid(), Guid.Empty)).GetProperty("status").GetString().ShouldBe("unavailable");
            tracker.Observe(false, 0, Guid.Empty, Guid.Empty);
        }
        Element(tracker.Snapshot()).GetProperty("status").GetString().ShouldBe("available");
        Element(tracker.Snapshot()).GetProperty("activeRequests").EnumerateArray().ShouldBeEmpty();
        Guid next = Guid.NewGuid();
        Element(tracker.Observe(true, 1, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("tracked");
        Element(tracker.Observe(false, 0, next, Guid.Empty)).GetProperty("status").GetString().ShouldBe("matched");
    }

    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value);

    private static async Task<JsonElement> WaitForPhaseAsync(string path, string phase)
    {
        using CancellationTokenSource inspection = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        inspection.CancelAfter(TimeSpan.FromSeconds(5));
        while (!File.Exists(path) || !Rows(path).Any(row => row.GetProperty("phase").GetString() == phase))
            await Task.Delay(20, inspection.Token).ConfigureAwait(false);
        return Rows(path).Last(row => row.GetProperty("phase").GetString() == phase);
    }

    private static JsonElement[] Rows(string path)
    {
        string text = File.ReadAllText(path);
        return text[..(text.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
    }
}
