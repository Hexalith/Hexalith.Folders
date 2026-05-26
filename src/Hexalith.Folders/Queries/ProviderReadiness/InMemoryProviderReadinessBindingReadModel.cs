using System.Collections.Concurrent;

using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed class InMemoryProviderReadinessBindingReadModel : IProviderReadinessBindingReader
{
    private readonly ConcurrentDictionary<string, OrganizationProviderBinding> _bindings = new(StringComparer.Ordinal);

    public Task<OrganizationProviderBinding?> GetAsync(
        ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _bindings.TryGetValue(Key(request.ManagedTenantId, request.ProviderBindingRef), out OrganizationProviderBinding? binding);
        return Task.FromResult(binding);
    }

    public void Save(OrganizationProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings[Key(binding.ManagedTenantId, binding.ProviderBindingRef)] = binding;
    }

    private static string Key(string managedTenantId, string providerBindingRef)
        => $"{managedTenantId}|{providerBindingRef}";
}
