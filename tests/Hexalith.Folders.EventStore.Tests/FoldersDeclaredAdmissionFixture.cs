using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Server;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>
/// Finite schema-two consumer admission proof using production SDK DI, real observers and trust validation.
/// Consumer adapters and available key policy are captured through SDK DI. Receiver declarations, targets,
/// topology and authentication facts are synthetic fixture inputs, not a capture of the production server.
/// This fixture supplies no full server certification, public/agent eligibility, Dapr process or production activation.
/// </summary>
internal sealed class FoldersDeclaredAdmissionFixture : IDisposable
{
    private const string ConsumerAppId = "eventstore";
    private const string ServerAppId = "folders-server";
    private readonly ServiceProvider _consumer;
    private readonly ServiceProvider _server;
    private readonly RouteCatalogHostOptions _consumerOptions = new() { Profile = "Development", DevelopmentFixture = true };
    private readonly RouteCatalogHostOptions _serverOptions = new() { Profile = "Development", DevelopmentFixture = true };
    private readonly IReadOnlyList<RouteContractDeclaration> _syntheticReceiverDeclarations;
    private readonly IReadOnlyList<RouteCatalogEntry> _syntheticReceiverTargets;

    /// <summary>Gets this fixture's isolated durable authority cell.</summary>
    public FoldersRouteCatalogStateStore Store { get; } = new();

    /// <summary>Gets the real authority resolved through production SDK registration.</summary>
    public IRouteCatalogAuthority Authority => _consumer.GetRequiredService<IRouteCatalogAuthority>();

    /// <summary>Gets the registry resolved through production SDK registration.</summary>
    public IIdempotencyIntentAdapterRegistry Registry => _consumer.GetRequiredService<IIdempotencyIntentAdapterRegistry>();

    /// <summary>Gets the installed production Folders adapter instances.</summary>
    public IReadOnlyList<IIdempotencyIntentAdapter> Adapters => _consumer.GetServices<IIdempotencyIntentAdapter>().ToArray();

    private FoldersDeclaredAdmissionFixture()
    {
        // These finite receiver facts model the existing /process binding; no server host is captured here.
        _syntheticReceiverDeclarations = FoldersCommandIntentDeclarations.All.Values.Select(static declaration => declaration.Route).ToArray();
        _syntheticReceiverTargets = _syntheticReceiverDeclarations.Select(static declaration => new RouteCatalogEntry(
            declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), ServerAppId,
            FoldersServerModule.ProcessRoute.TrimStart('/'), declaration.ContractVersion, declaration.CredentialKind,
            declaration.Operation, false, "tenant-a")).ToArray();

        ServiceCollection consumer = BaseServices(_consumerOptions);
        // The same production registration as Program supplies authority, encoder and registry.
        consumer.AddEventStoreServer(new ConfigurationBuilder().Build());
        consumer.AddFoldersIdempotencyIntentAdapters();
        consumer.AddSingleton<IOptions<IdempotencyAdmissionOptions>>(Options.Create(new IdempotencyAdmissionOptions
        {
            Enabled = true,
            ActiveDigestKeyVersion = "folders-test-v1",
            DigestKeySource = IdempotencyDigestKeySource.Configuration,
            DigestKeySecretStoreName = "folders-test-secretstore",
            DigestKeySecretName = "folders-test-digest-ring",
            DigestKeySecretGeneration = "folders-test-v1",
            DigestKeys = new(StringComparer.Ordinal) { ["folders-test-v1"] = Convert.ToBase64String(new byte[32]) },
        }));
        consumer.AddEventStoreRouteCatalogObservation(CaptureConsumerAsync);
        _consumer = consumer.BuildServiceProvider();

        ServiceCollection server = BaseServices(_serverOptions);
        server.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        server.AddEventStoreRouteCatalogObservation((_, _) => Task.FromResult(SyntheticReceiverObservation()));
        _server = server.BuildServiceProvider();
    }

    /// <summary>Captures the installed consumer identity and constructs the synthetic receiver identity before a proposal.</summary>
    public static async Task<FoldersDeclaredAdmissionFixture> CreateAsync()
    {
        FoldersDeclaredAdmissionFixture fixture = new();
        fixture._consumerOptions.Identity = ObservedRouteCatalogHost.Identity(
            await fixture.CaptureConsumerAsync(fixture._consumer, default).ConfigureAwait(false));
        fixture._serverOptions.Identity = ObservedRouteCatalogHost.Identity(fixture.SyntheticReceiverObservation());
        return fixture;
    }

    /// <summary>Seals owner metadata and synthetic receiver bindings with independently observed installed consumer policy.</summary>
    /// <param name="change">An optional deliberate candidate drift for refusal tests.</param>
    public async Task<RouteCatalogActivation> CandidateAsync(Func<RouteCatalogEnvelope, RouteCatalogEnvelope>? change = null)
    {
        // Read installed adapters and available keys independently, before constructing a candidate.
        IReadOnlyList<IdempotencyCatalogEntry> installed =
            await RouteCatalogAdmissionObservation.CaptureAsync(_consumer, ConsumerAppId).ConfigureAwait(false);
        RouteCatalogEnvelope envelope = new(2, 1, "Development", false, _syntheticReceiverDeclarations, _syntheticReceiverTargets, installed, "", "");
        envelope = RouteCatalogCodec.Seal(change?.Invoke(envelope) ?? envelope);
        RouteCatalogActivation candidate = new("folders-finite-attempt", RouteCatalogCodec.Encode(envelope),
            RouteCatalogCodec.ComputeRootDigest(envelope), null,
            [_consumerOptions.Identity!, _serverOptions.Identity!], "Development-fixture", "");
        return candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
    }

    /// <summary>Runs both real SDK authorities through activation using observed consumer and synthetic receiver facts.</summary>
    /// <param name="candidate">The candidate, or null to build the unchanged owner policy.</param>
    public async Task ActivateAsync(RouteCatalogActivation? candidate = null)
    {
        candidate ??= await CandidateAsync().ConfigureAwait(false);
        await Authority.PrepareAsync(candidate).ConfigureAwait(false);
        await Authority.ReadyAsync(candidate.AttemptId).ConfigureAwait(false);
        await _server.GetRequiredService<IRouteCatalogAuthority>().ReadyAsync(candidate.AttemptId).ConfigureAwait(false);
        await Authority.CommitAsync(candidate.AttemptId).ConfigureAwait(false);
    }

    /// <summary>Uses the public SDK constructor to test a deliberately changed installed registration.</summary>
    /// <param name="replacement">The changed adapter.</param>
    public IIdempotencyIntentAdapterRegistry RegistryWith(IIdempotencyIntentAdapter replacement)
    {
        // Keep the public production constructor/authority for deliberately drifted registrations.
        return new IdempotencyIntentAdapterRegistry(Authority,
            Adapters.Select(adapter => adapter.CommandType == replacement.CommandType ? replacement : adapter),
            _consumer.GetRequiredService<CanonicalIdempotencyIntentEncoder>());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _consumer.Dispose();
        _server.Dispose();
    }

    private ServiceCollection BaseServices(RouteCatalogHostOptions options)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
        {
            EnvironmentName = Environments.Development,
        });
        services.AddSingleton<IRouteCatalogStateStore>(Store);
        services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(options));
        return services;
    }

    private async Task<RouteCatalogHostObservation> CaptureConsumerAsync(IServiceProvider services, CancellationToken token)
        => Observation(ConsumerAppId, [], [], await RouteCatalogAdmissionObservation.CaptureAsync(services, ConsumerAppId, token).ConfigureAwait(false));

    private RouteCatalogHostObservation SyntheticReceiverObservation() => Observation(ServerAppId, _syntheticReceiverDeclarations, _syntheticReceiverTargets, []);

    private RouteCatalogHostObservation Observation(string appId, IReadOnlyList<RouteContractDeclaration> declarations,
        IReadOnlyList<RouteCatalogEntry> targets, IReadOnlyList<IdempotencyCatalogEntry> admission)
        => new(appId, appId, declarations, targets, [ConsumerAppId, ServerAppId],
            [JsonSerializer.SerializeToUtf8Bytes(new
            {
                kind = "Component",
                metadata = new { name = Store.Coordinates.StateStore },
                spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } },
                scopes = new[] { ConsumerAppId, ServerAppId },
            }), JsonSerializer.SerializeToUtf8Bytes(new
            {
                spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } },
            })],
            JsonSerializer.SerializeToUtf8Bytes(new { environment = "Development", trust = "finite-sdk-fixture" }),
            admission, [], [], Store.Coordinates);
}
