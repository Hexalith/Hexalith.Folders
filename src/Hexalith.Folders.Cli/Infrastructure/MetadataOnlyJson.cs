namespace Hexalith.Folders.Cli.Infrastructure;

/// <summary>Preserves the adapter's metadata-only serializer facade.</summary>
internal static class MetadataOnlyJson
{
    /// <summary>Serializes a value with the shared metadata filter and wire formatting.</summary>
    public static string Serialize(object value) => AdapterCore.MetadataOnlyJson.Serialize(value);
}
