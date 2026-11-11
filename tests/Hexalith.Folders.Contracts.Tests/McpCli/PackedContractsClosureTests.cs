using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

public sealed class PackedContractsClosureTests
{
    [Fact]
    public async Task ReleasePackageDependsOnlyOnMcpCliAbstractionsAndTheNetRuntimeAsync()
    {
        string output = Path.Combine(Path.GetTempPath(), "folders-contracts-closure-" + Guid.NewGuid().ToString("N"));
        string artifacts = Path.Combine(output, "artifacts");
        string currentAssets = Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "src/Hexalith.Folders.Contracts/obj/project.assets.json");
        byte[] originalAssets = File.ReadAllBytes(currentAssets);
        Directory.CreateDirectory(output);
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = McpCliContractTestSupport.RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in new[]
            {
                "pack", "src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj",
                "--configuration", "Release", "--artifacts-path", artifacts, "--output", output,
                "-p:UseNuGetDeps=true", "-p:UseHexalithProjectReferences=false",
            })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)!;
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            process.ExitCode.ShouldBe(0, await standardOutput.ConfigureAwait(true) + await standardError.ConfigureAwait(true));
            File.ReadAllBytes(currentAssets).ShouldBe(originalAssets);
            using ZipArchive package = ZipFile.OpenRead(Directory.GetFiles(output, "*.nupkg").Single());
            ZipArchiveEntry nuspec = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            using Stream stream = nuspec.Open();
            XDocument manifest = XDocument.Load(stream);
            manifest.Descendants().Where(node => node.Name.LocalName == "dependency")
                .Select(node => (string)node.Attribute("id")!).ShouldBe(["Hexalith.McpCli.Abstractions"]);
            manifest.Descendants().Where(node => node.Name.LocalName is "frameworkReference" or "frameworkAssembly")
                .Select(node => (string?)node.Attribute("name") ?? (string?)node.Attribute("assemblyName"))
                .ShouldAllBe(name => name == "Microsoft.NETCore.App");

            JsonNode assets = JsonNode.Parse(File.ReadAllText(Directory.EnumerateFiles(artifacts,
                "project.assets.json", SearchOption.AllDirectories).Single()))!;
            assets["project"]!["frameworks"]!["net10.0"]!["frameworkReferences"]!.AsObject()
                .Select(item => item.Key).ShouldBe(["Microsoft.NETCore.App"]);
            assets["libraries"]!.AsObject().Select(item => item.Key)
                .ShouldBe(["Hexalith.McpCli.Abstractions/2.0.1"]);
            ZipArchiveEntry assembly = package.GetEntry("lib/net10.0/Hexalith.Folders.Contracts.dll")!;
            assembly.ShouldNotBeNull();
            using Stream assemblyStream = assembly.Open();
            using var image = new MemoryStream();
            assemblyStream.CopyTo(image);
            image.Position = 0;
            using var reader = new PEReader(image);
            MetadataReader metadata = reader.GetMetadataReader();
            metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .Where(name => !name.StartsWith("System.", StringComparison.Ordinal) && name != "System")
                .ShouldBe(["Hexalith.McpCli.Abstractions"]);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }
}
