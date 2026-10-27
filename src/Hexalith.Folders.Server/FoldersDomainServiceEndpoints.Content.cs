using System.Text.Json;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Hexalith.Folders.Server;

public static partial class FoldersDomainServiceEndpoints
{
    internal static IResult ContentDomainProblem(Hexalith.Folders.Aggregates.Folder.FolderResultCode code, string correlation, string task)
    {
        var mapped = FolderCanonicalErrorMapper.ForDomain(code);
        if (code is Hexalith.Folders.Aggregates.Folder.FolderResultCode.AppendConflict
            or Hexalith.Folders.Aggregates.Folder.FolderResultCode.ReplayConflict)
            return ContentProblem("concurrency_conflict", correlation, task);
        if (mapped.StatusCode is 403 or 404) return ContentProblem("resource_unavailable", correlation, task);
        if (mapped.StatusCode == 503) return ContentProblem("file_policy_unavailable", correlation, task);
        return FolderProblemDetailsFactory.ForDomain(mapped.StatusCode, mapped.Category, mapped.Category, mapped.Retryable, correlation, task);
    }
    private static async Task<(FileMutationHttpRequest? Request, byte[]? StreamedBytes)> ReadFileMutationBodyAsync(HttpRequest request, CancellationToken token)
    {
        if (request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type)) throw new JsonException();
            string boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value ?? throw new JsonException();
            if (boundary.Length is < 1 or > 128) throw new JsonException();
            var reader = new MultipartReader(boundary, request.Body) { BodyLengthLimit = 1100000, HeadersLengthLimit = 16384 };
            var metadata = await reader.ReadNextSectionAsync(token).ConfigureAwait(false) ?? throw new JsonException();
            if (!ContentDispositionHeaderValue.TryParse(metadata.ContentDisposition, out var disposition)
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "metadata") throw new JsonException();
            byte[] json = await ReadBoundedContentAsync(metadata.Body, 16384, token).ConfigureAwait(false);
            var body = JsonSerializer.Deserialize<FileMutationHttpRequest>(json, RequestJsonOptions);
            var content = await reader.ReadNextSectionAsync(token).ConfigureAwait(false) ?? throw new JsonException();
            if (!ContentDispositionHeaderValue.TryParse(content.ContentDisposition, out disposition)
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "content") throw new JsonException();
            byte[] bytes = await ReadBoundedContentAsync(content.Body, 1048576, token).ConfigureAwait(false);
            if (await reader.ReadNextSectionAsync(token).ConfigureAwait(false) is not null) throw new JsonException();
            return (body, bytes);
        }
        byte[] document = await ReadBoundedContentAsync(request.Body, 1500000, token).ConfigureAwait(false);
        return (JsonSerializer.Deserialize<FileMutationHttpRequest>(document, RequestJsonOptions), null);
    }
    internal static async Task<byte[]> ReadBoundedContentAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int count = await stream.ReadAsync(chunk, token).ConfigureAwait(false);
            if (count == 0) return buffer.ToArray();
            if (buffer.Length + count > maximum) throw new WorkspaceContentPolicyException("file_content_limit_exceeded");
            await buffer.WriteAsync(chunk.AsMemory(0, count), token).ConfigureAwait(false);
        }
    }
    internal static IResult ContentProblem(string code, string? correlationId, string? taskId)
    {
        (int status, string category, bool retryable, string action) = code switch
        {
            "resource_unavailable" => (404, "tenant_access_denied", false, "no_action"),
            "file_policy_unavailable" => (503, "file_policy_unavailable", true, "retry"),
            "file_content_limit_exceeded" => (422, "input_limit_exceeded", false, "revise_request"),
            "d9_inline_limit_exceeded" => (413, "input_limit_exceeded", true, "revise_request"),
            "path_validation_failed" => (400, "path_validation_failed", false, "revise_request"),
            "confidential_operation_not_durable" => (422, "validation_error", false, "correct_input"),
            "native_evidence_changed" or "native_contention" => (409, "concurrency_conflict", true, "retry"),
            "idempotency_conflict" => (409, "idempotency_conflict", false, "revise_request"),
            "target_conflict" => (409, "concurrency_conflict", false, "revise_request"),
            "concurrency_conflict" => (409, "concurrency_conflict", false, "revise_request"),
            _ => (400, "validation_error", false, "revise_request"),
        };
        return Results.Problem(type: "https://hexalith.dev/errors/folders/" + code,
            title: status == 404 ? "Resource not available." : "Workspace content operation unavailable.", statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = category,
                ["code"] = code,
                ["message"] = status == 404 ? "The requested resource is not available to the caller." : "The workspace content operation could not be completed.",
                ["correlationId"] = correlationId,
                ["taskId"] = taskId,
                ["retryable"] = retryable,
                ["clientAction"] = action,
                ["details"] = new Dictionary<string, object?> { ["visibility"] = "metadata_only" },
            });
    }
}
