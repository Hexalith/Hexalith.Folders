using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class Pd10ConformanceSetTests
{
    private const string ManifestPath = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml";
    private const string StoryOwnedPathsPath = "scripts/pd10-v2-story-owned-paths.txt";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void InventoryCoversEveryRuntimeAndTestCompileInputIncludingExtractedHelpers()
    {
        HashSet<string> inventory = ListedCandidatePaths();
        const string rootsPath = "scripts/pd10-v2-coverage-roots.txt";
        inventory.ShouldContain(rootsPath);
        foreach (string root in File.ReadAllLines(Path.Combine(RepositoryRoot, rootsPath)))
        {
            Directory.Exists(Path.Combine(RepositoryRoot, root)).ShouldBeTrue(root);
            string[] compileInputs = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, root), "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
                .Select(path => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/'))
                .Where(path => !path.Split('/').Any(segment => segment is "bin" or "obj"))
                .ToArray();
            compileInputs.ShouldNotBeEmpty(root);
            foreach (string input in compileInputs) inventory.ShouldContain(input, $"Unbound compile input: {input}");
        }
    }

    [Fact]
    public void CurrentSourceInventoryPreservesHistoricalEvidenceAndRequiresRequalification()
    {
        const string original = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml";
        const string originalHash = "6289c143af409f72e53aff555310d37faa25da0bb852488fe9a2f5ab7196a286";
        const string previous = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending.yaml";
        const string previousHash = "08ea78bcbcffa422632fd7d944b745a1d1a65c93120c80be8cc2c793e83b0c70";
        Sha256(Path.Combine(RepositoryRoot, original)).ShouldBe(originalHash);
        Sha256(Path.Combine(RepositoryRoot, previous)).ShouldBe(previousHash);
        YamlMappingNode prior = Load(Path.Combine(RepositoryRoot, previous));
        Scalar(prior, "prior_conformance_set_path").ShouldBe(original);
        Scalar(prior, "prior_conformance_set_sha256").ShouldBe(originalHash);
        YamlMappingNode current = Load(Path.Combine(RepositoryRoot, ManifestPath));
        Scalar(current, "prior_conformance_set_path").ShouldBe(previous);
        Scalar(current, "prior_conformance_set_sha256").ShouldBe(previousHash);
        Scalar(current, "generated_for_date").ShouldBe("2026-11-12");
        Scalar(current, "source_identity_qualification").ShouldBe("pending-after-qualified-source-pins");
        Scalar(current, "source_baseline_commit").ShouldBe("5c318e6f4abea88076d1e7f1eb0abfaf9b9c0e25");
        Scalar(current, "approval_status").ShouldBe("pending-a6b");
        Scalar(current, "production_exposure").ShouldBe("disabled");
        Sha256(Path.Combine(RepositoryRoot, "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml"))
            .ShouldBe("d417afd8454dac16824af17c2c0a787fe579c342a009e13b6985b147724d89a7");
    }

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
            string kind = Scalar(artifact, "kind");
            kind.ShouldBe("file", path);
            File.Exists(fullPath).ShouldBeTrue(path);
            new FileInfo(fullPath).Length.ShouldBe(long.Parse(Scalar(artifact, "bytes")), path);
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

        entries.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal)
            .SetEquals(ListedCandidatePaths()).ShouldBeTrue();
        entries.Select(entry => entry.Path).ShouldNotContain(".github/workflows/release.yml");
        entries.Select(entry => entry.Path).ShouldNotContain("Directory.Packages.props");
        entries.Select(entry => entry.Path).ShouldNotContain(path => path.StartsWith("references/", StringComparison.Ordinal));
    }

    [Fact]
    public void CandidateCoverageIncludesIndependentRuntimeDependencyClosure()
        => Pd10CandidateCoverage.MissingPaths(RepositoryRoot, ListedCandidatePaths()).ShouldBeEmpty();

    [Theory]
    [InlineData("src/shared/AdapterCore/SafeBearerTokenHandler.cs")]
    [InlineData("src/Hexalith.Folders.Cli/Composition/FoldersClientFactory.cs")]
    [InlineData("src/Hexalith.Folders.Cli/Program.cs")]
    [InlineData("src/Hexalith.Folders.Mcp/Composition/FoldersClientRegistration.cs")]
    [InlineData("src/Hexalith.Folders.Mcp/Tools/FileTools.cs")]
    [InlineData("src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj")]
    [InlineData("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj")]
    [InlineData("src/Hexalith.Folders/Aggregates/StreamNameSegment.cs")]
    [InlineData("src/Hexalith.Folders/Hashing/LengthPrefixedHash.cs")]
    [InlineData("src/Hexalith.Folders.Server/Authentication/HumanAuthorityReceiptVerifier.cs")]
    [InlineData("tests/tools/Task36FoldObserver/Task36FoldObserver.csproj")]
    [InlineData("tests/tools/Task36FoldObserver/FoldDiagnosticObserver.cs")]
    public void CandidateCoverageRejectsAnOmittedExtractedOrTransitiveDependency(string removedPath)
    {
        HashSet<string> complete = Pd10CandidateCoverage.RequiredPaths(RepositoryRoot);
        complete.Remove(removedPath).ShouldBeTrue();
        Pd10CandidateCoverage.MissingPaths(RepositoryRoot, complete).ShouldBe([removedPath]);
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

    [Theory]
    [InlineData("docs/contract/authorization-matrix.md")]
    [InlineData("src/Hexalith.Folders.Contracts/openapi/baselines/hexalith.folders.v1.pd10.yaml")]
    [InlineData("_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending.yaml")]
    public void GeneratorRejectsOutputThatAliasesCandidateOrHistoricalInput(string relativePath)
    {
        string input = Path.Combine(RepositoryRoot, relativePath);
        string before = Sha256(input);
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
        start.ArgumentList.Add(input);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the conformance-set generator.");
        process.WaitForExit(60_000).ShouldBeTrue();
        string diagnostic = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.ExitCode.ShouldNotBe(0);
        diagnostic.ShouldContain("must not alias", Case.Insensitive);
        Sha256(input).ShouldBe(before);
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static HashSet<string> ListedCandidatePaths()
        => File.ReadAllLines(Path.Combine(RepositoryRoot, StoryOwnedPathsPath))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

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
