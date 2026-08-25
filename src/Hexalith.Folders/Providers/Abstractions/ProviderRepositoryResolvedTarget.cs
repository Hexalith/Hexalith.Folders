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
        if (!IsProviderName(Owner)
            || !IsProviderName(RepositoryName)
            || !IsRefValue(DefaultBranch)
            || !IsRefValue(SelectedRef)
            || !Enum.IsDefined(Visibility)
            || !Enum.IsDefined(SelectedRefKind)
            || (ExpectedCanonicalRepositoryId is not null && !IsBoundedValue(ExpectedCanonicalRepositoryId))
            || (EquivalentExistingAuthorized && string.IsNullOrWhiteSpace(ExpectedCanonicalRepositoryId)))
        {
            failureReason = "resolved_provider_target_malformed";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Enforces GitHub's owner/repository name grammar. These values are interpolated into API paths
    /// unescaped, so a path separator, a traversal segment, or a percent-escape here would redirect
    /// the call to an unintended endpoint.
    /// </summary>
    private static bool IsProviderName(string? value)
        => value is { Length: > 0 and <= 100 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            && value[0] is not ('.' or '-')
            && value is not ("." or "..");

    /// <summary>
    /// Enforces a bounded branch/ref grammar. A ref may carry path separators (<c>release/1.0</c>)
    /// but never a traversal segment, an empty segment, or a URL-reserved character.
    /// </summary>
    private static bool IsRefValue(string? value)
        => value is { Length: > 0 and <= 256 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/')
            && !value.Contains("..", StringComparison.Ordinal)
            && !value.Contains("//", StringComparison.Ordinal)
            && value[0] is not ('/' or '.')
            && value[^1] is not ('/' or '.');

    private static bool IsBoundedValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);
}
