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
    private const string BaselineCommit = "3f1056d998ac4688f36eb869c516812c1a4ddb71";
    private static readonly HashSet<string> RequiredPaths = new(StringComparer.Ordinal)
    {
        ".github/workflows/ci.yml",
        ".github/workflows/contract-spine.yml",
        "docs/contract/authorization-matrix.md",
        "docs/operations/canonical-error-catalog.md",
        "docs/sdk/api-reference.md",
        "scripts/generate-pd10-v2-conformance-set.py",
        "scripts/generate-pd10-v2-contract.py",
        "scripts/generate-pd10-v2-runtime-catalog.py",
        "scripts/generate-v2-conformance-set.ps1",
        "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
        "src/Hexalith.Folders.Client/nswag.json",
        "src/Hexalith.Folders.Client/Generation/Program.cs",
        "src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs",
        "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml",
        "src/Hexalith.Folders.Server/Pd10V2RuntimeResponseCatalog.g.cs",
        "tests/fixtures/parity-contract.schema.json",
        "tests/fixtures/parity-contract.yaml",
        "tests/fixtures/previous-spine.yaml",
        "tests/tools/parity-oracle-generator/Program.cs",
        "tests/tools/run-consumer-docs-gates.ps1",
        "tests/tools/run-contract-parity-ci-gates.ps1",
        "tests/tools/run-contract-spine-gates.ps1",
        "tests/tools/run-governance-completeness-gates.ps1",
        "tests/tools/run-provider-error-docs-gates.ps1",
    };
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ManifestBindsEveryListedCandidateArtifactAndOrderedSetDigest()
    {
        YamlMappingNode root = Load(Path.Combine(RepositoryRoot, ManifestPath));
        Scalar(root, "governance_status").ShouldBe("candidate-awaiting-a6b");
        Scalar(root, "production_routed").ShouldBe("false");
        Scalar(root, "hash_algorithm").ShouldBe("SHA-256");

        YamlSequenceNode artifacts = (YamlSequenceNode)root.Children[new YamlScalarNode("artifacts")];
        Dictionary<string, string> gitlinks = GitLinks();
        List<(string Path, string Digest)> entries = [];
        foreach (YamlMappingNode artifact in artifacts.Children.Cast<YamlMappingNode>())
        {
            string path = Scalar(artifact, "path");
            string digest = Scalar(artifact, "sha256");
            string fullPath = Path.Combine(RepositoryRoot, path);
            string kind = Scalar(artifact, "kind");
            if (kind == "gitlink")
            {
                Directory.Exists(fullPath).ShouldBeTrue(path);
                string objectId = Scalar(artifact, "git_object");
                gitlinks[path].ShouldBe(objectId, path);
                Encoding.UTF8.GetByteCount(objectId).ShouldBe(int.Parse(Scalar(artifact, "bytes")), path);
                Sha256(Encoding.UTF8.GetBytes(objectId)).ShouldBe(digest, path);
            }
            else
            {
                kind.ShouldBe("file", path);
                File.Exists(fullPath).ShouldBeTrue(path);
                new FileInfo(fullPath).Length.ShouldBe(long.Parse(Scalar(artifact, "bytes")), path);
                Sha256(fullPath).ShouldBe(digest, path);
            }
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

        entries.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal)
            .SetEquals(IndependentCandidatePaths(gitlinks)).ShouldBeTrue();
    }

    [Fact]
    public void GeneratorReproducesTheCommittedManifestByteForByte()
    {
        string temporary = Path.Combine(RepositoryRoot, $"pd10-v2-conformance-{Guid.NewGuid():N}.yaml");
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

    [Fact]
    public void GeneratorRejectsOutputThatAliasesARequiredCandidateInput()
    {
        string matrix = Path.Combine(RepositoryRoot, "docs", "contract", "authorization-matrix.md");
        string before = Sha256(matrix);
        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("scripts/generate-pd10-v2-conformance-set.py");
        start.ArgumentList.Add("--repository-root");
        start.ArgumentList.Add(RepositoryRoot);
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(matrix);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the conformance-set generator.");
        process.WaitForExit(60_000).ShouldBeTrue();
        string diagnostic = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.ExitCode.ShouldNotBe(0);
        diagnostic.ShouldContain("must not alias", Case.Insensitive);
        Sha256(matrix).ShouldBe(before);
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static HashSet<string> IndependentCandidatePaths(IReadOnlyDictionary<string, string> gitlinks)
    {
        HashSet<string> expected = new(RequiredPaths, StringComparer.Ordinal);
        foreach (string path in GitNulRecords("diff", "--name-only", "--diff-filter=ACMRT", "-z", BaselineCommit, "--")
            .Concat(GitNulRecords("ls-files", "--others", "--exclude-standard", "-z")))
        {
            string normalized = path.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            if (normalized == ManifestPath
                || normalized == "_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml"
                || normalized.StartsWith("_bmad-output/gates/", StringComparison.Ordinal)
                || normalized.StartsWith("_bmad-output/implementation-artifacts/", StringComparison.Ordinal)
                || normalized.StartsWith("_bmad-output/planning-artifacts/", StringComparison.Ordinal)
                || parts.Contains("bin", StringComparer.Ordinal)
                || parts.Contains("obj", StringComparer.Ordinal)
                || (!gitlinks.ContainsKey(normalized) && !File.Exists(Path.Combine(RepositoryRoot, normalized))))
            {
                continue;
            }

            expected.Add(normalized);
        }

        return expected;
    }

    private static Dictionary<string, string> GitLinks()
        => GitNulRecords("ls-files", "--stage", "-z")
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts[0].StartsWith("160000 ", StringComparison.Ordinal))
            .ToDictionary(
                parts => parts[1].Replace('\\', '/'),
                parts => parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[1],
                StringComparer.Ordinal);

    private static string[] GitNulRecords(params string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
        using MemoryStream output = new();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);
        return Encoding.UTF8.GetString(output.ToArray())
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

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
