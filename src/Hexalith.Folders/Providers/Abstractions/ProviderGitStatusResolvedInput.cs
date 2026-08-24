namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitStatusResolvedInput(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    string ExpectedCommitSha,
    DateTimeOffset AuthoritativeUnknownOutcomeObservedAt,
    DateTimeOffset AuthoritativeRequestedAt,
    int AuthoritativeCheckNumber)
{
    public override string ToString() => nameof(ProviderGitStatusResolvedInput);
}
