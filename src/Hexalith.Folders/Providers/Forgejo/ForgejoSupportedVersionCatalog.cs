namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoSupportedVersionCatalog
{
    private static readonly ForgejoSupportedVersionEntry[] Entries =
    [
        new(
            "15.0.2",
            "15.0",
            "latest-stable-lts",
            "https://forgejo.org/releases/",
            "tests/contracts/forgejo/15.0.2/swagger.v1.json",
            "supported",
            "platform-engineering",
            "folders-provider-maintainers",
            "2026-05-20",
            "sha256:e8af1aa23b7b05a49d49261060552d9390c8bdc915676b2d5579db6e788ad0b4"),
        new(
            "14.0.5",
            "14.0",
            "n-1-discontinued-reference",
            "https://forgejo.org/releases/",
            "tests/contracts/forgejo/14.0.5/swagger.v1.json",
            "additive-compatible",
            "platform-engineering",
            "folders-provider-maintainers",
            "2026-05-20",
            "sha256:86685a7022d4641cd49b63f0ec7103d49bc81d8ef9906fd547b1697d991096a3"),
        new(
            "11.0.14",
            "11.0",
            "older-lts",
            "https://forgejo.org/releases/",
            "tests/contracts/forgejo/11.0.14/swagger.v1.json",
            "supported",
            "platform-engineering",
            "folders-provider-maintainers",
            "2026-05-20",
            "sha256:b261134584865b0643241a8999ca75758d964aca85aeed93558d85d8dfc9cf37"),
    ];

    public static IReadOnlyList<ForgejoSupportedVersionEntry> SupportedVersions => Entries;

    public static bool IsSupported(string snapshotVersion)
        => TryFind(snapshotVersion, out _);

    public static bool TryFind(string productVersion, out ForgejoSupportedVersionEntry entry)
    {
        entry = Entries[0];
        string normalizedVersion = NormalizeVersion(productVersion);
        ForgejoSupportedVersionEntry? match = Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, normalizedVersion, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        entry = match;
        return true;
    }

    private static string NormalizeVersion(string productVersion)
        => string.IsNullOrWhiteSpace(productVersion) ? string.Empty : productVersion.Trim();
}
