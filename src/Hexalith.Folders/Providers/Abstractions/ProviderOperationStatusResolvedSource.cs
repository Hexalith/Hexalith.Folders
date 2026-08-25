namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationStatusResolvedSource(
    ProviderGitOperationResolvedTarget Target,
    string IntendedCommitSha)
{
    public override string ToString() => nameof(ProviderOperationStatusResolvedSource);
}
