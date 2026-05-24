namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationProviderBindingPolicy(
    string? PolicyRef,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static OrganizationProviderBindingPolicy Empty { get; } =
        new(null, new Dictionary<string, string>(StringComparer.Ordinal));
}
