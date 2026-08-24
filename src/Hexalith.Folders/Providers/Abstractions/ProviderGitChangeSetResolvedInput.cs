namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitChangeSetResolvedInput(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    IReadOnlyList<ProviderGitResolvedFileChange> Changes)
{
    public override string ToString() => nameof(ProviderGitChangeSetResolvedInput);
}
