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
            "sha256:fbdfd151ab27ab269a10a97a80d89f81513e7cc24512d730ed63db99fe8301b8",
            9,
            "sha256:25651192cf84359dd57ff6fbfabef9fe735895fbb8105ac58c08cbe134b765c1"),
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
            "sha256:03ce2724a696818ebb25bdf67bb1041ec213500779cbd018783b306d26294150",
            9,
            "sha256:1038a51a2ea831b8e66eef23d0bf3fd12066dfb473dcf98b7a40f2f0d14381ba"),
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
    {
        if (string.IsNullOrWhiteSpace(productVersion))
        {
            return string.Empty;
        }

        string value = productVersion.Trim();
        int buildMetadata = value.IndexOf("+gitea-", StringComparison.Ordinal);
        return buildMetadata > 0
            && value[(buildMetadata + "+gitea-".Length)..] is { Length: > 0 } upstreamVersion
            && upstreamVersion.All(static character => char.IsAsciiDigit(character) || character == '.')
                ? value[..buildMetadata]
                : value;
    }
}
