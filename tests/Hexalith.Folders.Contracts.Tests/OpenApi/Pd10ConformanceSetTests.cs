using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class Pd10ConformanceSetTests
{
    private const string ManifestPath = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] IndependentlyRequiredPaths =
    [
        ".github/workflows/ci.yml",
        ".github/workflows/contract-spine.yml",
        "scripts/generate-pd10-v2-conformance-set.py",
        "scripts/generate-pd10-v2-contract.py",
        "scripts/generate-v2-conformance-set.ps1",
        "src/Hexalith.Folders.Client/Generation/Program.cs",
        "src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
        "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml",
        "tests/fixtures/parity-contract.schema.json",
        "tests/fixtures/parity-contract.yaml",
        "tests/fixtures/previous-spine.yaml",
        "tests/tools/parity-oracle-generator/Program.cs",
        "tests/tools/run-consumer-docs-gates.ps1",
        "tests/tools/run-contract-parity-ci-gates.ps1",
        "tests/tools/run-contract-spine-gates.ps1",
        "tests/tools/run-governance-completeness-gates.ps1",
        "tests/tools/run-provider-error-docs-gates.ps1",
    ];

    [Fact]
    public void ManifestBindsEveryListedCandidateArtifactAndOrderedSetDigest()
    {
        YamlMappingNode root = Load(Path.Combine(RepositoryRoot, ManifestPath));
        Scalar(root, "governance_status").ShouldBe("candidate-awaiting-a6b");
        Scalar(root, "production_routed").ShouldBe("false");
        Scalar(root, "hash_algorithm").ShouldBe("SHA-256");

        YamlSequenceNode artifacts = (YamlSequenceNode)root.Children[new YamlScalarNode("artifacts")];
        List<(string Path, string Digest)> entries = [];
        foreach (YamlMappingNode artifact in artifacts.Children.Cast<YamlMappingNode>())
        {
            string path = Scalar(artifact, "path");
            string digest = Scalar(artifact, "sha256");
            string fullPath = Path.Combine(RepositoryRoot, path);
            File.Exists(fullPath).ShouldBeTrue(path);
            File.ReadAllBytes(fullPath).Length.ShouldBe(int.Parse(Scalar(artifact, "bytes")), path);
            Sha256(fullPath).ShouldBe(digest, path);
            entries.Add((path, digest));
        }

        entries.Select(entry => entry.Path).ShouldBe(entries.Select(entry => entry.Path).Order(StringComparer.Ordinal).ToArray());
        entries.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Count().ShouldBe(entries.Count);
        int.Parse(Scalar(root, "artifact_count")).ShouldBe(entries.Count);

        string material = string.Concat(entries.Select(entry => $"{entry.Path}\0{entry.Digest}\n"));
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant()
            .ShouldBe(Scalar(root, "candidate_set_sha256"));
        entries.Single(entry => entry.Path == "docs/contract/authorization-matrix.md").Digest
            .ShouldBe(Scalar(root, "authorization_matrix_sha256"));
        foreach (string requiredPath in IndependentlyRequiredPaths)
        {
            entries.Select(entry => entry.Path).ShouldContain(requiredPath);
        }
        entries.Select(entry => entry.Path).ShouldAllBe(path =>
            !path.StartsWith("_bmad-output/", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratorReproducesTheCommittedManifestByteForByte()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"pd10-v2-conformance-{Guid.NewGuid():N}.yaml");
        try
        {
            ProcessStartInfo start = new()
            {
                FileName = "python3",
                WorkingDirectory = RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("scripts/generate-pd10-v2-conformance-set.py");
            start.ArgumentList.Add("--repository-root");
            start.ArgumentList.Add(RepositoryRoot);
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(temporary);

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the conformance-set generator.");
            process.WaitForExit(60_000).ShouldBeTrue();
            string diagnostic = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.ExitCode.ShouldBe(0, diagnostic);
            File.ReadAllBytes(temporary).ShouldBe(File.ReadAllBytes(Path.Combine(RepositoryRoot, ManifestPath)));
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static YamlMappingNode Load(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static string Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[new YamlScalarNode(key)]).Value ?? string.Empty;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Hexalith.Folders repository root was not found.");
    }
}
