namespace Hexalith.Folders.Providers.Abstractions;

public sealed class FoldersProviderCredentialOptions
{
    public const string SectionName = "Folders:ProviderCredentials";

    public string SecretStoreName { get; set; } = "folders-provider-credentials";

    public string AccessTokenKey { get; set; } = "access_token";
}
