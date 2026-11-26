using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.Server.Events;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Provisioning;

using Shouldly;

using Xunit;

using static Hexalith.Folders.Server.Tests.Provisioning.ProvisioningQualificationFixture;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Qualifies the G-2 Folder stream across two application, sidecar, and Redis generations.</summary>
public sealed class ProvisioningQualificationTests
{
    private const string Before = "before-restart";
    private const string After = "after-restart";
    private const string SharedResults = "/results";
    private static readonly string[] RestrictedGrants = [$"{Actor}:manage_folder_access", $"{Actor}:read_metadata"];

    [Fact]
    public async Task CommitAndLoseReply()
    {
        RequirePhase(Before);
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = await StartAsync(frozen: false, token).ConfigureAwait(true);
        await using var fixtureLifetime = fixture.ConfigureAwait(true);
        await fixture.SetMembershipAsync(allowed: true, token);
        await fixture.SeedBaselineAsync(token);

        DurableTaskLease primary = await fixture.AdmitAsync("lost-response", Actor, token);
        FolderProvisioningRequest? original = null;
        FolderProvisioningReceipt? receipt = null;
        string? retry = null;
        string? changedIntent = null;
        string? staleVersion = null;
        int crossTenant = 0;
        int revoked = 0;
        int revokedLookup = 0;
        int revokedDuringRead = 0;
        int missingProof = 0;
        int changedDigest = 0;
        var dropReply = new ProvisioningQualificationEffect(async (step, cancellationToken) =>
        {
            var missing = await fixture.LookupAsync(Request(step), cancellationToken).ConfigureAwait(true);
            missing.Status.ShouldBe(204);
            missing.Payload.ShouldBeNull();
            return (DurableTaskEffectLookup.Absent, null);
        }, async (step, cancellationToken) =>
        {
            original = Request(step);
            (await fixture.SubmitAsync(original).ConfigureAwait(true)).ShouldBe((200, 3, null));
            await fixture.ProjectAsync(original, cancellationToken).ConfigureAwait(true);
            (await fixture.GrantsAsync(original, cancellationToken).ConfigureAwait(true)).ShouldBe(RestrictedGrants);
            receipt = ReadReceipt(await fixture.HistoryAsync(original).ConfigureAwait(true));
            receipt.OriginalActorId.ShouldBe(Actor);
            receipt.OwnerVersion.ShouldBe(3);

            retry = (await fixture.SubmitAsync(original with { DisplayName = "  " + DisplayName + "  " }).ConfigureAwait(true)).Outcome;
            retry.ShouldBe("receipt_available");
            var equivalent = await fixture.LookupAsync(original with { DisplayName = "  " + DisplayName + "  " }, cancellationToken)
                .ConfigureAwait(true);
            equivalent.Status.ShouldBe(200);
            equivalent.Payload.ShouldNotBeNull().GetProperty("receipt").Deserialize<FolderProvisioningReceipt>(FixtureJson).ShouldBe(receipt);
            changedIntent = (await fixture.SubmitAsync(original with { DisplayName = "Changed" }).ConfigureAwait(true)).Outcome;
            changedIntent.ShouldBe("conflict");
            FolderProvisioningRequest differentDigest = original with
            {
                DisplayName = "Changed",
                RequestSha256 = FolderProvisioningContract.ComputeRequestSha256("Changed"),
            };
            changedDigest = (await fixture.SubmitAsync(differentDigest).ConfigureAwait(true)).Status;
            changedDigest.ShouldBe(404);
            (await fixture.LookupAsync(differentDigest, cancellationToken).ConfigureAwait(true)).Status.ShouldBe(404);
            staleVersion = (await fixture.SubmitAsync(original with { ExpectedVersion = 1 }).ConfigureAwait(true)).Outcome;
            staleVersion.ShouldBe("conflict");
            crossTenant = (await fixture.SubmitAsync(original, "tenant-b").ConfigureAwait(true)).Status;
            crossTenant.ShouldBe(404);
            (await fixture.HistoryAsync(original, "tenant-b").ConfigureAwait(true)).ShouldBeEmpty();

            await fixture.SetMembershipAsync(allowed: false, cancellationToken).ConfigureAwait(true);
            revoked = (await fixture.SubmitAsync(original).ConfigureAwait(true)).Status;
            revoked.ShouldBe(404);
            var withheld = await fixture.LookupAsync(original, cancellationToken).ConfigureAwait(true);
            revokedLookup = withheld.Status;
            revokedLookup.ShouldBe(404);
            withheld.Payload.ShouldBeNull();
            await fixture.SetMembershipAsync(allowed: true, cancellationToken).ConfigureAwait(true);

            int observedMetadata = 0;
            fixture.Streams.BeforeReply = async (read, readToken) =>
            {
                if (read.ToSequence == 0 && ++observedMetadata == 4)
                {
                    await fixture.SetMembershipAsync(allowed: false, readToken).ConfigureAwait(true);
                }
            };
            var interrupted = await fixture.LookupAsync(original, cancellationToken).ConfigureAwait(true);
            fixture.Streams.BeforeReply = null;
            observedMetadata.ShouldBe(4);
            revokedDuringRead = interrupted.Status;
            revokedDuringRead.ShouldBe(404);
            interrupted.Payload.ShouldBeNull();
            await fixture.SetMembershipAsync(allowed: true, cancellationToken).ConfigureAwait(true);

            fixture.Streams.OmitNativeProof = true;
            var unverified = await fixture.LookupAsync(original, cancellationToken).ConfigureAwait(true);
            fixture.Streams.OmitNativeProof = false;
            missingProof = unverified.Status;
            missingProof.ShouldBe(503);
            unverified.Payload.ShouldBeNull();
            throw new IOException("Simulated lost response after the owner committed.");
        });
        await Should.ThrowAsync<IOException>(() => fixture.Execution.RunStepAsync(primary, ProvisioningQualificationActorHost.StepNumber, dropReply, token));
        DurableTaskProgress pending = (await fixture.ProgressAsync(primary.Task, token).ConfigureAwait(true)).ShouldNotBeNull();
        pending.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);
        pending.EffectPending.ShouldBeTrue();
        pending.Receipts.ShouldBeEmpty();
        FolderProvisioningRequest request = original.ShouldNotBeNull();
        FolderProvisioningReceipt committed = receipt.ShouldNotBeNull();
        EventEnvelope[] committedHistory = await fixture.HistoryAsync(request);
        committedHistory.Length.ShouldBe(3);
        var nativeProof = (await fixture.InspectSourceAsync(request).ConfigureAwait(true)).Proof.ShouldNotBeNull();
        nativeProof.HeadSequence.ShouldBe(3);

        DurableTaskLease occupiedLease = await fixture.AdmitAsync("incumbent", Actor, token);
        var occupied = new ProvisioningQualificationEffect(Absent, async (step, cancellationToken) =>
        {
            FolderProvisioningRequest collision = Request(step);
            await fixture.SeedIncumbentAsync(collision, cancellationToken).ConfigureAwait(true);
            string before = Digest(await fixture.HistoryAsync(collision).ConfigureAwait(true));
            (await fixture.SubmitAsync(collision).ConfigureAwait(true)).ShouldBe((200, 0, "conflict"));
            (await fixture.LookupAsync(collision, cancellationToken).ConfigureAwait(true)).Status.ShouldBe(409);
            Digest(await fixture.HistoryAsync(collision).ConfigureAwait(true)).ShouldBe(before);
            (await fixture.GrantsAsync(collision, cancellationToken).ConfigureAwait(true)).ShouldContain($"{Other}:read_metadata");
            return (DurableTaskEffectLookup.Rejected, null);
        });
        await fixture.Execution.RunStepAsync(occupiedLease, ProvisioningQualificationActorHost.StepNumber, occupied, token);
        string occupiedKey = $"{occupiedLease.Task.TaskId}/step/{ProvisioningQualificationActorHost.StepNumber}";
        FolderProvisioningRequest occupiedRequest = request with { TaskId = occupiedLease.Task.TaskId, StepKey = occupiedKey, Fence = occupiedLease.Fence };
        EventEnvelope[] incumbent = await fixture.HistoryAsync(occupiedRequest);
        incumbent.Length.ShouldBe(1);
        ReadCreate(incumbent).ProvisioningReceipt.ShouldBeNull();

        DurableTaskLease outageLease = await fixture.AdmitAsync("outage", Actor, token);
        var outage = new ProvisioningQualificationEffect(Absent, async (step, cancellationToken) =>
        {
            FolderProvisioningRequest outageRequest = Request(step);
            fixture.SetLookupDependency(available: false);
            try
            {
                (await fixture.SubmitAsync(outageRequest).ConfigureAwait(true)).ShouldBe((503, 0, null));
                var withheld = await fixture.LookupAsync(request, cancellationToken).ConfigureAwait(true);
                withheld.Status.ShouldBe(503);
                withheld.Payload.ShouldBeNull();
                (await fixture.HistoryAsync(outageRequest).ConfigureAwait(true)).ShouldBeEmpty();
            }
            finally
            {
                fixture.SetLookupDependency(available: true);
            }

            throw new IOException("The owner could not reach the lookup dependency.");
        });
        await Should.ThrowAsync<IOException>(() => fixture.Execution.RunStepAsync(outageLease, ProvisioningQualificationActorHost.StepNumber, outage, token));

        DurableTaskLease sameNameLease = await fixture.AdmitAsync("same-name", Actor, token);
        var sameName = new ProvisioningQualificationEffect(Absent, async (step, cancellationToken) =>
        {
            FolderProvisioningRequest unrelated = Request(step);
            FolderId(unrelated).ShouldNotBe(committed.FolderId);
            (await fixture.SubmitAsync(unrelated).ConfigureAwait(true)).ShouldBe((200, 3, null));
            await fixture.ProjectAsync(unrelated, cancellationToken).ConfigureAwait(true);
            EventEnvelope[] history = await fixture.HistoryAsync(unrelated).ConfigureAwait(true);
            ReadCreate(history).DisplayName.ShouldBe(DisplayName);

            await fixture.RevokeManageAsync(unrelated, cancellationToken).ConfigureAwait(true);
            (await fixture.SubmitAsync(unrelated).ConfigureAwait(true)).ShouldBe((404, 0, null));
            var withheld = await fixture.LookupAsync(unrelated, cancellationToken).ConfigureAwait(true);
            withheld.Status.ShouldBe(404);
            withheld.Payload.ShouldBeNull();
            EventEnvelope[] retained = await fixture.HistoryAsync(unrelated).ConfigureAwait(true);
            retained.Length.ShouldBe(4);
            ReadReceipt(retained).ShouldBe(ReadReceipt(history));
            return (DurableTaskEffectLookup.Unresolved, null);
        });
        (await fixture.Execution.RunStepAsync(sameNameLease, ProvisioningQualificationActorHost.StepNumber, sameName, token)
            .ConfigureAwait(true)).ShouldBeNull();
        var unresolved = await fixture.ProgressAsync(sameNameLease.Task, token).ConfigureAwait(true);
        unresolved.ShouldNotBeNull().Status.ShouldBe(DurableTaskStatus.NeedsAttention);

        await SaveAsync(Before, new
        {
            request, receipt = committed, nativeProof, folderHead = committedHistory.Length, folderSha256 = Digest(committedHistory),
            acl = RestrictedGrants, incumbent = occupiedRequest, incumbentHead = incumbent.Length,
            incumbentSha256 = Digest(incumbent), sameDisplayNameDistinctFolder = true,
            equivalentRetry = retry, changedIntent, changedDigest, staleVersion, crossTenant, revoked, revokedLookup,
            revokedDuringRead, missingProof, explicitManageRevoked = 404, unresolved = unresolved.Status.ToString(),
            taskBeforeRestart = pending.Status.ToString(), taskReceiptsBeforeRestart = pending.Receipts.Length,
            authorizedAbsence = 204, dependencyFailure = 503, occupiedCommand = "conflict", occupiedLookup = 409, responseDropped = true,
        }, token);
    }

    [Fact]
    public async Task RecoverWithMutationsFrozen()
    {
        RequirePhase(After);
        CancellationToken token = TestContext.Current.CancellationToken;
        using var prior = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(SharedResults, Before + ".json"), token));
        JsonElement previous = prior.RootElement;
        var old = previous.GetProperty("request").Deserialize<FolderProvisioningRequest>(FixtureJson).ShouldNotBeNull();
        var receipt = previous.GetProperty("receipt").Deserialize<FolderProvisioningReceipt>(FixtureJson).ShouldNotBeNull();
        var incumbent = previous.GetProperty("incumbent").Deserialize<FolderProvisioningRequest>(FixtureJson).ShouldNotBeNull();
        var fixture = await StartAsync(frozen: true, token).ConfigureAwait(true);
        await using var fixtureLifetime = fixture.ConfigureAwait(true);
        DurableTaskLease replacement = (await fixture.Execution.TryAcquireAsync(old.TaskId, token)).ShouldNotBeNull();
        replacement.Fence.ShouldBe(old.Fence + 1);

        var stale = await fixture.LookupAsync(old, token).ConfigureAwait(true);
        stale.Status.ShouldBe(404);
        stale.Payload.ShouldBeNull();

        JsonElement recovered = default;
        var recover = new ProvisioningQualificationEffect(async (step, cancellationToken) =>
        {
            FolderProvisioningRequest request = Request(step);
            var response = await fixture.LookupAsync(request, cancellationToken).ConfigureAwait(true);
            response.Status.ShouldBe(200);
            recovered = response.Payload.ShouldNotBeNull();
            var observed = recovered.GetProperty("receipt").Deserialize<FolderProvisioningReceipt>(FixtureJson);
            observed.ShouldBe(receipt);
            recovered.GetProperty("currentOwnerVersion").GetInt64().ShouldBe(3);
            recovered.GetProperty("disposition").GetString().ShouldBe("NeedsAttention");
            return (DurableTaskEffectLookup.Committed, observed!.ReceiptId);
        });
        (await fixture.Execution.RunStepAsync(replacement, ProvisioningQualificationActorHost.StepNumber, recover, token))
            .ShouldBe(receipt.ReceiptId);
        DurableTaskProgress completedStep = (await fixture.ProgressAsync(replacement.Task, token).ConfigureAwait(true)).ShouldNotBeNull();
        completedStep.EffectPending.ShouldBeFalse();
        completedStep.Receipts.ShouldBe([receipt.ReceiptId]);

        (await fixture.SubmitAsync(old with { Fence = replacement.Fence })).ShouldBe((503, 0, null));
        EventEnvelope[] folder = await fixture.HistoryAsync(old);
        EventEnvelope[] retainedIncumbent = await fixture.HistoryAsync(incumbent);
        Digest(folder).ShouldBe(previous.GetProperty("folderSha256").GetString());
        Digest(retainedIncumbent).ShouldBe(previous.GetProperty("incumbentSha256").GetString());
        ReadReceipt(folder).ShouldBe(receipt);
        var nativeProof = (await fixture.InspectSourceAsync(old).ConfigureAwait(true)).Proof.ShouldNotBeNull();
        nativeProof.ShouldBe(previous.GetProperty("nativeProof").Deserialize<Hexalith.EventStore.Server.Events.SourceProof>(FixtureJson));
        (await fixture.GrantsAsync(old, token)).ShouldBe(RestrictedGrants);
        (await fixture.GrantsAsync(incumbent, token)).ShouldContain($"{Other}:read_metadata");
        await SaveAsync(After, new
        {
            replacementFence = replacement.Fence, staleFence = old.Fence, staleFenceStatus = stale.Status,
            newProvisioningDisabled = true, mutationsFrozen = true, frozenProvisionStatus = 503,
            folderHead = folder.Length, folderSha256 = Digest(folder), acl = RestrictedGrants,
            incumbentHead = retainedIncumbent.Length, incumbentSha256 = Digest(retainedIncumbent),
            ownerExecutionCalls = 0, recovered, nativeProof, taskReceiptsAfterRestart = completedStep.Receipts,
        }, token);
    }

    private static Task<(DurableTaskEffectLookup, string?)> Absent(DurableTaskEffectStep step, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Absent, null));
    }

    private static FolderCreated ReadCreate(EventEnvelope[] history)
        => (FolderCreated)ProvisioningQualificationInvoker.ReadFolderEvent(history[0].EventTypeName, history[0].Payload);

    private static FolderProvisioningReceipt ReadReceipt(EventEnvelope[] history)
        => ReadCreate(history).ProvisioningReceipt.ShouldNotBeNull();

    private static string Digest(EventEnvelope[] history)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(history, FixtureJson)));

    private static Task SaveAsync(string phase, object record, CancellationToken token)
        => File.WriteAllBytesAsync(Path.Combine(SharedResults, phase + ".json"), JsonSerializer.SerializeToUtf8Bytes(record, FixtureJson), token);

    private static void RequirePhase(string phase)
        => Assert.Equal(phase, Environment.GetEnvironmentVariable("G2_QUALIFICATION_PHASE"));
}
