using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;

namespace Hexalith.Folders.Mcp.Tooling;

/// <summary>
/// Deserializes a tool's request-body input (the operation's Contract Spine body, supplied as inline JSON)
/// into the matching generated request type. Introduces no new request fields: the JSON is the spine body
/// verbatim. Malformed JSON is a pre-SDK usage error raised before any HTTP call; the metadata-only message
/// never echoes the JSON content.
/// </summary>
internal static class RequestBody
{
    /// <summary>
    /// Reads and deserializes a request body of type <typeparamref name="T"/> from inline JSON. Returns a
    /// default-constructed body when no value is supplied (mirrors the CLI <c>--request</c> behavior).
    /// </summary>
    /// <typeparam name="T">The spine request type.</typeparam>
    /// <param name="requestJson">The raw inline JSON request body, or <see langword="null"/>/blank.</param>
    /// <returns>The deserialized request body.</returns>
    /// <exception cref="McpUsageException">Thrown when <paramref name="requestJson"/> is not valid JSON for the operation.</exception>
    public static T Read<T>(string? requestJson)
        where T : new()
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return new T();
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(requestJson) ?? new T();
        }
        catch (JsonException)
        {
            throw new McpUsageException("The request body is not valid JSON for this operation.");
        }
    }
}
