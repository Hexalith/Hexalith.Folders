using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

/// <summary>Models the repository REST fields used to validate binding, including the disabled flag.</summary>
internal sealed class GitHubBindingRepository
{
    /// <summary>Gets or sets the canonical repository identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets whether the repository is archived.</summary>
    public bool Archived { get; set; }

    /// <summary>Gets or sets whether GitHub has disabled the repository.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the repository's default branch.</summary>
    public string? DefaultBranch { get; set; }

    /// <summary>Gets or sets the explicit repository visibility when the response includes it.</summary>
    public RepositoryVisibility? Visibility { get; set; }

    /// <summary>Gets or sets the legacy private-repository flag.</summary>
    public bool Private { get; set; }

    /// <summary>Gets or sets the caller's repository permissions.</summary>
    public RepositoryPermissions? Permissions { get; set; }
}
