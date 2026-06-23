using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Tier-3 cross-process proof that the full Folders topology — the <c>eventstore</c> folder-events publisher and
/// the <c>folders-workers</c> / <c>memories</c> subscribers — boots together with the Story 10.3 D1 wiring
/// (the <c>folders.events</c> EventStore publish-topic override plus the production pub/sub scopes), activating
/// the Epic 9 routing that is dormant in-process. Runs only on a DCP-capable host opted in via
/// <see cref="AspireFoldersAppHostFixture.OptInEnvironmentVariable"/>; otherwise every test skips.
/// </summary>
public sealed class FoldersTopologyCrossProcessTests(AspireFoldersAppHostFixture fixture)
    : IClassFixture<AspireFoldersAppHostFixture>
{
    private readonly AspireFoldersAppHostFixture _fixture = fixture;

    [Fact]
    public void FullFoldersTopologyBootsRunningAcrossProcesses()
    {
        _fixture.SkipIfUnavailable();

        foreach (string resourceName in AspireFoldersAppHostFixture.TopologyResourceNames)
        {
            _fixture.App.ResourceNotifications.TryGetCurrentState(resourceName, out ResourceEvent? resourceEvent)
                .ShouldBeTrue($"Resource '{resourceName}' is missing from the running topology.");
            resourceEvent.ShouldNotBeNull();
            resourceEvent.Snapshot.State?.Text.ShouldBe(
                KnownResourceStates.Running,
                $"Resource '{resourceName}' did not boot to Running.");
        }
    }

    [Fact]
    public void EventStorePublisherAndFolderWorkerSubscriberAreLive()
    {
        _fixture.SkipIfUnavailable();

        // The publish/subscribe participants of the semantic-indexing path are each running across the process
        // boundary: eventstore publishes folder.events, folders-workers subscribes folders.events + memories-events,
        // and memories subscribes memories-events. The deny-by-default Dapr scopes that authorize this are pinned
        // separately by DaprPolicyConformanceTests; here we prove the processes actually come up.
        foreach (string participant in new[] { "eventstore", "folders-workers", "memories" })
        {
            _fixture.App.ResourceNotifications.TryGetCurrentState(participant, out ResourceEvent? resourceEvent)
                .ShouldBeTrue($"Publish/subscribe participant '{participant}' is missing from the running topology.");
            resourceEvent!.Snapshot.State?.Text.ShouldBe(
                KnownResourceStates.Running,
                $"Publish/subscribe participant '{participant}' did not boot to Running.");
        }

        // The folders REST gateway (the entry point a real folder mutation hits to emit a folder domain event)
        // published its HTTP endpoint — Aspire resolves it through service discovery.
        using HttpClient foldersClient = _fixture.App.CreateHttpClient("folders");
        foldersClient.BaseAddress.ShouldNotBeNull();
    }
}
