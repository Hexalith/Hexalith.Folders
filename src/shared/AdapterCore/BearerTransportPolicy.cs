using System.Net;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Limits bearer destinations to HTTPS or HTTP on a literal loopback host.</summary>
internal static class BearerTransportPolicy
{
    /// <summary>Gets the constant metadata-only transport rejection diagnostic.</summary>
    public const string RejectionMessage = "Bearer transport requires HTTPS or HTTP loopback.";

    /// <summary>Determines whether the absolute destination permits bearer transport, without DNS lookup.</summary>
    public static bool IsAllowed(Uri? destination)
    {
        if (destination is null || !destination.IsAbsoluteUri)
        {
            return false;
        }

        if (destination.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return destination.Scheme == Uri.UriSchemeHttp
            && (string.Equals(destination.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(destination.IdnHost, out IPAddress? address) && IPAddress.IsLoopback(address)));
    }

    /// <summary>Rejects unsafe destinations before credentials are acquired or attached.</summary>
    public static void Validate(Uri? destination)
    {
        if (!IsAllowed(destination))
        {
            throw new UnsafeBearerTransportException();
        }
    }

    /// <summary>Creates the adapters' primary transport with redirects disabled and HTTP loopback proxy bypass.</summary>
    public static HttpClientHandler CreatePrimaryHandler() => new()
    {
        AllowAutoRedirect = false,
        Proxy = new LoopbackBypassProxy(HttpClient.DefaultProxy),
    };
}
