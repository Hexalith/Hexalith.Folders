namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Describes one ordered file change using only opaque references and safe fingerprints.
/// </summary>
/// <param name="Sequence">The zero-based caller-authoritative order.</param>
/// <param name="Kind">The requested operation.</param>
/// <param name="PathReference">An opaque reference resolved only inside the authorized provider boundary.</param>
/// <param name="SafePathFingerprint">A metadata-safe path fingerprint.</param>
/// <param name="ContentReference">An opaque content reference for add or change operations.</param>
/// <param name="SafeContentFingerprint">A metadata-safe content fingerprint for add or change operations.</param>
/// <param name="ContentType">The validated content type.</param>
public sealed record ProviderOrderedFileChange(
    int Sequence,
    ProviderFileChangeKind Kind,
    string PathReference,
    string SafePathFingerprint,
    string? ContentReference,
    string? SafeContentFingerprint,
    ProviderFileContentType ContentType = ProviderFileContentType.RegularFile)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderOrderedFileChange);
}
