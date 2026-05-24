using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubCredentialModeValidator
{
    public static bool TryGetSupportedMode(
        IReadOnlyList<ProviderCredentialMode> credentialModes,
        out ProviderCredentialMode mode,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(credentialModes);

        ProviderCredentialMode[] distinctModes = credentialModes
            .Where(static x => x != ProviderCredentialMode.None)
            .Distinct()
            .Order()
            .ToArray();

        mode = ProviderCredentialMode.None;
        failureReason = null;

        if (distinctModes.Length == 0)
        {
            failureReason = "missing_github_credential_mode";
            return false;
        }

        if (distinctModes.Length > 1)
        {
            failureReason = "ambiguous_github_credential_mode";
            return false;
        }

        mode = distinctModes[0];
        if (mode is ProviderCredentialMode.AppInstallationReference or ProviderCredentialMode.UserDelegatedReference)
        {
            return true;
        }

        failureReason = "unsupported_github_credential_mode";
        return false;
    }
}

