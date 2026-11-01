using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>An isolated serialized authority cell with real compare/exchange semantics.</summary>
internal sealed class FoldersRouteCatalogStateStore : IRouteCatalogStateStore
{
    private readonly object _gate = new();
    private byte[] _state = JsonSerializer.SerializeToUtf8Bytes(RouteCatalogActivationState.Empty);
    private long _version;

    /// <inheritdoc/>
    public RouteCatalogStateCoordinates Coordinates { get; } =
        new("folders-finite-route-catalog", "folders-finite-authority-v1", "http://127.0.0.1:3500/");

    /// <inheritdoc/>
    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult((JsonSerializer.Deserialize<RouteCatalogActivationState>(_state)!,
                _version.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <inheritdoc/>
    public Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (etag != _version.ToString(CultureInfo.InvariantCulture))
            {
                return Task.FromResult(false);
            }

            _state = JsonSerializer.SerializeToUtf8Bytes(state);
            _version++;
            return Task.FromResult(true);
        }
    }
}
