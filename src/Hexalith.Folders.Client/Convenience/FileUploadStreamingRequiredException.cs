using System;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Signals that file content exceeds the inline transport boundary and must be staged out-of-band and
/// submitted through the streamed transport (see <see cref="FoldersFileUploadExtensions.UploadStreamedFileAsync"/>).
/// </summary>
/// <remarks>
/// This is the client-side equivalent of the REST <c>413 Payload Too Large</c> + <c>X-Hexalith-Retry-Transport: stream</c>
/// contract finalized in Story 4.6. To preserve authorization-before-observation, the message never discloses
/// the configured byte limit, the workspace path, or any file content.
/// </remarks>
public sealed class FileUploadStreamingRequiredException : Exception
{
    private const string DefaultMessage =
        "Inline file upload is not supported for this content; stage the content and retry through the streamed transport.";

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadStreamingRequiredException"/> class.
    /// </summary>
    public FileUploadStreamingRequiredException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadStreamingRequiredException"/> class wrapping the
    /// server response that requested transport substitution.
    /// </summary>
    /// <param name="innerException">The originating transport exception (for example, an HTTP 413 response).</param>
    public FileUploadStreamingRequiredException(Exception innerException)
        : base(DefaultMessage, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadStreamingRequiredException"/> class with a
    /// custom message. Callers must keep the message metadata-only (no byte limits, paths, or content).
    /// </summary>
    /// <param name="message">A metadata-only message.</param>
    public FileUploadStreamingRequiredException(string message)
        : base(message)
    {
    }
}
