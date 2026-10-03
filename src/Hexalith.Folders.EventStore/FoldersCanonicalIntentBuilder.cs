using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>
/// Builds Contract Spine canonical intent from a trusted command payload.
/// </summary>
internal static class FoldersCanonicalIntentBuilder
{
    public const string AdapterId = "hexalith-folders";

    public const string PolicyVersion = "folders-contract-spine-v2";

    public const int DescriptorVersion = 1;

    private static readonly IReadOnlySet<string> CanonicalSetArrayPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "branchRefPolicy.allowedRefPatterns",
        "branchRefPolicy.protectedRefPatterns",
        "auditMetadataKeys",
    };

    // Domain payload fields are the semantic authority. In particular, file transport
    // evidence can differ between retries of the same file mutation.
    private static readonly IReadOnlySet<string> SemanticPayloadPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "requestSchemaVersion", "archiveReasonCode", "folderId", "parentFolderId",
        "folderMetadata.displayName", "folderMetadata.metadataClass",
        "operations.principalKind", "operations.principalId", "operations.action",
        "providerBindingRef", "providerFamilyRef", "capabilityProfileRef",
        "nonSecretCredentialReference", "repositoryBindingId", "repositoryProfileRef",
        "externalRepositoryRef",
        "branchRefPolicy.requestSchemaVersion", "branchRefPolicy.repositoryBindingId",
        "branchRefPolicy.policyRef", "branchRefPolicy.defaultRef",
        "branchRefPolicy.allowedRefPatterns", "branchRefPolicy.protectedRefPatterns",
        "workspaceId", "branchRefPolicyRef", "workspacePolicyRef", "taskId",
        "lockIntent", "requestedLeaseSeconds", "lockId", "lockOwnershipProof",
        "releaseReasonCode", "operationId", "fileOperationKind",
        "pathMetadata.normalizedPath", "pathMetadata.displayName",
        "pathMetadata.pathPolicyClass", "pathMetadata.unicodeNormalization",
        "contentHashReference", "byteLength", "mediaType", "branchRefTarget",
        "changedPathMetadataDigest", "authorMetadataReference",
        "commitMessageClassification", "auditMetadataKeys",
    };

    public static IdempotencyCanonicalIntent Create(
        IdempotencyIntentCommand command,
        IReadOnlyDictionary<string, string?> semanticFields,
        string? delegatedTaskScope = null,
        string? credentialScope = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(semanticFields);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string?> pair in semanticFields.OrderBy(static field => field.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                if (pair.Value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(pair.Value);
                }
            }

            writer.WriteEndObject();
        }

        return new IdempotencyCanonicalIntent(
            $"{command.Tenant}/{command.Domain}/{command.AggregateId}",
            stream.ToArray(),
            SemanticOptions: CanonicalPayloadGuard(command),
            PolicyVersion,
            delegatedTaskScope,
            credentialScope);
    }

    public static JsonDocument ParsePayload(IdempotencyIntentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        JsonDocument document = JsonDocument.Parse(command.Payload);
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Canonical intent requires an object payload.");
            }

            ValidateUniqueProperties(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public static string? ReadString(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            ? current.ToString()
            : current.ValueKind == JsonValueKind.Null
                ? null
                : current.GetRawText();
    }

    public static string? ReadTaskScope(IdempotencyIntentCommand command, JsonElement root)
    {
        if (!root.TryGetProperty("taskId", out JsonElement task)
            || task.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(task.GetString()))
        {
            throw new JsonException("Canonical intent requires a payload task scope.");
        }

        string fromPayload = task.GetString()!;
        if (command.Extensions is not null
            && command.Extensions.TryGetValue("taskId", out string? fromExtension)
            && !string.Equals(fromPayload, fromExtension, StringComparison.Ordinal))
        {
            throw new JsonException("Envelope task scope differs from the command payload.");
        }

        return fromPayload;
    }

    /// <summary>Validates the fixed server credential scope without accepting a caller-selected scope.</summary>
    public static string ReadCredentialScope(JsonElement root)
    {
        const string scope = "provider_binding";
        if (root.TryGetProperty("credentialScopeClass", out JsonElement supplied)
            && (supplied.ValueKind != JsonValueKind.String
                || !string.Equals(supplied.GetString(), scope, StringComparison.Ordinal)))
        {
            throw new JsonException("Canonical intent does not accept a caller-selected credential scope.");
        }

        return scope;
    }

    public static string? ReadCanonicalObject(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            return current.ToString();
        }

        if (current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalValue(writer, current);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string? ReadCanonicalArray(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            return current.ValueKind == JsonValueKind.Null ? null : current.GetRawText();
        }

        List<string> values = [];
        foreach (JsonElement item in current.EnumerateArray())
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonicalValue(writer, item);
            }

            values.Add(Encoding.UTF8.GetString(stream.ToArray()));
        }

        values.Sort(StringComparer.Ordinal);
        return $"[{string.Join(',', values)}]";
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element)
        => WriteCanonicalValue(writer, element, sortArrays: false, path: string.Empty);

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element, bool sortArrays, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(
                        writer,
                        property.Value,
                        sortArrays,
                        path.Length == 0 ? property.Name : $"{path}.{property.Name}");
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                if (sortArrays && CanonicalSetArrayPaths.Contains(path))
                {
                    List<string> items = [];
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        using MemoryStream itemStream = new();
                        using (Utf8JsonWriter itemWriter = new(itemStream))
                        {
                            WriteCanonicalValue(itemWriter, item, sortArrays: true, path: path);
                        }

                        items.Add(Encoding.UTF8.GetString(itemStream.ToArray()));
                    }

                    foreach (string item in items.Order(StringComparer.Ordinal))
                    {
                        writer.WriteRawValue(item, skipInputValidation: false);
                    }
                }
                else
                {
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        WriteCanonicalValue(writer, item, sortArrays, path);
                    }
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long integer))
                {
                    writer.WriteNumberValue(integer);
                }
                else if (element.TryGetDecimal(out decimal precise))
                {
                    writer.WriteNumberValue(precise);
                }
                else
                {
                    throw new JsonException("Canonical intent contains an unsupported numeric value.");
                }

                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static IReadOnlyDictionary<string, string> CanonicalPayloadGuard(IdempotencyIntentCommand command)
    {
        using JsonDocument document = ParsePayload(command);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteSemanticValue(writer, document.RootElement, path: string.Empty);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["semantic_payload_sha256"] = Convert.ToHexStringLower(SHA256.HashData(stream.ToArray())),
        };
    }

    private static void WriteSemanticValue(Utf8JsonWriter writer, JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
            {
                string childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                if (!SemanticPayloadPaths.Contains(childPath)
                    && !SemanticPayloadPaths.Any(allowed => allowed.StartsWith($"{childPath}.", StringComparison.Ordinal)))
                {
                    continue;
                }

                writer.WritePropertyName(property.Name);
                WriteSemanticValue(writer, property.Value, childPath);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            if (CanonicalSetArrayPaths.Contains(path))
            {
                List<string> items = [];
                foreach (JsonElement item in element.EnumerateArray())
                {
                    using MemoryStream itemStream = new();
                    using (Utf8JsonWriter itemWriter = new(itemStream))
                    {
                        WriteSemanticValue(itemWriter, item, path);
                    }

                    items.Add(Encoding.UTF8.GetString(itemStream.ToArray()));
                }

                foreach (string item in items.Order(StringComparer.Ordinal))
                {
                    writer.WriteRawValue(item, skipInputValidation: false);
                }
            }
            else
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteSemanticValue(writer, item, path);
                }
            }

            writer.WriteEndArray();
        }
        else
        {
            WriteCanonicalValue(writer, element);
        }
    }

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Canonical intent contains duplicate JSON properties.");
                }

                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateUniqueProperties(item);
            }
        }
    }
}
