using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubFileChangeSetRequest(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    IReadOnlyList<ProviderGitResolvedFileChange> Changes,
    string SafeTargetFingerprint,
    string ReconciliationReference);
