using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Contracts.Events;
using Hexalith.Tenants.Contracts.Identity;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Qualifies fixture-only real-event maintenance without weakening production freshness or authority.</summary>
public sealed class FolderDurabilityTenantMaintenanceTests
{
    private const string Tenant = "durability-test";
    private const string Principal = "fixture-principal";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CommandUsesOnlyTheApprovedOwnTenantMetadataAndCanonicalScope()
    {
        SubmitCommandRequest command = FolderDurabilityTenantMaintenance.Command(Tenant, "original-command");
        command.MessageId.ShouldBe("original-command");
        command.Tenant.ShouldBe(TenantPlatformScopes.Tenants);
        command.Domain.ShouldBe("tenants");
        command.AggregateId.ShouldBe(Tenant);
        command.CommandType.ShouldBe("UpdateTenant");
        command.Payload.EnumerateObject().Select(property => property.Name).ShouldBe(["tenantId", "name", "description"]);
        command.Payload.GetProperty("tenantId").GetString().ShouldBe(Tenant);
        command.Payload.GetProperty("name").GetString().ShouldBe("Durability acceptance");
        command.Payload.GetProperty("description").GetString().ShouldBe("Disposable local acceptance tenant");
        command.IdempotencyKey.ShouldBeNull();
        using HttpRequestMessage request = new();
        FolderDurabilityGatewayScope.Apply(request, command.Tenant);
        request.Headers.GetValues("X-Hexalith-Scope").ShouldBe([TenantPlatformScopes.Tenants]);
        request.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(119)]
    public async Task FreshOriginalSourceAndWorkersEvidenceNeverSubmitMaintenance(int age)
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddSeconds(-age));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        int commands = 0;
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => Task.FromResult(source), _ => Task.FromResult<FolderTenantAccessProjection?>(projection),
            (_, _, _) => { commands++; throw new InvalidOperationException("Unexpected maintenance."); });
        (await maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        commands.ShouldBe(0);
        projection.LastEventTimestamp.ShouldBe(source.Events[^1].Timestamp);
    }

    [Fact]
    public async Task OldEvidenceRequiresMatchingNewSourceAndRealHandlerDeliveryWithIdenticalAuthority()
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        var store = new InMemoryFolderTenantAccessProjectionStore();
        FolderTenantAccessProjection seed = Projection(source.Events[^1]);
        seed.Version = 0;
        await store.SaveAsync(seed, TestContext.Current.CancellationToken);
        FolderTenantAccessProjection before = (await store.GetAsync(Tenant, TestContext.Current.CancellationToken))!;
        string originalPrefix = JsonSerializer.Serialize(source.Events, Json);
        int commands = 0;
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => Task.FromResult(source), token => store.GetAsync(Tenant, token),
            async (command, correlation, token) =>
            {
                commands++;
                token.CanBeCanceled.ShouldBeTrue();
                command.Tenant.ShouldBe(TenantPlatformScopes.Tenants);
                command.AggregateId.ShouldBe(Tenant);
                StreamReadEvent updated = Updated(command.MessageId, correlation);
                source = Append(source, updated);
                FolderTenantAccessEvent delivery = new FoldersTenantAccessEventMapper().Map(FolderTenantAccessEventKind.TenantUpdated,
                    Tenant, Tenant, updated.MessageId, updated.SequenceNumber, updated.Timestamp, correlation,
                    fingerprintParts: [FolderDurabilityTenantMaintenance.TenantName]);
                await new FolderTenantAccessHandler(store, new SystemUtcClock(), new TenantAccessOptions()).HandleAsync(delivery, token).ConfigureAwait(false);
                return Completed(command.MessageId, correlation);
            });

        (await maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        commands.ShouldBe(1);
        FolderTenantAccessProjection after = (await store.GetAsync(Tenant, TestContext.Current.CancellationToken))!;
        after.Enabled.ShouldBe(before.Enabled);
        after.Principals.OrderBy(pair => pair.Key).ShouldBe(before.Principals.OrderBy(pair => pair.Key));
        after.ConfigurationKeys.ShouldBe(before.ConfigurationKeys);
        after.RemovedConfigurationKeys.ShouldBe(before.RemovedConfigurationKeys);
        after.Watermark.ShouldBe(3);
        after.LastEventTimestamp.ShouldBe(source.Events[^1].Timestamp);
        after.ProcessedMessages[source.Events[^1].MessageId].SequenceNumber.ShouldBe(3);
        JsonSerializer.Serialize(source.Events.Take(2), Json).ShouldBe(originalPrefix);
        (await maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        commands.ShouldBe(1);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Failed")]
    [InlineData("PublishFailed")]
    [InlineData("TimedOut")]
    [InlineData("Accepted")]
    [InlineData("wrong-message")]
    [InlineData("wrong-correlation")]
    public async Task AnUnsuccessfulOrUnboundTerminalResultCannotEstablishFreshness(string fault)
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        int reads = 0;
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => { reads++; return Task.FromResult(source); }, _ => Task.FromResult<FolderTenantAccessProjection?>(projection),
            (command, correlation, _) => Task.FromResult(JsonSerializer.SerializeToElement(new
            {
                status = fault.StartsWith("wrong-", StringComparison.Ordinal) ? "Completed" : fault,
                messageId = fault == "wrong-message" ? "another-command" : command.MessageId,
                correlationId = fault == "wrong-correlation" ? "another-correlation" : correlation,
            })));
        await Should.ThrowAsync<InvalidOperationException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken));
        reads.ShouldBe(1);
        projection.Watermark.ShouldBe(2);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("scope")]
    [InlineData("domain")]
    [InlineData("truncated")]
    [InlineData("sequence")]
    [InlineData("message")]
    [InlineData("correlation")]
    [InlineData("actor")]
    [InlineData("event")]
    [InlineData("timestamp")]
    [InlineData("metadata")]
    [InlineData("description")]
    [InlineData("prefix")]
    public async Task ChangedOrUnrelatedOriginalSourceCannotEstablishFreshness(string fault)
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => Task.FromResult(source), _ => Task.FromResult<FolderTenantAccessProjection?>(projection),
            (command, correlation, _) =>
            {
                StreamReadEvent updated = Updated(command.MessageId, correlation);
                updated = fault switch
                {
                    "sequence" => updated with { SequenceNumber = 4 },
                    "message" => updated with { CausationId = "another-command" },
                    "correlation" => updated with { CorrelationId = "another-correlation" },
                    "actor" => updated with { UserId = "another-user" },
                    "event" => updated with { EventTypeName = typeof(TenantEnabled).FullName! },
                    "timestamp" => updated with { Timestamp = source.Events[^1].Timestamp },
                    "metadata" => updated with { MetadataVersion = 2 },
                    "description" => updated with
                    {
                        Payload = JsonSerializer.SerializeToUtf8Bytes(new TenantUpdated(Tenant,
                        FolderDurabilityTenantMaintenance.TenantName, "changed", DateTimeOffset.UtcNow), Json)
                    },
                    _ => updated,
                };
                source = Append(source, updated);
                source = fault switch
                {
                    "tenant" => source with { AggregateId = "another-tenant" },
                    "scope" => source with { Tenant = Tenant },
                    "domain" => source with { Domain = "folders" },
                    "truncated" => source with { Metadata = source.Metadata with { IsTruncated = true } },
                    "prefix" => source with { Events = [source.Events[0] with { UserId = "changed" }, source.Events[1], updated] },
                    _ => source,
                };
                return Task.FromResult(Completed(command.MessageId, correlation));
            });
        await Should.ThrowAsync<InvalidOperationException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken));
        projection.Watermark.ShouldBe(2);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("principal")]
    [InlineData("role")]
    [InlineData("configuration")]
    [InlineData("removed-configuration")]
    [InlineData("conflict")]
    [InlineData("malformed")]
    [InlineData("timestamp")]
    [InlineData("fingerprint")]
    [InlineData("message")]
    [InlineData("prior-evidence")]
    public async Task ChangedAuthorityOrUnmatchedWorkersEvidenceCannotEstablishFreshness(string fault)
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => Task.FromResult(source), _ => Task.FromResult<FolderTenantAccessProjection?>(projection),
            (command, correlation, _) =>
            {
                StreamReadEvent updated = Updated(command.MessageId, correlation);
                source = Append(source, updated);
                FolderTenantAccessProjection next = Projection(updated);
                foreach (var pair in projection.ProcessedMessages) next.ProcessedMessages.Add(pair.Key, pair.Value);
                next.Version = projection.Version + 1;
                switch (fault)
                {
                    case "enabled": next.Enabled = false; break;
                    case "principal": next.Principals["extra"] = new("extra", "TenantOwner"); break;
                    case "role": next.Principals[Principal] = new(Principal, "TenantContributor"); break;
                    case "configuration": next.ConfigurationKeys.Add("folders.changed"); break;
                    case "removed-configuration": next.RemovedConfigurationKeys.Add("folders.changed"); break;
                    case "conflict": next.ReplayConflict = true; break;
                    case "malformed": next.MalformedEvidence = true; break;
                    case "timestamp": next.LastEventTimestamp = updated.Timestamp.AddSeconds(-1); break;
                    case "fingerprint": next.ProcessedMessages[updated.MessageId] = next.ProcessedMessages[updated.MessageId] with { PayloadFingerprint = "wrong" }; break;
                    case "message": next.ProcessedMessages.Remove(updated.MessageId); break;
                    case "prior-evidence": next.ProcessedMessages.Remove(projection.ProcessedMessages.Keys.Single()); break;
                }
                projection = next;
                return Task.FromResult(Completed(command.MessageId, correlation));
            });
        await Should.ThrowAsync<InvalidOperationException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("command")]
    [InlineData("new-source")]
    [InlineData("projection")]
    public async Task CallerCancellationReachesEveryIoAndNeverTreatsAbsenceOrLagAsFresh(string stage)
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        bool submitted = false;
        CancellationToken ioToken = default;
        async Task CancelAsync(CancellationToken token)
        {
            ioToken = token;
            caller.Cancel();
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            async token =>
            {
                if (stage == "source" || stage == "new-source" && submitted) await CancelAsync(token).ConfigureAwait(false);
                return source;
            }, async token =>
            {
                if (stage == "projection" && submitted) await CancelAsync(token).ConfigureAwait(false);
                return projection;
            }, async (command, correlation, token) =>
            {
                submitted = true;
                if (stage == "command") await CancelAsync(token).ConfigureAwait(false);
                if (stage == "projection") source = Append(source, Updated(command.MessageId, correlation));
                return Completed(command.MessageId, correlation);
            });
        await Should.ThrowAsync<OperationCanceledException>(() => maintenance.EnsureFreshAsync(caller.Token));
        ioToken.IsCancellationRequested.ShouldBeTrue();
        ioToken.ShouldNotBe(caller.Token);
        projection.Watermark.ShouldBe(2);
    }

    [Fact]
    public async Task WholeMaintenanceDeadlineCancelsARealOutstandingIoWithoutWaitingForItsOwnLimit()
    {
        var timer = new FolderDurabilityTenantMaintenanceTimer();
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal, async token =>
        {
            timer.DueTime.ShouldBe(TimeSpan.FromSeconds(90));
            timer.Expire();
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable.");
        }, _ => throw new InvalidOperationException("Unexpected projection."),
            (_, _, _) => throw new InvalidOperationException("Unexpected command."), timer);
        await Should.ThrowAsync<OperationCanceledException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("new-source")]
    [InlineData("projection")]
    [InlineData("initial-projection")]
    public async Task CompletedCommandCannotTurnActuallyAbsentOrLaggingEvidenceIntoFreshness(string missing)
    {
        var timer = new FolderDurabilityTenantMaintenanceTimer();
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        bool submitted = false;
        int observedMissing = 0;
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal, _ =>
        {
            if (submitted && missing == "new-source")
            {
                observedMissing++;
                timer.Expire();
            }
            return Task.FromResult(source);
        }, _ =>
        {
            if (missing == "initial-projection" || submitted && missing == "projection")
            {
                observedMissing++;
                timer.Expire();
            }
            return Task.FromResult(missing == "initial-projection" ? null : (FolderTenantAccessProjection?)projection);
        }, (command, correlation, _) =>
        {
            submitted = true;
            if (missing == "projection") source = Append(source, Updated(command.MessageId, correlation));
            return Task.FromResult(Completed(command.MessageId, correlation));
        }, timer);

        await Should.ThrowAsync<OperationCanceledException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken));
        submitted.ShouldBe(missing != "initial-projection");
        observedMissing.ShouldBe(1);
        projection.Watermark.ShouldBe(2);
        projection.LastEventTimestamp.ShouldBe(source.Events[1].Timestamp);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("projection")]
    [InlineData("command")]
    public async Task IoFailureIsPropagatedWithoutAnInventedFreshObservation(string stage)
    {
        StreamReadPage source = Original(DateTimeOffset.UtcNow.AddMinutes(-3));
        FolderTenantAccessProjection projection = Projection(source.Events[^1]);
        var failure = new IOException("Controlled fixture transport failure.");
        var maintenance = new FolderDurabilityTenantMaintenance(Tenant, Principal,
            _ => stage == "source" ? Task.FromException<StreamReadPage>(failure) : Task.FromResult(source),
            _ => stage == "projection" ? Task.FromException<FolderTenantAccessProjection?>(failure)
                : Task.FromResult<FolderTenantAccessProjection?>(projection),
            (_, _, _) => Task.FromException<JsonElement>(failure));
        (await Should.ThrowAsync<IOException>(() => maintenance.EnsureFreshAsync(TestContext.Current.CancellationToken))).ShouldBeSameAs(failure);
        projection.Watermark.ShouldBe(2);
    }

    [Fact]
    public void RealmAddsTheSinglePermissionOnceAndPreservesEveryOtherIdentityAndGrant()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        string before = original.ToJsonString();
        JsonObject actual = FolderDurabilityBootstrapConfiguration.Realm(original);
        JsonObject admin = actual["users"]!.AsArray().Single(user => user!["id"]!.GetValue<string>() == "admin-user")!.AsObject();
        JsonArray permissions = admin["attributes"]!["permissions"]!.AsArray();
        permissions.Count(value => value!.GetValue<string>() == "tenants:update").ShouldBe(1);
        foreach (JsonNode? user in actual["users"]!.AsArray().Where(user => user!["id"]!.GetValue<string>() != "admin-user"))
        {
            user!["attributes"]!["permissions"]!.AsArray().ShouldNotContain(value => value!.GetValue<string>() == "tenants:update");
            user.ToJsonString().ShouldBe(original["users"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == user["id"]!.GetValue<string>())!.ToJsonString());
        }
        JsonObject alreadyGranted = original.DeepClone().AsObject();
        alreadyGranted["users"]!.AsArray().Single(user => user!["id"]!.GetValue<string>() == "admin-user")!["attributes"]!["permissions"]!.AsArray().Add("tenants:update");
        JsonArray repeated = FolderDurabilityBootstrapConfiguration.Realm(alreadyGranted)["users"]!.AsArray()
            .Single(user => user!["id"]!.GetValue<string>() == "admin-user")!["attributes"]!["permissions"]!.AsArray();
        repeated.Count(value => value!.GetValue<string>() == "tenants:update").ShouldBe(1);
        repeated.Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal)
            .ShouldBe(permissions.Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        original.ToJsonString().ShouldBe(before);
    }

    private static StreamReadPage Original(DateTimeOffset timestamp)
    {
        StreamReadEvent first = new(1, typeof(TenantCreated).FullName!, [], "json", 1, "created", "create", "create", timestamp.AddSeconds(-1), "admin-user");
        StreamReadEvent last = new(2, typeof(UserAddedToTenant).FullName!, [], "json", 1, "member", "membership", "membership", timestamp, "admin-user");
        return new(TenantIdentity.DefaultTenantId, "tenants", Tenant, [first, last], new(0, null, 2, 2, 2, false, null));
    }

    private static StreamReadEvent Updated(string messageId, string correlation)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new(3, typeof(TenantUpdated).FullName!, JsonSerializer.SerializeToUtf8Bytes(new TenantUpdated(Tenant,
            FolderDurabilityTenantMaintenance.TenantName, FolderDurabilityTenantMaintenance.TenantDescription, now), Json),
            "json", 1, "updated-" + messageId, correlation, messageId, now, "admin-user");
    }

    private static StreamReadPage Append(StreamReadPage before, StreamReadEvent updated)
        => before with { Events = [.. before.Events, updated], Metadata = new(0, null, 3, 3, 3, false, null) };

    private static FolderTenantAccessProjection Projection(StreamReadEvent source)
    {
        var projection = new FolderTenantAccessProjection
        {
            TenantId = Tenant,
            Enabled = true,
            Version = 1,
            Watermark = source.SequenceNumber,
            LastEventTimestamp = source.Timestamp,
            ProjectionWatermark = $"{Tenant}:{source.SequenceNumber}",
        };
        projection.Principals[Principal] = new(Principal, "TenantOwner");
        projection.ConfigurationKeys.Add("folders.existing");
        projection.RemovedConfigurationKeys.Add("folders.removed");
        string kind = source.SequenceNumber == 3 ? "TenantUpdated" : "UserAddedToTenant";
        string fingerprint = source.SequenceNumber == 3
            ? new FoldersTenantAccessEventMapper().Map(FolderTenantAccessEventKind.TenantUpdated, Tenant, Tenant, source.MessageId,
                source.SequenceNumber, source.Timestamp, source.CorrelationId!, fingerprintParts: [FolderDurabilityTenantMaintenance.TenantName]).PayloadFingerprint
            : "membership-fingerprint";
        projection.ProcessedMessages[source.MessageId] = new(source.MessageId, Tenant, kind, source.SequenceNumber, source.Timestamp, fingerprint);
        return projection;
    }

    private static JsonElement Completed(string messageId, string correlation)
        => JsonSerializer.SerializeToElement(new { status = "Completed", messageId, correlationId = correlation });
}
