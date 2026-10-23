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

    /// <inheritdoc/>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        if (property.PropertyName is not null && ForbiddenPropertyNames.Contains(property.PropertyName))
        {
            property.ShouldSerialize = _ => false;
            property.Ignored = true;
        }

        return property;
    }
}
