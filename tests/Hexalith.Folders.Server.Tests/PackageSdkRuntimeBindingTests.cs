using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using NuGet.Packaging;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Adversarial restore/dependency/archive controls for runtime SDK provenance.</summary>
public sealed class PackageSdkRuntimeBindingTests
{
    /// <summary>Validates exact runtime identity and refuses independently altered provenance inputs.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("archive")]
    [InlineData("payload")]
    [InlineData("mvid")]
    [InlineData("missing-archive")]
    [InlineData("ambiguous-archive")]
    [InlineData("missing-asset")]
    [InlineData("ambiguous-runtime")]
    [InlineData("restore-hash")]
    [InlineData("dependency-hash")]
    [InlineData("dependency-version")]
    [InlineData("duplicate-metadata")]
    [InlineData("manifest")]
    [InlineData("missing-assets")]
    [InlineData("target")]
    [InlineData("altered-assets")]
    [InlineData("ambiguous-payload")]
    [InlineData("missing-payload")]
    [InlineData("missing-manifest")]
    [InlineData("missing-deps")]
    [InlineData("non-package")]
    [InlineData("package-id")]
    public void PackageIdentityBindsEveryInput(string fault)
    {
        string root = Path.Combine(Path.GetTempPath(), "folders-package-binding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string name = "Hexalith.Folders.Server.Tests";
            const string key = "Fixture.Sdk/1.2.3";
            const string asset = "lib/net11.0/Hexalith.Folders.Server.Tests.dll";
            const string packagePath = "fixture.sdk/1.2.3";
            Assembly assembly = typeof(PackageSdkRuntimeBindingTests).Assembly;
            byte[] loaded = File.ReadAllBytes(assembly.Location);
            Guid mvid = assembly.ManifestModule.ModuleVersionId;
            string packageRoot = Path.Combine(root, "packages");
            string package = Path.Combine(packageRoot, packagePath);
            Directory.CreateDirectory(package);
            string archivePath = Path.Combine(package, "fixture.sdk.1.2.3.nupkg");
            using (var archive = new ZipArchive(File.Create(archivePath), ZipArchiveMode.Create))
            {
                if (fault != "missing-payload")
                {
                    using Stream stream = archive.CreateEntry(asset).Open();
                    stream.Write(loaded);
                }
                if (fault == "ambiguous-payload")
                {
                    using Stream stream = archive.CreateEntry(asset).Open();
                    stream.Write(loaded);
                }
                if (fault != "missing-manifest")
                {
                    using var writer = new StreamWriter(archive.CreateEntry("Fixture.Sdk.nuspec").Open());
                    writer.Write("<package><metadata><id>" + (fault == "package-id" ? "Other.Sdk" : "Fixture.Sdk") + "</id><version>" + (fault == "manifest" ? "9.9.9" : "1.2.3") + "</version></metadata></package>");
                }
            }
            string hash = Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(archivePath)));
            File.WriteAllText(archivePath + ".sha512", hash);
            var runtime = new JsonObject { [asset] = new JsonObject() };
            var dependencies = new JsonObject
            {
                ["runtimeTarget"] = new JsonObject { ["name"] = ".NETCoreApp,Version=v11.0" },
                ["targets"] = new JsonObject { [".NETCoreApp,Version=v11.0"] = new JsonObject { [key] = new JsonObject { ["runtime"] = runtime } } },
                ["libraries"] = new JsonObject { [key] = new JsonObject { ["type"] = "package", ["sha512"] = "sha512-" + hash, ["path"] = packagePath } },
            };
            var assets = new JsonObject
            {
                ["targets"] = new JsonObject { ["net11.0"] = new JsonObject { [key] = new JsonObject { ["runtime"] = runtime.DeepClone() } } },
                ["libraries"] = new JsonObject { [key] = new JsonObject { ["type"] = "package", ["sha512"] = hash, ["path"] = packagePath, ["files"] = new JsonArray(asset) } },
                ["packageFolders"] = new JsonObject { [packageRoot] = new JsonObject() },
                ["project"] = new JsonObject
                {
                    ["restore"] = new JsonObject { ["originalTargetFrameworks"] = new JsonArray("net11.0"), ["frameworks"] = new JsonObject { ["net11.0"] = new JsonObject { ["targetAlias"] = "net11.0" } } },
                    ["frameworks"] = new JsonObject { ["net11.0"] = new JsonObject { ["targetAlias"] = "net11.0" } },
                },
            };
            if (fault == "archive") File.AppendAllText(archivePath, "tamper");
            if (fault == "payload") loaded[^1] ^= 1;
            if (fault == "mvid") mvid = Guid.Empty;
            if (fault == "missing-archive") File.Delete(archivePath);
            if (fault == "ambiguous-archive")
            {
                string other = Path.Combine(root, "other");
                Directory.CreateDirectory(Path.Combine(other, packagePath));
                File.Copy(archivePath, Path.Combine(other, packagePath, Path.GetFileName(archivePath)));
                assets["packageFolders"]![other] = new JsonObject();
            }
            if (fault == "missing-asset") assets["targets"]!["net11.0"]![key]!["runtime"] = new JsonObject();
            if (fault == "ambiguous-runtime") dependencies["targets"]![".NETCoreApp,Version=v11.0"]!["Other/1.2.3"] = new JsonObject { ["runtime"] = runtime.DeepClone() };
            if (fault == "non-package") dependencies["libraries"]![key]!["type"] = "project";
            if (fault == "restore-hash") assets["libraries"]![key]!["sha512"] = Convert.ToBase64String(new byte[64]);
            if (fault == "dependency-hash") dependencies["libraries"]![key]!["sha512"] = "sha512-" + Convert.ToBase64String(new byte[64]);
            if (fault == "dependency-version") dependencies["libraries"]![key]!["path"] = "fixture.sdk/9.9.9";
            if (fault == "target") dependencies["runtimeTarget"]!["name"] = ".NETCoreApp,Version=v10.0";
            string assetsPath = Path.Combine(root, "project.assets.json");
            string depsPath = Path.Combine(root, "test.deps.json");
            File.WriteAllText(assetsPath, fault == "duplicate-metadata" ? assets.ToJsonString().Replace("\"targets\":", "\"targets\":{},\"targets\":", StringComparison.Ordinal) : assets.ToJsonString());
            File.WriteAllText(depsPath, dependencies.ToJsonString());
            string expectedAssetsHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assetsPath)));
            if (fault == "missing-assets") File.Delete(assetsPath);
            if (fault == "missing-deps") File.Delete(depsPath);
            if (fault == "altered-assets") File.AppendAllText(assetsPath, " ");
            if (fault == "none")
            {
                string proof = PackageSdkRuntimeBinding.VerifyPackagePayload(name, loaded, mvid, assetsPath, depsPath, expectedAssetsHash);
                proof.ShouldContain("Fixture.Sdk");
                proof.ShouldContain(Convert.ToHexString(SHA256.HashData(loaded)));
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => PackageSdkRuntimeBinding.VerifyPackagePayload(name, loaded, mvid, assetsPath, depsPath, expectedAssetsHash));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
    /// <summary>A genuine repository-signed restored package uses canonical content identity and refuses changed payload bytes.</summary>
    [Fact]
    public async Task GenuineSignedPackageContentHashRejectsChangedArchiveAsync()
    {
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sdk-binding.project.assets.json")));
        JsonProperty library = assets.RootElement.GetProperty("libraries").EnumerateObject().Single(value => value.Name.StartsWith("xunit.v3.assert/", StringComparison.Ordinal));
        string packagePath = library.Value.GetProperty("path").GetString()!;
        string archiveName = library.Name.Replace('/', '.').ToLowerInvariant() + ".nupkg";
        string archivePath = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, packagePath, archiveName)).Single(File.Exists);
        byte[] original = File.ReadAllBytes(archivePath);
        string expected = library.Value.GetProperty("sha512").GetString()!;
        using (var reader = new PackageArchiveReader(new MemoryStream(original)))
        {
            (await reader.IsSignedAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        Convert.ToBase64String(SHA512.HashData(original)).ShouldNotBe(expected);
        PackageSdkRuntimeBinding.VerifyArchiveContent(original, expected);
        using var altered = new MemoryStream();
        altered.Write(original);
        using (var archive = new ZipArchive(altered, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry payload = archive.Entries.First(entry => entry.FullName.EndsWith(".dll", StringComparison.Ordinal));
            string name = payload.FullName;
            payload.Delete();
            using Stream replacement = archive.CreateEntry(name).Open();
            replacement.Write(new byte[] { 1, 2, 3 });
        }
        Assert.ThrowsAny<Exception>(() => PackageSdkRuntimeBinding.VerifyArchiveContent(altered.ToArray(), expected));
    }

}
