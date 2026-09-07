namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRefObservation(
    string FullRef,
    string ObjectType,
    string ObjectSha);
