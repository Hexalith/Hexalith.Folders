using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>Maps a public provider-binding reference into its reserved EventStore actor namespace.</summary>
public static class ProviderBindingActorId
{
    /// <summary>The prefix reserved from folder and tenant actor identifiers.</summary>
    public const string Prefix = "provider-binding-";

    /// <summary>Returns the stable internal actor ID for a public binding reference.</summary>
    public static string Create(string providerBindingRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerBindingRef);
        return Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(providerBindingRef))).ToLowerInvariant();
    }

    /// <summary>Reports whether an identifier belongs to the internal binding actor namespace.</summary>
    public static bool IsReserved(string? identifier)
        => identifier?.StartsWith(Prefix, StringComparison.Ordinal) == true;
}
