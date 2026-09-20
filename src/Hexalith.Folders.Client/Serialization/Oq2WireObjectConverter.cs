using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>
/// Validates the closed OQ2 wire shapes before Newtonsoft.Json can coerce tokens or fill omitted values
/// with CLR defaults. Serialization remains owned by the generated client.
/// </summary>
public sealed class Oq2WireObjectConverter : JsonConverter
{
    private static readonly Regex OpaqueIdentifier = new("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex HashReference = new("^hashref_[A-Za-z0-9]{32,96}$", RegexOptions.CultureInvariant);
    private static readonly Regex MediaType = new("^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*$", RegexOptions.CultureInvariant);
    private static readonly Regex UtcDateTime = new("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?(Z|[+-]00:00)$", RegexOptions.CultureInvariant);
    private const int MaximumBase64Characters = 349528;

    private static readonly HashSet<string> SupportedTypes =
    [
        "PathMetadata",
        "VisiblePathMetadata",
        "ContentAllowedPathMetadata",
        "FileMutationRequest",
        "AddFileRequest",
        "ChangeFileRequest",
        "RemoveFileRequest",
        "FileRangeReadCompleteResult",
        "FileRangeReadPartialResult",
        "FileSearchResult",
        "FileSafeResourceUnavailableProblem",
        "FileRangeUnsatisfiableProblem",
        "FilePolicyUnavailableProblem",
        "FileContentEvidenceInvalidProblem",
        "FileContentEvidenceInvalidOrValidationProblem",
        "FileInlineTransportRequiredProblem",
        "FileContentLimitExceededProblem",
        "FileContentLimitExceededOrWorkspaceTransitionProblem",
        "FileMutationUnavailableProblem",
        "FileContextUnavailableProblem",
    ];

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override bool CanConvert(Type objectType)
    {
        ArgumentNullException.ThrowIfNull(objectType);
        return SupportedTypes.Contains(objectType.Name);
    }

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(serializer);
        DateParseHandling previousDateParseHandling = reader.DateParseHandling;
        reader.DateParseHandling = DateParseHandling.None;
        JObject value;
        try
        {
            value = JObject.Load(
                reader,
                new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        finally
        {
            reader.DateParseHandling = previousDateParseHandling;
        }
        Validate(objectType.Name, value);

        object target = existingValue ?? Activator.CreateInstance(objectType)
            ?? throw new JsonSerializationException($"Could not create {objectType.Name}.");
        using JsonReader objectReader = value.CreateReader();
        serializer.Populate(objectReader, target);
        SynchronizeShadowedProperties(objectType.Name, target);
        return target;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotSupportedException("OQ2 uses the generated serializer for writes.");

    private static void Validate(string typeName, JObject value)
    {
        switch (typeName)
        {
            case "PathMetadata":
            case "VisiblePathMetadata":
            case "ContentAllowedPathMetadata":
                ValidatePath(value, typeName);
                return;
            case "FileMutationRequest":
            case "AddFileRequest":
            case "ChangeFileRequest":
            case "RemoveFileRequest":
                ValidateMutation(value, typeName);
                return;
            case "FileRangeReadCompleteResult":
                ValidateRange(value, expectedPartial: false);
                return;
            case "FileRangeReadPartialResult":
                ValidateRange(value, expectedPartial: true);
                return;
            case "FileSearchResult":
                ValidateSearch(value);
                return;
            case "FileSafeResourceUnavailableProblem":
                ValidateExactProblem(value, 404, "tenant_access_denied", "resource_unavailable", false, "no_action", "redacted", "Access unavailable", "The requested resource is unavailable.");
                return;
            case "FileRangeUnsatisfiableProblem":
                ValidateExactProblem(value, 416, "range_unsatisfiable", "range_unsatisfiable", false, "revise_request", "metadata_only", "Range unsatisfiable", "The requested byte range cannot be satisfied.");
                return;
            case "FilePolicyUnavailableProblem":
                ValidateExactProblem(value, 503, "file_policy_unavailable", "file_policy_unavailable", true, "retry", "redacted", "File policy unavailable", "The file policy cannot be verified for this request.");
                return;
            case "FileContentEvidenceInvalidProblem":
                ValidateExactProblem(value, 400, "validation_error", "content_evidence_invalid", false, "revise_request", "metadata_only", "Content evidence invalid", "The supplied content evidence is not valid.");
                return;
            case "FileInlineTransportRequiredProblem":
                ValidateExactProblem(value, 413, "input_limit_exceeded", "d9_inline_limit_exceeded", true, "revise_request", "metadata_only", "Inline payload too large", "The inline payload exceeds the configured D-9 boundary.");
                return;
            case "FileContentLimitExceededProblem":
                ValidateExactProblem(value, 422, "input_limit_exceeded", "file_content_limit_exceeded", false, "revise_request", "metadata_only", "File content limit exceeded", "The file content exceeds the permitted maximum.");
                return;
            case "FileContentEvidenceInvalidOrValidationProblem":
                ValidateProblemUnion(value, 400, "validation_error", "content_evidence_invalid", false, "revise_request", false);
                return;
            case "FileContentLimitExceededOrWorkspaceTransitionProblem":
                ValidateProblemUnion(value, 422, "input_limit_exceeded", "file_content_limit_exceeded", false, "revise_request", true);
                return;
            case "FileMutationUnavailableProblem":
            case "FileContextUnavailableProblem":
                ValidateProblemUnion(value, 503, "file_policy_unavailable", "file_policy_unavailable", true, "retry", true);
                return;
            default:
                throw new JsonSerializationException($"Unsupported OQ2 wire type {typeName}.");
        }
    }

    private static void ValidatePath(JObject value, string typeName)
    {
        RequireExactMembers(value, ["normalizedPath", "displayName", "pathPolicyClass", "unicodeNormalization"], typeName);
        string path = RequireString(value, "normalizedPath");
        string displayName = RequireString(value, "displayName");
        string policyClass = RequireString(value, "pathPolicyClass");
        RequireValue(value, "unicodeNormalization", "NFC");

        string[] segments = path.Split('/');
        if (path.Length is < 1 or > 500
            || path[0] == '/'
            || path[^1] == '/'
            || path.Contains("//", StringComparison.Ordinal)
            || path.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/'))
            || segments.Any(static segment => segment is "." or ".." || segment.EndsWith(".", StringComparison.Ordinal))
            || segments.Any(IsReservedPathSegment))
        {
            throw Invalid("normalizedPath does not satisfy the canonical ASCII path profile.");
        }

        if (displayName.Length is < 1 or > 128
            || displayName.Any(static c => c <= 0x1f || c == 0x7f || c is '/' or '\\'))
        {
            throw Invalid("displayName does not satisfy the metadata-safe profile.");
        }

        string[] allowed = typeName switch
        {
            "ContentAllowedPathMetadata" => ["content_allowed"],
            "VisiblePathMetadata" => ["content_allowed", "metadata_only"],
            _ => ["content_allowed", "metadata_only", "excluded", "restricted"],
        };
        if (!allowed.Contains(policyClass, StringComparer.Ordinal))
        {
            throw Invalid("pathPolicyClass is not a canonical string value for this wire type.");
        }
    }

    private static void ValidateMutation(JObject value, string typeName)
    {
        RequireAllowedAndRequiredMembers(
            value,
            ["requestSchemaVersion", "fileOperationKind", "transportOperation", "operationId", "pathMetadata", "contentHashReference", "byteLength", "inlineContent", "streamDescriptor"],
            ["requestSchemaVersion", "fileOperationKind", "transportOperation", "operationId", "pathMetadata"],
            typeName);
        RequireValue(value, "requestSchemaVersion", "v2");
        string kind = RequireString(value, "fileOperationKind");
        string transport = RequireString(value, "transportOperation");
        if (!new[] { "add", "change", "remove" }.Contains(kind, StringComparer.Ordinal))
        {
            throw Invalid("fileOperationKind must be a canonical string token.");
        }

        string? expectedKind = typeName switch
        {
            "AddFileRequest" => "add",
            "ChangeFileRequest" => "change",
            "RemoveFileRequest" => "remove",
            _ => null,
        };
        if (expectedKind is not null && !string.Equals(kind, expectedKind, StringComparison.Ordinal))
        {
            throw Invalid($"{typeName} requires fileOperationKind {expectedKind}.");
        }

        if (!OpaqueIdentifier.IsMatch(RequireString(value, "operationId")))
        {
            throw Invalid("operationId is not a bounded opaque identifier.");
        }

        ValidatePath(RequireObject(value, "pathMetadata"), "PathMetadata");

        if (kind == "remove")
        {
            if (transport != "metadataOnlyRemoval"
                || value.Properties().Any(static property => property.Name is "contentHashReference" or "byteLength" or "inlineContent" or "streamDescriptor"))
            {
                throw Invalid("Remove requests require metadataOnlyRemoval and omit all content evidence.");
            }

            return;
        }

        string hashReference = RequireString(value, "contentHashReference");
        if (!HashReference.IsMatch(hashReference))
        {
            throw Invalid("contentHashReference is malformed.");
        }

        long byteLength = RequireInteger(value, "byteLength", 0, 1048576);
        if (transport == "PutFileInline")
        {
            if (byteLength > 262144 || value["streamDescriptor"] is not null)
            {
                throw Invalid("Inline evidence violates its size or omission rules.");
            }

            JObject inline = RequireObject(value, "inlineContent");
            RequireAllowedAndRequiredMembers(inline, ["mediaType", "contentBytes", "contentMediaType"], ["mediaType", "contentBytes"], "PutFileInline");
            ValidateMediaType(RequireString(inline, "mediaType"));
            if (inline["contentMediaType"] is not null)
            {
                ValidateMediaType(RequireString(inline, "contentMediaType"));
            }

            byte[] bytes = DecodeBase64(RequireStringToken(inline, "contentBytes"));
            if (bytes.LongLength != byteLength
                || !string.Equals("hashref_" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), hashReference, StringComparison.Ordinal))
            {
                throw Invalid("Inline length/hash evidence is inconsistent with contentBytes.");
            }

            return;
        }

        if (transport == "PutFileStream")
        {
            if (byteLength <= 262144 || value["inlineContent"] is not null)
            {
                throw Invalid("Stream evidence violates its size or omission rules.");
            }

            JObject stream = RequireObject(value, "streamDescriptor");
            RequireExactMembers(stream, ["mediaType", "declaredLength", "observedLength", "stagingReference", "observedContentHashReference", "uploadMode"], "PutFileStream");
            ValidateMediaType(RequireString(stream, "mediaType"));
            long declared = RequireInteger(stream, "declaredLength", 262145, 1048576);
            long observed = RequireInteger(stream, "observedLength", 262145, 1048576);
            if (declared != byteLength || observed != byteLength)
            {
                throw Invalid("Stream length evidence is inconsistent.");
            }

            if (!OpaqueIdentifier.IsMatch(RequireString(stream, "stagingReference"))
                || !string.Equals(RequireString(stream, "observedContentHashReference"), hashReference, StringComparison.Ordinal))
            {
                throw Invalid("Stream staging/hash evidence is inconsistent.");
            }

            RequireValue(stream, "uploadMode", "request_body_stream");
            return;
        }

        throw Invalid("transportOperation is not a canonical string token for an upload.");
    }

    private static void ValidateRange(JObject value, bool expectedPartial)
    {
        RequireExactMembers(value, ["path", "range", "contentBytes", "limits", "freshness"], "FileRangeReadResult");
        ValidatePath(RequireObject(value, "path"), "ContentAllowedPathMetadata");

        JObject range = RequireObject(value, "range");
        RequireExactMembers(range, ["startOffset", "endOffset", "actualBytes", "partial"], "FileRangeReadDescriptor");
        long start = RequireInteger(range, "startOffset", 0, long.MaxValue);
        long end = RequireInteger(range, "endOffset", 0, long.MaxValue);
        long actual = RequireInteger(range, "actualBytes", 0, 262144);
        bool partial = RequireBoolean(range, "partial");
        long requestedLength = end - start;
        if (partial != expectedPartial
            || end < start
            || requestedLength > 262144
            || (expectedPartial ? actual <= 0 || requestedLength <= actual : requestedLength != actual))
        {
            throw Invalid("Range window evidence is inconsistent.");
        }

        byte[] content = DecodeBase64(RequireStringToken(value, "contentBytes"));
        if (content.LongLength != actual)
        {
            throw Invalid("Range content length does not match actualBytes.");
        }

        JObject limits = RequireObject(value, "limits");
        ValidateLimits(limits, "range", 262144);
        if (RequireInteger(limits, "actualBytes", 0, 1048576) != actual)
        {
            throw Invalid("Range limit metadata does not match actualBytes.");
        }

        ValidateFreshness(RequireObject(value, "freshness"));
    }

    private static void ValidateSearch(JObject value)
    {
        RequireExactMembers(value, ["items", "page", "limits", "freshness"], "FileSearchResult");
        JArray items = RequireArray(value, "items");
        if (items.Count > 500)
        {
            throw Invalid("Search items exceed the canonical 500-result bound.");
        }

        foreach (JToken itemToken in items)
        {
            if (itemToken is not JObject item)
            {
                throw Invalid("Search items must be objects.");
            }

            RequireAllowedAndRequiredMembers(
                item,
                ["path", "kind", "byteLength", "sensitivity", "redaction"],
                ["path", "kind", "byteLength", "sensitivity", "redaction"],
                "ContentAllowedFileMetadataItem");
            ValidatePath(RequireObject(item, "path"), "ContentAllowedPathMetadata");
            RequireValue(item, "kind", "file");
            _ = RequireInteger(item, "byteLength", 0, 17592186044416);

            if (!new[] { "public_metadata", "tenant_sensitive", "credential_sensitive", "secret" }
                .Contains(RequireString(item, "sensitivity"), StringComparer.Ordinal))
            {
                throw Invalid("sensitivity is not a canonical string token.");
            }

            RequireValue(item, "redaction", "not_redacted");
        }

        JObject page = RequireObject(value, "page");
        RequireAllowedAndRequiredMembers(page, ["cursor", "limit", "isTruncated", "truncatedReason"], ["limit", "isTruncated"], "FileSearchPaginationMetadata");
        if (page["cursor"] is not null)
        {
            string cursor = RequireStringToken(page, "cursor");
            if (cursor.Length > 256)
            {
                throw Invalid("cursor exceeds its canonical bound.");
            }
        }

        long pageLimit = RequireInteger(page, "limit", 1, 500);
        ValidateOptionalTruncation(page);

        JObject limits = RequireObject(value, "limits");
        ValidateLimits(limits, "search", 500);
        if (items.Count > pageLimit
            || RequireInteger(limits, "configuredLimit", 0, 500) != pageLimit
            || RequireInteger(limits, "actualCount", 0, 500) != items.Count
            || RequireBoolean(limits, "isTruncated") != RequireBoolean(page, "isTruncated")
            || (RequireBoolean(page, "isTruncated")
                && !string.Equals(RequireString(limits, "truncatedReason"), RequireString(page, "truncatedReason"), StringComparison.Ordinal)))
        {
            throw Invalid("Search pagination and limit metadata are inconsistent.");
        }

        ValidateFreshness(RequireObject(value, "freshness"));
    }

    private static void ValidateProblemUnion(
        JObject value,
        int status,
        string category,
        string code,
        bool retryable,
        string clientAction,
        bool categoryIsSignal)
    {
        bool exactSignal = string.Equals(value["code"]?.Value<string>(), code, StringComparison.Ordinal)
            || (categoryIsSignal && string.Equals(value["category"]?.Value<string>(), category, StringComparison.Ordinal));
        if (exactSignal)
        {
            ValidateExactProblem(
                value,
                status,
                category,
                code,
                retryable,
                clientAction,
                status is 404 or 503 ? "redacted" : "metadata_only",
                ExactTitle(code),
                ExactMessage(code));
            return;
        }

        RequireRequiredMembers(
            value,
            ["type", "title", "status", "category", "code", "message", "correlationId", "retryable", "clientAction", "details"],
            "ProblemDetails");
        _ = RequireInteger(value, "status", 100, 599);
        _ = RequireString(value, "category");
        _ = RequireString(value, "code");
        _ = RequireBoolean(value, "retryable");
        _ = RequireString(value, "clientAction");
        ValidateProblemIdentity(value, exact: false);
        ValidateOptionalProblemUriAndText(value);
        if (value["details"] is not JObject)
        {
            throw Invalid("Problem details must be an object.");
        }
    }

    private static void ValidateExactProblem(
        JObject value,
        int status,
        string category,
        string code,
        bool retryable,
        string clientAction,
        string visibility,
        string title,
        string message)
    {
        RequireExactMembers(value, ["type", "title", "status", "category", "code", "message", "correlationId", "retryable", "clientAction", "details"], "ExactFileProblem");
        if (RequireInteger(value, "status", 100, 599) != status
            || !string.Equals(RequireString(value, "category"), category, StringComparison.Ordinal)
            || !string.Equals(RequireString(value, "code"), code, StringComparison.Ordinal)
            || RequireBoolean(value, "retryable") != retryable
            || !string.Equals(RequireString(value, "clientAction"), clientAction, StringComparison.Ordinal)
            || !string.Equals(RequireString(value, "title"), title, StringComparison.Ordinal)
            || !string.Equals(RequireString(value, "message"), message, StringComparison.Ordinal))
        {
            throw Invalid("Exact file problem contains noncanonical values.");
        }

        ValidateProblemIdentity(value, exact: true);
        JObject details = RequireObject(value, "details");
        RequireExactMembers(details, ["visibility"], "ExactFileProblem.details");
        RequireValue(details, "visibility", visibility);
    }

    private static void ValidateProblemIdentity(JObject value, bool exact)
    {
        ValidateUriReference(RequireStringToken(value, "type"), "type");
        string title = RequireStringToken(value, "title");
        string code = RequireString(value, "code");
        string message = RequireStringToken(value, "message");
        if ((exact && (title.Length is < 1 or > 128 || message.Length is < 1 or > 512))
            || code.Length > 128
            || !Regex.IsMatch(code, "^[a-z0-9][a-z0-9_:-]{0,127}$", RegexOptions.CultureInvariant))
        {
            throw Invalid("Problem identity fields exceed their canonical bounds.");
        }

        if (!OpaqueIdentifier.IsMatch(RequireString(value, "correlationId")))
        {
            throw Invalid("correlationId is not a bounded opaque identifier.");
        }
    }

    private static void ValidateOptionalProblemUriAndText(JObject value)
    {
        if (value["detail"] is not null)
        {
            _ = RequireStringToken(value, "detail");
        }

        if (value["instance"] is not null)
        {
            ValidateUriReference(RequireStringToken(value, "instance"), "instance");
        }
    }

    private static void ValidateUriReference(string value, string propertyName)
    {
        if (!Uri.IsWellFormedUriString(value, UriKind.RelativeOrAbsolute))
        {
            throw Invalid($"{propertyName} must be a valid URI reference.");
        }
    }

    private static string ExactTitle(string code) => code switch
    {
        "resource_unavailable" => "Access unavailable",
        "range_unsatisfiable" => "Range unsatisfiable",
        "file_policy_unavailable" => "File policy unavailable",
        "content_evidence_invalid" => "Content evidence invalid",
        "d9_inline_limit_exceeded" => "Inline payload too large",
        "file_content_limit_exceeded" => "File content limit exceeded",
        _ => throw Invalid("The exact file problem code is not declared."),
    };

    private static string ExactMessage(string code) => code switch
    {
        "resource_unavailable" => "The requested resource is unavailable.",
        "range_unsatisfiable" => "The requested byte range cannot be satisfied.",
        "file_policy_unavailable" => "The file policy cannot be verified for this request.",
        "content_evidence_invalid" => "The supplied content evidence is not valid.",
        "d9_inline_limit_exceeded" => "The inline payload exceeds the configured D-9 boundary.",
        "file_content_limit_exceeded" => "The file content exceeds the permitted maximum.",
        _ => throw Invalid("The exact file problem code is not declared."),
    };

    private static void ValidateLimits(JObject limits, string queryFamily, long maximumConfiguredLimit)
    {
        RequireExactMembers(limits, ["queryFamily", "configuredLimit", "actualCount", "actualBytes", "elapsedMilliseconds", "isTruncated", "truncatedReason"], "ContextQueryLimitMetadata");
        RequireValue(limits, "queryFamily", queryFamily);
        _ = RequireInteger(limits, "configuredLimit", 0, maximumConfiguredLimit);
        _ = RequireInteger(limits, "actualCount", 0, maximumConfiguredLimit);
        _ = RequireInteger(limits, "actualBytes", 0, 1048576);
        _ = RequireInteger(limits, "elapsedMilliseconds", 0, 86400000);
        ValidateTruncation(limits);
    }

    private static void ValidateTruncation(JObject value)
    {
        bool truncated = RequireBoolean(value, "isTruncated");
        string reason = RequireString(value, "truncatedReason");
        if ((truncated && reason == "not_truncated") || (!truncated && reason != "not_truncated"))
        {
            throw Invalid("Truncation flag and reason disagree.");
        }
    }

    private static void ValidateOptionalTruncation(JObject value)
    {
        bool truncated = RequireBoolean(value, "isTruncated");
        JToken? reasonToken = value["truncatedReason"];
        if (reasonToken is null)
        {
            if (truncated)
            {
                throw Invalid("A truncated page requires truncatedReason.");
            }

            return;
        }

        string reason = RequireString(value, "truncatedReason");
        if (!truncated || reason == "not_truncated"
            || !new[] { "result_count_limit", "response_budget_limit", "query_timeout" }.Contains(reason, StringComparer.Ordinal))
        {
            throw Invalid("Pagination truncation flag and reason disagree.");
        }
    }

    private static void ValidateFreshness(JObject value)
    {
        RequireAllowedAndRequiredMembers(value, ["readConsistency", "observedAt", "projectionWatermark", "stale"], ["readConsistency", "observedAt"], "FreshnessMetadata");
        if (!new[] { "snapshot_per_task", "read_your_writes", "eventually_consistent" }
            .Contains(RequireString(value, "readConsistency"), StringComparer.Ordinal))
        {
            throw Invalid("readConsistency is not a canonical string token.");
        }

        JToken? observedAt = value["observedAt"];
        if (observedAt?.Type != JTokenType.String
            || !UtcDateTime.IsMatch(observedAt.Value<string>()!))
        {
            throw Invalid("observedAt must be a canonical UTC JSON date-time string.");
        }
        if (value["projectionWatermark"] is not null
            && !OpaqueIdentifier.IsMatch(RequireString(value, "projectionWatermark")))
        {
            throw Invalid("projectionWatermark is not a bounded opaque identifier.");
        }

        if (value["stale"] is not null)
        {
            _ = RequireBoolean(value, "stale");
        }
    }

    private static void SynchronizeShadowedProperties(string typeName, object target)
    {
        if (target is Hexalith.Folders.Client.Generated.VisiblePathMetadata visible)
        {
            ((Hexalith.Folders.Client.Generated.PathMetadata)visible).PathPolicyClass = visible.PathPolicyClass switch
            {
                Hexalith.Folders.Client.Generated.VisiblePathMetadataPathPolicyClass.Content_allowed => Hexalith.Folders.Client.Generated.PathMetadataPathPolicyClass.Content_allowed,
                Hexalith.Folders.Client.Generated.VisiblePathMetadataPathPolicyClass.Metadata_only => Hexalith.Folders.Client.Generated.PathMetadataPathPolicyClass.Metadata_only,
                _ => throw Invalid("VisiblePathMetadata contains a noncanonical policy class."),
            };
        }
        else if (target is Hexalith.Folders.Client.Generated.ContentAllowedPathMetadata content)
        {
            ((Hexalith.Folders.Client.Generated.PathMetadata)content).PathPolicyClass = Hexalith.Folders.Client.Generated.PathMetadataPathPolicyClass.Content_allowed;
        }

        if (target is Hexalith.Folders.Client.Generated.FileRangeReadCompleteResult complete)
        {
            ((Hexalith.Folders.Client.Generated.FileRangeReadDescriptor)complete.Range).Partial = complete.Range.Partial;
            ((Hexalith.Folders.Client.Generated.FileRangeReadResult)complete).Range = complete.Range;
        }
        else if (target is Hexalith.Folders.Client.Generated.FileRangeReadPartialResult partial)
        {
            ((Hexalith.Folders.Client.Generated.FileRangeReadDescriptor)partial.Range).Partial = partial.Range.Partial;
            ((Hexalith.Folders.Client.Generated.FileRangeReadResult)partial).Range = partial.Range;
        }

        if (target is Hexalith.Folders.Client.Generated.AddFileRequest add)
        {
            ((Hexalith.Folders.Client.Generated.FileMutationRequest)add).FileOperationKind = Hexalith.Folders.Client.Generated.FileMutationRequestFileOperationKind.Add;
        }
        else if (target is Hexalith.Folders.Client.Generated.ChangeFileRequest change)
        {
            ((Hexalith.Folders.Client.Generated.FileMutationRequest)change).FileOperationKind = Hexalith.Folders.Client.Generated.FileMutationRequestFileOperationKind.Change;
        }
        else if (target is Hexalith.Folders.Client.Generated.RemoveFileRequest remove)
        {
            ((Hexalith.Folders.Client.Generated.FileMutationRequest)remove).FileOperationKind = Hexalith.Folders.Client.Generated.FileMutationRequestFileOperationKind.Remove;
        }

        if (target is Hexalith.Folders.Client.Generated.FileSearchResult search)
        {
            foreach (Hexalith.Folders.Client.Generated.ContentAllowedFileMetadataItem item in search.Items)
            {
                Hexalith.Folders.Client.Generated.ContentAllowedPathMetadata contentPath = item.Path;
                Hexalith.Folders.Client.Generated.VisiblePathMetadata visiblePath = new()
                {
                    NormalizedPath = contentPath.NormalizedPath,
                    DisplayName = contentPath.DisplayName,
                    UnicodeNormalization = contentPath.UnicodeNormalization,
                    PathPolicyClass = Hexalith.Folders.Client.Generated.VisiblePathMetadataPathPolicyClass.Content_allowed,
                };
                ((Hexalith.Folders.Client.Generated.PathMetadata)visiblePath).PathPolicyClass = Hexalith.Folders.Client.Generated.PathMetadataPathPolicyClass.Content_allowed;
                ((Hexalith.Folders.Client.Generated.FileMetadataItem)item).Path = visiblePath;
                ((Hexalith.Folders.Client.Generated.FileMetadataItem)item).Kind = Hexalith.Folders.Client.Generated.FileMetadataItemKind.File;
            }
        }
    }

    private static void ValidateMediaType(string value)
    {
        if (value.Length > 128 || !MediaType.IsMatch(value))
        {
            throw Invalid("mediaType is not a canonical type/subtype token.");
        }
    }

    private static bool IsReservedPathSegment(string segment)
    {
        string baseName = segment.Split('.', 2)[0];
        return string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)
            || new[] { "con", "prn", "aux", "nul" }.Contains(baseName, StringComparer.OrdinalIgnoreCase)
            || (baseName.Length == 4
                && (baseName.StartsWith("com", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("lpt", StringComparison.OrdinalIgnoreCase))
                && baseName[3] is >= '1' and <= '9');
    }

    private static byte[] DecodeBase64(string value)
    {
        if (value.Length > MaximumBase64Characters || (value.Length & 3) != 0)
        {
            throw Invalid("contentBytes exceeds the encoded bound or is not canonical base64.");
        }

        byte[] buffer = new byte[(value.Length / 4) * 3];
        if (!Convert.TryFromBase64Chars(value, buffer, out int bytesWritten)
            || Convert.ToBase64String(buffer, 0, bytesWritten) != value)
        {
            throw Invalid("contentBytes is not canonical base64.");
        }

        return buffer[..bytesWritten];
    }

    private static string RequireString(JObject value, string propertyName)
    {
        string result = RequireStringToken(value, propertyName);
        if (string.IsNullOrEmpty(result))
        {
            throw Invalid($"{propertyName} must be a nonempty JSON string.");
        }

        return result;
    }

    private static string RequireStringToken(JObject value, string propertyName)
    {
        JToken? token = value[propertyName];
        if (token?.Type != JTokenType.String)
        {
            throw Invalid($"{propertyName} must be a JSON string.");
        }

        return token.Value<string>()!;
    }

    private static long RequireInteger(JObject value, string propertyName, long minimum, long maximum)
    {
        JToken? token = value[propertyName];
        if (token?.Type != JTokenType.Integer)
        {
            throw Invalid($"{propertyName} must be a JSON integer.");
        }

        long result;
        try
        {
            result = token.Value<long>();
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            throw new JsonSerializationException($"{propertyName} is outside the supported integer range.", exception);
        }

        if (result < minimum || result > maximum)
        {
            throw Invalid($"{propertyName} is outside its canonical bounds.");
        }

        return result;
    }

    private static bool RequireBoolean(JObject value, string propertyName)
    {
        JToken? token = value[propertyName];
        if (token?.Type != JTokenType.Boolean)
        {
            throw Invalid($"{propertyName} must be a JSON Boolean.");
        }

        return token.Value<bool>();
    }

    private static JObject RequireObject(JObject value, string propertyName) =>
        value[propertyName] as JObject ?? throw Invalid($"{propertyName} must be a JSON object.");

    private static JArray RequireArray(JObject value, string propertyName) =>
        value[propertyName] as JArray ?? throw Invalid($"{propertyName} must be a JSON array.");

    private static void RequireValue(JObject value, string propertyName, string expected)
    {
        if (!string.Equals(RequireString(value, propertyName), expected, StringComparison.Ordinal))
        {
            throw Invalid($"{propertyName} must equal {expected}.");
        }
    }

    private static void RequireExactMembers(JObject value, string[] required, string context) =>
        RequireAllowedAndRequiredMembers(value, required, required, context);

    private static void RequireRequiredMembers(JObject value, string[] required, string context)
    {
        HashSet<string> actual = value.Properties().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (required.Any(requiredProperty => !actual.Contains(requiredProperty) || value[requiredProperty]?.Type == JTokenType.Null))
        {
            throw Invalid($"{context} has missing or null members.");
        }
    }

    private static void RequireAllowedAndRequiredMembers(JObject value, string[] allowed, string[] required, string context)
    {
        HashSet<string> actual = value.Properties().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (actual.Except(allowed, StringComparer.Ordinal).Any()
            || required.Any(requiredProperty => !actual.Contains(requiredProperty) || value[requiredProperty]?.Type == JTokenType.Null))
        {
            throw Invalid($"{context} has missing, null, or unknown members.");
        }
    }

    private static JsonSerializationException Invalid(string message) => new(message);
}
