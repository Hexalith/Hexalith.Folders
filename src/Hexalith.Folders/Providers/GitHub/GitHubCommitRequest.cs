using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCommitRequest(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    string StagedTreeSha,
    string CommitMessage,
    string SafeTargetFingerprint,
    string ReconciliationReference);
