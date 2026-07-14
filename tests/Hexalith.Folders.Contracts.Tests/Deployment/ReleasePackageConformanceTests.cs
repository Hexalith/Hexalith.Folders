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
    private const string WorkflowPath = ".github/workflows/release-packages.yml";
    private const string ManifestPath = "deploy/nuget/release-packages.yaml";
    private const string GateScriptPath = "tests/tools/run-release-package-gates.ps1";
    private const string OperatorDocPath = "docs/operations/release-packages.md";
    private const string ReportPath = "_bmad-output/gates/release-packages/latest.json";

    private static readonly string[] ExpectedPushedPackages =
    [
        "Hexalith.Folders.Contracts",
        "Hexalith.Folders",
        "Hexalith.Folders.Client",
        "Hexalith.Folders.Aspire",
        "Hexalith.Folders.Testing",
    ];

    private static readonly string[] EpicMandatedPackages =
    [
        "Hexalith.Folders.Contracts",
        "Hexalith.Folders.Client",
        "Hexalith.Folders.Aspire",
        "Hexalith.Folders.Testing",
    ];

    private static readonly string[] RootBuildSubmodules =
    [
        "references/Hexalith.AI.Tools",
        "references/Hexalith.Builds",
        "references/Hexalith.Commons",
        "references/Hexalith.EventStore",
        "references/Hexalith.FrontComposer",
        "references/Hexalith.Memories",
        "references/Hexalith.PolymorphicSerializations",
        "references/Hexalith.Tenants",
    ];

    [Fact]
    public void ReleaseWorkflowShouldUseReleaseOnlyTriggersAndStableSetup()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);

        workflow.GetReleaseScalar("name").ShouldBe("release-packages");
        YamlMappingNode triggers = workflow.GetReleaseMapping("on");
        triggers.Children.ContainsKey(new YamlScalarNode("release")).ShouldBeTrue();
        triggers.Children.ContainsKey(new YamlScalarNode("workflow_dispatch")).ShouldBeTrue();
        triggers.Children.ContainsKey(new YamlScalarNode("pull_request")).ShouldBeFalse();
        triggers.Children.ContainsKey(new YamlScalarNode("push")).ShouldBeFalse();
        triggers.GetReleaseMapping("release").GetReleaseSequence("types").Children.Select(static x => x.ToString()).ShouldBe(["published"]);
        workflow.GetReleaseMapping("permissions").GetReleaseScalar("contents").ShouldBe("read");

        YamlMappingNode dispatchInputs = triggers.GetReleaseMapping("workflow_dispatch").GetReleaseMapping("inputs");
        dispatchInputs.GetReleaseMapping("dry_run_version").GetReleaseScalar("type").ShouldBe("string");
        dispatchInputs.GetReleaseMapping("dry_run_source_revision_id").GetReleaseScalar("type").ShouldBe("string");

        YamlMappingNode jobs = workflow.GetReleaseMapping("jobs");
        foreach (string jobName in new[] { "release-prerequisite-gates", "release-package-conformance", "publish-packages" })
        {
            YamlMappingNode job = jobs.GetReleaseMapping(jobName);
            job.GetReleaseScalar("runs-on").ShouldBe("ubuntu-latest");
            YamlMappingNode checkout = FindStep(job, "actions/checkout@v6");
            checkout.GetReleaseMapping("with").GetReleaseScalar("fetch-depth").ShouldBe("0");
            checkout.GetReleaseMapping("with").GetReleaseScalar("submodules").ShouldBe("false");

            YamlMappingNode setupDotnet = FindStep(job, "actions/setup-dotnet@v5");
            YamlMappingNode setupWith = setupDotnet.GetReleaseMapping("with");
            setupWith.GetReleaseScalar("global-json-file").ShouldBe("global.json");
            setupWith.GetReleaseScalar("cache").ShouldBe("true");

            string submoduleCommand = FindNamedStep(job, "Initialize root-level build submodules").GetReleaseScalar("run");
            submoduleCommand.ShouldStartWith("git submodule update --init ", Case.Sensitive);
            submoduleCommand.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
            foreach (string module in RootBuildSubmodules)
            {
                submoduleCommand.ShouldContain(module, Case.Sensitive);
            }
        }
    }

    [Fact]
    public void ReleaseWorkflowShouldProveGatesBeforePublishAndUseMinimumPermissions()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);
        YamlMappingNode jobs = workflow.GetReleaseMapping("jobs");
        YamlMappingNode publishJob = jobs.GetReleaseMapping("publish-packages");

        publishJob.GetReleaseSequence("needs").Children.Select(static x => x.ToString())
            .ShouldBe(["release-prerequisite-gates", "release-package-conformance"]);
        publishJob.GetReleaseScalar("if").ShouldContain("needs.release-prerequisite-gates.result == 'success'", Case.Sensitive);
        publishJob.GetReleaseScalar("if").ShouldContain("needs.release-package-conformance.result == 'success'", Case.Sensitive);
        publishJob.GetReleaseMapping("permissions").GetReleaseScalar("contents").ShouldBe("read");
        publishJob.GetReleaseMapping("permissions").GetReleaseScalar("packages").ShouldBe("write");
        publishJob.GetReleaseMapping("permissions").Children.Keys.Select(static key => key.ToString()).Order(StringComparer.Ordinal)
            .ShouldBe(["contents", "packages"]);

        foreach (string jobName in new[] { "release-package-conformance", "publish-packages" })
        {
            YamlMappingNode packageJob = jobs.GetReleaseMapping(jobName);
            FindNamedStep(packageJob, "Restore").GetReleaseScalar("run")
                .ShouldBe("dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1 -p:NuGetAudit=false");
            FindNamedStep(packageJob, "Build").GetReleaseScalar("run")
                .ShouldBe("dotnet build Hexalith.Folders.slnx -c Release -p:UseNuGetDeps=true --no-restore -m:1");
        }

        string workflowText = ReadText(WorkflowPath);
        foreach (string command in new[]
        {
            "dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false",
            "dotnet build Hexalith.Folders.slnx --no-restore -m:1",
            "dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1 -p:NuGetAudit=false",
            "dotnet build Hexalith.Folders.slnx -c Release -p:UseNuGetDeps=true --no-restore -m:1",
            "./tests/tools/run-contract-parity-ci-gates.ps1",
            "./tests/tools/run-security-redaction-ci-gates.ps1",
            "./tests/tools/run-capacity-smoke-ci-gates.ps1",
            "./tests/tools/run-retention-deletion-gates.ps1",
            "./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild",
            "./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild",
            "./tests/tools/run-release-package-gates.ps1",
            "-Mode Publish",
            "-FeedSource 'https://nuget.pkg.github.com/Hexalith/index.json'",
            "-ApiKeyEnvironmentVariable GITHUB_TOKEN",
        })
        {
            workflowText.ShouldContain(command, Case.Sensitive);
        }

        workflowText.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflowText.ShouldNotContain("id-token:", Case.Insensitive);
        workflowText.ShouldNotContain("pull-requests:", Case.Insensitive);
        workflowText.ShouldNotContain("deployments:", Case.Insensitive);
        workflowText.ShouldNotContain("checks:", Case.Insensitive);
        workflowText.ShouldNotContain("statuses:", Case.Insensitive);
        workflowText.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void PackageManifestShouldDeclareDeterministicReleaseSetAndScopeDrift()
    {
        YamlMappingNode manifest = LoadSingleYamlDocument(ManifestPath);
        manifest.GetReleaseScalar("kind").ShouldBe("ReleasePackageManifest");

        ReleasePackage[] releaseSet = manifest.GetReleaseSequence("releaseSet").Children.Cast<YamlMappingNode>().Select(ParseReleasePackage).ToArray();
        releaseSet.Where(static p => p.PushedInStory79).Select(static p => p.PackageId).ShouldBe(ExpectedPushedPackages);

        foreach (string packageId in EpicMandatedPackages)
        {
            releaseSet.ShouldContain(p => p.PackageId == packageId && p.PushedInStory79);
        }

        ReleasePackage core = releaseSet.Single(static p => p.PackageId == "Hexalith.Folders");
        core.DependencyRationale.ShouldContain("dependency closure", Case.Insensitive);

        ReleasePackage[] excluded = manifest.GetReleaseSequence("excludedPackableProjects").Children.Cast<YamlMappingNode>().Select(ParseReleasePackage).ToArray();
        excluded.Select(static p => p.PackageId).Order(StringComparer.Ordinal)
            .ShouldBe(["Hexalith.Folders.Cli", "Hexalith.Folders.ServiceDefaults"]);
        excluded.All(static p => p.PublishMode == "excluded" && !p.PushedInStory79).ShouldBeTrue();

        foreach (ReleasePackage package in releaseSet)
        {
            XDocument project = XDocument.Load(RepositoryPath(package.ProjectPath));
            GetProperty(project, "IsPackable").ShouldBe("true");
        }

        foreach (string projectPath in manifest.GetReleaseSequence("nonPackageableProjects").Children.Select(static x => x.ToString()))
        {
            XDocument project = XDocument.Load(RepositoryPath(projectPath));
            GetOptionalProperty(project, "IsPackable").ShouldNotBe("true");
        }
    }

    [Fact]
    public void PackageMetadataShouldCarryTraceabilityAndAspireShouldBePackable()
    {
        string directoryBuildProps = ReadText("Directory.Build.props");
        foreach (string required in new[]
        {
            "<Deterministic>true</Deterministic>",
            "<PublishRepositoryUrl>true</PublishRepositoryUrl>",
            "<EmbedUntrackedSources>true</EmbedUntrackedSources>",
            "<IncludeSymbols>true</IncludeSymbols>",
            "<SymbolPackageFormat>snupkg</SymbolPackageFormat>",
            "<PackageLicenseExpression>MIT</PackageLicenseExpression>",
            "<PackageProjectUrl>https://github.com/Hexalith/Hexalith.Folders</PackageProjectUrl>",
            "<RepositoryUrl>https://github.com/Hexalith/Hexalith.Folders</RepositoryUrl>",
            "<RepositoryType>git</RepositoryType>",
            "<PackageReadmeFile>README.md</PackageReadmeFile>",
        })
        {
            directoryBuildProps.ShouldContain(required, Case.Sensitive);
        }

        XDocument aspireProject = XDocument.Load(RepositoryPath("src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj"));
        GetProperty(aspireProject, "IsPackable").ShouldBe("true");
        GetProperty(aspireProject, "Description").ShouldNotBeNullOrWhiteSpace();
        GetProperty(aspireProject, "PackageTags").ShouldContain("aspire", Case.Insensitive);

        string combined = directoryBuildProps + ReadText("src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj");
        ForbiddenCredentialPattern().IsMatch(combined).ShouldBeFalse();
    }

    [Fact]
    public void ReleasePackageGateScriptShouldFailClosedAndKeepPublishingExplicit()
    {
        string script = ReadText(GateScriptPath);

        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            "_bmad-output/gates/release-packages/latest.json",
            "_bmad-output/gates/release-packages/packages",
            "deploy/nuget/release-packages.yaml",
            "PackageVersion=$Version",
            "RepositoryCommit=$SourceRevisionId",
            "SourceRevisionId=$SourceRevisionId",
            "ContinuousIntegrationBuild=true",
            "IncludeSymbols=true",
            "SymbolPackageFormat=snupkg",
            "'-p:Configuration=Release'",
            "'-p:UseNuGetDeps=true'",
            "-m:1",
            "dotnet",
            "nuget",
            "push",
            "--source",
            "--api-key",
            "--skip-duplicate",
            "--no-symbols",
            "$LASTEXITCODE",
            "ContractVersion",
            "0.0.0-scaffold",
            "unexpected-package",
            "contract-version-placeholder-blocks-live-publish",
            "_bmad-output/gates/retention-deletion/latest.json",
            "stale-retention-deletion-evidence",
            "c3-retention-approval-blocks-live-publish",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        foreach (string packageId in ExpectedPushedPackages)
        {
            script.ShouldContain(packageId, Case.Sensitive);
        }

        script.ShouldContain("^[0-9a-fA-F]{40}$", Case.Sensitive);
        script.ShouldContain("invalid-semver", Case.Sensitive);
        script.IndexOf("unexpected-package", StringComparison.Ordinal).ShouldBeLessThan(
            script.IndexOf("Invoke-RestoreBuild", StringComparison.Ordinal),
            "The release gate must reject a manifest with extra pushed packages before restore, pack, or publish can run.");
        script.ShouldNotContain("dotnet pack Hexalith.Folders.slnx", Case.Insensitive);
        script.ShouldNotContain("nuget.config", Case.Insensitive);
        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void NonReleaseWorkflowsShouldNotPublishPackagesOrRequestPackageWrite()
    {
        foreach (string path in new[]
        {
            ".github/workflows/ci.yml",
            ".github/workflows/contract-spine.yml",
            ".github/workflows/nightly-drift.yml",
            ".github/workflows/policy-conformance.yml",
        })
        {
            string workflow = ReadText(path);
            workflow.ShouldNotContain("dotnet nuget push", Case.Insensitive);
            workflow.ShouldNotContain("dotnet pack", Case.Insensitive);
            workflow.ShouldNotContain("packages: write", Case.Insensitive);
            workflow.ShouldNotContain("run-release-package-gates.ps1", Case.Insensitive);
        }
    }

    [Fact]
    public void ReleasePackageReportShouldStayMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("release-packages");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        ReadStringArray(root, "pushed_package_ids").ShouldBe(ExpectedPushedPackages);
        RequiredString(root, "source_revision_id").Length.ShouldBe(40);
        RequiredString(root, "openapi_spine_path").ShouldBe("src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml");
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void ReleasePackageDocumentationShouldDefineMaintainerHandoff()
    {
        string documentation = ReadText(OperatorDocPath);

        foreach (string packageId in ExpectedPushedPackages)
        {
            documentation.ShouldContain(packageId, Case.Sensitive);
        }

        foreach (string required in new[]
        {
            "release: published",
            "workflow_dispatch",
            "v1.2.3",
            "0.0.0-local.1",
            "tests/tools/run-release-package-gates.ps1",
            "_bmad-output/gates/release-packages/latest.json",
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml",
            "GITHUB_TOKEN",
            "contents: read",
            "packages: write",
            "dotnet nuget push",
            "--skip-duplicate",
            "git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants",
            "PR CI",
            "scheduled drift",
            "policy conformance",
            "container archive validation",
            "metadata-only",
        })
        {
            documentation.ShouldContain(required, Case.Sensitive);
        }

        documentation.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        ForbiddenCredentialPattern().IsMatch(documentation).ShouldBeFalse();
    }

    private static YamlMappingNode FindStep(YamlMappingNode job, string uses)
        => job.GetReleaseSequence("steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("uses"), out YamlNode? value)
                && string.Equals(value.ToString(), uses, StringComparison.Ordinal));

    private static YamlMappingNode FindNamedStep(YamlMappingNode job, string name)
        => job.GetReleaseSequence("steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? value)
                && string.Equals(value.ToString(), name, StringComparison.Ordinal));

    private static ReleasePackage ParseReleasePackage(YamlMappingNode node)
        => new(
            node.GetReleaseScalar("packageId"),
            node.GetReleaseScalar("projectPath"),
            node.GetReleaseScalar("role"),
            node.GetReleaseScalar("publishMode"),
            node.GetReleaseScalar("dependencyRationale"),
            bool.Parse(node.GetReleaseScalar("pushedInStory79")),
            bool.Parse(node.GetReleaseScalar("symbolPackageRequired")));

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

    private static string? GetOptionalProperty(XDocument document, string name)
        => document.Descendants(name).SingleOrDefault()?.Value;

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.Array, $"JSON property '{propertyName}' must be an array.");
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
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Release package report value must not contain an absolute path: {value}");
                ForbiddenReportDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Release package report value must stay metadata-only: {value}");
                break;
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenReportDiagnosticPattern();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|\bclient_secret\b|\bprivate_key\b|BEGIN [A-Z ]*PRIVATE KEY|\bpassword\s*=|\btoken\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenCredentialPattern();

    private sealed record ReleasePackage(
        string PackageId,
        string ProjectPath,
        string Role,
        string PublishMode,
        string DependencyRationale,
        bool PushedInStory79,
        bool SymbolPackageRequired);
}

internal static class ReleasePackageYamlNodeExtensions
{
    public static string GetReleaseScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        YamlScalarNode scalar = value.ShouldBeOfType<YamlScalarNode>();
        return scalar.Value.ShouldNotBeNull();
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
