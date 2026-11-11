using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Task36FoldObserver;

/// <summary>Captures already-started singleton instances; never admits a new factory or transient resolution.</summary>
internal sealed class ExistingHostedServiceObservation(
    IServiceProvider provider, IServiceCollection services, ServiceDescriptor[] initialRegistrations)
{
    private IHostedService[]? _instances;
    private int _attempted;

    /// <summary>Captures once after startup under canonical provider, singleton and stable inventory gates.</summary>
    internal object Capture(bool applicationStarted)
    {
        if (!applicationStarted) return Unavailable("host_not_started");
        if (Interlocked.Exchange(ref _attempted, 1) != 0) return Unavailable("already_attempted");
        try
        {
            Type providerType = provider.GetType();
            if (providerType.Assembly != typeof(ServiceProvider).Assembly
                || providerType.FullName is not ("Microsoft.Extensions.DependencyInjection.ServiceProvider"
                    or "Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope"))
                return Unavailable("provider_unsupported");
            if (services.Any(descriptor => !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(IEnumerable<IHostedService>)))
                return Unavailable("custom_enumerable");
            ServiceDescriptor[] current = services.Where(descriptor => !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(IHostedService)).ToArray();
            if (current.Length != initialRegistrations.Length
                || current.Where((descriptor, index) => !ReferenceEquals(descriptor, initialRegistrations[index])).Any())
                return Unavailable("registration_changed");
            if (current.Any(descriptor => descriptor.Lifetime != ServiceLifetime.Singleton))
                return Unavailable("non_singleton");
            if (current.Length > 128) return Unavailable("inventory_too_large");

            // Host.StartAsync has already resolved this exact default-DI enumerable before
            // ApplicationStarted. Every element is a cached singleton under the gates above.
            IHostedService[] existing = provider.GetServices<IHostedService>().ToArray();
            if (existing.Length != current.Length) return Unavailable("inventory_mismatch");
            object[] metadata = existing.Select((service, ordinal) => (object)new
            {
                ordinal,
                type = service.GetType().FullName,
                assembly = service.GetType().Assembly.GetName().Name,
                backgroundService = service is BackgroundService,
                hostedLifecycleService = service is IHostedLifecycleService,
            }).ToArray();
            Volatile.Write(ref _instances, existing);
            return new { status = "available", reason = "existing_singletons", services = metadata };
        }
        catch { return Unavailable("capture_unavailable"); }
    }

    /// <summary>Reads public task flags only; never reads exception, async state or application objects.</summary>
    internal object[]? States()
    {
        IHostedService[]? existing = Volatile.Read(ref _instances);
        return existing?.Select((service, ordinal) => State(service, ordinal)).ToArray();
    }

    private static object State(IHostedService service, int ordinal)
    {
        if (service is not BackgroundService background)
            return new { ordinal, state = "not_background" };
        try
        {
            Task? task = background.ExecuteTask;
            return new
            {
                ordinal,
                state = task is null ? "not_started" : "observed",
                taskStatus = task?.Status,
                completed = task?.IsCompleted,
                canceled = task?.IsCanceled,
                faulted = task?.IsFaulted,
            };
        }
        catch { return new { ordinal, state = "task_unavailable" }; }
    }

    private static object Unavailable(string reason) => new { status = "unavailable", reason };
}
