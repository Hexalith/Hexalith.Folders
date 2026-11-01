using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.Configuration;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Checks fixture-only durable scheduling and truthful authority observation without starting external resources.</summary>
public sealed class FolderDurabilityBootstrapProjectionTests
{
    [Fact]
    public async Task ActualGatewayModelSelectsOnlyTheBootstrapDomainAndPreservesOtherHostsAndModes()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore");
        ProjectResource folders = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders");
        var gatewayBuilder = builder.CreateResourceBuilder(gateway);
        const string selected = "EventStore__Projections__Domains__global-administrators__RefreshIntervalMs";
        (await ProjectionEnvironmentAsync(gateway)).ShouldNotContainKey(selected);
        gatewayBuilder.WithEnvironment("EventStore__Projections__Domains__folders__RefreshIntervalMs", "2468");
        Dictionary<string, string?> before = await ProjectionEnvironmentAsync(gateway);
        Dictionary<string, string?> foldersBefore = await ProjectionEnvironmentAsync(folders);

        FolderDurabilityBootstrapConfiguration.ProjectionPolling(gatewayBuilder);

        Dictionary<string, string?> after = await ProjectionEnvironmentAsync(gateway);
        after.Keys.Except(before.Keys).ShouldBe([selected]);
        foreach (var setting in before) after[setting.Key].ShouldBe(setting.Value);
        (await ProjectionEnvironmentAsync(folders)).OrderBy(pair => pair.Key).ShouldBe(foldersBefore.OrderBy(pair => pair.Key));
        IConfiguration configured = new ConfigurationBuilder().AddInMemoryCollection(after.ToDictionary(
            pair => pair.Key.Replace("__", ":", StringComparison.Ordinal), pair => pair.Value)).Build();
        configured.GetValue<int>("EventStore:Projections:Domains:global-administrators:RefreshIntervalMs").ShouldBe(1000);
        configured.GetValue<int>("EventStore:Projections:Domains:folders:RefreshIntervalMs").ShouldBe(2468);
        configured["EventStore:Projections:DefaultRefreshIntervalMs"].ShouldBe(before.GetValueOrDefault("EventStore__Projections__DefaultRefreshIntervalMs"));
    }

    [Theory]
    [InlineData(false, "valid")]
    [InlineData(true, "valid")]
    [InlineData(false, "principal")]
    [InlineData(true, "principal")]
    [InlineData(false, "version")]
    [InlineData(true, "version")]
    [InlineData(false, "time")]
    [InlineData(true, "time")]
    [InlineData(false, "malformed")]
    [InlineData(true, "malformed")]
    public async Task ObservationWaitsOnlyForAbsenceAndImmediatelyValidatesEveryPresentAuthority(bool initiallyAbsent, string scenario)
    {
        int reads = 0;
        Task<JsonObject?> ReadAsync(CancellationToken token)
        {
            token.ShouldBe(TestContext.Current.CancellationToken);
            reads++;
            return Task.FromResult(initiallyAbsent && reads == 1 ? null : Authority(scenario));
        }

        Task observation = FolderDurabilityBootstrapEvidence.WaitForAuthorityAsync(ReadAsync, "administrator", TestContext.Current.CancellationToken);
        if (scenario == "valid") await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        else await Should.ThrowAsync<InvalidOperationException>(() => observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        reads.ShouldBe(initiallyAbsent ? 2 : 1);
    }

    [Fact]
    public async Task AbsentAuthorityUsesTheExistingCallerCancellationWithoutInventingCompletion()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int reads = 0;
        Task observation = FolderDurabilityBootstrapEvidence.WaitForAuthorityAsync(token =>
        {
            token.ShouldBe(caller.Token);
            reads++;
            return Task.FromResult<JsonObject?>(null);
        }, "administrator", caller.Token);
        observation.IsCompleted.ShouldBeFalse();
        caller.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        reads.ShouldBe(1);
    }

    [Fact]
    public async Task StorageFailureIsNeverReinterpretedAsAnAbsentProjection()
    {
        int reads = 0;
        await Should.ThrowAsync<IOException>(() => FolderDurabilityBootstrapEvidence.WaitForAuthorityAsync(_ =>
        {
            reads++;
            return Task.FromException<JsonObject?>(new IOException("test storage unavailable"));
        }, "administrator", TestContext.Current.CancellationToken));
        reads.ShouldBe(1);
    }

    private static JsonObject Authority(string scenario) => scenario == "malformed"
        ? new JsonObject { ["projectionVersion"] = 42 }
        : JsonSerializer.SerializeToNode(new
        {
            administrators = new[] { scenario == "principal" ? "workload:tenants" : "administrator" },
            projectionVersion = scenario == "version" ? "tenant-sequence:0" : "tenant-sequence:1",
            projectedAt = scenario == "time" ? default : DateTimeOffset.UtcNow,
        })!.AsObject();

    private static async Task<Dictionary<string, string?>> ProjectionEnvironmentAsync(IResource resource)
    {
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(context).ConfigureAwait(true);
        return context.EnvironmentVariables.Where(pair => pair.Key.StartsWith("EventStore__Projections__", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }
}
