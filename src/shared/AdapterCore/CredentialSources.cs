namespace Hexalith.Folders.AdapterCore;

/// <summary>Selects the first nonblank credential without evaluating lower-priority sources.</summary>
internal static class CredentialSources
{
    /// <summary>Resolves ordered sources while preserving each surface's whitespace policy.</summary>
    public static string? FirstNonBlank(bool trim, params Func<string?>[] sources)
    {
        foreach (Func<string?> source in sources)
        {
            string? token = source();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return trim ? token.Trim() : token;
            }
        }

        return null;
    }
}
