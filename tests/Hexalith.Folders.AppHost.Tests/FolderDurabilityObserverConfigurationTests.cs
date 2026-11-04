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
