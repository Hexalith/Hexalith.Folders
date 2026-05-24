namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoVersionEvidence(
    string ProductVersion,
    string SnapshotVersion,
    string ApiSurfaceVersion,
    string CompatibilityPosture,
    string DriftClassification);
