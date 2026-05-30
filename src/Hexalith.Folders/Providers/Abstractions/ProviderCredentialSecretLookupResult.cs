namespace Hexalith.Folders.Providers.Abstractions;

internal enum ProviderCredentialSecretLookupStatus
{
    Found,
    Missing,
    Denied,
    Unavailable,
}

internal sealed record ProviderCredentialSecretLookupResult(
    ProviderCredentialSecretLookupStatus Status,
    IReadOnlyDictionary<string, string> Values,
    TimeSpan? RetryAfter = null)
{
    public static ProviderCredentialSecretLookupResult Found(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(ProviderCredentialSecretLookupStatus.Found, values);
    }

    public static ProviderCredentialSecretLookupResult Missing()
        => new(ProviderCredentialSecretLookupStatus.Missing, Empty());

    public static ProviderCredentialSecretLookupResult Denied()
        => new(ProviderCredentialSecretLookupStatus.Denied, Empty());

    public static ProviderCredentialSecretLookupResult Unavailable(TimeSpan? retryAfter = null)
        => new(ProviderCredentialSecretLookupStatus.Unavailable, Empty(), retryAfter);

    private static IReadOnlyDictionary<string, string> Empty()
        => new Dictionary<string, string>(StringComparer.Ordinal);
}
