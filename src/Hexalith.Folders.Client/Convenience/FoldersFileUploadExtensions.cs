using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Convenience extensions over <see cref="IClient"/> that submit file mutations without requiring callers
/// to hand-build the polymorphic <see cref="FileMutationRequest"/> transport union.
/// </summary>
/// <remarks>
/// Each method reduces 1:1 to an existing generated operation (<see cref="IClient.AddFileAsync(string, string, string, string, string, FileMutationRequest, CancellationToken)"/>
/// or <see cref="IClient.ChangeFileAsync(string, string, string, string, string, FileMutationRequest, CancellationToken)"/>)
/// and adds no lifecycle semantics beyond the Contract Spine. Header values (idempotency key, correlation ID,
/// task ID) are passed through explicitly; see <see cref="CorrelationAndTaskId"/> for parity-preserving sourcing
/// and <see cref="FileUpload.ComputeIdempotencyKey"/> for idempotency-key computation.
/// </remarks>
public static class FoldersFileUploadExtensions
{
    /// <summary>
    /// Uploads in-memory file content using the inline transport, selecting it automatically because the
    /// content is at or below the inline boundary. Content above the boundary throws
    /// <see cref="FileUploadStreamingRequiredException"/> directing the caller to the streamed path.
    /// </summary>
    /// <param name="client">The typed Folders client.</param>
    /// <param name="descriptor">The file mutation descriptor.</param>
    /// <param name="content">The decoded file content.</param>
    /// <param name="idempotencyKey">The caller-sourced idempotency key (never SDK-generated).</param>
    /// <param name="correlationId">The resolved correlation ID.</param>
    /// <param name="taskId">The caller-provided task ID (required, never SDK-generated).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The accepted-command acknowledgement.</returns>
    public static Task<AcceptedCommand> UploadFileAsync(
        this IClient client,
        FileUploadDescriptor descriptor,
        ReadOnlyMemory<byte> content,
        string idempotencyKey,
        string correlationId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(descriptor);

        FileMutationRequest request = FileUpload.BuildInlineFileMutation(
            content,
            descriptor.MediaType,
            descriptor.PathMetadata,
            descriptor.OperationId,
            descriptor.FileOperationKind,
            descriptor.ContentMediaType,
            descriptor.ContentHashReference);

        return InvokeFileMutationAsync(client, descriptor, request, idempotencyKey, correlationId, taskId, cancellationToken);
    }

    /// <summary>
    /// Uploads file content from a stream using the inline transport. The stream is read up to one byte past
    /// the inline boundary; if more content remains the method throws <see cref="FileUploadStreamingRequiredException"/>
    /// without buffering the whole stream.
    /// </summary>
    /// <param name="client">The typed Folders client.</param>
    /// <param name="descriptor">The file mutation descriptor.</param>
    /// <param name="content">The content stream.</param>
    /// <param name="idempotencyKey">The caller-sourced idempotency key (never SDK-generated).</param>
    /// <param name="correlationId">The resolved correlation ID.</param>
    /// <param name="taskId">The caller-provided task ID (required, never SDK-generated).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The accepted-command acknowledgement.</returns>
    public static async Task<AcceptedCommand> UploadFileAsync(
        this IClient client,
        FileUploadDescriptor descriptor,
        Stream content,
        string idempotencyKey,
        string correlationId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(content);

        ReadOnlyMemory<byte> inlineContent = await ReadInlineCandidateAsync(content, cancellationToken).ConfigureAwait(false);
        return await UploadFileAsync(client, descriptor, inlineContent, idempotencyKey, correlationId, taskId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads previously staged content using the streamed transport, built from staging evidence.
    /// </summary>
    /// <param name="client">The typed Folders client.</param>
    /// <param name="descriptor">The file mutation descriptor.</param>
    /// <param name="stagingEvidence">Evidence describing the out-of-band staged content.</param>
    /// <param name="idempotencyKey">The caller-sourced idempotency key (never SDK-generated).</param>
    /// <param name="correlationId">The resolved correlation ID.</param>
    /// <param name="taskId">The caller-provided task ID (required, never SDK-generated).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The accepted-command acknowledgement.</returns>
    public static Task<AcceptedCommand> UploadStreamedFileAsync(
        this IClient client,
        FileUploadDescriptor descriptor,
        FileStreamStagingEvidence stagingEvidence,
        string idempotencyKey,
        string correlationId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(stagingEvidence);

        FileMutationRequest request = FileUpload.BuildStreamedFileMutation(
            stagingEvidence,
            descriptor.MediaType,
            descriptor.PathMetadata,
            descriptor.OperationId,
            descriptor.FileOperationKind);

        return InvokeFileMutationAsync(client, descriptor, request, idempotencyKey, correlationId, taskId, cancellationToken);
    }

    private static async Task<AcceptedCommand> InvokeFileMutationAsync(
        IClient client,
        FileUploadDescriptor descriptor,
        FileMutationRequest request,
        string idempotencyKey,
        string correlationId,
        string taskId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        try
        {
            return descriptor.FileOperationKind switch
            {
                FileMutationRequestFileOperationKind.Add => await client
                    .AddFileAsync(descriptor.FolderId, descriptor.WorkspaceId, idempotencyKey, correlationId, taskId, request, cancellationToken)
                    .ConfigureAwait(false),
                FileMutationRequestFileOperationKind.Change => await client
                    .ChangeFileAsync(descriptor.FolderId, descriptor.WorkspaceId, idempotencyKey, correlationId, taskId, request, cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(descriptor),
                    "File uploads support only Add and Change file operation kinds."),
            };
        }
        catch (HexalithFoldersApiException apiException) when (apiException.StatusCode == 413)
        {
            // Server signalled inline-over-boundary via 413 + X-Hexalith-Retry-Transport: stream.
            // Surface the transport-substitution requirement without echoing the byte limit or content.
            throw new FileUploadStreamingRequiredException(apiException);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadInlineCandidateAsync(Stream content, CancellationToken cancellationToken)
    {
        // Read at most one byte past the boundary so an over-boundary stream is detected without buffering
        // unbounded content into memory.
        int capacity = FileUpload.InlineTransportBoundaryBytes + 1;
        byte[] buffer = new byte[capacity];
        int total = 0;

        while (total < capacity)
        {
            int read = await content.ReadAsync(buffer.AsMemory(total, capacity - total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > FileUpload.InlineTransportBoundaryBytes)
        {
            throw new FileUploadStreamingRequiredException();
        }

        return new ReadOnlyMemory<byte>(buffer, 0, total);
    }
}
