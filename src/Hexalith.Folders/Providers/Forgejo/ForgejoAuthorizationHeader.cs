namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoAuthorizationHeader(string Scheme, string Parameter)
{
    public static ForgejoAuthorizationHeader FromBearerToken(ForgejoCredentialLease credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential.AccessToken);
        return new("Bearer", credential.AccessToken);
    }
}
