namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>Owner-configured provider profiles; requests select references and cannot supply target values.</summary>
public sealed class FoldersProviderBindingOptions
{
    /// <summary>The host configuration section for provider binding profiles.</summary>
    public const string SectionName = "Folders:ProviderBindings";
    /// <summary>Gets or sets profiles keyed by their public opaque capability-profile reference.</summary>
    public Dictionary<string, ProviderBindingProfile> Profiles { get; set; } = new(StringComparer.Ordinal);
}
