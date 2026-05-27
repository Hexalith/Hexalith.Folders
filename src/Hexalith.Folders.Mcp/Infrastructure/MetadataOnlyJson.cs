using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hexalith.Folders.Mcp.Infrastructure;

/// <summary>
/// Serializes SDK result, success-envelope, and failure shapes to JSON while enforcing the metadata-only
/// invariant centrally: content-bearing properties (notably <c>contentBytes</c> on
/// <c>FileRangeReadResult</c> and the inline/streamed upload transport fields) are never emitted to any
/// output channel, at any nesting depth. Filtering is centralized here rather than duplicated per tool
/// (project-context: keep sensitive-metadata filtering centralized), so even an authorized
/// <c>read-file-range</c> result cannot leak file bytes.
/// </summary>
/// <remarks>
/// Uses Newtonsoft (matching the generated SDK serializer) so the explicit <c>[JsonProperty]</c> names on
/// SDK types are honored and the forbidden-property filter keys off those exact wire names. CamelCase
/// matches the SDK/wire <c>ProblemDetails</c> shape (category, code, correlationId, …) — Epic 5 hinges on
/// cross-adapter wire parity (Story 5.2 reviewer finding).
/// </remarks>
internal static class MetadataOnlyJson
{
    /// <summary>
    /// Wire property names that may carry raw or base64-encoded file content or transport descriptors.
    /// These are dropped from every serialized payload regardless of nesting depth.
    /// </summary>
    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "contentBytes",
        "inlineContent",
        "streamDescriptor",
    };

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new MetadataOnlyContractResolver(ForbiddenPropertyNames)
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };

    /// <summary>
    /// Serializes <paramref name="value"/> to indented camelCase JSON with content-bearing fields removed.
    /// </summary>
    /// <param name="value">The metadata-bearing value to render.</param>
    /// <returns>A metadata-only JSON string.</returns>
    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

    private sealed class MetadataOnlyContractResolver(HashSet<string> forbidden) : DefaultContractResolver
    {
        private readonly HashSet<string> _forbidden = forbidden;

        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (property.PropertyName is not null && _forbidden.Contains(property.PropertyName))
            {
                property.ShouldSerialize = _ => false;
                property.Ignored = true;
            }

            return property;
        }
    }
}
