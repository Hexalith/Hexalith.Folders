using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.ProviderReadiness;

using Hexalith.Folders.Tests.OperationalStatus;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.ProviderReadiness;

/// <summary>Verifies current source ordering, rejection positions, process replacement and safe corruption handling.</summary>
public sealed class DurableProviderBindingTests
{
    [Fact]
    public async Task PrivateProfileContextIsNotPersistedAndChangingTheTargetCannotRetargetAnExistingBinding()
    {
        const string profileRef = "opaque-profile";
        ProviderBindingProfile profile = new()
        {
            ManagedTenantId = "tenant-a",
            ProviderKind = "forgejo",
            NamingPolicy = new("opaque-target-policy", new Dictionary<string, string>
            {
                ["repository_owner"] = "private-owner-sentinel",
                ["repository_name"] = "private-repository-sentinel",
                ["authorized_base_url"] = "https://private-provider.invalid",
            }),
            BranchPolicy = new("opaque-ref-policy", new Dictionary<string, string>
            {
                ["selected_ref"] = "private-ref-sentinel",
            }),
        };
        OrganizationProviderBindingPolicy retainedNaming = new(profile.NamingPolicy.PolicyRef, new Dictionary<string, string>
        {
            [ProviderBindingProfileFingerprint.ProfileRefKey] = profileRef,
            [ProviderBindingProfileFingerprint.FingerprintKey] = ProviderBindingProfileFingerprint.Compute(profileRef, profile),
        });
        ProviderBindingConfigured configured = WithFingerprint(Configured() with
        {
            NamingPolicy = retainedNaming,
            BranchPolicy = new(profile.BranchPolicy.PolicyRef, new Dictionary<string, string>()),
        });
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 1 };
        string actor = ProviderBindingActorId.Create("binding-a");
        EventStoreProviderReadinessBindingReadModel source = new(store, head);
        await source.ApplyAsync("tenant-a", actor, "configured-1", 1, "digest", configured, TestContext.Current.CancellationToken);
        ConfiguredProviderReadinessBindingReader reader = new(source, Options.Create(new FoldersProviderBindingOptions
        { Profiles = new Dictionary<string, ProviderBindingProfile> { [profileRef] = profile } }));
        var request = new ProviderReadinessBindingReadRequest("tenant-a", "binding-a", "corr-a");
        OrganizationProviderBinding context = (await reader.GetAsync(request, TestContext.Current.CancellationToken))!;
        context.NamingPolicy.Metadata["repository_owner"].ShouldBe("private-owner-sentinel");
        context.BranchPolicy.Metadata["selected_ref"].ShouldBe("private-ref-sentinel");
        string retained = JsonSerializer.Serialize(store.Snapshot<ProviderBindingProjection>("statestore",
            EventStoreProviderReadinessBindingReadModel.Key("tenant-a", actor)));
        foreach (string sentinel in new[] { "private-owner-sentinel", "private-repository-sentinel", "private-ref-sentinel", "https://" })
            retained.ShouldNotContain(sentinel, Case.Sensitive);
        profile.BranchPolicy = profile.BranchPolicy with
        { Metadata = new Dictionary<string, string> { ["selected_ref"] = "changed-private-target" } };
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetAsync(request, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("credential")]
    [InlineData("naming-policy")]
    [InlineData("branch-policy")]
    public async Task CorruptProjectedProviderSemanticsFailBeforePrivateContextUse(string concern)
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 1 };
        string actor = ProviderBindingActorId.Create("binding-a");
        string key = EventStoreProviderReadinessBindingReadModel.Key("tenant-a", actor);
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        await reader.ApplyAsync("tenant-a", actor, "configured", 1, "digest", Configured(), TestContext.Current.CancellationToken);
        ProviderBindingProjection original = store.Snapshot<ProviderBindingProjection>("statestore", key)!;
        var binding = original.Binding!;
        binding = concern switch
        {
            "credential" => binding with { CredentialReferenceId = "different-reference" },
            "naming-policy" => binding with { NamingPolicy = new("altered-policy", new Dictionary<string, string>()) },
            _ => binding with { BranchPolicy = new("altered-policy", new Dictionary<string, string>()) },
        };
        store.SeedRaw("statestore", key, original with { Binding = binding });
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetAsync(new("tenant-a", "binding-a", "corr-a"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectedPositionsAndExactDuplicatesPreserveCurrentBindingAcrossReaderReplacement()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 0 };
        string actor = ProviderBindingActorId.Create("binding-a");
        head.Head = 3;
        EventStoreProviderReadinessBindingReadModel writer = new(store, head);
        await writer.ApplyAsync("tenant-a", actor, "rejection-1", 1, "rejected", null, TestContext.Current.CancellationToken);
        await writer.ApplyAsync("tenant-a", actor, "accepted-2", 2, "accepted", Configured(), TestContext.Current.CancellationToken);
        await writer.ApplyAsync("tenant-a", actor, "rejection-3", 3, "rejected-again", null, TestContext.Current.CancellationToken);
        await writer.ApplyAsync("tenant-a", actor, "accepted-2", 2, "accepted", Configured(), TestContext.Current.CancellationToken);
        EventStoreProviderReadinessBindingReadModel restarted = new(store, head);
        OrganizationProviderBinding? binding = await restarted.GetAsync(new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken);
        binding.ShouldNotBeNull().CredentialReferenceId.ShouldBe("reference-a");
        await Should.ThrowAsync<InvalidOperationException>(() => restarted.ApplyAsync("tenant-a", actor,
            "accepted-2", 2, "different-bytes", Configured(), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<PermissionProjectionConcurrencyException>(() => restarted.ApplyAsync("tenant-a", actor,
            "gap-5", 5, "gap", null, TestContext.Current.CancellationToken));
        head.Head = 4;
        await Should.ThrowAsync<InvalidOperationException>(() => restarted.GetAsync(new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingViewWithRetainedSourceIsUnavailableWhileEmptySourceIsAbsent()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 0 };
        string actor = ProviderBindingActorId.Create("binding-a");
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        (await reader.GetAsync(new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken)).ShouldBeNull();
        head.Head = 1;
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetAsync(new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletePrefixRebuildIncludesRejectionsAndRestoresOnlyCurrentConfiguration()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 3 };
        string actor = ProviderBindingActorId.Create("binding-a");
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        FolderProjectionMessage[] messages = [new(1, "rejected-1", "digest-1"),
            new(2, "configured-2", "digest-2"), new(3, "rejected-3", "digest-3")];
        await reader.RebuildAsync("tenant-a", actor, OrganizationState.Empty.Apply([Configured()]),
            messages, [ProviderBindingConfigurationEvidence.Create(2, "digest-2", Configured())], TestContext.Current.CancellationToken);
        (await new EventStoreProviderReadinessBindingReadModel(store, head).GetAsync(
            new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => reader.RebuildAsync("tenant-a", actor,
            OrganizationState.Empty.Apply([Configured()]), [messages[0], messages[1], messages[2] with { PayloadDigest = "different" }],
            [ProviderBindingConfigurationEvidence.Create(2, "digest-2", Configured())],
            TestContext.Current.CancellationToken));
        head.Head = 4;
        await Should.ThrowAsync<PermissionProjectionConcurrencyException>(() => reader.RebuildAsync("tenant-a", actor,
            OrganizationState.Empty.Apply([Configured()]), messages, [ProviderBindingConfigurationEvidence.Create(2, "digest-2", Configured())], TestContext.Current.CancellationToken));
    }

    private static ProviderBindingConfigured WithFingerprint(ProviderBindingConfigured value)
        => value with
        {
            IdempotencyFingerprint = OrganizationProviderBindingCommandValidator.Validate(new(
            value.ManagedTenantId, value.OrganizationId, OrganizationAclPrincipalKind.User, "principal-a", value.ProviderBindingRef,
            value.ProviderKind, value.CredentialReferenceId, value.NamingPolicy, value.BranchPolicy, value.CorrelationId,
            value.TaskId, value.IdempotencyKey, value.OccurredAt, null)).IdempotencyFingerprint!
        };

    [Fact]
    public async Task DeliveryBetweenRebuildReadAndCompareAndSwapPreservesTheNewerValidConfiguration()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 1 };
        string actor = ProviderBindingActorId.Create("binding-a");
        string key = EventStoreProviderReadinessBindingReadModel.Key("tenant-a", actor);
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        ProviderBindingConfigured original = Configured();
        ProviderBindingConfigured newer = WithFingerprint(original with
        { CredentialReferenceId = "reference-newer", IdempotencyKey = "key-newer" });
        await reader.ApplyAsync("tenant-a", actor, "configured-1", 1, "digest-1", original, TestContext.Current.CancellationToken);
        bool delivered = false;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            store.ConcurrentWriteBeforeTrySave = null;
            head.Head = 2;
            reader.ApplyAsync("tenant-a", actor, "configured-2", 2, "digest-2", newer,
                TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            delivered = true;
        };

        await Should.ThrowAsync<PermissionProjectionConcurrencyException>(() => reader.RebuildAsync("tenant-a", actor,
            OrganizationState.Empty.Apply([original]), [new FolderProjectionMessage(1, "configured-1", "digest-1")],
            [ProviderBindingConfigurationEvidence.Create(1, "digest-1", original)],
            TestContext.Current.CancellationToken));

        delivered.ShouldBeTrue();
        ProviderBindingProjection retained = store.Snapshot<ProviderBindingProjection>("statestore", key)!;
        retained.LastSequence.ShouldBe(2);
        retained.Messages.ShouldBe([new(1, "configured-1", "digest-1"), new(2, "configured-2", "digest-2")]);
        (await reader.GetAsync(new("tenant-a", "binding-a", "corr-a"), TestContext.Current.CancellationToken))!
            .CredentialReferenceId.ShouldBe("reference-newer");
    }

    private static ProviderBindingConfigured Configured()
        => WithFingerprint(new("tenant-a", "tenant-a", "binding-a", "forgejo", "reference-a",
            OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
            "corr-a", "task-a", "key-a", "fingerprint-a", "configured", DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task OlderCoherentConfigurationFailsAndCompleteSourceRepairsDamagedLatestBinding()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 2 };
        string actor = ProviderBindingActorId.Create("binding-a");
        string key = EventStoreProviderReadinessBindingReadModel.Key("tenant-a", actor);
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        var original = Configured();
        var newer = WithFingerprint(original with { CredentialReferenceId = "new-reference", IdempotencyKey = "new-key" });
        await reader.ApplyAsync("tenant-a", actor, "configuration-1", 1, "source-1", original, TestContext.Current.CancellationToken);
        var old = store.Snapshot<ProviderBindingProjection>("statestore", key)!;
        await reader.ApplyAsync("tenant-a", actor, "configuration-2", 2, "source-2", newer, TestContext.Current.CancellationToken);
        var current = store.Snapshot<ProviderBindingProjection>("statestore", key)!;
        foreach (var damaged in new[] { old.Binding!, current.Binding! with { CredentialReferenceId = "damaged" } })
        {
            store.SeedRaw("statestore", key, current with { Binding = damaged });
            await Should.ThrowAsync<InvalidOperationException>(() => reader.GetAsync(new("tenant-a", "binding-a", "corr"), TestContext.Current.CancellationToken));
            await reader.RebuildAsync("tenant-a", actor, OrganizationState.Empty.Apply([original, newer]), current.Messages,
                current.Configurations, TestContext.Current.CancellationToken);
            (await reader.GetAsync(new("tenant-a", "binding-a", "corr"), TestContext.Current.CancellationToken))!
                .CredentialReferenceId.ShouldBe("new-reference");
        }
    }

    [Fact]
    public async Task SourceAdvancingDuringConditionalRebuildSaveRequiresRetry()
    {
        InMemoryReadModelStore store = new();
        OperationalStatusSequenceReader head = new() { Head = 1 };
        string actor = ProviderBindingActorId.Create("binding-a");
        EventStoreProviderReadinessBindingReadModel reader = new(store, head);
        store.ConcurrentWriteBeforeTrySave = () => { store.ConcurrentWriteBeforeTrySave = null; head.Head = 2; };
        var error = await Should.ThrowAsync<PermissionProjectionConcurrencyException>(() => reader.RebuildAsync("tenant-a", actor,
            OrganizationState.Empty.Apply([Configured()]), [new FolderProjectionMessage(1, "configured", "source")],
            [ProviderBindingConfigurationEvidence.Create(1, "source", Configured())], TestContext.Current.CancellationToken));
        error.Message.ShouldContain("advanced");
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetAsync(new("tenant-a", "binding-a", "corr"), TestContext.Current.CancellationToken));
    }
}
