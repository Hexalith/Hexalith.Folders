using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hexalith.Folders.Cli.Infrastructure;

/// <summary>
/// Serializes SDK result and problem shapes to JSON while enforcing the metadata-only invariant centrally:
/// content-bearing properties (notably <c>contentBytes</c> on <c>FileRangeReadResult</c> / inline file
/// uploads) are never emitted to any output channel. Filtering is centralized here rather than duplicated
/// per command (project-context: keep sensitive-metadata filtering centralized).
/// </summary>
internal static class MetadataOnlyJson
{
    /// <summary>
    /// Wire property names that may carry raw or base64-encoded file content. These are dropped from every
    /// serialized payload regardless of nesting depth, so neither <c>human</c> nor <c>json</c> output can
    /// leak file bytes even when an authorized read-range result carries them.
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

        // CamelCase so projected shapes (e.g. ProjectedProblem) match the wire/SDK ProblemDetails casing
        // (category, code, correlationId, …). Generated SDK types carry explicit [JsonProperty] names, which
        // CamelCaseNamingStrategy preserves (OverrideSpecifiedNames defaults to false), so they are unaffected
        // and the forbidden-property filter (which keys off those explicit names) keeps working.
        ContractResolver = new MetadataOnlyContractResolver(ForbiddenPropertyNames)
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };

    /// <summary>
    /// Serializes <paramref name="value"/> to indented JSON with content-bearing fields removed.
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
