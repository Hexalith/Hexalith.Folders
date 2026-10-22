namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Server-issued evidence describing exact bytes already persisted at the authoritative content
/// staging boundary, used to build a streamed (<c>PutFileStream</c>) file mutation.
/// </summary>
/// <remarks>
/// These values must come from EventStore's authenticated content capability. A descriptor is never proof
/// by itself: the server verifies persisted bytes, task/tenant scope, length, hash and media before admission.
/// Use the raw-stream HTTP convenience when content has not already been ingested. None of these fields
/// is a local path or raw content; receipt possession grants no read or mutation authority.
/// </remarks>
public sealed record FileStreamStagingEvidence
{
    /// <summary>
    /// Gets the tenant/task-scoped authoritative opaque receipt (opaque identifier; never a local path).
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
