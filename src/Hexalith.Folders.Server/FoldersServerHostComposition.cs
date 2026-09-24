using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server;

/// <summary>Composes the Folders server host: its services and its request pipeline.</summary>
public static class FoldersServerHostComposition
{
    /// <summary>Registers the Folders server host services and validates the API routing mode.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The validated API routing mode.</returns>
    /// <exception cref="InvalidOperationException">The API routing mode setting is empty or unknown.</exception>
    public static FoldersApiRoutingMode AddFoldersServerHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Validate before any registration so an invalid mode fails host startup.
        FoldersApiRoutingMode mode = FoldersApiRouting.ResolveMode(builder.Configuration);

        builder.AddServiceDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddAuthorization();
        builder.Services.AddFoldersProductionAuthentication(builder.Configuration, builder.Environment);

        // Production safety gate: AddInMemoryFolderRepository registers a dictionary-backed
        // repository that loses every event on process restart. It is safe for dev/staging hosts
        // and integration tests; it is not safe for production. Gate the call on environment so a
        // production build that forgets to register an EventStore-backed repository fails at
        // startup (via FolderRepositoryStartupAssertion) rather than silently running on a
        // dictionary. Remove this gate and the assertion together when Epic 7 wires the
        // EventStore-backed repository as the production default.
        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            // TODO Epic 7: replace AddInMemoryFolderRepository with an EventStore-backed
            // repository for all environments.
            builder.Services.AddInMemoryFolderRepository();
        }

        builder.Services.AddHostedService<FolderRepositoryStartupAssertion>();

        // Validate scoped service lifetimes at build time so a missing or misconfigured DI
        // registration (e.g., a forgotten ILayeredFolderAuthorizationResultAccessor or a captive
        // dependency) fails at host startup rather than on the first request.
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        return mode;
    }

    /// <summary>Composes the Folders server request pipeline for the configured API routing mode.</summary>
    /// <param name="app">The built web application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="InvalidOperationException">The API routing mode setting is empty or unknown.</exception>
    public static WebApplication UseFoldersServerPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        FoldersApiRoutingMode mode = FoldersApiRouting.ResolveMode(app.Configuration);

        app.UseCloudEvents();
        app.UseAuthentication();
        if (mode != FoldersApiRoutingMode.V1Only)
        {
            // The seam rewrites /api/v2 requests onto their v1 handlers, so it must run before routing
            // selects an endpoint. V1Only keeps the implicit routing position unchanged.
            app.UsePd10V2CandidateCompatibilitySeam();
            app.UseRouting();
            if (mode == FoldersApiRoutingMode.V2Only)
            {
                app.UsePd10HistoricalRouteRetirement();
            }
        }

        app.UseAuthorization();
        app.MapSubscribeHandler();
        app.MapFoldersServerEndpoints();

        return app;
    }
}
