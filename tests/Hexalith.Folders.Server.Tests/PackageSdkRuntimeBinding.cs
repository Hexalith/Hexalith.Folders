using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

using NuGet.Packaging;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Proves the selected source or restored-package authority for each loaded SDK binary.</summary>
internal static class PackageSdkRuntimeBinding
{
    /// <summary>Checks the authority recorded by the evaluated test build; identity failures never fall back.</summary>
    internal static void Verify(Assembly assembly)
    {
        Assembly owner = typeof(PackageSdkRuntimeBinding).Assembly;
        string Metadata(string key) => owner.GetCustomAttributes<AssemblyMetadataAttribute>().Single(value => value.Key == key).Value
            ?? throw new InvalidOperationException("Missing SDK identity metadata.");
        byte[] loaded = File.ReadAllBytes(assembly.Location);
        string name = assembly.GetName().Name!;
        Guid mvid = ReadMvid(loaded);
        Require(mvid == assembly.ManifestModule.ModuleVersionId, "Loaded SDK MVID differs from its file.");
        string authority = Metadata("FoldersSdkIdentityAuthority");
#if FOLDERS_OWNER_METADATA_SOURCE
        Require(authority == "source" && Metadata("FoldersSdkConfiguration") == "Debug", "Source identity requires the evaluated Debug source build.");
        string sourcePath = Path.Combine(Metadata("FoldersSdkSourceRoot"), "src", name, "bin", "Debug", "net11.0", name + ".dll");
        byte[] source = File.ReadAllBytes(sourcePath);
        Require(SHA256.HashData(loaded).SequenceEqual(SHA256.HashData(source)) && ReadMvid(source) == mvid, "SDK source bytes or MVID differ.");
        Console.WriteLine("COMPONENT_RUNTIME_BINDING " + JsonSerializer.Serialize(new
        { name, authority, version = assembly.GetName().Version!.ToString(), sha256 = Convert.ToHexString(SHA256.HashData(loaded)), mvid }));
#else
        Require(authority == "package", "Package identity requires evaluated package metadata.");
        Console.WriteLine("COMPONENT_RUNTIME_BINDING " + VerifyPackagePayload(name, loaded, mvid,
            Path.Combine(AppContext.BaseDirectory, "sdk-binding.project.assets.json"),
            Path.Combine(AppContext.BaseDirectory, owner.GetName().Name + ".deps.json"), Metadata("FoldersSdkAssetsSha256")));
#endif
    }

    /// <summary>Resolves one runtime asset from dependency and restore metadata, then binds its archive and payload bytes.</summary>
    internal static string VerifyPackagePayload(string name, byte[] loaded, Guid loadedMvid, string assetsPath, string depsPath, string expectedAssetsSha256)
    {
        byte[] assetsBytes = File.ReadAllBytes(assetsPath);
        Require(Convert.ToHexString(SHA256.HashData(assetsBytes)) == expectedAssetsSha256, "Restore metadata differs from the evaluated build identity.");
        using JsonDocument assets = JsonDocument.Parse(assetsBytes);
        using JsonDocument deps = JsonDocument.Parse(File.ReadAllBytes(depsPath));
        RejectDuplicateProperties(assets.RootElement);
        RejectDuplicateProperties(deps.RootElement);
        JsonElement dependency = deps.RootElement;
        string runtimeTarget = dependency.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        JsonElement target = dependency.GetProperty("targets").GetProperty(runtimeTarget);
        string fileName = name + ".dll";
        var matches = target.EnumerateObject().SelectMany(library => library.Value.TryGetProperty("runtime", out JsonElement runtime)
            ? runtime.EnumerateObject().Where(asset => Path.GetFileName(asset.Name) == fileName).Select(asset => (Library: library.Name, Asset: asset.Name))
            : []).ToArray();
        Require(matches.Length == 1, "SDK runtime dependency is missing or ambiguous.");
        string libraryKey = matches[0].Library;
        string assetPath = matches[0].Asset;
        string[] identity = libraryKey.Split('/');
        Require(identity.Length == 2 && identity.All(value => !string.IsNullOrWhiteSpace(value)), "Invalid package identity.");
        JsonElement dependencyLibrary = dependency.GetProperty("libraries").GetProperty(libraryKey);
        Require(dependencyLibrary.GetProperty("type").GetString() == "package", "Runtime dependency is not a package.");
        JsonElement restore = assets.RootElement;
        JsonElement library = restore.GetProperty("libraries").GetProperty(libraryKey);
        Require(library.GetProperty("type").GetString() == "package", "Restore dependency is not a package.");
        string restoreHash = library.GetProperty("sha512").GetString()!;
        byte[] expectedHash = Convert.FromBase64String(restoreHash);
        Require(expectedHash.Length == 64, "Restore content identity is invalid.");
        string dependencyHash = dependencyLibrary.GetProperty("sha512").GetString()!;
        Require(dependencyHash == "sha512-" + restoreHash || dependencyHash == restoreHash, "Dependency and restore content identities differ.");
        string packagePath = library.GetProperty("path").GetString()!;
        Require(packagePath == identity[0].ToLowerInvariant() + "/" + identity[1].ToLowerInvariant(), "Restore package path differs from resolved identity.");
        Require(dependencyLibrary.GetProperty("path").GetString() == packagePath, "Dependency and restore package paths differ.");
        string targetFramework = restore.GetProperty("project").GetProperty("restore").GetProperty("originalTargetFrameworks").EnumerateArray().Single().GetString()!;
        JsonElement framework = restore.GetProperty("project").GetProperty("frameworks").GetProperty(targetFramework);
        string targetAlias = framework.GetProperty("targetAlias").GetString()!;
        // SDK assets use the target alias, whereas .deps uses its full framework moniker.
        Require(targetAlias == targetFramework && targetFramework.StartsWith("net", StringComparison.Ordinal)
            && runtimeTarget == ".NETCoreApp,Version=v" + targetFramework[3..], "Restore and dependency runtime targets differ.");
        JsonElement restoreTarget = restore.GetProperty("targets").GetProperty(targetFramework);
        JsonElement runtimeAssets = restoreTarget.GetProperty(libraryKey).GetProperty("runtime");
        Require(runtimeAssets.EnumerateObject().Count(asset => Path.GetFileName(asset.Name) == fileName) == 1 && runtimeAssets.TryGetProperty(assetPath, out _), "Restore and dependency runtime assets differ.");
        Require(library.GetProperty("files").EnumerateArray().Count(file => file.GetString() == assetPath) == 1, "Restored package inventory omits the runtime asset.");
        Require(!Path.IsPathRooted(packagePath) && !packagePath.Split('/').Any(part => part is ".." or "." or ""), "Invalid restored package path.");
        string archiveName = identity[0].ToLowerInvariant() + "." + identity[1].ToLowerInvariant() + ".nupkg";
        string[] archives = restore.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, packagePath, archiveName)).Where(File.Exists).ToArray();
        Require(archives.Length == 1, "Restored package archive is missing or ambiguous.");
        byte[] archiveBytes = File.ReadAllBytes(archives[0]);
        string archiveSha512 = Convert.ToBase64String(SHA512.HashData(archiveBytes));
        Require(File.ReadAllText(archives[0] + ".sha512").Trim() == archiveSha512, "Package archive differs from its restored raw archive hash.");
        VerifyArchiveContent(archiveBytes, restoreHash);
        using var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
        ZipArchiveEntry[] entries = archive.Entries.Where(entry => entry.FullName == assetPath).ToArray();
        Require(entries.Length == 1, "Package runtime payload is missing or ambiguous.");
        ZipArchiveEntry[] nuspecs = archive.Entries.Where(entry => !entry.FullName.Contains('/') && entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).ToArray();
        Require(nuspecs.Length == 1, "Package manifest is missing or ambiguous.");
        using (Stream manifest = nuspecs[0].Open())
        {
            XElement metadata = XDocument.Load(manifest).Root!.Elements().Single(element => element.Name.LocalName == "metadata");
            Require(metadata.Elements().Single(element => element.Name.LocalName == "id").Value.Equals(identity[0], StringComparison.OrdinalIgnoreCase)
                && metadata.Elements().Single(element => element.Name.LocalName == "version").Value == identity[1], "Package manifest identity differs from resolution.");
        }
        using Stream payload = entries[0].Open();
        using var bytes = new MemoryStream();
        payload.CopyTo(bytes);
        byte[] packaged = bytes.ToArray();
        Require(SHA256.HashData(loaded).SequenceEqual(SHA256.HashData(packaged)) && ReadMvid(packaged) == loadedMvid, "Loaded SDK bytes or MVID differ from the package payload.");
        return JsonSerializer.Serialize(new { name, authority = "package", packageId = identity[0], packageVersion = identity[1], runtimeAsset = assetPath,
            packageSha512 = restoreHash, archiveSha512, sha256 = Convert.ToHexString(SHA256.HashData(loaded)), mvid = loadedMvid });
    }

    /// <summary>Recomputes NuGet's canonical SHA-512 from archive bytes, including signed-package record adjustments.</summary>
    internal static void VerifyArchiveContent(byte[] archiveBytes, string expectedContentHash)
    {
        using var reader = new PackageArchiveReader(new MemoryStream(archiveBytes));
        Require(reader.GetContentHash(CancellationToken.None) == expectedContentHash, "Package archive differs from restore content identity.");
    }

    private static Guid ReadMvid(byte[] bytes)
    {
        using var pe = new PEReader(new MemoryStream(bytes));
        MetadataReader metadata = pe.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "Ambiguous metadata property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }
}
