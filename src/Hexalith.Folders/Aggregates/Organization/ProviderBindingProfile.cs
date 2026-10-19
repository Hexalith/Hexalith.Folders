namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>Owner-configured metadata selected by an existing capability-profile reference.</summary>
public sealed class ProviderBindingProfile
{
    /// <summary>Gets or sets the tenant that owns this profile.</summary>
    public string ManagedTenantId { get; set; } = string.Empty;
    /// <summary>Gets or sets the provider family that may consume the profile.</summary>
    public string ProviderKind { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact repository target policy.</summary>
    public OrganizationProviderBindingPolicy NamingPolicy { get; set; } = OrganizationProviderBindingPolicy.Empty;
    /// <summary>Gets or sets the exact branch/ref policy.</summary>
    public OrganizationProviderBindingPolicy BranchPolicy { get; set; } = OrganizationProviderBindingPolicy.Empty;
}
