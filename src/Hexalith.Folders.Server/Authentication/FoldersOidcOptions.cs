namespace Hexalith.Folders.Server.Authentication;

public sealed class FoldersOidcOptions
{
    public const string SectionName = "Folders:Authentication";

    public string? Authority { get; set; }

    public string? MetadataAddress { get; set; }

    public string? Audience { get; set; }

    public string? ValidIssuer { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;
}
