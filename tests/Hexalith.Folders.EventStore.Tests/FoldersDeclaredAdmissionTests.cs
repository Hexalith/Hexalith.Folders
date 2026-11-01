using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Mvc;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Proves finite schema-two admission without changing the legacy intent representation.</summary>
public sealed class FoldersDeclaredAdmissionTests
{
    /// <summary>Gets every existing discriminator from the pre-migration byte capture.</summary>
    public static IEnumerable<object[]> LegacyCommands => LegacyDescriptors().Select(static row => new object[] { row.CommandType });

    /// <summary>Requires one golden row for every adapter installed by production registration.</summary>
    [Fact]
    public async Task LegacyFixtureDiscriminatorsExactlyMatchProductionAdapters()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        string[] fixtureDiscriminators = LegacyDescriptors().Select(static row => row.CommandType).ToArray();
        fixtureDiscriminators.Distinct(StringComparer.Ordinal).Count().ShouldBe(fixtureDiscriminators.Length);
        fixtureDiscriminators.Order(StringComparer.Ordinal).ShouldBe(
            fixture.Adapters.Select(static adapter => adapter.CommandType).Order(StringComparer.Ordinal));
    }

    /// <summary>Compares every real admitted descriptor with its complete pre-migration bytes.</summary>
    /// <param name="commandType">The existing discriminator.</param>
    [Theory]
    [MemberData(nameof(LegacyCommands))]
    public async Task EveryProductionAdapterResolvesItsExactLegacyBytesAfterCommittedAdmission(string commandType)
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        fixture.Registry.ShouldBeOfType<IdempotencyIntentAdapterRegistry>();
        fixture.Authority.ShouldBeOfType<RouteCatalogAuthority>();
        await fixture.ActivateAsync().ConfigureAwait(true);
        LegacyFoldersIntentDescriptor legacy = LegacyDescriptors().Single(row => row.CommandType == commandType);

        TrustedIdempotencyDescriptor descriptor = await fixture.Registry.ResolveAsync(Command(legacy), TestContext.Current.CancellationToken).ConfigureAwait(true);

        descriptor.CanonicalIntent.ShouldBe(Convert.FromBase64String(legacy.CanonicalIntent));
        Convert.ToHexStringLower(SHA256.HashData(descriptor.CanonicalIntent)).ShouldBe(legacy.Sha256);
        descriptor.OperationId.ShouldBe(legacy.OperationId);
        descriptor.AdapterId.ShouldBe("hexalith-folders");
        descriptor.DescriptorVersion.ShouldBe(1);
        descriptor.RetentionTier.ShouldBe(legacy.OperationId == "commit-workspace"
            ? IdempotencyReplayRetentionTier.Commit : IdempotencyReplayRetentionTier.Mutation);
        // This preserves legacy equivalence only; subject authorization remains in the Folders request handler.
        (await fixture.Registry.ResolveAsync(Command(legacy) with { AuthenticatedSubject = "another-authorized-user" }, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .CanonicalIntent.ShouldBe(descriptor.CanonicalIntent);
        RouteCatalogActivationState state = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State;
        state.Committed.ShouldNotBeNull();
        state.Pending.ShouldBeNull();
        state.Committed.RequiredHosts.Count.ShouldBe(2);
        state.Epoch.ShouldBe(1);
        state.Ready.ShouldBeEmpty();
        state.Leases.ShouldBeEmpty();
    }

    /// <summary>Checks the custom descriptor labels against actual legacy encoder component values.</summary>
    [Fact]
    public async Task DescriptorLabelsMapToCompleteExistingEncoderComponents()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        foreach (LegacyFoldersIntentDescriptor legacy in LegacyDescriptors())
        {
            IDeclaredIdempotencyIntentAdapter adapter = fixture.Adapters.Single(adapter => adapter.CommandType == legacy.CommandType)
                .ShouldBeAssignableTo<IDeclaredIdempotencyIntentAdapter>();
            CommandIntentDeclaration declaration = adapter.Declaration;
            RouteCatalogValidator.ValidateDeclaration(declaration.Route);
            RouteCatalogValidator.ValidateDescriptor(declaration.DescriptorSchema);
            declaration.DescriptorSchema.SchemaId.ShouldBe($"folders-{legacy.OperationId}-intent");
            declaration.RequiresAuthenticatedSubject.ShouldBeFalse();
            declaration.Route.Execution!.HandlerType.ShouldBe(typeof(FolderDomainProcessor).FullName);
            declaration.Route.Execution.AliasPolicy.ShouldBe("exact");
            declaration.Route.Execution.Aliases.ShouldBe([legacy.CommandType]);
            declaration.DescriptorSchema.Fields.Select(static field => field.Name).Order(StringComparer.Ordinal)
                .ShouldBe(new[] { "credential-scope", "delegated-task-scope", "semantic-json", "semantic-payload-sha256", "target" });
            declaration.DescriptorSchema.Fields.ShouldAllBe(static field => field.Type == "string");

            SubmitCommand command = Command(legacy);
            IdempotencyCanonicalIntent intent = adapter.CreateIntent(new IdempotencyIntentCommand(command.CommandType,
                command.Tenant, command.Domain, command.AggregateId, command.Payload, command.Extensions));
            Dictionary<byte, byte[]> encoded = ReadFields((await fixture.Registry.ResolveAsync(command, TestContext.Current.CancellationToken).ConfigureAwait(true)).CanonicalIntent);
            Encoding.UTF8.GetString(encoded[6]).ShouldBe(intent.CanonicalTarget); // target:string
            Encoding.UTF8.GetString(encoded[7]).ShouldBe(Encoding.UTF8.GetString(intent.SemanticPayload)); // semantic-json:string
            using MemoryStream options = new(encoded[8]);
            ReadInt32(options).ShouldBe(1);
            ReadString(options).ShouldBe("semantic_payload_sha256");
            ReadString(options).ShouldBe(intent.SemanticOptions!["semantic_payload_sha256"]); // semantic-payload-sha256:string
            options.Position.ShouldBe(options.Length);
            Encoding.UTF8.GetString(encoded[9]).ShouldBe("folders-contract-spine-v2");
            Encoding.UTF8.GetString(encoded[10]).ShouldBe(intent.DelegatedTaskScope ?? string.Empty); // delegated-task-scope:string
            Encoding.UTF8.GetString(encoded[11]).ShouldBe(intent.CredentialScope ?? string.Empty); // credential-scope:string
        }
    }

    /// <summary>Proves absent and uncommitted generations cannot admit commands.</summary>
    [Fact]
    public async Task RegistryRefusesBeforeActivationAndWhilePreparedButNotCommitted()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        SubmitCommand command = Command(LegacyDescriptors()[0]);
        RouteCatalogUnavailableException unavailable = await Should.ThrowAsync<RouteCatalogUnavailableException>(
            () => fixture.Registry.ResolveAsync(command, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        unavailable.Message.ShouldContain("No complete catalog generation is serving");
        RouteCatalogActivation candidate = await fixture.CandidateAsync().ConfigureAwait(true);
        await fixture.Authority.PrepareAsync(candidate, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Registry.ResolveAsync(command, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        RouteCatalogActivationState state = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State;
        state.Committed.ShouldBeNull();
        state.Pending.ShouldNotBeNull();
        state.Leases.ShouldBeEmpty();
    }

    /// <summary>Releases the actual acquired catalog lease when the admitted adapter cannot parse its payload.</summary>
    [Fact]
    public async Task MalformedPayloadAfterAdmissionReleasesLeaseAndAllowsValidResolution()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        LegacyFoldersIntentDescriptor legacy = LegacyDescriptors().Single(static row => row.OperationId == "create-folder");
        SubmitCommand valid = Command(legacy);
        long priorLease = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.LastLease;

        await Should.ThrowAsync<JsonException>(() => fixture.Registry.ResolveAsync(
            valid with { Payload = "{"u8.ToArray() }, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        RouteCatalogActivationState afterFailure = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State;
        afterFailure.LastLease.ShouldBeGreaterThan(priorLease);
        afterFailure.Leases.ShouldBeEmpty();
        (await fixture.Registry.ResolveAsync(valid, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .CanonicalIntent.ShouldBe(Convert.FromBase64String(legacy.CanonicalIntent));
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Proves a missing command admission never becomes a serving catalog.</summary>
    [Fact]
    public async Task IncompleteAdmissionCannotBeSealedOrBecomeServing()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogValidationException>(() => fixture.CandidateAsync(envelope => envelope with
        {
            Idempotency = envelope.Idempotency.Skip(1).ToArray(),
        })).ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Registry.ResolveAsync(Command(LegacyDescriptors()[0]), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.Committed.ShouldBeNull();
    }

    /// <summary>Refuses requests outside the finite exact tenant, domain and discriminator scope.</summary>
    /// <param name="mismatch">The changed request coordinate.</param>
    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("discriminator")]
    public async Task CommandsOutsideTheExactCommittedScopeAreRefused(string mismatch)
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        SubmitCommand command = Command(LegacyDescriptors()[0]);
        command = mismatch switch
        {
            "tenant" => command with { Tenant = "tenant-b" },
            "domain" => command with { Domain = "other-domain" },
            _ => command with { CommandType = "CreateFolder" },
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Registry.ResolveAsync(command, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Refuses installed declaration drift before calling the canonical intent implementation.</summary>
    /// <param name="mismatch">The changed declaration facet.</param>
    [Theory]
    [InlineData("declaration")]
    [InlineData("policy")]
    [InlineData("schema")]
    [InlineData("version")]
    [InlineData("tier")]
    public async Task RegisteredPolicyDriftIsRefusedBeforeCreatingIntent(string mismatch)
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        IDeclaredIdempotencyIntentAdapter original = (IDeclaredIdempotencyIntentAdapter)fixture.Adapters[0];
        DeclaredFoldersIntentAdapter changed = new(original, Drift(original.Declaration, mismatch));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.RegistryWith(changed)
            .ResolveAsync(Command(LegacyDescriptors().Single(row => row.CommandType == original.CommandType)), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        changed.CreateIntentCalls.ShouldBe(0);
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Refuses candidates that disagree with independently captured installed policy.</summary>
    /// <param name="mismatch">The changed admission facet.</param>
    [Theory]
    [InlineData("policy")]
    [InlineData("schema")]
    [InlineData("tier")]
    [InlineData("key-policy")]
    public async Task ObserverRejectsCandidatePolicyThatDisagreesWithInstalledAdapters(string mismatch)
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        RouteCatalogActivation candidate = await fixture.CandidateAsync(envelope => envelope with
        {
            Idempotency = envelope.Idempotency.Select(entry =>
            {
                IdempotencyCatalogEntry changed = mismatch switch
                {
                    "policy" => entry with { PolicyVersion = "changed-policy-v2" },
                    "schema" => entry with { DescriptorSchema = entry.DescriptorSchema with { SchemaId = "changed-schema" } },
                    "tier" => entry with { RetentionTier = entry.RetentionTier == "commit" ? "mutation" : "commit" },
                    _ => entry with { SecretMapGeneration = "changed-key-map" },
                };
                changed = changed with { DescriptorDigest = RouteCatalogCodec.ComputeDescriptorDigest(changed.DescriptorSchema) };
                return changed with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(changed) };
            }).ToArray(),
        }).ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.PrepareAsync(candidate, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.CommitAsync(candidate.AttemptId, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        RouteCatalogActivationState state = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State;
        state.Committed.ShouldBeNull();
        state.Pending.ShouldBeNull();
        state.Ready.ShouldBeEmpty();
    }

    /// <summary>Keeps public discovery and full compiled schema-three enrollment independently unavailable.</summary>
    [Fact]
    public async Task SchemaTwoAdmissionDoesNotEnableSchemaThreePublicDiscovery()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        RoutingCatalogMetadataController controller = new(fixture.Authority);
        StatusCodeResult response = (await controller.Get(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeOfType<StatusCodeResult>();
        response.StatusCode.ShouldBe(503);
        // Static admission declarations alone do not enroll complete executable operation schemas.
        Should.Throw<RouteCatalogUnavailableException>(() => CompiledRouteCatalogDeclarations.ReadOperations(
            [typeof(FolderDomainProcessor).Assembly, typeof(FoldersCommandIntentDeclarations).Assembly]));
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Changes one declaration facet for registered-policy and hosted-gate refusal checks.</summary>
    /// <param name="declaration">The complete owner declaration.</param>
    /// <param name="mismatch">The facet to change.</param>
    internal static CommandIntentDeclaration Drift(CommandIntentDeclaration declaration, string mismatch) => mismatch switch
    {
        "declaration" => declaration with { Route = declaration.Route with { Execution = declaration.Route.Execution! with { HandlerType = "Changed.Handler" } } },
        "policy" => declaration with { PolicyVersion = "changed-policy-v2" },
        "schema" => declaration with { DescriptorSchema = declaration.DescriptorSchema with { Fields = [new("changed-field", "string")] } },
        "version" => declaration with { DescriptorSchema = declaration.DescriptorSchema with { Version = 2 } },
        "tier" => declaration with { RetentionTier = IdempotencyReplayRetentionTier.Commit },
        "subject" => declaration with { RequiresAuthenticatedSubject = true },
        _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
    };

    private static LegacyFoldersIntentDescriptor[] LegacyDescriptors() => JsonSerializer.Deserialize<LegacyFoldersIntentDescriptor[]>(
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-idempotency-descriptors.json")))!;

    private static SubmitCommand Command(LegacyFoldersIntentDescriptor row) => new("01K00000000000000000000000", "tenant-a",
        "folders", row.AggregateId, row.CommandType, Encoding.UTF8.GetBytes(row.Payload), "correlation-a", "user-a",
        new Dictionary<string, string> { ["taskId"] = "task-a" }, IdempotencyKey: "01K00000000000000000000000");

    private static Dictionary<byte, byte[]> ReadFields(byte[] encoded)
    {
        Dictionary<byte, byte[]> fields = [];
        using MemoryStream input = new(encoded);
        while (input.Position < input.Length)
        {
            byte tag = checked((byte)input.ReadByte());
            byte[] value = new byte[ReadInt32(input)];
            input.ReadExactly(value);
            fields.Add(tag, value);
        }

        return fields;
    }

    private static int ReadInt32(Stream input)
    {
        Span<byte> value = stackalloc byte[4];
        input.ReadExactly(value);
        return BinaryPrimitives.ReadInt32BigEndian(value);
    }

    private static string ReadString(Stream input)
    {
        byte[] value = new byte[ReadInt32(input)];
        input.ReadExactly(value);
        return Encoding.UTF8.GetString(value);
    }
}
