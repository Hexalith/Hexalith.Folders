using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Removes content properties using their case-insensitive wire names.</summary>
internal sealed class MetadataOnlyContractResolver : DefaultContractResolver
{
    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "contentBytes",
        "inlineContent",
        "streamDescriptor",
    };

    /// <summary>Reports whether a wire property can carry content.</summary>
    internal static bool IsContentProperty(string name) => ForbiddenPropertyNames.Contains(name);

    /// <inheritdoc/>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        if (property.PropertyName is { } name && IsContentProperty(name))
        {
            property.ShouldSerialize = _ => false;
            property.Ignored = true;
        }

        return property;
    }
}
