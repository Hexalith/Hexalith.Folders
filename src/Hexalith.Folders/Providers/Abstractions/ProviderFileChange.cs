namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Describes one ordered provider-neutral file change using opaque source references.
/// </summary>
/// <param name="OperationReference">The opaque identity of this change.</param>
/// <param name="PathReference">The opaque reference used to resolve the authorized path.</param>
/// <param name="Kind">The requested file effect.</param>
/// <param name="ContentReference">The opaque content reference for add or change operations.</param>
/// <param name="ByteLength">The validated content length, or zero for removal.</param>
/// <param name="MediaType">The validated media type for add or change operations.</param>
public sealed record ProviderFileChange(
    string OperationReference,
    string PathReference,
    ProviderFileChangeKind Kind,
    string? ContentReference,
    long ByteLength,
    string? MediaType);
