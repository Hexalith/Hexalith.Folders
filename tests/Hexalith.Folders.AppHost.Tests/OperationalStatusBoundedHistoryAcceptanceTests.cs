using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        ProjectionEventDto[] original = History(folder, count, timestamp, firstGlobalPosition: 2);
        ProjectionEventDto[] other = History(otherFolder, 1, timestamp);
        other[0] = other[0] with { GlobalPosition = count + 2 };
        string sourceHash = Hash(original);
        string otherHash = Hash(other);
        TimeSpan phaseBudget = TimeSpan.FromMinutes(15);
        string operation = FolderDurabilityAcceptanceFixture.NewId();
        var scope = OperationalStatusGeneration.Scope(fixture.TenantId);
        var coordinator = new SharedProjectionEpochCoordinator(fixture.RawStore,
            fixture.RawStore.ShouldBeAssignableTo<IReadModelBatchStore>());
        // Establish the production writer/epoch through its authenticated ordinary SDK callback.
        // Both large capture owners still have empty checkpoints before their private staging.
        string primer = folder + "-primer";
        (await fixture.NamedDeliveryAsync(primer, History(primer, 1, timestamp)[0])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, primer).ShouldBe(1);
        fixture.RecordRuntimeBindings(output.WriteLine);
        string key = OperationalStatusKeys.Checkpoint(fixture.TenantId, folder);
        output.WriteLine($"CAPACITY input=synthetic-original-owner count={count} owner={folder} sourceSha256={sourceHash} otherSha256={otherHash}");
        using (HttpResponseMessage staged = await fixture.NamedProjectAsync(folder, original, operation,
            stopAfterStage: true, additionalOwners: [(otherFolder, other)], retryBudget: phaseBudget, observe: output.WriteLine))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(staged, HttpStatusCode.OK, "Large private staging");
        (await coordinator.GetStatusAsync(scope, token)).IsBuilding.ShouldBeTrue();
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value.ShouldBeNull();
        foreach (var process in await fixture.RestartHostsAsync())
        {
            process.Value.Before.ShouldNotBe(process.Value.After);
            output.WriteLine($"CAPACITY process-replacement count={count} resource={process.Key} before={process.Value.Before} after={process.Value.After}");
        }
        fixture.RecordRuntimeBindings(output.WriteLine);
        // This is a sealed-stage restart; the SDK packet separately proves true mid-stage/partial-handoff exits.
        using (HttpResponseMessage resumed = await fixture.NamedProjectAsync(folder, original, operation,
            additionalOwners: [(otherFolder, other)], retryBudget: phaseBudget, observe: output.WriteLine))
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
        string sdkKey = Task36QualificationEvidence.CheckpointKey(scope, folder);
        string retainedBefore = await fixture.QualificationPersistedValueAsync(sdkKey);
        retainedBefore.ShouldNotBeNullOrWhiteSpace();
        retainedBefore.ShouldContain(captured.CapturedIdentityReference!.NamespaceHash);
        retainedBefore.ShouldContain(captured.CapturedIdentityReference.RootDigest);
        string selectedBefore = JsonSerializer.Serialize(checkpoint);
        string lifecycleBefore = JsonSerializer.Serialize((await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope,
            OperationalStatusKeys.Lifecycle(fixture.TenantId, folder), token)).Value);
        var sealedLeaf = await coordinator.CapturedIdentities.ReadPageAsync(scope, captured.CapturedIdentityReference, 1, token);
        SharedProjectionDelivery canonical = Task36QualificationEvidence.Delivery(fixture.TenantId, folder, original[0]);
        Task36QualificationEvidence.CapturedDigest(canonical).ShouldBe(sealedLeaf.Digests[..32]);
        string canonicalBefore = Task36QualificationEvidence.Hash(canonical.CanonicalPayload);
        var oldLease = await coordinator.RefreshLeaseAsync(scope, OperationalStatusGeneration.WriterId, token);
        output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "before-inheritance", operation, sourceHash,
            owner = folder, sdkKey, rawCheckpoint = retainedBefore, checkpoint = captured, canonicalSha256 = canonicalBefore, capturedIdentitySha256 = Convert.ToHexString(sealedLeaf.Digests[..32]), canonicalOrigin = "reconstructed-DTO-matched-to-persisted-SDK-captured-leaf" }));
        using (HttpResponseMessage inherited = await fixture.NamedProjectAsync(otherFolder, other,
            FolderDurabilityAcceptanceFixture.NewId(), retryBudget: phaseBudget, observe: output.WriteLine))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(inherited, HttpStatusCode.OK, "Independent owner generation");
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value!.SourceDigest.ShouldBe(checkpoint.SourceDigest);
        (await fixture.QualificationPersistedValueAsync(sdkKey)).ShouldBe(retainedBefore);
        JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value).ShouldBe(selectedBefore);
        var epoch = (await fixture.RawStore.GetAsync<JsonObject>(scope.StoreName, "shared-epoch:" + scope.ComputeHash(), token)).Value.ShouldNotBeNull();
        var captureMap = epoch.Single(pair => pair.Key.Equals("CaptureHighWatermarks", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
        captureMap.ContainsKey(folder).ShouldBeFalse();
        captureMap.ContainsKey(otherFolder).ShouldBeTrue();
        JsonSerializer.Serialize((await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope,
            OperationalStatusKeys.Lifecycle(fixture.TenantId, folder), token)).Value).ShouldBe(lifecycleBefore);
        (await coordinator.JournalAsync(scope, oldLease, canonical, token)).ShouldBe(SharedProjectionJournalResult.StaleLease);
        var lease = await coordinator.RefreshLeaseAsync(scope, OperationalStatusGeneration.WriterId, token);
        int beforeFolds = Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder);
        foreach (int index in new[] { 0, count / 2, count - 1 })
        {
            var delivery = Task36QualificationEvidence.Delivery(fixture.TenantId, folder, original[index]);
            (await coordinator.JournalAsync(scope, lease, delivery, token)).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
            var replay = await fixture.NamedDeliveryAsync(folder, original[index]);
            output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "inherited-replay", index,
                outcome = replay, canonicalSha256 = Task36QualificationEvidence.Hash(delivery.CanonicalPayload) }));
            replay.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder).ShouldBe(beforeFolds);
        (await fixture.QualificationPersistedValueAsync(sdkKey)).ShouldBe(retainedBefore);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, folder, token)).ShouldBe(JsonSerializer.Serialize(captured));
        Task36QualificationEvidence.Hash(Task36QualificationEvidence.Delivery(fixture.TenantId, folder, original[0]).CanonicalPayload).ShouldBe(canonicalBefore);
        output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "after-inherited-replay", operation,
            epoch, checkpoint = captured, rawCheckpoint = retainedBefore, canonicalSha256 = canonicalBefore, foldCallbackDelta = Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder) - beforeFolds }));

        // Damage only task-owned derived proof, then restore its original value before the next control.
        string proofKey = "shared-identity-v1:" + captured.CapturedIdentityReference!.NamespaceHash + ":node:" + captured.CapturedIdentityReference.RootDigest;
        var proof = (await fixture.RawStore.GetAsync<SharedProjectionCapturedIdentityPage>(scope.StoreName, proofKey, token)).Value.ShouldNotBeNull();
        foreach (string fault in new[] { "missing-root", "corrupt-root" })
        {
            try
            {
                if (fault == "missing-root") await fixture.RemoveQualificationProofAsync(proofKey);
                else await fixture.RawStore.SaveAsync(scope.StoreName, proofKey, proof with { LastPosition = proof.LastPosition - 1 }, token);
                await Should.ThrowAsync<InvalidOperationException>(() => coordinator.JournalAsync(scope, lease, canonical, token));
                var refused = await fixture.NamedDeliveryAsync(folder, original[0]);
                output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "proof-fault", fault, proofKey, outcome = refused }));
                refused.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
                refused.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
                Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder).ShouldBe(beforeFolds);
                (await fixture.QualificationPersistedValueAsync(sdkKey)).ShouldBe(retainedBefore);
            }
            finally { await fixture.RawStore.SaveAsync(scope.StoreName, proofKey, proof, token); }
        }
        (await fixture.NamedDeliveryAsync(folder, original[0])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionEventDto growth = Rejection(count + 1, timestamp) with { GlobalPosition = count + 3 };
        (await fixture.NamedDeliveryAsync(folder, growth)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder).ShouldBe(beforeFolds + 1);
        ProjectionEventDto[] grown = [.. original, growth];
        using (HttpResponseMessage rebuilt = await fixture.NamedProjectAsync(folder, grown,
            FolderDurabilityAcceptanceFixture.NewId(), retryBudget: phaseBudget, observe: output.WriteLine))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rebuilt, HttpStatusCode.OK, "Complete grown owner rebuild");
        (await coordinator.GetCheckpointAsync(scope, folder, token))!.CapturedIdentityReference!.Count.ShouldBe(count + 1);
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint(fixture.TenantId, otherFolder), token)).Value!.Sequence.ShouldBe(1);
        foreach (int index in new[] { 0, count / 2, count })
            (await fixture.NamedDeliveryAsync(folder, grown[index])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "grown-rebuild", status = await coordinator.GetStatusAsync(scope, token),
            checkpoint = await coordinator.GetCheckpointAsync(scope, folder, token), rawCheckpoint = await fixture.QualificationPersistedValueAsync(sdkKey) }));
        // Repeat omission after growth so every altered identity below exercises inherited-owner proof.
        using (HttpResponseMessage omittedAgain = await fixture.NamedProjectAsync(otherFolder, other,
            FolderDurabilityAcceptanceFixture.NewId(), retryBudget: phaseBudget, observe: output.WriteLine))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(omittedAgain, HttpStatusCode.OK, "Omit grown original owner");
        var omittedState = (await fixture.RawStore.GetAsync<JsonObject>(scope.StoreName, "shared-epoch:" + scope.ComputeHash(), token)).Value!;
        omittedState.Single(pair => pair.Key.Equals("CaptureHighWatermarks", StringComparison.OrdinalIgnoreCase)).Value!.AsObject().ContainsKey(folder).ShouldBeFalse();
        string finalRaw = await fixture.QualificationPersistedValueAsync(sdkKey);
        string finalSdk = JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, folder, token));
        string finalLifecycle = JsonSerializer.Serialize((await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope,
            OperationalStatusKeys.Lifecycle(fixture.TenantId, folder), token)).Value);
        int finalFolds = Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder);
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
        foreach (var altered in new[] { grown[0] with { GlobalPosition = grown[0].GlobalPosition + 1 }, grown[0] with { SequenceNumber = 2 } })
        {
            var conflict = await fixture.NamedDeliveryAsync(folder, altered);
            conflict.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            conflict.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        (await fixture.QualificationPersistedValueAsync(sdkKey)).ShouldBe(finalRaw);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, folder, token)).ShouldBe(finalSdk);
        JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, key, token)).Value).ShouldBe(selected);
        JsonSerializer.Serialize((await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope,
            OperationalStatusKeys.Lifecycle(fixture.TenantId, folder), token)).Value).ShouldBe(finalLifecycle);
        Task36QualificationEvidence.FoldCount(fixture.FoldObservationPath, folder).ShouldBe(finalFolds);
        output.WriteLine("TASK36_EVIDENCE " + JsonSerializer.Serialize(new { phase = "final-state", sourceHash, otherHash,
            checkpoint = await coordinator.GetCheckpointAsync(scope, folder, token), status = await coordinator.GetStatusAsync(scope, token),
            rawCheckpoint = await fixture.QualificationPersistedValueAsync(sdkKey), selectedCheckpoint = selected }));
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

    private ProjectionEventDto[] History(string folder, int count, DateTimeOffset timestamp, long firstGlobalPosition = 1)
    {
        ProjectionEventDto[] events = new ProjectionEventDto[count];
        FolderCreated created = Created(folder, timestamp);
        events[0] = new(typeof(FolderCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(created), "json", 1,
            timestamp, "capacity-correlation", "capacity-message-1", GlobalPosition: firstGlobalPosition)
        { MetadataVersion = 1 };
        for (int index = 1; index < count; index++)
            events[index] = Rejection(index + 1, timestamp) with { GlobalPosition = firstGlobalPosition + index };
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
