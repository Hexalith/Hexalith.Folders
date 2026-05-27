namespace Hexalith.Folders.Aggregates.Folder;

public sealed record PathMetadata(
    string NormalizedPath,
    string DisplayName,
    string PathPolicyClass,
    string UnicodeNormalization);
