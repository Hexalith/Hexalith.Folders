using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextQueryPath(
    PathMetadata Path,
    string PathMetadataDigest,
    string PathPolicyClass,
    string Sensitivity,
    string Redaction);
