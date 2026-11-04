using System.Diagnostics;
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

/// <summary>Qualifies the lifecycle-only opt-out without changing host services or fold evidence.</summary>
[Collection("Task36 fold observation")]
public sealed class Task36HostingStartupControlTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("false", true)]
    [InlineData("invalid", true)]
    [InlineData("TRUE", true)]
    [InlineData(" true", true)]
    [InlineData("1", true)]
    [InlineData("true", false)]
    public async Task EventStoreRetainsItsServicesAndNoFoldObserverWhileOnlyExactTrueDisablesLifecycle(string? disabled, bool expected)
    {
        string directory = Directory.CreateTempSubdirectory("lifecycle-control-").FullName;
        string path = Path.Combine(directory, "lifecycle.jsonl");
        Dictionary<string, string?> original = SetEnvironment(disabled, path, "");
        WebApplication? app = null;
        LifecycleDiagnosticObserver? observer = null;
        LifecycleDiagnosticTestService existing = new();
        existing.Release.SetResult();
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IHostedService>(existing);
            ServiceDescriptor registration = builder.Services.Last();
            new Task36HostingStartup().Configure(builder.WebHost);
            app = builder.Build();
            builder.Services.ShouldContain(registration);
            IHostedService[] services = app.Services.GetServices<IHostedService>().ToArray();
            services.ShouldContain(service => ReferenceEquals(service, existing));
            services.OfType<FoldDiagnosticObserver>().ShouldBeEmpty();
            services.OfType<LifecycleDiagnosticObserver>().Count().ShouldBe(expected ? 1 : 0);
            observer = services.OfType<LifecycleDiagnosticObserver>().SingleOrDefault();

            await app.StartAsync(TestContext.Current.CancellationToken);
            await app.StopAsync(TestContext.Current.CancellationToken);
            existing.Stopping.Task.IsCompletedSuccessfully.ShouldBeTrue();
            await app.DisposeAsync();
            app = null;
            if (observer is not null)
            {
                await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                JsonElement[] records = File.ReadAllLines(path).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
                records.ShouldContain(record => record.GetProperty("phase").GetString() == "observer_registered");
                records.ShouldContain(record => record.GetProperty("phase").GetString() == "application_stopped");
            }
            else File.Exists(path).ShouldBeFalse();
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
            if (observer is not null)
                await observer.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            RestoreEnvironment(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("true")]
    public async Task FoldersKeepsMandatoryFoldObservationWithNoLifecyclePathRegardlessOfTheOptionalFlag(string? disabled)
    {
        string directory = Directory.CreateTempSubdirectory("fold-control-").FullName;
        string path = Path.Combine(directory, "fold.jsonl");
        Dictionary<string, string?> original = SetEnvironment(disabled, "", path);
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            new Task36HostingStartup().Configure(builder.WebHost);
            await using WebApplication app = builder.Build();
            IHostedService[] services = app.Services.GetServices<IHostedService>().ToArray();
            services.OfType<FoldDiagnosticObserver>().Count().ShouldBe(1);
            services.OfType<LifecycleDiagnosticObserver>().ShouldBeEmpty();
            await app.StartAsync(TestContext.Current.CancellationToken);
            using DiagnosticListener listener = new(FoldDiagnosticObserver.ListenerName);
            listener.IsEnabled(FoldDiagnosticObserver.EventName).ShouldBeTrue();
            listener.Write(FoldDiagnosticObserver.EventName, new KeyValuePair<string, long>("folder-control", 7));
            JsonElement[] records = File.ReadAllLines(path).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
            records.ShouldContain(record => record.GetProperty("action").GetString() == "registered");
            JsonElement fold = records.Single(record => record.GetProperty("action").GetString() == "fold");
            fold.GetProperty("source").GetString().ShouldBe("folder-control");
            fold.GetProperty("position").GetInt64().ShouldBe(7);
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            RestoreEnvironment(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, string?> SetEnvironment(string? disabled, string shutdown, string fold)
    {
        Dictionary<string, string?> values = new()
        {
            ["FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED"] = disabled,
            ["FOLDERS_TASK36_SHUTDOWN_LOG"] = shutdown,
            ["FOLDERS_TASK36_FOLD_LOG"] = fold,
            ["FOLDERS_CONTENT_QUALIFICATION_CLOCK"] = null,
        };
        Dictionary<string, string?> original = values.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        foreach (var value in values) Environment.SetEnvironmentVariable(value.Key, value.Value);
        return original;
    }

    private static void RestoreEnvironment(Dictionary<string, string?> original)
    {
        foreach (var value in original) Environment.SetEnvironmentVariable(value.Key, value.Value);
    }
}
