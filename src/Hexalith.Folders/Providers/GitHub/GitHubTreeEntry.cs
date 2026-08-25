namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubTreeEntry(
    string Path,
    string Mode,
    string Type,
    string Sha);
