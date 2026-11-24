using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Server;

/// <summary>
/// Serializer options shared by the Audit, Domain, OpsConsole and provider-readiness endpoint groups.
/// </summary>
internal static class FoldersServerJsonOptions
{
    /// <summary>
    /// Gets the read-only options for HTTP response bodies: Web defaults that omit null properties.
    /// </summary>
    /// <remarks>
    /// Request options stay with each endpoint group because their strictness differs: the Domain endpoints also
    /// reject duplicate properties and normalize JSON elements, while provider readiness rejects only unmapped members.
    /// The content endpoints keep their own options for the same reason.
    /// </remarks>
    public static JsonSerializerOptions Response { get; } = CreateResponseOptions();

    private static JsonSerializerOptions CreateResponseOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
