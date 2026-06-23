using System.Globalization;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingSourceIdentity
{
    public SemanticIndexingSourceIdentity(
        string sourceScheme,
        string sourceAuthority,
        string sourceResourceId)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sourceScheme);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sourceAuthority);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sourceResourceId);

        if (string.Equals(sourceScheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A semantic-indexing source identity must not expose a raw filesystem URI scheme.", nameof(sourceScheme));
        }

        if (sourceResourceId.StartsWith("/", StringComparison.Ordinal) || sourceResourceId.StartsWith("\\", StringComparison.Ordinal) || sourceResourceId.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("A semantic-indexing source identity must use a stable resource id, not a raw filesystem path.", nameof(sourceResourceId));
        }

        // Reject a Windows drive-letter prefix (e.g. "C:/...") that would otherwise slip past the
        // backslash/leading-slash checks above and leak a raw filesystem path as public identity.
        if (sourceResourceId.Length >= 2 && char.IsAsciiLetter(sourceResourceId[0]) && sourceResourceId[1] == ':')
        {
            throw new ArgumentException("A semantic-indexing source identity must use a stable resource id, not a raw filesystem path.", nameof(sourceResourceId));
        }

        SourceScheme = sourceScheme;
        SourceAuthority = sourceAuthority;
        SourceResourceId = sourceResourceId;
    }

    public string SourceScheme { get; init; }

    public string SourceAuthority { get; init; }

    public string SourceResourceId { get; init; }

    public string ToUriString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{SourceScheme}://{SourceAuthority}/{SourceResourceId}");
}
