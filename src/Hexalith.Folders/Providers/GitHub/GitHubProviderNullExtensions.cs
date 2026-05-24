namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubProviderNullExtensions
{
    public static T ShouldNotBeNullForProvider<T>(this T? value)
        where T : class
        => value ?? throw new InvalidOperationException("GitHub provider seam returned an inconsistent result shape.");
}

