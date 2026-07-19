namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed record RecordedGitHubHttpRequest(
    HttpMethod Method,
    Uri RequestUri,
    IReadOnlyDictionary<string, string[]> Headers,
    string? Body);
