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
    private readonly RouteCatalogEnvelope _catalog;

    /// <summary>Builds a component catalog for the actual operational projection and an optional declared fixture query.</summary>
    /// <param name="additionalQuery">The optional compiled query declaration matched to a registered handler.</param>
    public FoldersProjectionCatalogFixture(RouteContractDeclaration? additionalQuery = null)
    {
        RouteContractDeclaration[] declarations = additionalQuery is null
            ? [.. FoldersProjectionCatalogDeclarations.Routes]
            : [.. FoldersProjectionCatalogDeclarations.Routes, additionalQuery];
        _catalog = DevelopmentRouteCatalogBuilder.Build(new(1, false, declarations,
            declarations.Select(declaration => new RouteCompositionBinding(declaration.RouteId, "folders",
                declaration.Kind == "query" ? "query" : "project/v2", "v1", false, "*")).ToArray(), []));
    }

    /// <summary>Registers the explicit Development trust fixture and serialized component authority store.</summary>
    /// <param name="builder">The production-composed Development test builder.</param>
    public void Configure(WebApplicationBuilder builder)
    {
        // The explicit SDK Development trust fixture is used only in this isolated component host.
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("The explicit SDK catalog fixture requires a Development host.");
        builder.Services.AddSingleton<IRouteCatalogStateStore>(_store);
        builder.Services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(_settings));
        builder.Services.AddEventStoreRouteCatalogObservation((_, _) => Task.FromResult(Observe()));
    }

    /// <summary>Observes actual registrations and commits a generation through the SDK lifecycle.</summary>
    /// <param name="app">The mapped production-composed component host.</param>
    /// <param name="mismatchTarget">Whether to propose an incorrect target for the negative control.</param>
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

    /// <summary>Binds a test invocation to this committed generation without replacing the receiver guard.</summary>
    /// <param name="request">The outgoing component request.</param>
    public void Bind(HttpRequestMessage request)
    {
        request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, RouteCatalogCodec.ComputeRootDigest(_catalog));
        request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
    }

    private RouteCatalogHostObservation Observe()
    {
        using IServiceScope scope = _app!.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        Type[] actualHandlers = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>().Select(handler => handler.GetType())
            .Concat(scope.ServiceProvider.GetServices<IDomainQueryHandler>().Select(handler => handler.GetType())).Distinct().ToArray();
        var declarations = CompiledRouteCatalogDeclarations.Read(actualHandlers.Select(type => type.Assembly).Distinct().ToArray())
            .Where(declaration => actualHandlers.Any(type => type.FullName == declaration.Execution!.HandlerType)).ToArray();
        var endpoints = ((IEndpointRouteBuilder)_app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        List<RouteCatalogEntry> targets = [];
        foreach (var declaration in declarations)
        {
            string actualMethod = declaration.Kind == "query" ? "/query" : "/project/v2";
            var endpoint = endpoints.Single(item => item.RoutePattern.RawText == actualMethod);
            var route = EventStoreDomainServiceRoutes.Operational.Single(item => item.Route == endpoint.RoutePattern.RawText);
            if (!endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(item => item.Policy == route.Policy)
                || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"]) != true)
                throw new InvalidOperationException("The loaded receiver endpoint does not match the fixture binding.");
            targets.Add(new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                identity.AppId, endpoint.RoutePattern.RawText!.TrimStart('/'), identity.ServiceVersion, declaration.CredentialKind,
                route.Operation, false, "*"));
        }
        return new("folders", identity.AppId, declarations,
            targets, ["folders"],
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
