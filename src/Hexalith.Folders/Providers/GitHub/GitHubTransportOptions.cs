namespace Hexalith.Folders.Providers.GitHub;

/// <summary>Configures deadlines for requests made through the GitHub API client.</summary>
internal sealed class GitHubTransportOptions
{
    /// <summary>The configuration section for GitHub API transport settings.</summary>
    internal const string SectionName = "Folders:Providers:GitHub";

    /// <summary>The default per-request deadline.</summary>
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets an Octokit request deadline between 1 and 120 seconds.</summary>
    public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;
}
