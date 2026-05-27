using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextItem(
    PathMetadata? Path,
    string Kind,
    long? ByteLength,
    string Sensitivity,
    string Redaction);
