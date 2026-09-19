using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ReleasePackageConformanceTests
{
    private const string BuildsExecutionSha = "b93e9889e9e7b67036837015b4b2b115e326c4da";
    private const string ManifestPath = "tools/release-packages.json";
    private const string PolicyPath = "deploy/nuget/release-packages.yaml";
    private const string ReportPath = "_bmad-output/gates/release-packages/latest.json";

    private static readonly string[] _expectedPackages =
    [
        "Hexalith.Folders.Contracts",
        "Hexalith.Folders",
        "Hexalith.Folders.Client",
        "Hexalith.Folders.Aspire",
        "Hexalith.Folders.Testing",
    ];

    [Fact]
    public void CiWorkflowShouldUseSharedReleaseMtpValidationAndRetainFoldersGates()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(".github/workflows/ci.yml");
        YamlMappingNode triggers = workflow.GetReleaseMapping("on");
        triggers.GetReleaseMapping("push").GetReleaseSequence("branches").Children.Select(static value => value.ToString()).ShouldBe(["main"]);
        triggers.GetReleaseMapping("pull_request").GetReleaseSequence("branches").Children.Select(static value => value.ToString()).ShouldBe(["main"]);

        YamlMappingNode shared = workflow.GetReleaseMapping("jobs").GetReleaseMapping("ci");
        shared.GetReleaseScalar("uses").ShouldBe("Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        YamlMappingNode inputs = shared.GetReleaseMapping("with");
        inputs.GetReleaseScalar("solution").ShouldBe("Hexalith.Folders.CI.slnx");
        inputs.GetReleaseScalar("test-platform").ShouldBe("microsoft-testing-platform");
        inputs.GetReleaseScalar("run-consumer-validation").ShouldBe("true");
        inputs.GetReleaseScalar("run-coverage-gate").ShouldBe("true");
        inputs.GetReleaseScalar("coverage-minimum-line").ShouldBe("75");
        inputs.GetReleaseScalar("coverage-required-branch").ShouldBe("80");
        inputs.GetReleaseScalar("coverage-isolation-targets")
            .ShouldContain("src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs", Case.Sensitive);
        inputs.GetReleaseScalar("unit-test-projects")
            .ShouldContain("tests/Hexalith.Folders.Contracts.Tests", Case.Sensitive);

        string text = ReadText(".github/workflows/ci.yml");
        foreach (string retainedGate in new[]
        {
            "run-baseline-ci-gates.ps1",
            "run-contract-parity-ci-gates.ps1",
            "run-security-redaction-ci-gates.ps1",
            "run-capacity-smoke-ci-gates.ps1",
            "run-safety-invariant-gates.ps1",
            "run-governance-completeness-gates.ps1",
            "run-accessibility-ci-gates.ps1",
            "run-e2e-ci-gates.ps1",
        })
        {
            text.ShouldContain(retainedGate, Case.Sensitive);
        }

        text.ShouldContain("submodules: false", Case.Sensitive);
        text.ShouldContain("git -c submodule.recurse=false submodule update --init", Case.Sensitive);
        text.ShouldContain("python3 -m unittest discover -s scripts/tests -p 'test_*.py'", Case.Sensitive);
        text.ShouldNotContain("NuGetAudit=false", Case.Sensitive);
        text.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        text.ShouldNotContain("dotnet nuget push", Case.Insensitive);
        text.ShouldNotContain("packages: write", Case.Insensitive);
    }

    [Fact]
    public void ReleaseWorkflowShouldBeManualExactSourceProtectedAndImmutable()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(".github/workflows/release.yml");
        YamlMappingNode triggers = workflow.GetReleaseMapping("on");
        triggers.Children.Keys.Select(static key => key.ToString()).ShouldBe(["workflow_dispatch"]);
        workflow.GetReleaseMapping("concurrency").GetReleaseScalar("group").ShouldBe("release-production");
        workflow.GetReleaseMapping("concurrency").GetReleaseScalar("cancel-in-progress").ShouldBe("false");

        YamlMappingNode jobs = workflow.GetReleaseMapping("jobs");
        YamlMappingNode verifySource = jobs.GetReleaseMapping("verify-source");
        string sourceProof = verifySource.GetReleaseSequence("steps").Children.Cast<YamlMappingNode>()
            .Single().GetReleaseScalar("run");
        sourceProof.ShouldContain("refs/heads/main", Case.Sensitive);
        sourceProof.ShouldContain("event=push", Case.Sensitive);
        sourceProof.ShouldContain("head_sha=\"$DISPATCH_SHA\"", Case.Sensitive);
        sourceProof.ShouldContain("conclusion == \"success\"", Case.Sensitive);

        YamlMappingNode release = jobs.GetReleaseMapping("release");
        release.GetReleaseScalar("needs").ShouldBe("verify-source");
        release.GetReleaseScalar("uses").ShouldBe(
            $"Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@{BuildsExecutionSha}");
        YamlMappingNode inputs = release.GetReleaseMapping("with");
        inputs.GetReleaseScalar("builds-execution-sha").ShouldBe(BuildsExecutionSha);
        inputs.GetReleaseScalar("environment-name").ShouldBe("production");
        inputs.GetReleaseScalar("source-branch").ShouldBe("main");
        inputs.GetReleaseScalar("source-ci-workflow").ShouldBe("ci.yml");
        inputs.GetReleaseScalar("package-manifest").ShouldBe(ManifestPath);
        inputs.GetReleaseScalar("expected-package-count").ShouldBe("5");
        inputs.GetReleaseScalar("test-platform").ShouldBe("microsoft-testing-platform");
        inputs.GetReleaseScalar("publish-containers").ShouldBe("false");
        inputs.GetReleaseScalar("require-publication-authority").ShouldBe("false");
        release.GetReleaseMapping("secrets").GetReleaseScalar("NUGET_API_KEY").ShouldBe("${{ secrets.NUGET_API_KEY }}");

        string text = ReadText(".github/workflows/release.yml");
        text.ShouldNotContain("secrets: inherit", Case.Insensitive);
        text.ShouldNotContain("nuget.pkg.github.com", Case.Insensitive);
    }

    [Fact]
    public void JsonManifestShouldBeTheSingleExactFivePackageInventory()
    {
        using JsonDocument document = JsonDocument.Parse(ReadText(ManifestPath));
        JsonElement[] packages = document.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        packages.Select(static package => package.GetProperty("id").GetString()).ShouldBe(_expectedPackages);
        packages.Select(static package => package.GetProperty("project").GetString()).Distinct(StringComparer.Ordinal).Count().ShouldBe(5);

        foreach (JsonElement package in packages)
        {
            string projectPath = package.GetProperty("project").GetString().ShouldNotBeNull();
            XDocument project = XDocument.Load(RepositoryPath(projectPath));
            GetProperty(project, "IsPackable").ShouldBe("true");
        }

        YamlMappingNode policy = LoadSingleYamlDocument(PolicyPath);
        policy.GetReleaseScalar("kind").ShouldBe("ReleasePackagePolicy");
        policy.GetReleaseScalar("inventoryPath").ShouldBe(ManifestPath);
        policy.GetReleaseScalar("expectedPackageCount").ShouldBe("5");
        policy.GetReleaseScalar("feed").ShouldBe("https://api.nuget.org/v3/index.json");
        policy.GetReleaseScalar("symbolsRequired").ShouldBe("true");
        policy.GetReleaseScalar("duplicatePolicy").ShouldBe("fail");
        policy.Children.ContainsKey(new YamlScalarNode("releaseSet")).ShouldBeFalse(
            "The deployment policy must point to the JSON inventory rather than duplicate its package list.");
        policy.GetReleaseSequence("excludedPackableProjects").Children.Select(static value => value.ToString())
            .ShouldBe([
                "src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj",
                "src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj",
            ]);
    }

    [Fact]
    public void StableReleaseShouldUseStableDaprIntegration()
    {
        XDocument packages = XDocument.Load(RepositoryPath("Directory.Packages.props"));
        XElement daprIntegration = packages.Descendants("PackageVersion")
            .Single(element => string.Equals(
                (string?)element.Attribute("Update"),
                "CommunityToolkit.Aspire.Hosting.Dapr",
                StringComparison.Ordinal));
        string version = ((string?)daprIntegration.Attribute("Version")).ShouldNotBeNull();

        version.ShouldBe("13.0.0");
        version.ShouldNotContain("-", Case.Sensitive, "a stable Folders.Aspire release cannot depend on a prerelease integration package");
        ((string?)daprIntegration.Attribute("Condition")).ShouldBe(
            "'$(MSBuildProjectName)' == 'Hexalith.Folders.Aspire'",
            "the stable release override must not downgrade AppHost or test graphs that consume EventStore.Aspire's newer preview dependency");
    }

    [Fact]
    public void ReleaseToolingShouldSealValidateConsumeAndFailOnDuplicateVersions()
    {
        string gate = ReadText("tests/tools/run-release-package-gates.ps1");
        string contract = ReadText("scripts/release_package_contract.py");
        string packer = ReadText("scripts/pack-release-packages.py");
        string validator = ReadText("scripts/validate-nuget-packages.py");
        string consumer = ReadText("scripts/validate-consumer-package-references.py");
        string preflight = ReadText("scripts/validate-publication-preflight.sh");
        string releaseConfig = ReadText(".releaserc.json");

        gate.ShouldContain("tools/release-packages.json", Case.Sensitive);
        gate.ShouldContain("scripts/pack-release-packages.py", Case.Sensitive);
        gate.ShouldContain("scripts/validate-nuget-packages.py", Case.Sensitive);
        gate.ShouldContain("scripts/validate-consumer-package-references.py", Case.Sensitive);
        gate.ShouldContain("Hexalith.Folders.CI.slnx", Case.Sensitive);
        gate.ShouldContain("UseNuGetDeps=true", Case.Sensitive);
        gate.ShouldContain("https://api.nuget.org/v3/index.json", Case.Sensitive);
        gate.ShouldContain("dotnet", Case.Sensitive);
        gate.ShouldContain("nuget", Case.Sensitive);
        gate.ShouldContain("push", Case.Sensitive);
        gate.ShouldNotContain("--skip-duplicate", Case.Insensitive);
        gate.ShouldNotContain("NuGetAudit=false", Case.Sensitive);

        contract.ShouldContain("testzip()", Case.Sensitive);
        contract.ShouldContain("unsafe archive path", Case.Sensitive);
        contract.ShouldContain("unpublished Folders dependencies", Case.Sensitive);
        contract.ShouldContain("read_symbol_metadata", Case.Sensitive);
        contract.ShouldContain("noncanonical Folders dependency ID", Case.Sensitive);
        contract.ShouldContain("instead of release version", Case.Sensitive);
        contract.ShouldContain(".snupkg", Case.Sensitive);
        packer.ShouldContain("UseHexalithProjectReferences=false", Case.Sensitive);
        packer.ShouldContain("repository-owned package directory", Case.Sensitive);
        packer.ShouldNotContain("UseFoldersSourceDependencies", Case.Sensitive);
        validator.ShouldContain("validate_packages", Case.Sensitive);
        consumer.ShouldContain("PackageReference", Case.Sensitive);
        consumer.ShouldContain("packageSourceMapping", Case.Sensitive);
        consumer.ShouldContain("Hexalith.Folders*", Case.Sensitive);
        consumer.ShouldNotContain("ProjectReference", Case.Sensitive);

        preflight.ShouldContain("event=push", Case.Sensitive);
        preflight.ShouldContain("v3-flatcontainer", Case.Sensitive);
        preflight.ShouldContain("already contains", Case.Sensitive);
        preflight.ShouldContain("protected-environment approval as publication authority", Case.Sensitive);
        releaseConfig.ShouldContain("@semantic-release/commit-analyzer", Case.Sensitive);
        releaseConfig.ShouldContain("@semantic-release/release-notes-generator", Case.Sensitive);
        releaseConfig.ShouldContain("@semantic-release/github", Case.Sensitive);
        releaseConfig.ShouldContain("nupkgs/*.nupkg", Case.Sensitive);
        releaseConfig.ShouldContain("nupkgs/*.snupkg", Case.Sensitive);
        releaseConfig.ShouldNotContain("--skip-duplicate", Case.Insensitive);
    }

    [Fact]
    public void MtpAndSupplyChainConfigurationShouldRemainEnabled()
    {
        using JsonDocument globalJson = JsonDocument.Parse(ReadText("global.json"));
        globalJson.RootElement.GetProperty("test").GetProperty("runner").GetString()
            .ShouldBe("Microsoft.Testing.Platform");
        ReadText("Directory.Build.targets")
            .ShouldContain("Microsoft.Testing.Extensions.CodeCoverage", Case.Sensitive);

        string dependabot = ReadText(".github/dependabot.yml");
        foreach (string ecosystem in new[] { "nuget", "npm", "github-actions" })
        {
            dependabot.ShouldContain($"package-ecosystem: {ecosystem}", Case.Sensitive);
        }

        foreach (string workflow in new[]
        {
            ".github/workflows/commitlint.yml",
            ".github/workflows/codeql.yml",
            ".github/workflows/dependency-review.yml",
        })
        {
            string text = ReadText(workflow);
            text.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/", Case.Sensitive);
            text.ShouldNotContain("NuGetAudit=false", Case.Sensitive);
            text.ShouldNotContain("packages: write", Case.Insensitive);
        }

        ReadText("package.json").ShouldContain("@commitlint/config-conventional", Case.Sensitive);
        File.Exists(RepositoryPath("package-lock.json")).ShouldBeTrue();
        File.Exists(RepositoryPath("commitlint.config.mjs")).ShouldBeTrue();
    }

    [Fact]
    public void ReleasePackageReportShouldStayMetadataOnlyWhenPresent()
    {
        if (!File.Exists(RepositoryPath(ReportPath)))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;
        RequiredString(root, "gate").ShouldBe("release-packages");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        ReadStringArray(root, "pushed_package_ids").ShouldBe(_expectedPackages);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void ReleaseDocumentationShouldDescribeTheGuardedOperatorHandoff()
    {
        string documentation = ReadText("docs/operations/release-packages.md");
        foreach (string package in _expectedPackages)
        {
            documentation.ShouldContain(package, Case.Sensitive);
        }
        foreach (string required in new[]
        {
            "workflow_dispatch",
            "current `main`",
            "exact-source",
            "production",
            "HEXALITH_RELEASE_PUBLISH_ENABLED",
            "NUGET_API_KEY",
            "NuGet.org",
            "tools/release-packages.json",
            "no package, tag, or GitHub Release",
            "--skip-duplicate",
            "scripts/validate-consumer-package-references.py",
            "immutable partial-publication incident",
            "metadata-only",
        })
        {
            documentation.ShouldContain(required, Case.Sensitive);
        }
        documentation.ShouldNotContain("nuget.pkg.github.com", Case.Insensitive);
        ForbiddenCredentialPattern().IsMatch(documentation).ShouldBeFalse();
    }

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RepositoryPath(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
            if (File.Exists(Path.Combine(directory, "Hexalith.Folders.slnx")))
            {
                return candidate;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private static string GetProperty(XDocument document, string name)
    {
        XElement? element = document.Descendants(name).SingleOrDefault();
        element.ShouldNotBeNull($"Project must contain exactly one {name} property.");
        return element.Value;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String);
        return property.GetString().ShouldNotBeNull();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.Array);
        return property.EnumerateArray().Select(static item => item.GetString().ShouldNotBeNull()).ToArray();
    }

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }
                break;
            case JsonValueKind.String:
                string value = element.GetString().ShouldNotBeNull();
                RootedPathPattern().IsMatch(value).ShouldBeFalse();
                ForbiddenReportDiagnosticPattern().IsMatch(value).ShouldBeFalse();
                break;
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenReportDiagnosticPattern();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|\bclient_secret\b|\bprivate_key\b|BEGIN [A-Z ]*PRIVATE KEY|\bpassword\s*=|\btoken\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenCredentialPattern();
}

internal static class ReleasePackageYamlNodeExtensions
{
    public static string GetReleaseScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    public static YamlMappingNode GetReleaseMapping(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    public static YamlSequenceNode GetReleaseSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }
}
