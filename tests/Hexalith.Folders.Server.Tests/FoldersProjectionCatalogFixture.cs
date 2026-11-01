using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using System.Text.Json;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Activates the actual named projection through the SDK authority, observer and receiver guard.</summary>
internal sealed class FoldersProjectionCatalogFixture
{
    private readonly FoldersCatalogStateStore _store = new();
    private readonly RouteCatalogHostOptions _settings = new() { Profile = "Development", DevelopmentFixture = true };
    private WebApplication? _app;
    private readonly RouteCatalogEnvelope _catalog = DevelopmentRouteCatalogBuilder.Build(new(1, false,
        FoldersProjectionCatalogDeclarations.Routes,
        [new("folders-operational-status", "folders", "project/v2", "v1", false, "*")], []));

    public void Configure(WebApplicationBuilder builder)
    {
        // The explicit SDK Development trust fixture is used only in this isolated component host.
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("The explicit SDK catalog fixture requires a Development host.");
        builder.Services.AddSingleton<IRouteCatalogStateStore>(_store);
        builder.Services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(_settings));
        builder.Services.AddEventStoreRouteCatalogObservation((_, _) => Task.FromResult(Observe()));
    }

    public async Task ActivateAsync(WebApplication app, bool mismatchTarget = false)
    {
        _app = app;
        RouteCatalogHostObservation observed = Observe();
        _settings.Identity = ObservedRouteCatalogHost.Identity(observed);
        RouteCatalogEnvelope proposed = mismatchTarget
            ? DevelopmentRouteCatalogBuilder.Build(new(1, false, FoldersProjectionCatalogDeclarations.Routes,
                [new("folders-operational-status", "folders", "project", "v1", false, "*")], []))
            : _catalog;
        var candidate = new RouteCatalogActivation("folders-projection-fixture", RouteCatalogCodec.Encode(proposed),
            RouteCatalogCodec.ComputeRootDigest(proposed), null, [_settings.Identity], "Development-fixture", "",
            observed.AuxiliaryOperations!.Where(operation => FoldersProjectionCatalogDeclarations.Routes.Any(declaration => declaration.Domain == operation.Domain)).ToArray(),
            observed.ProjectionCapabilities);
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        var authority = app.Services.GetRequiredService<IRouteCatalogAuthority>();
        await authority.PrepareAsync(candidate).ConfigureAwait(false);
        await authority.ReadyAsync(candidate.AttemptId).ConfigureAwait(false);
        await authority.CommitAsync(candidate.AttemptId).ConfigureAwait(false);
    }

    public void Bind(HttpRequestMessage request)
    {
        request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, RouteCatalogCodec.ComputeRootDigest(_catalog));
        request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
    }

    private RouteCatalogHostObservation Observe()
    {
        using IServiceScope scope = _app!.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Single(item => item.Domain == "folders" && item.ProjectionType == OperationalStatusKeys.ProjectionType);
        var declarations = CompiledRouteCatalogDeclarations.Read([handler.GetType().Assembly]);
        var declaration = declarations.Single(item => item.Execution!.HandlerType == handler.GetType().FullName);
        var endpoint = ((IEndpointRouteBuilder)_app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText == "/project/v2");
        var route = EventStoreDomainServiceRoutes.Operational.Single(item => item.Route == endpoint.RoutePattern.RawText);
        if (!endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(item => item.Policy == route.Policy)
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"]) != true)
            throw new InvalidOperationException("The loaded named projection endpoint does not match the fixture binding.");
        RouteCatalogEntry target = new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
            identity.AppId, endpoint.RoutePattern.RawText!.TrimStart('/'), identity.ServiceVersion, declaration.CredentialKind,
            route.Operation, false, "*");
        return new("folders", identity.AppId, declarations,
            [target], ["folders"],
            [JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = _store.Coordinates.StateStore },
                spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { "folders" } }),
             JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } })],
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                environment = _app.Environment.EnvironmentName,
                issuer = _app.Configuration["Authentication:JwtBearer:Issuer"],
                audience = _app.Configuration["Authentication:JwtBearer:Audience"],
                workload = scope.ServiceProvider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value.AppId,
            }), [], RouteCatalogDomainObservation.Capabilities(scope.ServiceProvider),
            RouteCatalogDomainObservation.Auxiliary(scope.ServiceProvider, ((IEndpointRouteBuilder)_app).DataSources.SelectMany(source => source.Endpoints)),
            _store.Coordinates);
    }
}
