using Hexalith.EventStore.DomainService.Content;
using Dapr.Client;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddHostedService(_ => new FoldDiagnosticObserver(
                Environment.GetEnvironmentVariable("FOLDERS_TASK36_FOLD_LOG")
                    ?? throw new InvalidOperationException("Qualification fold sink is required.")));
        });
    }
}
