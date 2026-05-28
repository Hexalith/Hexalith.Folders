namespace Hexalith.Folders.UI.Configuration;

public sealed class FoldersAuthenticationOptions
{
    public const string SectionName = "Folders:Authentication";

    public const string OidcMode = "oidc";

    public const string HermeticTestMode = "hermetic-test";

    public string? Authority { get; set; }

    public string ClientId { get; set; } = "hexalith-folders";

    public bool RequireHttpsMetadata { get; set; }

    public string Mode { get; set; } = OidcMode;
}
