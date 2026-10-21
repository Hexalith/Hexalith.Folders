using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Qualifies synthetic original owner histories through the production authenticated Folders SDK transport.</summary>
[Collection("Folder durability acceptance")]
public sealed class OperationalStatusBoundedHistoryAcceptanceTests(FolderDurabilityAcceptanceFixture fixture, ITestOutputHelper output)
    : IClassFixture<FolderDurabilityAcceptanceFixture>
{
    /// <summary>Preserves complete large histories across private staging, process replacement, inheritance and further growth.</summary>
    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task OriginalOwnersRebuildGrowAndRetainIdentityThroughAuthenticatedTransport(int count)
    {
        if (!fixture.Enabled)
        {
            Assert.Skip($"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true to execute bounded-history acceptance.");
            return;
        }
        CancellationToken token = TestContext.Current.CancellationToken;
        string folder = "capacity-" + count + "-" + FolderDurabilityAcceptanceFixture.NewId();
        string otherFolder = folder + "-other";
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        ProjectionEventDto[] original = History(folder, count, timestamp);
        ProjectionEventDto[] other = History(otherFolder, 1, timestamp);
        other[0] = other[0] with { GlobalPosition = count + 1 };
        string sourceHash = Hash(original);
        string otherHash = Hash(other);
        TimeSpan phaseBudget = TimeSpan.FromMinutes(15);
        string operation = FolderDurabilityAcceptanceFixture.NewId();
        var scope = OperationalStatusGeneration.Scope(fixture.TenantId);
        var coordinator = new SharedProjectionEpochCoordinator(fixture.RawStore,
            fixture.RawStore.ShouldBeAssignableTo<IReadModelBatchStore>());
        string key = OperationalStatusKeys.Checkpoint(fixture.TenantId, folder);
        output.WriteLine($"CAPACITY input=synthetic-original-owner count={count} owner={folder} sourceSha256={sourceHash} otherSha256={otherHash}");
        using (HttpResponseMessage staged = await fixture.NamedProjectAsync(folder, original, operation,
            stopAfterStage: true, additionalOwners: [(otherFolder, other)], retryBudget: phaseBudget))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(staged, HttpStatusCode.OK, "Large private staging");
        (await coordinator.GetStatusAsync(scope, token)).IsBuilding.ShouldBeTrue();
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value.ShouldBeNull();
        foreach (var process in await fixture.RestartHostsAsync())
        {
            process.Value.Before.ShouldNotBe(process.Value.After);
            output.WriteLine($"CAPACITY process-replacement count={count} resource={process.Key} before={process.Value.Before} after={process.Value.After}");
        }
        // This is a sealed-stage restart; the SDK packet separately proves true mid-stage/partial-handoff exits.
        using (HttpResponseMessage resumed = await fixture.NamedProjectAsync(folder, original, operation,
            additionalOwners: [(otherFolder, other)], retryBudget: phaseBudget))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(resumed, HttpStatusCode.OK, "Large staged recovery");
        var checkpoint = (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value.ShouldNotBeNull();
        checkpoint.Sequence.ShouldBe(count);
        JsonSerializer.SerializeToUtf8Bytes(checkpoint).Length.ShouldBeLessThan(16_384);
        var captured = (await coordinator.GetCheckpointAsync(scope, folder, token)).ShouldNotBeNull();
        captured.CapturedEventDigests.ShouldBeNull();
        captured.CapturedIdentityReference!.Count.ShouldBe(count);
        JsonSerializer.SerializeToUtf8Bytes(captured).Length.ShouldBeLessThan(4096);
        (await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope,
            OperationalStatusKeys.Lifecycle(fixture.TenantId, folder), token)).Value!.LifecycleState.ShouldBe(FolderLifecycleProjectionState.Active);
        using (HttpResponseMessage inherited = await fixture.NamedProjectAsync(otherFolder, other,
            FolderDurabilityAcceptanceFixture.NewId(), retryBudget: phaseBudget))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(inherited, HttpStatusCode.OK, "Independent owner generation");
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value!.SourceDigest.ShouldBe(checkpoint.SourceDigest);
        foreach (int index in new[] { 0, count / 2, count - 1 })
            (await fixture.NamedDeliveryAsync(folder, original[index])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionEventDto growth = Rejection(count + 1, timestamp) with { GlobalPosition = count + 2 };
        (await fixture.NamedDeliveryAsync(folder, growth)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto[] grown = [.. original, growth];
        using (HttpResponseMessage rebuilt = await fixture.NamedProjectAsync(folder, grown,
            FolderDurabilityAcceptanceFixture.NewId(), retryBudget: phaseBudget))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rebuilt, HttpStatusCode.OK, "Complete grown owner rebuild");
        (await coordinator.GetCheckpointAsync(scope, folder, token))!.CapturedIdentityReference!.Count.ShouldBe(count + 1);
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint(fixture.TenantId, otherFolder), token)).Value!.Sequence.ShouldBe(1);
        foreach (int index in new[] { 0, count / 2, count })
            (await fixture.NamedDeliveryAsync(folder, grown[index])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        string selected = JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value);
        foreach (int index in new[] { 0, count / 2, count })
        {
            ProjectionEventDto changedMessage = grown[index] with { MessageId = "conflicting-" + index };
            var messageConflict = await fixture.NamedDeliveryAsync(folder, changedMessage);
            messageConflict.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            messageConflict.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            ProjectionEventDto changedPayload = grown[index] with
            {
                Payload = index == 0
                ? JsonSerializer.SerializeToUtf8Bytes(Created(folder, timestamp) with { DisplayName = "contradictory" })
                : Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"ChangedUnknownCommand\"}")
            };
            var payloadConflict = await fixture.NamedDeliveryAsync(folder, changedPayload);
            payloadConflict.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            payloadConflict.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value).ShouldBe(selected);
        Hash(original).ShouldBe(sourceHash);
        Hash(other).ShouldBe(otherHash);
        (await coordinator.GetStatusAsync(scope, token)).IsBuilding.ShouldBeFalse();
        long[] retainedBounds = (await fixture.RetainedOperationalBoundsAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        retainedBounds.Length.ShouldBe(4);
        retainedBounds[0].ShouldBeGreaterThan(count);
        retainedBounds[1].ShouldBeLessThan(1_048_576);
        retainedBounds[2].ShouldBeGreaterThan(0);
        retainedBounds[2].ShouldBeLessThan(16_384);
        retainedBounds[3].ShouldBeLessThan(4096);
        output.WriteLine($"PASS CAPACITY count={count} grown={count + 1} sourceSha256={sourceHash} otherSha256={otherHash} retainedValues={retainedBounds[0]} finalRetainedSharedValueMaxBytes={retainedBounds[1]} identityMaxBytes={retainedBounds[2]} sdkCheckpointMaxBytes={retainedBounds[3]}; 256-event parts; configured admission=40000; SDK batch limits unchanged");
    }

    private ProjectionEventDto[] History(string folder, int count, DateTimeOffset timestamp)
    {
        ProjectionEventDto[] events = new ProjectionEventDto[count];
        FolderCreated created = Created(folder, timestamp);
        events[0] = new(typeof(FolderCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(created), "json", 1,
            timestamp, "capacity-correlation", "capacity-message-1", GlobalPosition: 1)
        { MetadataVersion = 1 };
        for (int index = 1; index < count; index++) events[index] = Rejection(index + 1, timestamp);
        return events;
    }

    private FolderCreated Created(string folder, DateTimeOffset timestamp)
        => new(fixture.TenantId, fixture.TenantId, folder, "Capacity metadata", null, null, [], FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound, "capacity-principal", "capacity-correlation", "capacity-task",
            "capacity-create-key", "capacity-fingerprint", timestamp);

    private static ProjectionEventDto Rejection(long sequence, DateTimeOffset timestamp)
        => new("Hexalith.Folders.Server.FolderCommandRejected",
            "{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"UnknownButLegitimate\"}"u8.ToArray(), "json", sequence,
            timestamp, "capacity-correlation", "capacity-message-" + sequence, GlobalPosition: sequence)
        { MetadataVersion = 1 };

    private static string Hash(ProjectionEventDto[] events)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(events)));
}
