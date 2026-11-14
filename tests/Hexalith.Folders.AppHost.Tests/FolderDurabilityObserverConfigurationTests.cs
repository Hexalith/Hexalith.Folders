using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Checks the actual fixture resource wiring without starting external resources.</summary>
public sealed class FolderDurabilityObserverConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("true")]
    public async Task OnlyEventStoreReceivesTheLifecycleControlAndAllExistingObservationPathsRemain(string? disabled)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        ProjectResource folders = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "folders");
        ProjectResource eventStore = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        ProjectResource[] others = builder.Resources.OfType<ProjectResource>().Where(resource => resource != folders && resource != eventStore).ToArray();
        Dictionary<ProjectResource, Dictionary<string, string?>> before = [];
        foreach (ProjectResource resource in others) before[resource] = await ObserverEnvironmentAsync(resource);

        FolderDurabilityAcceptanceFixture.ConfigureObservers(builder.CreateResourceBuilder(folders), builder.CreateResourceBuilder(eventStore),
            "/fixture/Task36FoldObserver.dll", "/fixture/fold.jsonl", "/fixture/lifecycle.jsonl", disabled);

        Dictionary<string, string?> folderEnvironment = await ObserverEnvironmentAsync(folders);
        Dictionary<string, string?> eventStoreEnvironment = await ObserverEnvironmentAsync(eventStore);
        folderEnvironment.ShouldBe(new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = "/fixture/Task36FoldObserver.dll",
            ["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "Task36FoldObserver",
            ["FOLDERS_TASK36_FOLD_LOG"] = "/fixture/fold.jsonl",
            ["FOLDERS_TASK36_SHUTDOWN_LOG"] = "",
        }, ignoreOrder: true);
        eventStoreEnvironment.ShouldBe(new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = "/fixture/Task36FoldObserver.dll",
            ["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "Task36FoldObserver",
            ["FOLDERS_TASK36_FOLD_LOG"] = "",
            ["FOLDERS_TASK36_SHUTDOWN_LOG"] = "/fixture/lifecycle.jsonl",
            ["FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED"] = disabled ?? "",
        }, ignoreOrder: true);
        foreach (ProjectResource resource in others)
            (await ObserverEnvironmentAsync(resource)).ShouldBe(before[resource], ignoreOrder: true);
    }

    /// <summary>Checks default-off forwarding while preserving every existing environment and resource annotation.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("true")]
    public async Task SdkShutdownTracingChangesOnlyTheOwnedGatewayAndPreservesObserversAndSecurity(string? enabled)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        ProjectResource folders = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "folders");
        FolderDurabilityAcceptanceFixture.ConfigureObservers(builder.CreateResourceBuilder(folders), builder.CreateResourceBuilder(gateway),
            "/fixture/Task36FoldObserver.dll", "/fixture/fold.jsonl", "/fixture/lifecycle.jsonl", "true");
        Dictionary<ProjectResource, Dictionary<string, object>> before = [];
        Dictionary<ProjectResource, IResourceAnnotation[]> annotations = [];
        foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>())
        {
            before[resource] = await CompleteEnvironmentAsync(resource).ConfigureAwait(true);
            annotations[resource] = resource.Annotations.ToArray();
        }

        FolderDurabilityAcceptanceFixture.ConfigureSdkShutdownTrace(builder.CreateResourceBuilder(gateway), enabled);

        Dictionary<string, object> additions = enabled == "true" ? new(StringComparer.Ordinal)
        {
            ["HEXALITH_EVENTSTORE_SHUTDOWN_TRACE"] = "true",
            ["Logging__LogLevel__Hexalith.EventStore.Server.Projections.ProjectionActivationWorker"] = "Debug",
            ["Logging__LogLevel__Hexalith.EventStore.ServiceDefaults.BoundedTelemetryShutdownService"] = "Debug",
            ["Logging__LogLevel__Hexalith.EventStore.Indexes.AdminOperationalIndexHostedService"] = "Debug",
        } : [];
        foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>())
        {
            Dictionary<string, object> expected = new(before[resource], StringComparer.Ordinal);
            if (resource == gateway)
                foreach (var addition in additions) expected[addition.Key] = addition.Value;
            Dictionary<string, object> actual = await CompleteEnvironmentAsync(resource).ConfigureAwait(true);
            actual.Keys.ShouldBe(expected.Keys, ignoreOrder: true);
            foreach (var setting in expected)
            {
                // Aspire callbacks recreate deferred endpoint/value providers. Their original
                // callback annotations remain identical below; compare all concrete settings exactly.
                if (setting.Value is string || setting.Value.GetType().IsValueType)
                    actual[setting.Key].ShouldBe(setting.Value);
                else
                    actual[setting.Key].GetType().ShouldBe(setting.Value.GetType());
            }
            resource.Annotations.Take(annotations[resource].Length).ShouldBe(annotations[resource]);
            if (resource != gateway || additions.Count == 0)
                resource.Annotations.Count.ShouldBe(annotations[resource].Length);
            else
                resource.Annotations.Skip(annotations[resource].Length).ShouldAllBe(annotation => annotation is EnvironmentCallbackAnnotation);
        }
    }

    private static async Task<Dictionary<string, object>> CompleteEnvironmentAsync(IResource resource)
    {
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(context).ConfigureAwait(true);
        return new(context.EnvironmentVariables, StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, string?>> ObserverEnvironmentAsync(IResource resource)
    {
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(context).ConfigureAwait(true);
        return context.EnvironmentVariables.Where(pair => pair.Key.StartsWith("FOLDERS_TASK36_", StringComparison.Ordinal)
                || pair.Key is "DOTNET_STARTUP_HOOKS" or "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES")
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }
}
