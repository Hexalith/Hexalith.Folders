namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoSupportedVersionEntry(
    string Version,
    string VersionFamily,
    string SupportClass,
    string SourceUrl,
    string SnapshotPath,
    string ExpectedApiCompatibilityPosture,
    string Owner,
    string Reviewer,
    string DatedSource,
    string SourceArtifactSha256,
    string SnapshotSha256,
    string IntegrityHash);
