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

/// <summary>Uses the real committed SDK authority and independently captured installed adapters in an isolated component catalog.</summary>
internal sealed class AdmissionCatalogFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    private AdmissionCatalogFixture(ServiceProvider provider) => _provider = provider;

    public IdempotencyIntentAdapterRegistry Registry => new(_provider.GetRequiredService<IRouteCatalogAuthority>(),
        _provider.GetServices<IIdempotencyIntentAdapter>(), new CanonicalIdempotencyIntentEncoder());

    public static async Task<AdmissionCatalogFixture> CreateAsync()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = Environments.Development });
        services.AddFoldersIdempotencyIntentAdapters();
        var store = new AdmissionCatalogStateStore();
        var options = new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true };
        services.AddSingleton<IRouteCatalogStateStore>(store);
        services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(options));
        services.AddSingleton<IOptions<IdempotencyAdmissionOptions>>(Options.Create(new IdempotencyAdmissionOptions
        {
            Enabled = true,
            ActiveDigestKeyVersion = "test1",
            DigestKeySource = IdempotencyDigestKeySource.Configuration,
            DigestKeySecretStoreName = "test-secrets",
            DigestKeySecretName = "test-ring",
            DigestKeySecretGeneration = "test1",
            DigestKeys = new() { ["test1"] = Convert.ToBase64String(new byte[32]) },
        }));
        services.AddSingleton<IIdempotencyDigestKeyProvider, ConfigurationIdempotencyDigestKeyProvider>();
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        RouteCatalogHostObservation? observation = null;
        services.AddEventStoreRouteCatalogObservation((_, _) => Task.FromResult(observation!));
        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            var declarations = FoldersRuntimeCatalogDeclarations.Commands.Select(command => command.Route).ToArray();
            var operations = FoldersRuntimeCatalogDeclarations.Operations.Where(operation => declarations.Any(route => route.RouteId == operation.RouteId)).ToArray();
            var targets = declarations.Select(route => new RouteCatalogEntry(route.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(route),
                "folders", "process", route.ContractVersion, route.CredentialKind, route.Operation, false, "*",
                operations.Single(operation => operation.RouteId == route.RouteId).Digest)).ToArray();
            var admission = await RouteCatalogAdmissionObservation.CaptureAsync(provider, "folders").ConfigureAwait(false);
            observation = new("folders", "folders", declarations, targets, ["folders"],
                [JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = store.Coordinates.StateStore },
                    spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { "folders" } }),
                 JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } })],
                "isolated-registry-component"u8.ToArray(), admission, [], [], store.Coordinates, Operations: operations);
            options.Identity = ObservedRouteCatalogHost.Identity(observation);
            var envelope = RouteCatalogCodec.Seal(new(3, 1, "Development", false, declarations, targets, admission,
                string.Empty, string.Empty, operations, []));
            var candidate = new RouteCatalogActivation("folders-admission-fixture", RouteCatalogCodec.Encode(envelope),
                RouteCatalogCodec.ComputeRootDigest(envelope), null, [options.Identity], "Development-fixture", "", [], []);
            candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
            var authority = provider.GetRequiredService<IRouteCatalogAuthority>();
            await authority.PrepareAsync(candidate).ConfigureAwait(false);
            await authority.ReadyAsync(candidate.AttemptId).ConfigureAwait(false);
            await authority.CommitAsync(candidate.AttemptId).ConfigureAwait(false);
            return new(provider);
        }
        catch { provider.Dispose(); throw; }
    }

    public void Dispose() => _provider.Dispose();
}
