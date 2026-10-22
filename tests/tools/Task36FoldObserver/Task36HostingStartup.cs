using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.DomainService.Content;
using Dapr.Client;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(Task36FoldObserver.Task36HostingStartup))]

namespace Task36FoldObserver;

/// <summary>Observes real callback entry in the disposable qualification host.</summary>
public sealed class Task36HostingStartup : IHostingStartup
{
    /// <inheritdoc/>
    public void Configure(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureServices(services =>
        {
            if (Environment.GetEnvironmentVariable("FOLDERS_CONTENT_QUALIFICATION_CLOCK") is { Length: > 0 } clockPath)
                services.AddScoped<IAuthoritativeContentStore>(provider => new DaprAuthoritativeContentStore(
                    provider.GetRequiredService<DaprClient>(), provider.GetRequiredService<IContentAuthority>(),
                    provider.GetRequiredService<IOptions<AuthoritativeContentOptions>>(), new ContentQualificationClock(clockPath)));
            services.AddSingleton<IEnumerable<IAsyncDomainProjectionHandler>>(provider =>
                services.Where(d => d.ServiceType == typeof(IAsyncDomainProjectionHandler)).Select(d =>
                {
                    var handler = (IAsyncDomainProjectionHandler)(d.ImplementationInstance
                        ?? d.ImplementationFactory?.Invoke(provider)
                        ?? ActivatorUtilities.GetServiceOrCreateInstance(provider, d.ImplementationType!));
                    if (handler is not IAsyncDomainSharedProjectionStreamingRebuildHandler streaming
                        || handler.ProjectionType != "operational-status") return handler;
                    var proxy = System.Reflection.DispatchProxy.Create<IAsyncDomainSharedProjectionStreamingRebuildHandler, ObservedProjectionHandler>();
                    ((ObservedProjectionHandler)(object)proxy).Inner = streaming;
                    ObservedProjectionHandler.Record("registered", null, null);
                    return (IAsyncDomainProjectionHandler)proxy;
                }).ToArray());
        });
    }
}
