namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoProviderNullExtensions
{
    public static T ShouldNotBeNullForProvider<T>(this T? value)
        where T : class
        => value ?? throw new InvalidOperationException("Forgejo provider seam returned an inconsistent result shape.");
}
