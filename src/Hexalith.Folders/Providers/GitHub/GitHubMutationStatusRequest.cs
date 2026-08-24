using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubMutationStatusRequest(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    string ExpectedCommitSha);
