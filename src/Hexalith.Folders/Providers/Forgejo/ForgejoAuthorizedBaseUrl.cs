namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoAuthorizedBaseUrl
{
    private static readonly HashSet<string> TokenQueryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "access_token",
    };

    public static bool TryCanonicalize(
        string? authorizedBaseUrl,
        out Uri canonicalBaseUri,
        out string? failureReason)
    {
        canonicalBaseUri = new Uri("https://forgejo.invalid/");
        failureReason = null;

        if (string.IsNullOrWhiteSpace(authorizedBaseUrl))
        {
            failureReason = "missing_forgejo_authorized_base_url";
            return false;
        }

        if (!Uri.TryCreate(authorizedBaseUrl.Trim(), UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            failureReason = "forgejo_base_url_invalid";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            failureReason = "forgejo_base_url_userinfo_rejected";
            return false;
        }

        if (uri.Query.Length > 0)
        {
            string[] queryParts = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            if (queryParts.Any(static part => TokenQueryNames.Contains(part.Split('=', 2)[0])))
            {
                failureReason = "forgejo_base_url_token_query_rejected";
                return false;
            }
        }

        UriBuilder builder = new(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty,
        };

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        canonicalBaseUri = builder.Uri;
        return true;
    }

    public static bool IsSameOrigin(Uri expectedBaseUri, Uri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(expectedBaseUri);
        ArgumentNullException.ThrowIfNull(redirectUri);

        return string.Equals(expectedBaseUri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedBaseUri.Host, redirectUri.Host, StringComparison.OrdinalIgnoreCase)
            && expectedBaseUri.Port == redirectUri.Port;
    }
}
