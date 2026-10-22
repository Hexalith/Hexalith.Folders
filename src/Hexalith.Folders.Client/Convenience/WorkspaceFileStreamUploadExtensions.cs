using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>Streams actual bytes through the existing one-item historical file transport; it enables no v2 route.</summary>
public static class WorkspaceFileStreamUploadExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    /// <summary>Submits verified inline or multipart bytes using the caller's authenticated HTTP client and explicit fences.</summary>
    public static async Task<HttpResponseMessage> UploadWorkspaceFileAsync(this HttpClient client, FileUploadDescriptor descriptor,
        Stream content, string idempotencyKey, string correlationId, string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client); ArgumentNullException.ThrowIfNull(descriptor); ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey); ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        if (descriptor.ExpectedLockFence is null or <= 0 || descriptor.ExpectedFileVersion is null or < 0)
            throw new ArgumentException("Explicit observed lock and file version fences are required.", nameof(descriptor));
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int count = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (buffer.Length + count > 1048576) throw new FileContentLimitExceededException(nameof(content));
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        byte[] bytes = buffer.ToArray();
        string hash = FileUpload.ComputeContentHashReference(bytes);
        if (descriptor.ContentHashReference is not null && descriptor.ContentHashReference != hash)
            throw new ArgumentException("Declared content hash differs from actual bytes.", nameof(descriptor));
        bool inline = bytes.Length <= 262144;
        string kind = descriptor.FileOperationKind switch
        {
            FileMutationRequestFileOperationKind.Add => "add", FileMutationRequestFileOperationKind.Change => "change",
            _ => throw new ArgumentException("Upload requires add or change.", nameof(descriptor)),
        };
        var metadata = new
        {
            requestSchemaVersion = "v1", operationId = descriptor.OperationId, fileOperationKind = kind,
            transportOperation = inline ? "PutFileInline" : "PutFileStream",
            pathMetadata = new { normalizedPath = descriptor.PathMetadata.NormalizedPath, displayName = descriptor.PathMetadata.DisplayName,
                pathPolicyClass = descriptor.PathMetadata.PathPolicyClass.ToString().ToLowerInvariant(), unicodeNormalization = "NFC" },
            contentHashReference = hash, byteLength = bytes.Length, ownerFileId = descriptor.OwnerFileId,
            expectedLockFence = descriptor.ExpectedLockFence, expectedFileVersion = descriptor.ExpectedFileVersion,
            inlineContent = inline ? new { mediaType = descriptor.MediaType, contentBytes = Convert.ToBase64String(bytes) } : null,
            streamDescriptor = inline ? null : new { mediaType = descriptor.MediaType, declaredLength = bytes.Length,
                observedLength = bytes.Length, observedContentHashReference = hash, stagingReference = "request_body_stream", uploadMode = "request_body_stream" },
        };
        using var request = new HttpRequestMessage(kind == "add" ? HttpMethod.Post : HttpMethod.Put,
            $"api/v1/folders/{Uri.EscapeDataString(descriptor.FolderId)}/workspaces/{Uri.EscapeDataString(descriptor.WorkspaceId)}/files/{kind}");
        if (inline) request.Content = JsonContent.Create(metadata, options: Json);
        else
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(JsonContent.Create(metadata, options: Json), "metadata");
            var body = new ByteArrayContent(bytes); body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(body, "content"); request.Content = multipart;
        }
        request.Headers.Add("Idempotency-Key", idempotencyKey); request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
