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
    private const string BuildsExecutionSha = "3639c8d9340fc81d6f8e0a90566a97e56d5d8446";
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
        text.ShouldContain("git -c submodule.recurse=false submodule update --init --checkout", Case.Sensitive);
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
        release.GetReleaseScalar("runs-on").ShouldBe("ubuntu-latest");
        release.GetReleaseScalar("timeout-minutes").ShouldBe("60");
        release.GetReleaseScalar("environment").ShouldBe("production");
        release.Children.ContainsKey(new YamlScalarNode("uses")).ShouldBeFalse();
        release.Children.ContainsKey(new YamlScalarNode("secrets")).ShouldBeFalse();
        YamlMappingNode permissions = release.GetReleaseMapping("permissions");
        permissions.Children.Keys.Select(static key => key.ToString()).Order(StringComparer.Ordinal)
            .ShouldBe(new[] { "actions", "contents", "id-token", "issues", "pull-requests" }.Order(StringComparer.Ordinal));
        permissions.GetReleaseScalar("actions").ShouldBe("read");
        foreach (string permission in new[] { "contents", "id-token", "issues", "pull-requests" })
        {
            permissions.GetReleaseScalar(permission).ShouldBe("write");
        }

        YamlMappingNode[] steps = release.GetReleaseSequence("steps").Children.Cast<YamlMappingNode>().ToArray();
        steps.Length.ShouldBe(6, "Checkout, shared preparation, NuGet login, and publication must run consecutively, followed only by the two failure-evidence uploads.");
        steps[0].GetReleaseScalar("uses").ShouldBe("actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1");
        YamlMappingNode checkout = steps[0].GetReleaseMapping("with");
        checkout.GetReleaseScalar("ref").ShouldBe("${{ github.sha }}");
        checkout.GetReleaseScalar("fetch-depth").ShouldBe("0");
        checkout.GetReleaseScalar("persist-credentials").ShouldBe("false");
        checkout.GetReleaseScalar("submodules").ShouldBe("false");
        steps[1].GetReleaseScalar("id").ShouldBe("prepare");
        steps[1].GetReleaseScalar("uses").ShouldBe(
            $"Hexalith/Hexalith.Builds/Github/prepare-domain-release@{BuildsExecutionSha}");
        YamlMappingNode inputs = steps[1].GetReleaseMapping("with");
        inputs.GetReleaseScalar("builds-execution-sha").ShouldBe(BuildsExecutionSha);
        inputs.GetReleaseScalar("solution").ShouldBe("Hexalith.Folders.CI.slnx");
        inputs.GetReleaseScalar("source-branch").ShouldBe("main");
        inputs.GetReleaseScalar("source-ci-workflow").ShouldBe("ci.yml");
        inputs.GetReleaseScalar("package-manifest").ShouldBe(ManifestPath);
        inputs.GetReleaseScalar("expected-package-count").ShouldBe("5");
        inputs.GetReleaseScalar("publication-flag").ShouldBe("${{ vars.HEXALITH_RELEASE_PUBLISH_ENABLED }}");
        inputs.GetReleaseScalar("nuget-user").ShouldBe("${{ vars.NUGET_USER }}");
        steps[2].GetReleaseScalar("id").ShouldBe("nuget-login");
        steps[2].GetReleaseScalar("uses").ShouldBe("NuGet/login@8d196754b4036150537f80ac539e15c2f1028841");
        steps[2].GetReleaseScalar("if").ShouldBe("${{ steps.prepare.outputs.publish-enabled == 'true' }}");
        steps[2].GetReleaseMapping("with").GetReleaseScalar("user").ShouldBe("${{ vars.NUGET_USER }}");
        steps[3].GetReleaseScalar("if").ShouldBe("${{ steps.prepare.outputs.publish-enabled == 'true' }}");
        steps[3].GetReleaseScalar("shell").ShouldBe("bash");
        steps[3].GetReleaseScalar("run").ShouldBe("npm exec --no -- semantic-release");
        YamlMappingNode publicationEnvironment = steps[3].GetReleaseMapping("env");
        publicationEnvironment.GetReleaseScalar("NUGET_API_KEY").ShouldBe("${{ steps.nuget-login.outputs.NUGET_API_KEY }}");
        publicationEnvironment.GetReleaseScalar("GITHUB_TOKEN").ShouldBe("${{ github.token }}");
        publicationEnvironment.GetReleaseScalar("HEXALITH_BUILDS_EXECUTION_SHA").ShouldBe(BuildsExecutionSha);
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_ENVIRONMENT").ShouldBe("production");
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_SOURCE_BRANCH").ShouldBe("main");
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_SOURCE_CI_WORKFLOW").ShouldBe("ci.yml");
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_PACKAGE_MANIFEST").ShouldBe(ManifestPath);
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT").ShouldBe("5");
        publicationEnvironment.GetReleaseScalar("HEXALITH_RELEASE_REQUIRE_AUTHORITY").ShouldBe("false");
        foreach (YamlMappingNode evidenceStep in steps[4..])
        {
            evidenceStep.GetReleaseScalar("if").ShouldBe("${{ always() && failure() }}");
            evidenceStep.GetReleaseScalar("uses").ShouldBe("actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a");
            evidenceStep.GetReleaseMapping("with").GetReleaseScalar("retention-days").ShouldBe("7");
            evidenceStep.GetReleaseMapping("with").GetReleaseScalar("if-no-files-found").ShouldBe("ignore");
        }
        YamlMappingNode reportUpload = steps[4].GetReleaseMapping("with");
        reportUpload.GetReleaseScalar("name").ShouldBe("release-package-report-${{ github.run_id }}-${{ github.run_attempt }}");
        reportUpload.GetReleaseScalar("path").ShouldBe(ReportPath);
        YamlMappingNode archiveUpload = steps[5].GetReleaseMapping("with");
        archiveUpload.GetReleaseScalar("name").ShouldBe("release-package-archives-${{ github.run_id }}-${{ github.run_attempt }}");
        archiveUpload.GetReleaseScalar("path").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ShouldBe(["nupkgs/*.nupkg", "nupkgs/*.snupkg"]);

        string text = ReadText(".github/workflows/release.yml");
        text.ShouldNotContain("secrets: inherit", Case.Insensitive);
        text.ShouldNotContain("secrets.NUGET_API_KEY", Case.Insensitive);
        text.ShouldNotContain("attestations:", Case.Insensitive);
        text.ShouldNotContain("nuget.pkg.github.com", Case.Insensitive);
    }

    [Fact]
    public void TrustedPublishingPolicyShouldGrantOnlyExistingInventoryToFoldersProduction()
    {
        YamlMappingNode policy = LoadSingleYamlDocument("deploy/nuget/trusted-publishing-policy.yaml");
        policy.GetReleaseScalar("kind").ShouldBe("NuGetTrustedPublishingPolicy");
        policy.GetReleaseScalar("version").ShouldBe("1");
        policy.GetReleaseScalar("policyName").ShouldBe("folders-production");
        policy.GetReleaseScalar("packageOwner").ShouldBe("Hexalith");
        policy.GetReleaseScalar("scope").ShouldBe("PackagePushVersion");
        YamlMappingNode publisher = policy.GetReleaseMapping("publisher");
        publisher.GetReleaseScalar("type").ShouldBe("GitHub");
        publisher.GetReleaseScalar("owner").ShouldBe("Hexalith");
        publisher.GetReleaseScalar("repository").ShouldBe("Hexalith.Folders");
        publisher.GetReleaseScalar("workflow").ShouldBe("release.yml");
        publisher.GetReleaseScalar("environment").ShouldBe("production");
        policy.GetReleaseMapping("profile").GetReleaseScalar("creator").ShouldBe("jpiquot");
        policy.GetReleaseMapping("profile").GetReleaseScalar("creatorRepositoryVariable").ShouldBe("NUGET_USER");
        using JsonDocument manifest = JsonDocument.Parse(ReadText(ManifestPath));
        string[] packageIds = manifest.RootElement.GetProperty("packages").EnumerateArray()
            .Select(static package => package.GetProperty("id").GetString().ShouldNotBeNull()).ToArray();
        policy.GetReleaseSequence("packages").Children.Select(static value => value.ToString()).ShouldBe(packageIds);
        packageIds.ShouldBe(_expectedPackages);
        ReadText("deploy/nuget/trusted-publishing-policy.yaml")
            .ShouldContain("does not register a remote policy", Case.Sensitive);
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
        XDocument packages = XDocument.Load(RepositoryPath("references/Hexalith.Builds/Props/Directory.Packages.props"));
        XElement stableVersion = packages.Descendants("HexalithAspireHostingDaprVersion")
            .Single(element => (string?)element.Attribute("Condition") ==
                "'$(MSBuildProjectName)' == 'Hexalith.Folders.Aspire'");
        XElement daprIntegration = packages.Descendants("PackageVersion")
            .Single(element => string.Equals(
                (string?)element.Attribute("Include"),
                "CommunityToolkit.Aspire.Hosting.Dapr",
                StringComparison.Ordinal));
        string version = stableVersion.Value;

        version.ShouldBe("13.0.0");
        version.ShouldNotContain("-", Case.Sensitive, "a stable Folders.Aspire release cannot depend on a prerelease integration package");
        ((string?)daprIntegration.Attribute("Version")).ShouldBe("$(HexalithAspireHostingDaprVersion)");

        XDocument localPackages = XDocument.Load(RepositoryPath("Directory.Packages.props"));
        localPackages.Descendants("HexalithAspireHostingDaprVersion").ShouldBeEmpty(
            "the Builds catalog owns the project-conditioned stable version");
        localPackages.Descendants("PackageVersion").ShouldNotContain(element =>
            (string?)element.Attribute("Include") == "CommunityToolkit.Aspire.Hosting.Dapr"
            || (string?)element.Attribute("Update") == "CommunityToolkit.Aspire.Hosting.Dapr",
            "a second local override must not duplicate or broaden the Builds catalog's stable version");
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
            "NuGet/login",
            "NUGET_USER",
            "jpiquot",
            "folders-production",
            "PackagePushVersion",
            "not a native NuGet API import",
            "committing this file does not register a remote policy",
            "v1.1.0",
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
