namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Caller-supplied evidence describing content that has already been staged at the transient content
/// staging boundary, used to build a streamed (<c>PutFileStream</c>) file mutation.
/// </summary>
/// <remarks>
/// The SDK does not stage content; staging is a server/worker concern (Story 4.6). The streamed transport
/// requires observed evidence — descriptor-only requests were rejected in review — so the caller obtains
/// these values from the staging boundary and supplies them here. None of these values is a local path or
/// raw content: <see cref="StagingReference"/> is a tenant-scoped opaque reference and
/// <see cref="ObservedContentHashReference"/> is an opaque <c>hashref_</c> reference.
/// </remarks>
public sealed record FileStreamStagingEvidence
{
    /// <summary>
    /// Gets the tenant-scoped transient content staging reference (opaque identifier; never a local path).
    /// </summary>
    public required string StagingReference { get; init; }

    /// <summary>
    /// Gets the content-hash reference observed at the staging boundary (an opaque <c>hashref_</c> value).
    /// This becomes the request's top-level content-hash reference, which it must match by contract.
    /// </summary>
    public required string ObservedContentHashReference { get; init; }

    /// <summary>
    /// Gets the observed byte length from the staging boundary. Must exceed the inline transport boundary.
    /// </summary>
    public required int ObservedLength { get; init; }
}
