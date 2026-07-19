namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderRepositoryResolvedTarget(
    string Owner,
    string RepositoryName,
    ProviderRepositoryVisibility Visibility,
    string DefaultBranch,
    string SelectedRef,
    bool RequireProtectedRef,
    bool RequireContentsPermission,
    bool RequireAdministrationPermission,
    string? ExpectedCanonicalRepositoryId,
    bool EquivalentExistingAuthorized,
    ProviderRepositoryRefKind SelectedRefKind = ProviderRepositoryRefKind.Branch)
{
    public override string ToString() => nameof(ProviderRepositoryResolvedTarget);

    public bool TryValidate(out string? failureReason)
    {
        failureReason = null;
        if (!IsBoundedValue(Owner)
            || !IsBoundedValue(RepositoryName)
            || !IsBoundedValue(DefaultBranch)
            || !IsBoundedValue(SelectedRef)
            || !Enum.IsDefined(Visibility)
            || !Enum.IsDefined(SelectedRefKind)
            || (ExpectedCanonicalRepositoryId is not null && !IsBoundedValue(ExpectedCanonicalRepositoryId)))
        {
            failureReason = "resolved_provider_target_malformed";
            return false;
        }

        return true;
    }

    private static bool IsBoundedValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);
}
