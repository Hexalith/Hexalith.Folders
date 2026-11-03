using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;
using Task36FoldObserver;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies cached singleton reuse, refusal before resolution and public background task states.</summary>
[Collection("Task36 fold observation")]
public sealed class Task36ExistingHostedServiceObservationTests
{
    [Fact]
    public async Task StartedCaptureShouldReuseExistingSingletonsAndObserveTheOriginalPendingTask()
    {
        string directory = Directory.CreateTempSubdirectory("existing-hosted-").FullName;
        string path = Path.Combine(directory, "private.jsonl");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        int constructions = 0;
        LifecycleDiagnosticBackgroundService? background = null;
        builder.Services.AddSingleton<IHostedService>(_ =>
        {
            constructions++;
            return background = new();
        });
        builder.Services.AddSingleton(provider => LifecycleDiagnosticObserver.Create(provider, builder.Services, path));
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LifecycleDiagnosticObserver>());
        WebApplication app = builder.Build();
        LifecycleDiagnosticObserver observer = app.Services.GetRequiredService<LifecycleDiagnosticObserver>();
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
            await background!.Running.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await observer.StartAsync(TestContext.Current.CancellationToken); // No second capture/subscription.
            Task stop = app.StopAsync(TestContext.Current.CancellationToken);
            await background.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using CancellationTokenSource inspection = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            inspection.CancelAfter(TimeSpan.FromSeconds(5));
            while (!File.Exists(path) || !Rows(path).Any(row => row.GetProperty("phase").GetString() == "stopping_snapshot"))
                await Task.Delay(20, inspection.Token);

            JsonElement[] rows = Rows(path);
            JsonElement capture = rows.Single(row => row.GetProperty("phase").GetString() == "existing_hosted_services").GetProperty("capture");
            capture.GetProperty("status").GetString().ShouldBe("available");
            JsonElement[] services = capture.GetProperty("services").EnumerateArray().ToArray();
            JsonElement owned = services.Single(service => service.GetProperty("type").GetString() == typeof(LifecycleDiagnosticBackgroundService).FullName);
            owned.GetProperty("backgroundService").GetBoolean().ShouldBeTrue();
            owned.GetProperty("hostedLifecycleService").GetBoolean().ShouldBeFalse();
            int ordinal = owned.GetProperty("ordinal").GetInt32();
            services.Last().GetProperty("type").GetString().ShouldBe("Microsoft.AspNetCore.Hosting.GenericWebHostService");
            JsonElement snapshot = rows.Last(row => row.GetProperty("phase").GetString() == "stopping_snapshot");
            snapshot.GetProperty("hostStopSeen").GetBoolean().ShouldBeTrue();
            snapshot.GetProperty("applicationStoppedSeen").GetBoolean().ShouldBeFalse();
            JsonElement task = snapshot.GetProperty("hostedServiceStates").EnumerateArray()
                .Single(value => value.GetProperty("ordinal").GetInt32() == ordinal);
            task.GetProperty("state").GetString().ShouldBe("observed");
            task.GetProperty("completed").GetBoolean().ShouldBeFalse();
            constructions.ShouldBe(1);
            stop.IsCompleted.ShouldBeFalse();

            background.Release.SetResult();
            await stop.ConfigureAwait(true);
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            constructions.ShouldBe(1);
            JsonElement final = Rows(path).Single(row => row.GetProperty("phase").GetString() == "application_stopped");
            final.GetProperty("hostedServiceStates").EnumerateArray()
                .Single(value => value.GetProperty("ordinal").GetInt32() == ordinal).GetProperty("completed").GetBoolean().ShouldBeTrue();
            File.ReadAllText(path).ShouldNotContain("AsyncState");
            File.ReadAllText(path).ShouldNotContain("Exception");
        }
        finally
        {
            background?.Release.TrySetResult();
            await app.DisposeAsync();
            await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("not_started", "host_not_started")]
    [InlineData("transient", "non_singleton")]
    [InlineData("scoped", "non_singleton")]
    [InlineData("custom_enumerable", "custom_enumerable")]
    [InlineData("changed", "registration_changed")]
    [InlineData("too_large", "inventory_too_large")]
    [InlineData("disposed", "capture_unavailable")]
    public void UnsafeCaptureShouldRefuseBeforeAnyNewFactoryOrTransientInstance(string mode, string expected)
    {
        IServiceCollection services = new ServiceCollection();
        int constructions = 0;
        IHostedService Create(IServiceProvider _)
        {
            constructions++;
            return new LifecycleDiagnosticTestService();
        }
        services.Add(new ServiceDescriptor(typeof(IHostedService), Create, mode switch
        {
            "transient" => ServiceLifetime.Transient,
            "scoped" => ServiceLifetime.Scoped,
            _ => ServiceLifetime.Singleton,
        }));
        if (mode == "too_large")
            for (int index = 1; index < 129; index++) services.AddSingleton<IHostedService>(Create);
        if (mode == "custom_enumerable")
            services.AddSingleton<IEnumerable<IHostedService>>(provider => [Create(provider)]);
        ServiceDescriptor[] initial = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToArray();
        if (mode == "changed") services[0] = ServiceDescriptor.Singleton<IHostedService>(Create);
        using ServiceProvider provider = services.BuildServiceProvider();
        if (mode == "disposed") provider.Dispose();
        ExistingHostedServiceObservation observation = new(provider, services, initial);

        JsonElement result = JsonSerializer.SerializeToElement(observation.Capture(mode != "not_started"));

        result.GetProperty("status").GetString().ShouldBe("unavailable");
        result.GetProperty("reason").GetString().ShouldBe(expected);
        constructions.ShouldBe(0);
        observation.States().ShouldBeNull();
    }

    [Fact]
    public void UnknownProviderShouldBeRefusedWithoutCallingIt()
    {
        LifecycleUnsupportedProvider provider = new();
        ExistingHostedServiceObservation observation = new(provider, new ServiceCollection(), []);

        JsonElement result = JsonSerializer.SerializeToElement(observation.Capture(applicationStarted: true));

        result.GetProperty("reason").GetString().ShouldBe("provider_unsupported");
        result.GetRawText().ShouldNotContain("private-provider-message");
        provider.Calls.ShouldBe(0);
        observation.States().ShouldBeNull();
    }

    private static JsonElement[] Rows(string path)
    {
        string text = File.ReadAllText(path);
        return text[..(text.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
    }
}
