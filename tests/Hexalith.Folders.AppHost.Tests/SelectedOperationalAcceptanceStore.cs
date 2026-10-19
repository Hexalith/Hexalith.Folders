using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Inspects SDK-selected task-owned values and locates their physical keys for explicit fault injection.</summary>
internal sealed class SelectedOperationalAcceptanceStore(IReadModelStore inner, string tenantId) : IReadModelStore, IReadModelConditionalEraser
{
    private readonly SharedProjectionScope _scope = OperationalStatusGeneration.Scope(tenantId);

    public async Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (!Operational(key)) return await inner.GetAsync<T>(storeName, key, cancellationToken).ConfigureAwait(false);
        return await inner.GetAsync<T>(storeName, await PhysicalAsync(key, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }
    public async Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => await inner.SaveAsync(storeName, await PhysicalAsync(key, cancellationToken).ConfigureAwait(false), value, cancellationToken).ConfigureAwait(false);
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => await inner.TrySaveAsync(storeName, await PhysicalAsync(key, cancellationToken).ConfigureAwait(false), value, etag, cancellationToken).ConfigureAwait(false);
    public async Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => await ((IReadModelConditionalEraser)inner).TryReadEtagAsync(storeName, await PhysicalAsync(key, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    public async Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => await ((IReadModelConditionalEraser)inner).TryEraseAsync(storeName, await PhysicalAsync(key, cancellationToken).ConfigureAwait(false), etag, cancellationToken).ConfigureAwait(false);

    internal Task<string> PhysicalKeyAsync(string key, CancellationToken cancellationToken)
        => PhysicalAsync(key, cancellationToken);

    private async Task<string> PhysicalAsync(string key, CancellationToken cancellationToken)
    {
        if (!Operational(key)) return key;
        var state = await inner.GetAsync<JsonObject>(_scope.StoreName, "shared-epoch:" + _scope.ComputeHash(), cancellationToken).ConfigureAwait(false);
        long generation = state.Value?["activeGeneration"]?.GetValue<long>() ?? 0;
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var pointer = await inner.GetAsync<JsonObject>(_scope.StoreName, "shared-cow-pointer:" + _scope.ComputeHash() + ":" + digest, cancellationToken).ConfigureAwait(false);
        if (pointer.Value is { } selected)
        {
            long latest = selected["latestGeneration"]!.GetValue<long>();
            generation = latest <= generation ? latest : selected["previousGeneration"]!.GetValue<long>();
        }
        return "shared-generation:" + _scope.ComputeHash() + ":" + generation + ":" + digest;
    }
    private static bool Operational(string key) => key.StartsWith("projection:folders:operational-status:", StringComparison.Ordinal);
}
