using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Workers;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class WorkersSubscriptionConfigurationTests
{
    [Theory]
    [InlineData("Production", null)]
    [InlineData("Production", "")]
    [InlineData("Production", " ")]
    [InlineData("Production", "pubsub")]
    [InlineData("Staging", null)]
    [InlineData("Staging", " ")]
    public async Task StartupRejectsMissingBlankOrSharedInboundComponentOutsideDevelopment(
        string environment, string? component)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        { EnvironmentName = environment });
        builder.Configuration["Folders:Workers:PubSubName"] = component;
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersTenantEventWorkers();
        await using WebApplication app = builder.Build();

        OptionsValidationException failure = await Should.ThrowAsync<OptionsValidationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));
        failure.Failures.ShouldContain(item => item.Contains("Folders:Workers:PubSubName", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Development", null, "pubsub")]
    [InlineData("Development", " ", "pubsub")]
    [InlineData("Development", "pubsub", "pubsub")]
    [InlineData("Production", "folders-workers-pubsub", "folders-workers-pubsub")]
    [InlineData("Staging", "isolated-inbound", "isolated-inbound")]
    public async Task StartupUsesExplicitInboundComponentWithSharedFallbackOnlyInDevelopment(
        string environment, string? component, string expected)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        { EnvironmentName = environment });
        builder.Configuration["Folders:Workers:PubSubName"] = component;
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersTenantEventWorkers();
        await using WebApplication app = builder.Build();
        await app.StartAsync(TestContext.Current.CancellationToken);
        app.Services.GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value.PubSubName
            .ShouldBe(expected);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
