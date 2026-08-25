namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitOperationResolvedTarget(
    string Owner,
    string RepositoryName,
    string RefName,
    string ExpectedHeadSha)
{
    public override string ToString() => nameof(ProviderGitOperationResolvedTarget);

    public bool TryValidate(out string? failureReason)
    {
        failureReason = null;
        if (!IsBoundedValue(Owner, 256)
            || !IsBoundedValue(RepositoryName, 256)
            || !IsBoundedValue(RefName, 256)
            || !IsValidBranchRef(RefName)
            || !IsGitObjectId(ExpectedHeadSha))
        {
            failureReason = "resolved_provider_operation_target_malformed";
            return false;
        }

        return true;
    }

    internal static bool IsGitObjectId(string? value)
        => value is { Length: 40 or 64 }
            && value.All(static character => char.IsAsciiDigit(character)
                || character is >= 'a' and <= 'f'
                || character is >= 'A' and <= 'F');

    internal static bool IsValidBranchRef(string? value)
    {
        if (value is null
            || !IsBoundedValue(value, 256)
            || !value.StartsWith("heads/", StringComparison.Ordinal))
        {
            return false;
        }

        string branch = value["heads/".Length..];
        string[] segments = branch.Split('/');
        return branch.Length > 0
            && branch != "@"
            && branch[0] is not '/' and not '.'
            && branch[^1] is not '/' and not '.'
            && segments.All(static segment => segment.Length > 0
                && segment[0] != '.'
                && !segment.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            && !branch.Contains("//", StringComparison.Ordinal)
            && !branch.Contains("..", StringComparison.Ordinal)
            && !branch.Contains("@{", StringComparison.Ordinal)
            && !branch.Any(static character => char.IsControl(character)
                || char.IsWhiteSpace(character)
                || character is '~' or '^' or ':' or '?' or '*' or '[' or '\\');
    }

    private static bool IsBoundedValue(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);
}
