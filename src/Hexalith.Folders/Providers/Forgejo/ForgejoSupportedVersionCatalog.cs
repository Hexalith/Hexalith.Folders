namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoSupportedVersionCatalog
{
    private static readonly ForgejoSupportedVersionEntry[] Entries =
    [
        new(
            "16.0.3",
            "16.0",
            "latest-stable",
            "https://code.forgejo.org/forgejo/forgejo/raw/tag/v16.0.3/templates/swagger/v1_json.tmpl",
            "tests/contracts/forgejo/16.0.3/swagger.v1.json",
            "supported",
            "platform-engineering",
            "folders-provider-maintainers",
            "2026-08-26",
            "sha256:5047480080ab408814b3a1b13db5fb17aac084831556be17b6e28321afb2c332",
            "sha256:57f33450842ec7333acb7195f51d3e3ab35ff0f1234846fc7ba99a196d2370f9",
            "sha256:51a0f06778162e9559c1b6ec0d07459aca6d90ef64d496654e805c3170bd1d44"),
        new(
            "15.0.7",
            "15.0",
            "long-term-support",
            "https://code.forgejo.org/forgejo/forgejo/raw/tag/v15.0.7/templates/swagger/v1_json.tmpl",
            "tests/contracts/forgejo/15.0.7/swagger.v1.json",
            "supported",
            "platform-engineering",
            "folders-provider-maintainers",
            "2026-08-26",
            "sha256:718e520e48dcdcb6796b11ecfd6b524f2d762edd8debfbba912846a06578a5d2",
            "sha256:534bdc6e7c4ad2f84a349b889eb8a8d4f31e947fb03fa9b162c2cee9179a3dd8",
            "sha256:49038093b5439e0331b6003f94c0ec49e7326f2ca520a06c2e73c91da05718c6"),
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
