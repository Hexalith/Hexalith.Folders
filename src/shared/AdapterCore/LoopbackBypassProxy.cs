using System.Net;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Bypasses proxies for permitted HTTP loopback traffic while delegating other proxy behavior.</summary>
internal sealed class LoopbackBypassProxy(IWebProxy inner) : IWebProxy
{
    /// <inheritdoc/>
    public ICredentials? Credentials
    {
        get => inner.Credentials;
        set => inner.Credentials = value;
    }

    /// <inheritdoc/>
    public Uri? GetProxy(Uri destination)
        => IsHttpLoopback(destination) ? destination : inner.GetProxy(destination);

    /// <inheritdoc/>
    public bool IsBypassed(Uri host) => IsHttpLoopback(host) || inner.IsBypassed(host);

    private static bool IsHttpLoopback(Uri destination)
        => destination.IsAbsoluteUri && destination.Scheme == Uri.UriSchemeHttp && BearerTransportPolicy.IsAllowed(destination);
}
