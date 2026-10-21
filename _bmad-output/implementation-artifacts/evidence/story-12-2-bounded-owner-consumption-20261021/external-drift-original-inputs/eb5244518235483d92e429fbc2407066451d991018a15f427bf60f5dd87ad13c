using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Tenants.Contracts.Tests;

public class PackageGovernanceTests {
    private const string GuardDispatchSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string RegistryDriftGuardStepName = "Require registry versions at or below the release tag floor";

    private const string TagRefsEndpoint = "repos/Hexalith/Hexalith.Tenants/git/refs/tags?per_page=100";

    private static readonly string ManifestEndpoint =
        $"repos/Hexalith/Hexalith.Tenants/contents/tools/release-packages.json?ref={GuardDispatchSha}";

    private static readonly string[] CentrallyGovernedBuildProperties =
    [
        "TargetFramework",
        "Nullable",
        "ImplicitUsings",
        "TreatWarningsAsErrors",
        "LangVersion",
    ];

    private static readonly string[] CentrallyGovernedContainerProperties =
    [
        "ContainerBaseImage",
        "ContainerFamily",
        "ContainerRegistry",
        "ContainerImageTag",
        "ContainerImageTags",
        "ContainerUser",
    ];

    private static readonly string[] OwnedProjectRoots =
    [
        "src",
        "tests",
        "samples",
    ];

    private static readonly string[] PublishablePackageProjects =
    [
        "src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj",
        "src/Hexalith.Tenants.Client/Hexalith.Tenants.Client.csproj",
        "src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj",
        "src/Hexalith.Tenants.Testing/Hexalith.Tenants.Testing.csproj",
        "src/Hexalith.Tenants.Aspire/Hexalith.Tenants.Aspire.csproj",
    ];

    private static readonly string[] ExplicitlyNonPackableProjects =
    [
        "src/Hexalith.Tenants/Hexalith.Tenants.csproj",
        "src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj",
        "src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj",
        "src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj",
        "samples/Hexalith.Tenants.Sample/Hexalith.Tenants.Sample.csproj",
        "samples/Hexalith.Tenants.Sample.Tests/Hexalith.Tenants.Sample.Tests.csproj",
        "tests/Hexalith.Tenants.AppHost.Tests/Hexalith.Tenants.AppHost.Tests.csproj",
    ];

    private static readonly string[] BlockingTestProjects =
    [
        "tests/Hexalith.Tenants.Contracts.Tests",
        "tests/Hexalith.Tenants.Client.Tests",
        "tests/Hexalith.Tenants.Testing.Tests",
        "tests/Hexalith.Tenants.UI.Tests",
        "samples/Hexalith.Tenants.Sample.Tests",
        "tests/Hexalith.Tenants.Server.Tests",
        "tests/Hexalith.Tenants.AppHost.Tests",
    ];

    private static readonly string[] ExpectedPackageIds =
    [
        "Hexalith.Tenants.Contracts",
        "Hexalith.Tenants.Client",
        "Hexalith.Tenants.Server",
        "Hexalith.Tenants.Testing",
        "Hexalith.Tenants.Aspire",
    ];

    private static readonly string[] BoundedArtifactGlobs =
    [
        "TestResults/**/*.trx",
        "TestResults/**/coverage.cobertura.xml",
        "nupkgs/*.nupkg",
    ];

    private static readonly string[] ForbiddenWorkflowFragments =
    [
        "submodules: recursive",
        "git submodule update --recursive",
        "Hexalith.EventStore.Tests",
        "Hexalith.EventStore.*.Tests",
        "Hexalith.EventStore.*.nupkg",
        "**/bin/**",
        "**/obj/**",
        "**/TestResults/**",
        "**/*.snupkg",
        "**/.nuget/**",
    ];

    [Fact]
    public void PackageReference_versions_are_centralized_for_Tenants_owned_projects() {
        string repoRoot = FindRepoRoot();
        XDocument centralPackages = XDocument.Load(Path.Combine(repoRoot, "Directory.Packages.props"));

        centralPackages.Descendants("ManagePackageVersionsCentrally").Single().Value.ShouldBe("true");
        HashSet<string> centralPackageIds = GetCentralPackageIds(repoRoot);

        List<string> violations = [];
        foreach (string projectPath in GetPackageReferenceGovernanceFiles(repoRoot)) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));

            foreach (XElement packageReference in project.Descendants("PackageReference")) {
                string packageId = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("Update")?.Value ?? "<unknown>";

                if (packageReference.Attribute("Version") is not null || packageReference.Attribute("VersionOverride") is not null) {
                    violations.Add(FormatViolation(projectPath, packageReference));
                }

                if (packageReference.Attribute("Include") is not null && !centralPackageIds.Contains(packageId)) {
                    violations.Add($"{projectPath}: PackageReference Include=\"{packageId}\" has no matching Directory.Packages.props PackageVersion: {FormatNode(packageReference)}");
                }
            }
        }

        violations.ShouldBeEmpty("Package governance violations must identify the project and offending XML node.");
    }

    [Fact]
    public void EventStore_host_dependencies_follow_one_complementary_source_package_policy() {
        const string SourceCondition = "'$(HexalithEventStoreFromSource)' == 'true'";
        const string PackageCondition = "'$(HexalithEventStoreFromSource)' != 'true'";
        const string VersionMetadata = "Version=$(HexalithEventStoreVersion)";
        const string HostProjectPath = "src/Hexalith.Tenants/Hexalith.Tenants.csproj";

        string repoRoot = FindRepoRoot();
        XDocument hostProject = XDocument.Load(Path.Combine(repoRoot, HostProjectPath));

        // Enumerated rather than looked up by literal package id: a rule naming individual packages
        // cannot see a newly added EventStore reference, which is exactly how a mixed
        // source/package graph would reappear after this story closed.
        List<(string PackageId, XElement Element)> projectEdges =
            EventStoreReferences(hostProject, "ProjectReference");
        List<(string PackageId, XElement Element)> packageEdges =
            EventStoreReferences(hostProject, "PackageReference");

        List<string> violations = [];

        foreach (IGrouping<string, (string PackageId, XElement Element)> duplicate in projectEdges
            .GroupBy(edge => edge.PackageId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)) {
            violations.Add($"{HostProjectPath}: {duplicate.Key} is referenced as a project {duplicate.Count()} times.");
        }

        HashSet<string> projectIds = [.. projectEdges.Select(edge => edge.PackageId)];
        HashSet<string> packageIds = [.. packageEdges.Select(edge => edge.PackageId)];

        foreach (string packageId in projectIds.Except(packageIds, StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
            violations.Add(
                $"{HostProjectPath}: {packageId} has a ProjectReference with no complementary PackageReference, "
                + "so package mode would drop it or resolve a mixed graph.");
        }

        foreach (string packageId in packageIds.Except(projectIds, StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
            violations.Add(
                $"{HostProjectPath}: {packageId} has a PackageReference with no complementary ProjectReference, "
                + "so source mode would resolve a package for a host dependency.");
        }

        foreach ((string packageId, XElement projectReference) in projectEdges.OrderBy(edge => edge.PackageId, StringComparer.Ordinal)) {
            string condition = EffectiveCondition(projectReference);
            if (!string.Equals(condition, SourceCondition, StringComparison.Ordinal)) {
                violations.Add($"{HostProjectPath}: {packageId} ProjectReference condition is '{condition}', expected '{SourceCondition}'.");
            }

            if (!string.Equals((string?)projectReference.Attribute("AdditionalProperties"), VersionMetadata, StringComparison.Ordinal)) {
                violations.Add($"{HostProjectPath}: {packageId} ProjectReference must carry AdditionalProperties=\"{VersionMetadata}\".");
            }
        }

        foreach ((string packageId, XElement packageReference) in packageEdges.OrderBy(edge => edge.PackageId, StringComparer.Ordinal)) {
            string condition = EffectiveCondition(packageReference);
            if (!string.Equals(condition, PackageCondition, StringComparison.Ordinal)) {
                violations.Add($"{HostProjectPath}: {packageId} PackageReference condition is '{condition}', expected '{PackageCondition}'.");
            }

            if (HasLocalVersionAuthority(packageReference)) {
                violations.Add($"{HostProjectPath}: {packageId} PackageReference declares a local version; the Builds catalog is the only authority.");
            }
        }

        projectEdges.ShouldNotBeEmpty("The domain host must declare its EventStore dependencies; an empty result would pass vacuously.");
        violations.ShouldBeEmpty(
            "EventStore host dependencies must arrive as complementary source/package pairs.");
    }

    [Fact]
    public void AppHost_EventStore_source_edges_preserve_dependency_owned_versions() {
        const string AppHostProjectPath = "src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj";

        string repoRoot = FindRepoRoot();
        XDocument appHostProject = XDocument.Load(Path.Combine(repoRoot, AppHostProjectPath));
        List<(string PackageId, XElement Element)> projectEdges =
            EventStoreReferences(appHostProject, "ProjectReference");

        List<string> violations = [];
        HashSet<string> projectIds = [.. projectEdges.Select(edge => edge.PackageId)];
        string[] requiredProjectIds =
        [
            "Hexalith.EventStore.Aspire",
            "Hexalith.EventStore",
            "Hexalith.EventStore.Admin.Server.Host",
            "Hexalith.EventStore.Admin.UI",
        ];

        foreach (string missingProjectId in requiredProjectIds.Except(projectIds, StringComparer.Ordinal)) {
            violations.Add($"{AppHostProjectPath}: required EventStore source edge {missingProjectId} is missing.");
        }

        foreach (IGrouping<string, (string PackageId, XElement Element)> duplicate in projectEdges
            .GroupBy(edge => edge.PackageId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)) {
            violations.Add($"{AppHostProjectPath}: {duplicate.Key} is referenced as a project {duplicate.Count()} times.");
        }

        foreach ((string packageId, XElement projectReference) in projectEdges) {
            string[] removedGlobalProperties = ItemMetadataValue(projectReference, "GlobalPropertiesToRemove")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!removedGlobalProperties.Contains("Version", StringComparer.OrdinalIgnoreCase)) {
                violations.Add(
                    $"{AppHostProjectPath}: {packageId} ProjectReference must remove the parent Version global property.");
            }

            if (AdditionalPropertiesExportVersion(ItemMetadataValue(projectReference, "AdditionalProperties"))) {
                violations.Add(
                    $"{AppHostProjectPath}: {packageId} ProjectReference must not export a Version global property.");
            }
        }

        projectEdges.ShouldNotBeEmpty("The AppHost must declare its EventStore source dependencies.");
        violations.ShouldBeEmpty(
            "Cross-repository source builds must let EventStore select its own centrally pinned version without exporting it back into Tenants projects.");
    }

    [Fact]
    public void Source_reference_workflow_executes_real_handler_projection_provenance() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/source-reference.yml"));

        workflow.ShouldContain("git -c submodule.recurse=false submodule update --init");
        workflow.ShouldContain("dotnet build tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj");
        workflow.ShouldContain("--configuration Debug -p:UseHexalithProjectReferences=true -warnaserror");
        workflow.ShouldContain("dotnet test tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj");
        workflow.ShouldContain("--no-build");
        workflow.ShouldContain("--minimum-expected-tests 4");
        workflow.ShouldContain(
            "--filter-method Hexalith.Tenants.IntegrationTests.TenantsApiGeneratedControllerTests.GlobalAdministratorsRealHandlerMetadataSurvivesRouterRestClientAndUiGateway");

        foreach (string forbiddenFragment in ForbiddenWorkflowFragments) {
            workflow.ShouldNotContain(forbiddenFragment);
        }
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Version=")]
    [InlineData("Version=$(HexalithEventStoreVersion)")]
    [InlineData("TargetFramework=net10.0;Version")]
    [InlineData("VERSION=1.0.0")]
    public void Additional_properties_version_tokens_are_treated_as_version_exports(string additionalProperties) {
        ArgumentNullException.ThrowIfNull(additionalProperties);
        AdditionalPropertiesExportVersion(additionalProperties).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("TargetFramework=net10.0")]
    [InlineData("VersionSuffix=beta")]
    [InlineData("InformationalVersion=1.0")]
    public void Additional_properties_without_a_version_name_are_not_version_exports(string additionalProperties) {
        ArgumentNullException.ThrowIfNull(additionalProperties);
        AdditionalPropertiesExportVersion(additionalProperties).ShouldBeFalse();
    }

    [Fact]
    public void Item_metadata_reader_combines_attribute_and_child_element_values() {
        XElement item = XElement.Parse(
            """
            <ProjectReference Include="Hexalith.EventStore.csproj" AdditionalProperties="TargetFramework=net10.0" GlobalPropertiesToRemove="TargetFramework">
              <AdditionalProperties>Version</AdditionalProperties>
              <GlobalPropertiesToRemove>Version</GlobalPropertiesToRemove>
            </ProjectReference>
            """);

        ItemMetadataValue(item, "AdditionalProperties").ShouldBe("TargetFramework=net10.0;Version");
        ItemMetadataValue(item, "GlobalPropertiesToRemove").ShouldBe("TargetFramework;Version");
        AdditionalPropertiesExportVersion(ItemMetadataValue(item, "AdditionalProperties")).ShouldBeTrue();
    }

    /// <summary>
    /// Rejects any EventStore <c>ProjectReference</c> that could still be live when the graph
    /// resolves from packages. AC3 forbids an EventStore project edge in Release/package mode
    /// anywhere in the consumer, not only in the domain host.
    /// </summary>
    [Fact]
    public void No_EventStore_project_reference_is_reachable_in_package_mode() {
        const string EventStoreSourceIntent = "'$(HexalithEventStoreFromSource)' == 'true'";
        const string ProjectReferenceIntent = "'$(UseHexalithProjectReferences)' == 'true'";

        string repoRoot = FindRepoRoot();
        List<string> violations = [];
        int inspected = 0;

        foreach (string projectPath in GetOwnedProjectFiles(repoRoot)) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));

            foreach ((string packageId, XElement projectReference) in EventStoreReferences(project, "ProjectReference")) {
                inspected++;

                // The effective condition includes every ancestor ItemGroup condition: the AppHost
                // gates its host references at the ItemGroup rather than the item.
                string condition = EffectiveCondition(projectReference);
                if (!condition.Contains(EventStoreSourceIntent, StringComparison.Ordinal)
                    && !condition.Contains(ProjectReferenceIntent, StringComparison.Ordinal)) {
                    violations.Add(
                        $"{projectPath}: EventStore ProjectReference {packageId} is not gated on source intent "
                        + $"(effective condition: '{condition}'), so it stays live in package mode.");
                }
            }

            foreach ((string packageId, XElement packageReference) in EventStoreReferences(project, "PackageReference")) {
                if (HasLocalVersionAuthority(packageReference)) {
                    violations.Add(
                        $"{projectPath}: EventStore PackageReference {packageId} declares a local version; "
                        + "the Builds catalog is the only version authority.");
                }
            }
        }

        inspected.ShouldBeGreaterThan(0, "No EventStore ProjectReference was inspected; the rule would pass vacuously.");
        violations.ShouldBeEmpty("No EventStore project edge may survive into package mode.");
    }

    [Fact]
    public void Standalone_external_dependencies_have_complementary_source_and_package_edges() {
        (string ProjectPath, string PackageId, string SourceProperty)[] expectedEdges =
        [
            ("src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj", "Hexalith.Commons.Aspire", "HexalithCommonsFromSource"),
            ("src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj", "Hexalith.McpCli.Abstractions", "HexalithMcpCliFromSource"),
            ("src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj", "Hexalith.FrontComposer.Contracts", "HexalithFrontComposerFromSource"),
            ("src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj", "Hexalith.FrontComposer.Shell", "HexalithFrontComposerFromSource"),
            ("tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj", "Hexalith.FrontComposer.Contracts", "HexalithFrontComposerFromSource"),
            ("tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj", "Hexalith.FrontComposer.Shell", "HexalithFrontComposerFromSource"),
            ("tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj", "Hexalith.FrontComposer.Testing", "HexalithFrontComposerFromSource"),
        ];

        string repoRoot = FindRepoRoot();
        List<string> violations = [];
        foreach ((string projectPath, string packageId, string sourceProperty) in expectedEdges) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            XElement[] projectReferences = project.Descendants("ProjectReference")
                .Where(reference => string.Equals(
                    ProjectFileToPackageId((string?)reference.Attribute("Include") ?? string.Empty),
                    packageId,
                    StringComparison.Ordinal))
                .ToArray();
            XElement[] packageReferences = project.Descendants("PackageReference")
                .Where(reference => string.Equals((string?)reference.Attribute("Include"), packageId, StringComparison.Ordinal))
                .ToArray();
            string sourceCondition = $"'$({sourceProperty})' == 'true'";
            string packageCondition = $"'$({sourceProperty})' != 'true'";

            if (projectReferences.Length != 1) {
                violations.Add($"{projectPath}: expected one {packageId} ProjectReference, found {projectReferences.Length}.");
            }
            else if (!string.Equals(EffectiveCondition(projectReferences[0]), sourceCondition, StringComparison.Ordinal)) {
                violations.Add($"{projectPath}: {packageId} ProjectReference must use condition {sourceCondition}.");
            }

            if (packageReferences.Length != 1) {
                violations.Add($"{projectPath}: expected one {packageId} PackageReference, found {packageReferences.Length}.");
            }
            else {
                if (!string.Equals(EffectiveCondition(packageReferences[0]), packageCondition, StringComparison.Ordinal)) {
                    violations.Add($"{projectPath}: {packageId} PackageReference must use condition {packageCondition}.");
                }

                if (HasLocalVersionAuthority(packageReferences[0])) {
                    violations.Add($"{projectPath}: {packageId} PackageReference must use the central catalog version.");
                }
            }
        }

        violations.ShouldBeEmpty("Every standalone-capable external edge must have exclusive source/package routing.");
    }

    [Theory]
    [InlineData(true, null, "true")]
    [InlineData(true, "false", "false")]
    [InlineData(false, null, "false")]
    public void FrontComposer_source_routing_is_available_by_default_and_explicitly_overridable(
        bool createSourceGraph,
        string? sourceOverride,
        string expectedFrontComposerRouting) {
        string repoRoot = FindRepoRoot();
        string sourceRoot = Path.Combine(Path.GetTempPath(), $"hexalith-frontcomposer-routing-{Guid.NewGuid():N}");

        try {
            if (createSourceGraph) {
                foreach (string relativeProject in new[] {
                    "src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj",
                    "src/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.csproj",
                    "src/Hexalith.FrontComposer.Testing/Hexalith.FrontComposer.Testing.csproj",
                }) {
                    string projectPath = Path.Combine(sourceRoot, relativeProject);
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
                    File.WriteAllText(projectPath, "<Project />");
                }
            }

            Dictionary<string, string> properties = EvaluateUiDependencyRouting(
                repoRoot,
                sourceRoot,
                sourceOverride);

            properties["HexalithFrontComposerFromSource"].ShouldBe(expectedFrontComposerRouting);
            properties["HexalithCommonsFromSource"].ShouldNotBe("true");
            properties["HexalithEventStoreFromSource"].ShouldNotBe("true");
            properties["HexalithMemoriesFromSource"].ShouldNotBe("true");
            properties["HexalithMcpCliFromSource"].ShouldNotBe("true");
        }
        finally {
            if (Directory.Exists(sourceRoot)) {
                Directory.Delete(sourceRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// Collects the <c>Hexalith.EventStore*</c> references of the requested item type, keyed by
    /// package id. A <c>ProjectReference</c> id is derived from its project file name so the rule is
    /// independent of path separator and repository layout.
    /// </summary>
    private static List<(string PackageId, XElement Element)> EventStoreReferences(
        XDocument project,
        string elementName) {
        List<(string PackageId, XElement Element)> references = [];

        foreach (XElement element in project.Descendants(elementName)) {
            string include = (string?)element.Attribute("Include") ?? string.Empty;
            if (include.Length == 0) {
                continue;
            }

            string packageId = string.Equals(elementName, "ProjectReference", StringComparison.Ordinal)
                ? ProjectFileToPackageId(include)
                : include;

            if (!packageId.StartsWith("Hexalith.EventStore", StringComparison.Ordinal)) {
                continue;
            }

            references.Add((packageId, element));
        }

        return references;
    }

    private static bool AdditionalPropertiesExportVersion(string additionalProperties) {
        ArgumentNullException.ThrowIfNull(additionalProperties);

        foreach (string property in additionalProperties.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            int separator = property.IndexOf('=');
            string name = separator < 0 ? property : property[..separator].Trim();
            if (string.Equals(name, "Version", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string ItemMetadataValue(XElement item, string metadataName) {
        IEnumerable<string> attributeValues = item.Attributes()
            .Where(attribute => string.Equals(attribute.Name.LocalName, metadataName, StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Value);
        IEnumerable<string> elementValues = item.Elements()
            .Where(element => string.Equals(element.Name.LocalName, metadataName, StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value);

        return string.Join(';', attributeValues.Concat(elementValues).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string ProjectFileToPackageId(string include) {
        string normalized = include.Replace('\\', '/');
        string fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return fileName.EndsWith(".csproj", StringComparison.Ordinal)
            ? fileName[..^".csproj".Length]
            : fileName;
    }

    /// <summary>
    /// Combines the item condition with every ancestor condition, outermost first. Inspecting only
    /// the item attribute would miss a reference moved into a conditional <c>ItemGroup</c>.
    /// </summary>
    private static string EffectiveCondition(XElement element) {
        IEnumerable<string> conditions = element
            .AncestorsAndSelf()
            .Reverse()
            .Select(node => (string?)node.Attribute("Condition"))
            .Where(condition => !string.IsNullOrWhiteSpace(condition))
            .Select(condition => condition!.Trim());

        return string.Join(" and ", conditions);
    }

    /// <summary>
    /// Detects consumer-local version authority supplied either as an attribute or as a child
    /// element; AC3 forbids both.
    /// </summary>
    private static bool HasLocalVersionAuthority(XElement packageReference)
        => packageReference.Attribute("Version") is not null
        || packageReference.Attribute("VersionOverride") is not null
        || packageReference.Elements().Any(child => child.Name.LocalName is "Version" or "VersionOverride");

    private static Dictionary<string, string> EvaluateUiDependencyRouting(
        string repoRoot,
        string frontComposerRoot,
        string? sourceOverride) {
        var startInfo = new ProcessStartInfo("dotnet") {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add("src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj");
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-getProperty:HexalithFrontComposerFromSource,HexalithCommonsFromSource,HexalithEventStoreFromSource,HexalithMemoriesFromSource,HexalithMcpCliFromSource");
        startInfo.ArgumentList.Add("-property:UseHexalithProjectReferences=false");
        startInfo.ArgumentList.Add($"-property:HexalithFrontComposerRoot={frontComposerRoot}");
        if (sourceOverride is not null) {
            startInfo.ArgumentList.Add($"-property:HexalithFrontComposerFromSource={sourceOverride}");
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet msbuild for FrontComposer routing evaluation.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(
            0,
            $"FrontComposer routing evaluation failed. stdout: {standardOutput}{Environment.NewLine}stderr: {standardError}");

        using JsonDocument evaluation = JsonDocument.Parse(standardOutput);
        return evaluation.RootElement.GetProperty("Properties")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }

    [Fact]
    public void Shared_build_defaults_keep_language_warning_metadata_and_EventStore_governance() {
        string repoRoot = FindRepoRoot();
        XDocument buildProps = XDocument.Load(Path.Combine(repoRoot, "Directory.Build.props"));
        string buildPropsText = File.ReadAllText(Path.Combine(repoRoot, "Directory.Build.props"));

        RequiredValueFor(buildProps, "TargetFramework").ShouldBe("net10.0");
        RequiredValueFor(buildProps, "Nullable").ShouldBe("enable");
        RequiredValueFor(buildProps, "ImplicitUsings").ShouldBe("enable");
        RequiredValueFor(buildProps, "TreatWarningsAsErrors").ShouldBe("true");
        RequiredValueFor(buildProps, "LangVersion").ShouldBe("latest");

        List<string> buildOverrideViolations = [];
        foreach (string projectPath in GetOwnedProjectFiles(repoRoot)) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            foreach (string propertyName in CentrallyGovernedBuildProperties) {
                buildOverrideViolations.AddRange(project
                    .Descendants(propertyName)
                    .Select(property => $"{projectPath}: central build property belongs in Directory.Build.props: {FormatNode(property)}"));
            }
        }

        RequiredValueFor(buildProps, "Authors").ShouldBe("Hexalith Contributors");
        RequiredValueFor(buildProps, "Company").ShouldBe("Hexalith");
        RequiredValueFor(buildProps, "PackageLicenseExpression").ShouldBe("MIT");
        RequiredValueFor(buildProps, "PackageProjectUrl").ShouldBe("https://github.com/Hexalith/Hexalith.Tenants");
        RequiredValueFor(buildProps, "RepositoryUrl").ShouldBe("https://github.com/Hexalith/Hexalith.Tenants");
        RequiredValueFor(buildProps, "RepositoryType").ShouldBe("git");
        RequiredValueFor(buildProps, "Description").ShouldContain("Multi-tenant management service");
        RequiredValueFor(buildProps, "PackageTags").ShouldContain("multi-tenancy");
        RequiredValueFor(buildProps, "PackageReadmeFile").ShouldBe("README.md");

        buildProps.Descendants("HexalithEventStoreRoot").Count().ShouldBe(6);
        buildPropsText.ShouldContain("references\\Hexalith.EventStore\\src\\Hexalith.EventStore.Contracts");
        buildPropsText.ShouldNotContain("GenerateDocumentationFile");
        GetForbiddenPackageReferenceViolations(repoRoot, ["StyleCop", "SonarAnalyzer", "Roslynator"])
            .ShouldBeEmpty("Analyzer names may appear in explanatory comments, but Tenants-owned PackageReference nodes must not import analyzer packages.");
        buildOverrideViolations.ShouldBeEmpty("Per-project build property overrides must not duplicate or weaken shared build defaults.");
    }

    [Fact]
    public void Package_and_host_projects_have_expected_packability_boundaries() {
        string repoRoot = FindRepoRoot();
        HashSet<string> expectedPackageProjects = PublishablePackageProjects.ToHashSet(StringComparer.Ordinal);
        HashSet<string> expectedNonPackageProjects = ExplicitlyNonPackableProjects.ToHashSet(StringComparer.Ordinal);
        HashSet<string> classifiedSourceAndSampleProjects = PublishablePackageProjects
            .Concat(ExplicitlyNonPackableProjects)
            .ToHashSet(StringComparer.Ordinal);

        string[] unclassifiedSourceAndSampleProjects = GetOwnedProjectFiles(repoRoot)
            .Where(project => project.StartsWith("src/", StringComparison.Ordinal) || project.StartsWith("samples/", StringComparison.Ordinal))
            .Where(project => !classifiedSourceAndSampleProjects.Contains(project))
            .ToArray();

        unclassifiedSourceAndSampleProjects.ShouldBeEmpty("Every source/sample project must be classified as one of the five packages or an explicitly non-packable host/sample project.");

        foreach (string projectPath in PublishablePackageProjects) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            ValueFor(project, "IsPackable").ShouldNotBe(
                "false",
                $"{projectPath}: expected packable package project, offending node: {FormatPropertyNodes(project, "IsPackable")}");
        }

        foreach (string projectPath in ExplicitlyNonPackableProjects) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            ValueFor(project, "IsPackable").ShouldBe(
                "false",
                $"{projectPath}: must explicitly opt out of NuGet packing, offending node: {FormatPropertyNodes(project, "IsPackable")}");
        }

        XDocument testProps = XDocument.Load(Path.Combine(repoRoot, "tests/Directory.Build.props"));
        ValueFor(testProps, "IsPackable").ShouldBe("false");
        ValueFor(testProps, "IsTestProject").ShouldBe("true");
        testProps.Descendants("PackageReference")
            .Single(reference => reference.Attribute("Include")?.Value == "Microsoft.Testing.Extensions.CodeCoverage")
            .ShouldNotBeNull();
        GetForbiddenPackageReferenceViolations(repoRoot, ["coverlet.collector"])
            .ShouldBeEmpty("MTP coverage must replace the VSTest-only collector in every Tenants-owned test project.");

        List<string> testPackabilityViolations = [];
        foreach (string projectPath in GetOwnedProjectFiles(repoRoot).Where(project => project.StartsWith("tests/", StringComparison.Ordinal))) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            foreach (XElement isPackable in project.Descendants("IsPackable").Where(property => property.Value != "false")) {
                testPackabilityViolations.Add($"{projectPath}: test project must inherit or preserve test non-packability: {FormatNode(isPackable)}");
            }
        }

        testPackabilityViolations.ShouldBeEmpty();
        GetOwnedProjectFiles(repoRoot).Where(expectedPackageProjects.Contains).Count().ShouldBe(PublishablePackageProjects.Length);
        GetOwnedProjectFiles(repoRoot).Where(expectedNonPackageProjects.Contains).Count().ShouldBe(ExplicitlyNonPackableProjects.Length);
    }

    [Fact]
    public void Container_publishing_stays_on_shared_dotnet_sdk_defaults() {
        string repoRoot = FindRepoRoot();
        XDocument targets = XDocument.Load(Path.Combine(repoRoot, "Directory.Build.targets"));
        XDocument hostProject = XDocument.Load(Path.Combine(repoRoot, "src/Hexalith.Tenants/Hexalith.Tenants.csproj"));
        XDocument uiProject = XDocument.Load(Path.Combine(repoRoot, "src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj"));

        RequiredValueFor(targets, "ContainerBaseImage").ShouldBe("mcr.microsoft.com/dotnet/aspnet:10.0-alpine");
        RequiredValueFor(targets, "ContainerRegistry").ShouldBe("registry.hexalith.com");
        RequiredValueFor(targets, "ContainerUser").ShouldBe("app");
        targets.Descendants("ContainerPort").Single().Attribute("Include")?.Value.ShouldBe("8080");

        string[] labelNames = targets.Descendants("ContainerLabel")
            .Select(label => label.Attribute("Include")?.Value)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .ToArray();

        labelNames.ShouldContain("org.opencontainers.image.source");
        labelNames.ShouldContain("org.opencontainers.image.licenses");
        labelNames.ShouldContain("org.opencontainers.image.vendor");

        RequiredValueFor(hostProject, "EnableContainer").ShouldBe("true");
        RequiredValueFor(hostProject, "ContainerRepository").ShouldBe("tenants");
        RequiredValueFor(uiProject, "EnableContainer").ShouldBe("true");
        RequiredValueFor(uiProject, "ContainerRepository").ShouldBe("tenants-ui");
        GetProjectContainerDefaultOverrides(repoRoot).ShouldBeEmpty("Container defaults belong in Directory.Build.targets; projects should only opt in with EnableContainer and ContainerRepository.");
        GetAdHocContainerFiles(repoRoot).ShouldBeEmpty("Phase 1 container governance must use .NET SDK publish properties instead of Dockerfiles or compose files.");
    }

    [Fact]
    public void Ci_workflow_enforces_build_test_coverage_and_artifact_governance() {
        string repoRoot = FindRepoRoot();
        string workflowPath = Path.Combine(repoRoot, ".github/workflows/ci.yml");
        string workflow = File.ReadAllText(workflowPath);

        workflow.ShouldContain("permissions:\n  contents: read");
        workflow.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        workflow.ShouldContain("solution: Hexalith.Tenants.slnx");
        workflow.ShouldContain("test-platform: microsoft-testing-platform");
        string ciJob = GetYamlJobBlock(workflow, "ci");
        YamlBlockContainsKey(ciJob, "dapr-version").ShouldBeFalse("CI uses the shared domain-ci Dapr default instead of overriding it locally.");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("run-coverage-gate: true");
        workflow.ShouldNotContain("recursive");

        foreach (string testProject in BlockingTestProjects) {
            workflow.ShouldContain(testProject);
        }

        workflow.ShouldContain("coverage-minimum-line: 80");
        workflow.ShouldContain("coverage-required-branch: 100");
        workflow.ShouldContain("coverage-line-scope:");
        string[] coverageLineScope = GetYamlLiteralBlockLines(ciJob, "coverage-line-scope");
        foreach (string coverageScope in new string[]
        {
            "src/Hexalith.Tenants.Contracts/",
            "src/Hexalith.Tenants.Client/",
            "src/Hexalith.Tenants.Server/",
            "src/Hexalith.Tenants.Testing/",
        }) {
            coverageLineScope.ShouldContain(coverageScope);
        }

        workflow.ShouldContain("coverage-isolation-targets:");
        workflow.ShouldContain("src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs");
        GetYamlLiteralBlockLines(ciJob, "integration-test-projects")
            .ShouldContain("tests/Hexalith.Tenants.AppHost.Tests");
        workflow.ShouldContain("aspire-test-project: tests/Hexalith.Tenants.IntegrationTests");

        foreach (string forbiddenFragment in ForbiddenWorkflowFragments) {
            workflow.ShouldNotContain(forbiddenFragment);
        }

        GetWorkflowActionReferences(workflow).ShouldAllBe(
            action => action.Action.StartsWith("Hexalith/Hexalith.Builds/.github/workflows/", StringComparison.Ordinal) && action.Reference == "main",
            "The local workflow delegates to the shared Hexalith.Builds workflow; action pinning is enforced there.");
    }

    [Fact]
    public void Release_workflow_packs_validates_and_publishes_only_expected_packages() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string releaseConfig = File.ReadAllText(Path.Combine(repoRoot, ".releaserc.json"));
        string releaseSecretsValidator = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-release-secrets.sh"));
        string packageValidator = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-nuget-packages.py"));

        GetRootSemanticReleaseConfigFiles(repoRoot).ShouldBe([".releaserc.json"], ".releaserc.json is the only live semantic-release config.");

        // Release is an intentional operator action: an unprotected caller-owned preflight must
        // prove the dispatch selected the live main tip with exact-SHA green push CI before the
        // protected release job may request approval or reach publication secrets.
        workflow.ShouldContain("on:\n  workflow_dispatch:");
        workflow.ShouldNotContain("workflow_run:");
        string verifySourceJob = GetYamlJobBlock(workflow, "verify-source");
        verifySourceJob.ShouldContain("actions: read");
        verifySourceJob.ShouldContain("refs/heads/main");
        verifySourceJob.ShouldContain("git/ref/heads/main");
        verifySourceJob.ShouldContain("actions/workflows/ci.yml/runs");
        verifySourceJob.ShouldContain("no longer the live main tip");
        string releaseJob = GetYamlJobBlock(workflow, "release");
        releaseJob.ShouldContain("needs: verify-source");
        releaseJob.ShouldContain("environment: production");
        releaseJob.ShouldContain("HEXALITH_RELEASE_ENVIRONMENT: production");
        releaseJob.ShouldContain("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT: '" + ExpectedPackageIds.Length.ToString(CultureInfo.InvariantCulture) + "'");
        releaseJob.ShouldContain("HEXALITH_RELEASE_PACKAGE_MANIFEST: tools/release-packages.json");
        releaseJob.ShouldContain("HEXALITH_RELEASE_SOURCE_BRANCH: main");
        releaseJob.ShouldContain("HEXALITH_RELEASE_SOURCE_CI_WORKFLOW: ci.yml");
        workflow.ShouldContain("cancel-in-progress: false");
        releaseJob.ShouldContain("dotnet build Hexalith.Tenants.slnx --no-restore --configuration Release -warnaserror");

        // The release job is owned here so NuGet Trusted Publishing can name this workflow file, but
        // the Builds composites it runs still load from one exact 40-hex Builds commit, checked out
        // and verified locally. That commit is independent of the references/Hexalith.Builds gitlink.
        Match buildsExecutionShaMatch = Regex.Match(releaseJob, @"(?m)^\s*HEXALITH_BUILDS_EXECUTION_SHA:\s*(?<sha>\S+)\s*$");
        buildsExecutionShaMatch.Success.ShouldBeTrue("The release job must declare HEXALITH_BUILDS_EXECUTION_SHA.");
        string buildsExecutionSha = buildsExecutionShaMatch.Groups["sha"].Value;
        IsFullCommitSha(buildsExecutionSha)
            .ShouldBeTrue($"The release workflow must pin an exact 40-hex Hexalith.Builds commit, not '{buildsExecutionSha}'.");
        releaseJob.ShouldContain("repository: Hexalith/Hexalith.Builds");
        releaseJob.ShouldContain("ref: ${{ env.HEXALITH_BUILDS_EXECUTION_SHA }}");
        releaseJob.ShouldContain("path: .hexalith/builds-execution");
        releaseJob.ShouldContain("git -C .hexalith/builds-execution rev-parse HEAD");
        releaseJob.ShouldContain("uses: ./.hexalith/builds-execution/Github/initialize-build");
        releaseJob.ShouldContain("uses: ./.hexalith/builds-execution/Github/publish-containers");
        releaseJob.ShouldContain("builds-execution-sha: ${{ env.HEXALITH_BUILDS_EXECUTION_SHA }}");
        releaseJob.ShouldContain("container-projects: ${{ env.HEXALITH_CONTAINER_PROJECTS }}");
        string[][] containerMappings = GetYamlLiteralBlockLines(releaseJob, "HEXALITH_CONTAINER_PROJECTS")
            .Select(mapping => mapping.Split('|', StringSplitOptions.TrimEntries))
            .ToArray();
        containerMappings.ShouldNotBeEmpty();
        containerMappings.ShouldAllBe(mapping => mapping.Length == 2, "Each release container mapping must contain exactly one project and repository.");
        containerMappings.Any(mapping => mapping is ["src/Hexalith.Tenants/Hexalith.Tenants.csproj", "tenants"])
            .ShouldBeTrue("The server container must remain in the release map.");
        containerMappings.Any(mapping => mapping.Length == 2
                && (string.Equals(mapping[0], "src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj", StringComparison.Ordinal)
                    || string.Equals(mapping[1], "tenants-ui", StringComparison.Ordinal)))
            .ShouldBeFalse("The UI image is environment-composed and must not be published by this release map.");
        releaseJob.ShouldContain("HEXALITH_ZOT_USERNAME: ${{ secrets.HEXALITH_ZOT_USERNAME }}");
        releaseJob.ShouldContain("HEXALITH_ZOT_API_KEY: ${{ secrets.HEXALITH_ZOT_API_KEY }}");
        workflow.ShouldNotContain("secrets: inherit");
        // verify-source already proved this exact head green, so the release path must not rerun
        // the test tiers.
        releaseJob.ShouldNotContain("dotnet test");
        releaseJob.ShouldNotContain("dapr-init");

        workflow.ShouldNotContain("dotnet nuget push **");
        workflow.ShouldNotContain("recursive");

        releaseConfig.ShouldContain("@semantic-release/exec");
        releaseConfig.ShouldContain("dotnet build Hexalith.Tenants.slnx --configuration Release");
        releaseConfig.ShouldContain("HEXALITH_REQUIRE_CONTAINER_PUBLISHER=true");
        releaseConfig.ShouldContain("bash scripts/validate-release-secrets.sh");
        releaseConfig.ShouldContain("python3 scripts/pack-release-packages.py ./nupkgs ${nextRelease.version}");
        releaseConfig.ShouldContain("python3 scripts/validate-nuget-packages.py ./nupkgs");
        releaseConfig.ShouldContain("python3 scripts/validate-consumer-package-references.py ./nupkgs");
        releaseConfig.ShouldContain("dotnet nuget push ./nupkgs/*.nupkg");
        releaseConfig.ShouldContain("./.hexalith/release/publish-containers.sh ${nextRelease.version}");

        // Existing package and tag identities are collisions, not something to skip past.
        releaseConfig.ShouldNotContain("--skip-duplicate");

        // The success path must not write comments or labels: a failure there turns a run red after
        // a completed publish, leaving the partial state recover-partial-release.yml exists to clean
        // up. The empty labels array stays too — the plugin appends its own "semantic-release" label
        // to the failure issue, and with the default the payload carried it twice, which is the HTTP
        // 422 that masked the real publication error in run 30291329462.
        releaseConfig.ShouldContain("\"labels\": []");
        releaseConfig.ShouldContain("\"successCommentCondition\": false");
        releaseConfig.ShouldContain("\"releasedLabels\": false");

        // The publication preflight must run before the first NuGet write and again before the
        // first container write, freezing publication identity at the verify phase.
        releaseConfig.ShouldContain("bash scripts/validate-publication-preflight.sh ${nextRelease.version} verify");
        releaseConfig.ShouldContain("bash scripts/validate-publication-preflight.sh ${nextRelease.version} publish");
        releaseConfig.IndexOf("validate-publication-preflight.sh ${nextRelease.version} publish", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("dotnet nuget push ./nupkgs/*.nupkg", StringComparison.Ordinal));

        // @semantic-release/git pushes a CHANGELOG commit to main during prepare, which would make
        // the publish-phase source proof fail after the tag is pushed and strand the version.
        // Match the quoted plugin names: "@semantic-release/github" contains "@semantic-release/git".
        releaseConfig.ShouldNotContain("\"@semantic-release/git\"");
        releaseConfig.ShouldNotContain("\"@semantic-release/changelog\"");
        releaseConfig.ShouldContain("\"@semantic-release/github\"");
        releaseConfig.ShouldContain("NUGET_API_KEY");
        releaseConfig.ShouldContain("--api-key \\\"$NUGET_API_KEY\\\"");
        releaseConfig.ShouldContain("\"assets\": [\"nupkgs/*.nupkg\"]");
        releaseConfig.ShouldNotContain(".snupkg");
        releaseConfig.ShouldNotContain("**/*.nupkg");
        releaseConfig.ShouldNotContain("--verbosity");
        releaseConfig.IndexOf("dotnet build Hexalith.Tenants.slnx", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("python3 scripts/pack-release-packages.py", StringComparison.Ordinal));
        releaseConfig.IndexOf("python3 scripts/pack-release-packages.py", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("python3 scripts/validate-nuget-packages.py", StringComparison.Ordinal));
        releaseConfig.IndexOf("python3 scripts/validate-nuget-packages.py", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("python3 scripts/validate-consumer-package-references.py", StringComparison.Ordinal));
        releaseConfig.IndexOf("bash scripts/validate-release-secrets.sh", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("dotnet nuget push ./nupkgs/*.nupkg", StringComparison.Ordinal));
        releaseConfig.IndexOf("bash scripts/validate-release-secrets.sh", StringComparison.Ordinal)
            .ShouldBeLessThan(releaseConfig.IndexOf("./.hexalith/release/publish-containers.sh", StringComparison.Ordinal));
        releaseSecretsValidator.ShouldContain("NUGET_API_KEY");
        releaseSecretsValidator.ShouldContain("HEXALITH_REQUIRE_CONTAINER_PUBLISHER");
        releaseSecretsValidator.ShouldContain("HEXALITH_CONTAINER_PROJECTS");
        releaseSecretsValidator.ShouldContain("HEXALITH_ZOT_USERNAME");
        releaseSecretsValidator.ShouldContain("HEXALITH_ZOT_API_KEY");
        releaseSecretsValidator.ShouldContain("tr -d '[:space:]'");
        releaseSecretsValidator.ShouldContain("before publishing NuGet packages");
        releaseSecretsValidator.ShouldContain("before publishing containers");
        releaseSecretsValidator.ShouldContain("Container publisher script is required");
        releaseSecretsValidator.ShouldContain("Container publisher script must be executable");
        releaseSecretsValidator.ShouldContain("HEXALITH_CONTAINER_PROJECTS is required");
        releaseSecretsValidator.ShouldNotContain("set -x");
        packageValidator.ShouldContain("not path.name.endswith(\".snupkg\")");
        packageValidator.ShouldContain("\".symbols.\" not in path.name");
        packageValidator.ShouldContain("DEFAULT_MANIFEST");
        packageValidator.ShouldContain("\"release-packages.json\"");
        packageValidator.ShouldContain("FORBIDDEN_DEPENDENCY_IDS");
        packageValidator.Contains("EXPECTED_DEPENDENCIES", StringComparison.Ordinal).ShouldBeFalse();

        foreach (string forbiddenFragment in ForbiddenWorkflowFragments) {
            workflow.ShouldNotContain(forbiddenFragment);
            releaseConfig.ShouldNotContain(forbiddenFragment);
        }

        // Publication is the deliberate exception to the "latest main" rule for Hexalith.Builds
        // references: Builds code reaches the release path only through the checkout verified
        // against HEXALITH_BUILDS_EXECUTION_SHA, never through a mutable remote reference.
        string[] actionReferences = GetWorkflowUsesValues(workflow);
        actionReferences.ShouldNotBeEmpty();
        actionReferences.ShouldAllBe(
            reference => !reference.StartsWith("Hexalith/Hexalith.Builds/", StringComparison.Ordinal),
            "The release workflow must load Hexalith.Builds code only from the verified execution checkout.");
        actionReferences.Where(reference => reference.StartsWith("./", StringComparison.Ordinal)).ShouldAllBe(
            reference => reference.StartsWith("./.hexalith/builds-execution/Github/", StringComparison.Ordinal),
            "Local actions in the release workflow must come from the verified Builds execution checkout.");
        GetWorkflowActionReferences(workflow).ShouldAllBe(
            action => action.Reference != "main" && action.Reference != "master",
            "Third-party release actions must reference a release tag or commit, not a moving branch.");
    }

    [Fact]
    public void Release_workflows_publish_to_nuget_through_trusted_publishing() {
        string repoRoot = FindRepoRoot();
        string releaseWorkflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string recoveryWorkflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/recover-partial-release.yml"));

        // nuget.org rejects the long-lived organization key with 403 (run 37802538796). Each workflow
        // that pushes packages exchanges its GitHub OIDC token for a one-hour key instead, and the
        // nuget.org policy names that workflow file and the production environment.
        AssertTrustedPublishingJob(releaseWorkflow, GetYamlJobBlock(releaseWorkflow, "release"), "Semantic Release");
        AssertTrustedPublishingJob(recoveryWorkflow, GetYamlJobBlock(recoveryWorkflow, "recover"), "Publish missing release artifacts");

        // OIDC issuance belongs to the protected publishing job alone: the unprotected preflight and
        // the read-only postcondition must not be able to mint a NuGet key.
        GetYamlJobBlock(releaseWorkflow, "verify-source").ShouldNotContain("id-token");
        GetYamlJobBlock(releaseWorkflow, "verify-publication").ShouldNotContain("id-token");
    }

    [Fact]
    public async Task Publication_preflight_rejects_release_versions_below_the_published_floor() {
        string repoRoot = FindRepoRoot();
        string bridge = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-publication-preflight.sh"));
        string contributing = File.ReadAllText(Path.Combine(repoRoot, "CONTRIBUTING.md"));

        // The documented static minimum must match the guard, independently of the current
        // reachable release line. A proposal at the minimum still needs the other preflight proofs.
        bridge.ShouldContain("minimum_release_version=4.0.0");
        contributing.ShouldContain("`minimum_release_version=4.0.0`");
        contributing.ShouldContain("**static safety minimum**");

        // Below-floor proposals must fail before any destination probe, with the same authentic
        // producing-commit and reachability policy as the earlier registry/tag-floor guard.
        (int ExitCode, string Error) collided = await RunPublicationPreflightAsync(repoRoot, "3.3.0", "verify");
        collided.ExitCode.ShouldBe(1);
        collided.Error.ShouldContain("below the 4.0.0 release floor");
        string normalizedContributing = Regex.Replace(contributing, @"\s+", " ");
        foreach (string guidance in new string[]
        {
            "Restore a missing release tag only when it is the authentic tag for that published version, "
                + "points to the commit that actually produced it, and is reachable from the dispatched source.",
            "If that provenance or reachability cannot be established, stop and use a "
                + "separately reviewed release-lineage recovery procedure; never create or move a tag merely to clear this guard.",
        })
        {
            collided.Error.ShouldContain(guidance);
            normalizedContributing.ShouldContain(guidance);
        }
        collided.Error.ShouldNotContain("restore the deleted tags");
        collided.Error.ShouldNotContain("BREAKING CHANGE");
        collided.Error.ShouldNotContain("Everything at or below 3.15.1 is published");
        normalizedContributing.ShouldNotContain("restore the deleted tags until semantic-release resumes above the floor");
        normalizedContributing.ShouldNotContain("major bump, so the change needs a `BREAKING CHANGE` footer");

        (int ExitCode, string Error) publishBlocked = await RunPublicationPreflightAsync(repoRoot, "3.3.0", "publish");
        publishBlocked.ExitCode.ShouldBe(1);
        publishBlocked.Error.ShouldBe(collided.Error);

        (int ExitCode, string Error) highestConsumed = await RunPublicationPreflightAsync(repoRoot, "3.15.1", "verify");
        highestConsumed.ExitCode.ShouldBe(1);
        highestConsumed.Error.ShouldContain("below the 4.0.0 release floor");

        // A prerelease below the floor is still below the floor: the core comparison must not become
        // an escape hatch into the consumed range.
        (int ExitCode, string Error) prereleaseBelowFloor = await RunPublicationPreflightAsync(repoRoot, "3.15.1-rc.1", "verify");
        prereleaseBelowFloor.ExitCode.ShouldBe(1);
        prereleaseBelowFloor.Error.ShouldContain("below the 4.0.0 release floor");

        // At and above the floor the bridge falls through to the existing identity checks, proving
        // the floor neither releases anything on its own nor short-circuits the rest of the preflight.
        // Each case asserts the next expected failure, so a version rejected for a new reason cannot
        // pass by merely omitting the floor message.
        foreach (string allowed in new string[] { "4.0.0", "4.1.0", "10.0.0", "4.0.0-rc.1" }) {
            (int ExitCode, string Error) accepted = await RunPublicationPreflightAsync(repoRoot, allowed, "verify");
            accepted.ExitCode.ShouldBe(1, $"{allowed} must reach the identity checks.");
            accepted.Error.ShouldContain("HEXALITH_BUILDS_EXECUTION_SHA must be an exact lowercase commit SHA.", customMessage: allowed);
            accepted.Error.ShouldNotContain("release floor", customMessage: allowed);
        }

        // A malformed version is still rejected first, by the unchanged format check.
        (int ExitCode, string Error) malformed = await RunPublicationPreflightAsync(repoRoot, "x", "verify");
        malformed.ExitCode.ShouldBe(1);
        malformed.Error.ShouldContain("A plain semantic release version is required.");
        malformed.Error.ShouldNotContain("release floor");
    }

    [Fact]
    public void Partial_release_recovery_is_protected_and_never_skips_duplicate_packages() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/recover-partial-release.yml"));
        string validator = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-partial-release-recovery.sh"));
        string publisher = File.ReadAllText(Path.Combine(repoRoot, "scripts/publish-partial-release.sh"));

        workflow.ShouldContain("workflow_dispatch:");
        workflow.ShouldContain("environment: production");
        workflow.ShouldContain("source_sha:");
        workflow.ShouldContain("HEXALITH_BUILDS_EXECUTION_SHA:");
        workflow.ShouldContain("git submodule update --init references/Hexalith.Builds");
        validator.ShouldContain("No successful push CI run exists for the exact source SHA");
        validator.ShouldContain("The package manifest must contain exactly five packages");
        validator.ShouldContain("missing-packages");
        publisher.ShouldContain("while IFS= read -r package_id");
        publisher.ShouldNotContain("--skip-duplicate");
        publisher.ShouldContain("gh release create");
        File.ReadAllText(Path.Combine(repoRoot, "scripts/verify-partial-release.sh")).ShouldContain("Release asset count is not five");
    }

    [Fact]
    public async Task Release_workflow_requires_a_published_tag_at_the_exact_dispatched_source() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string publicationJob = GetYamlJobBlock(workflow, "verify-publication");
        const string releasesInvocation = "api --paginate repos/Hexalith/Hexalith.Tenants/releases?per_page=100";
        const string versionTag = "v1.2.3";
        string versionTagEndpoint = TagRefEndpoint(versionTag);

        publicationJob.ShouldContain("needs: release");
        publicationJob.ShouldContain("contents: read");
        publicationJob.ShouldNotContain("contents: write");
        publicationJob.ShouldContain("select(.draft == false)");
        publicationJob.ShouldContain("repos/${REPOSITORY}/git/ref/tags/${encoded_tag}");
        publicationJob.ShouldContain("repos/${REPOSITORY}/git/tags/${object_sha}");
        publicationJob.ShouldContain("[ \"$tag_sha\" = \"$DISPATCH_SHA\" ]");
        publicationJob.ShouldContain("Inspect the Semantic Release output");
        publicationJob.ShouldContain("redispatching will not help");
        publicationJob.ShouldContain("Redispatch only when the source became stale");
        publicationJob.ShouldNotContain("repos/${REPOSITORY}/commits/");
        publicationJob.ShouldNotContain("releases/latest");
        publicationJob.ShouldNotContain("git/ref/heads/main");

        string dispatchSha = new('a', 40);
        (int ExitCode, string Output, string Error) noRelease = await RunPublicationPostconditionAsync(
            workflow,
            "[]",
            GitHubResponses(),
            dispatchSha,
            [releasesInvocation]);
        noRelease.ExitCode.ShouldBe(1, noRelease.Output);
        noRelease.Error.ShouldStartWith("Inspect the Semantic Release output");
        noRelease.Error.ShouldContain("no published GitHub Release tag resolves to dispatched SHA");
        noRelease.Error.ShouldContain("redispatching will not help");

        string differentSha = new('b', 40);
        (int ExitCode, string Output, string Error) differentRelease = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":false,\"tag_name\":\"v1.2.3\"}]",
            GitHubResponses((versionTagEndpoint, 0, TagRefResponse(versionTag, "commit", differentSha))),
            dispatchSha,
            [releasesInvocation, $"api {versionTagEndpoint}"]);
        differentRelease.ExitCode.ShouldBe(1, differentRelease.Output);
        differentRelease.Error.ShouldContain("Inspect the Semantic Release output");

        const string unrelatedTag = "v1.2.2";
        string unrelatedTagEndpoint = TagRefEndpoint(unrelatedTag);
        (int ExitCode, string Output, string Error) matchingRelease = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":false,\"tag_name\":\"v1.2.2\"},{\"draft\":false,\"tag_name\":\"v1.2.3\"},{\"draft\":false,\"tag_name\":\"v1.2.4\"}]",
            GitHubResponses(
                (unrelatedTagEndpoint, 0, TagRefResponse(unrelatedTag, "commit", differentSha)),
                (versionTagEndpoint, 0, TagRefResponse(versionTag, "commit", dispatchSha))),
            dispatchSha,
            [releasesInvocation, $"api {unrelatedTagEndpoint}", $"api {versionTagEndpoint}"]);
        matchingRelease.ExitCode.ShouldBe(0, matchingRelease.Error);
        matchingRelease.Output.ShouldContain("resolves to dispatched SHA");

        (int ExitCode, string Output, string Error) draftOnlyRelease = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":true,\"tag_name\":\"v1.2.3\"}]",
            GitHubResponses(),
            dispatchSha,
            [releasesInvocation]);
        draftOnlyRelease.ExitCode.ShouldBe(1, draftOnlyRelease.Output);
        draftOnlyRelease.Error.ShouldContain("No published GitHub Release tag resolves");

        (int ExitCode, string Output, string Error) malformedReleases = await RunPublicationPostconditionAsync(
            workflow,
            "{}",
            GitHubResponses(),
            dispatchSha,
            [releasesInvocation]);
        malformedReleases.ExitCode.ShouldBe(1, malformedReleases.Output);
        malformedReleases.Error.ShouldContain("GitHub Releases response could not be validated safely");

        (string Name, string TagRef, string ExpectedError)[] malformedTagCases =
        [
            ("wrong ref identity", TagRefResponse("v9.9.9", "commit", dispatchSha), "response could not be validated safely"),
            ("malformed ref SHA", TagRefResponse(versionTag, "commit", "not-a-sha"), "object must have an exact lowercase SHA"),
            ("unsupported ref object", TagRefResponse(versionTag, "tree", dispatchSha), "unsupported object type 'tree'"),
        ];
        foreach ((string name, string tagRef, string expectedError) in malformedTagCases) {
            (int ExitCode, string Output, string Error) malformedTag = await RunPublicationPostconditionAsync(
                workflow,
                "[{\"draft\":false,\"tag_name\":\"v1.2.3\"}]",
                GitHubResponses((versionTagEndpoint, 0, tagRef)),
                dispatchSha,
                [releasesInvocation, $"api {versionTagEndpoint}"]);
            malformedTag.ExitCode.ShouldBe(1, $"{name}: {malformedTag.Output}");
            malformedTag.Error.ShouldContain(expectedError);
        }

        (int ExitCode, string Output, string Error) releasesApiFailure = await RunPublicationPostconditionAsync(
            workflow,
            "[]",
            GitHubResponses(),
            dispatchSha,
            [releasesInvocation],
            releasesExitCode: 22);
        releasesApiFailure.ExitCode.ShouldBe(1, releasesApiFailure.Output);
        releasesApiFailure.Error.ShouldContain("GitHub Releases API could not be queried safely");

        (int ExitCode, string Output, string Error) tagRefApiFailure = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":false,\"tag_name\":\"v1.2.3\"}]",
            GitHubResponses((versionTagEndpoint, 22, string.Empty)),
            dispatchSha,
            [releasesInvocation, $"api {versionTagEndpoint}"]);
        tagRefApiFailure.ExitCode.ShouldBe(1, tagRefApiFailure.Output);
        tagRefApiFailure.Error.ShouldContain("exact tag ref 'refs/tags/v1.2.3' could not be resolved");

        string tagObjectSha = new('c', 40);
        string tagObjectEndpoint = TagObjectEndpoint(tagObjectSha);
        (int ExitCode, string Output, string Error) peelApiFailure = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":false,\"tag_name\":\"v1.2.3\"}]",
            GitHubResponses(
                (versionTagEndpoint, 0, TagRefResponse(versionTag, "tag", tagObjectSha)),
                (tagObjectEndpoint, 22, string.Empty)),
            dispatchSha,
            [releasesInvocation, $"api {versionTagEndpoint}", $"api {tagObjectEndpoint}"]);
        peelApiFailure.ExitCode.ShouldBe(1, peelApiFailure.Output);
        peelApiFailure.Error.ShouldContain("could not be peeled");

        (int ExitCode, string Output, string Error) malformedPeeledObject = await RunPublicationPostconditionAsync(
            workflow,
            "[{\"draft\":false,\"tag_name\":\"v1.2.3\"}]",
            GitHubResponses(
                (versionTagEndpoint, 0, TagRefResponse(versionTag, "tag", tagObjectSha)),
                (tagObjectEndpoint, 0, TagObjectResponse(tagObjectSha, "commit", "not-a-sha"))),
            dispatchSha,
            [releasesInvocation, $"api {versionTagEndpoint}", $"api {tagObjectEndpoint}"]);
        malformedPeeledObject.ExitCode.ShouldBe(1, malformedPeeledObject.Output);
        malformedPeeledObject.Error.ShouldContain("did not peel to an exact lowercase commit SHA");
    }

    [Fact]
    public void Release_workflow_proves_the_tag_floor_covers_the_registry_before_approval() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string verifySourceJob = GetYamlJobBlock(workflow, "verify-source");

        // Published versions above the highest reachable release tag leave lineage unproven and
        // risk a collision; the guard does not compute Semantic Release's proposal. It must remain
        // in the unprotected job, before an invalid dispatch reaches approval or release secrets.
        verifySourceJob.ShouldContain($"- name: {RegistryDriftGuardStepName}");
        verifySourceJob.ShouldContain("authentic tag for that published version");
        verifySourceJob.ShouldContain("commit that actually produced it");
        verifySourceJob.ShouldContain("reachable from the dispatched source");
        verifySourceJob.ShouldContain("separately reviewed release-lineage recovery procedure");
        verifySourceJob.ShouldContain("never create or move a tag merely to clear this guard");
        verifySourceJob.ShouldNotContain("tag at or above the highest published version");
        GetYamlJobBlock(workflow, "release").ShouldContain("needs: verify-source");

        // The behavioural harness supplies its own environment, so the step's declared env: is
        // only verified here. A missing name is an unbound variable under set -u at dispatch.
        string guardStep = GetYamlStepBlock(workflow, RegistryDriftGuardStepName);
        guardStep.ShouldContain("GH_TOKEN: ${{ github.token }}");
        guardStep.ShouldContain("REPOSITORY: ${{ github.repository }}");
        guardStep.ShouldContain("DISPATCH_SHA: ${{ github.sha }}");
        guardStep.ShouldContain("PACKAGE_MANIFEST: tools/release-packages.json");

        // The stub cannot see curl's flags, so the transport hardening is pinned here: --location
        // keeps a registry redirect from reading as a hard block, --max-time bounds a hung
        // connection, and --retry keeps one CDN blip from failing an operator-initiated release.
        guardStep.ShouldContain("api.nuget.org/v3-flatcontainer");
        guardStep.ShouldContain("--location");
        guardStep.ShouldContain("--max-time 30");
        guardStep.ShouldContain("--retry 3");

        // The unprotected job runs no actions, so the guard reads the manifest through the contents
        // API rather than checking the source out.
        verifySourceJob.ShouldNotContain("uses:");

        // The inventory comes from the manifest; restating ids here would let the two drift apart.
        foreach (string packageId in ExpectedPackageIds) {
            verifySourceJob.ShouldNotContain(packageId);
        }
    }

    /// <summary>
    /// Gets invalid plain release tags for fallback and invalid-only evidence checks.
    /// </summary>
    public static TheoryData<string> InvalidPlainReleaseTags { get; } = new()
    {
        "v09.0.0",
        "v3.016.0",
        "v3.15.02",
        "v0009.0.0",
        "v3.0016.0",
        "v3.15.0002",
        "v00.0.0",
        "v0.00.0",
        "v0.0.00",
        "v9007199254740992.0.0",
        "v0.9007199254740992.0",
        "v0.0.9007199254740992",
        "v10000000000000000.0.0",
        "v0.10000000000000000.0",
        "v0.0.10000000000000000",
        "v9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999.0.0",
        "v0.9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999.0",
        "v0.0.9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999",
        "v99.0.0+build.1",
        "v99.0.0.0",
    };

    /// <summary>
    /// Invalid release tags cannot hide registry drift or trigger reachability reads.
    /// </summary>
    /// <param name="invalidTag">The release tag under test.</param>
    [Theory]
    [MemberData(nameof(InvalidPlainReleaseTags))]
    public async Task ReleaseWorkflowIgnoresTagsOutsidePlainReleasePolicy(string invalidTag)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string validTag = "v3.15.1";
        const string packageId = "Hexalith.Tenants.Contracts";

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(invalidTag, validTag)),
                (CompareEndpoint(validTag), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions("4.0.0"))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint(validTag)}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl(packageId)}",
            ]);

        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain($"exceed the release tag floor {validTag}");
        result.Error.ShouldContain($"{packageId} 4.0.0");
        result.Error.ShouldNotContain(invalidTag);
    }

    /// <summary>
    /// Invalid-only release tag evidence cannot establish a release floor.
    /// </summary>
    /// <param name="invalidTag">The release tag under test.</param>
    [Theory]
    [MemberData(nameof(InvalidPlainReleaseTags))]
    public async Task ReleaseWorkflowCannotEstablishFloorFromInvalidTag(string invalidTag)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses((TagRefsEndpoint, 0, TagRefsResponse(invalidTag))),
            RegistryResponses(),
            [$"api --paginate {TagRefsEndpoint}"]);

        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain("No release tag is reachable");
    }

    /// <summary>
    /// Canonical release tags at the supported numeric boundaries remain accepted.
    /// </summary>
    /// <param name="validTag">The release tag under test.</param>
    [Theory]
    [InlineData("v0.0.0")]
    [InlineData("v1000000000000000.0.0")]
    [InlineData("v0.1000000000000000.0")]
    [InlineData("v0.0.1000000000000000")]
    [InlineData("v9007199254740991.0.0")]
    [InlineData("v0.9007199254740991.0")]
    [InlineData("v0.0.9007199254740991")]
    [InlineData("v9007199254740991.9007199254740991.9007199254740991")]
    public async Task ReleaseWorkflowAcceptsSemanticReleaseNumericBoundaries(string validTag)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(validTag)),
                (CompareEndpoint(validTag), 0, CompareResponse("identical")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions("0.0.0"))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint(validTag)}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl(packageId)}",
            ]);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain($"Release tag floor {validTag} is at or above every published version");
    }

    /// <summary>
    /// Valid boundary tags retain numeric ordering and require source reachability.
    /// </summary>
    [Fact]
    public async Task ReleaseWorkflowOrdersSafeIntegerBoundariesBeforeReachability()
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string maximumTag = "v9007199254740991.9007199254740991.9007199254740991";
        const string lowerTag = "v9007199254740991.9007199254740991.9007199254740990";
        const string packageId = "Hexalith.Tenants.Contracts";

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(lowerTag, "v9.10.0", maximumTag, lowerTag)),
                (CompareEndpoint(maximumTag), 0, CompareResponse("behind")),
                (CompareEndpoint(lowerTag), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions("4.0.0"))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint(maximumTag)}",
                $"api {CompareEndpoint(lowerTag)}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl(packageId)}",
            ]);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain($"Release tag floor {lowerTag} is at or above every published version");
    }

    /// <summary>
    /// Gets invalid registry versions that must be rejected before prerelease exclusion.
    /// </summary>
    public static TheoryData<string, bool> InvalidRegistryVersions
    {
        get
        {
            string[] versions =
            [
                "03.15.1", "3.015.1", "3.15.01", "3.15.1.01",
                "00.0.0", "0.00.0", "0.0.00", "0.0.0.00",
                "03.15.1-rc.1", "3.015.1-rc.1", "3.15.01-rc.1", "3.15.1.01-rc.1",
                "2147483648.0.0", "0.2147483648.0", "0.0.2147483648", "0.0.0.2147483648",
                "2147483648.0.0-rc.1", "0.2147483648.0-rc.1", "0.0.2147483648-rc.1", "0.0.0.2147483648-rc.1",
                "9999999999999999999999999999999999999999.0.0",
                "9999999999999999999999999999999999999999.0.0-rc.1",
                "3.15.1-", "3.15.1-.", "3.15.1-.rc", "3.15.1-rc.", "3.15.1-rc..1",
                "3.15.1-01", "3.15.1-00", "3.15.1-rc.01", "3.15.1-01.rc", "3.15.1-rc.00.1",
                "3.15.1-rc_1", "3.15.1-révision", "3.15.1-rc/1",
                "3.15.1+build.1", "3.15.1-rc.1+build.1",
                "3.15.1\n", "3.15.1-rc.1\n", " 3.15.1", "3.15.1 ",
                "3", "3.15.1.0.1", "3.15..1", "v3.15.1", "",
                "4.1.0-" + new string('a', 59), "4.1.0-" + new string('9', 59),
            ];
            TheoryData<string, bool> data = new();
            foreach (string version in versions)
            {
                data.Add(version, false);
                data.Add(version, true);
            }

            return data;
        }
    }

    /// <summary>
    /// Malformed registry evidence fails closed in isolated and mixed version indexes.
    /// </summary>
    /// <param name="invalidVersion">The untrusted registry version.</param>
    /// <param name="includeStable">Whether valid stable evidence accompanies the invalid entry.</param>
    [Theory]
    [MemberData(nameof(InvalidRegistryVersions))]
    public async Task ReleaseRegistryRejectsInvalidVersionBeforeClassification(string invalidVersion, bool includeStable)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";
        const string floorTag = "v3.15.1";
        string[] versions = includeStable ? ["3.15.1", invalidVersion, "4.1.0-rc.1"] : [invalidVersion];

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(floorTag)),
                (CompareEndpoint(floorTag), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions(versions))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint(floorTag)}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl(packageId)}",
            ]);

        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain($"The published version index for {packageId} could not be validated safely.");
        result.Output.ShouldNotContain("is at or above every published version");
        result.Error.ShouldNotContain("Published package versions exceed");
    }

    /// <summary>
    /// Valid prerelease identifiers remain excluded even when their cores exceed the floor.
    /// </summary>
    /// <param name="version">The valid prerelease version.</param>
    [Theory]
    [InlineData("4.1.0-rc.1")]
    [InlineData("4.1.0-0")]
    [InlineData("4.1.0-1")]
    [InlineData("4.1.0-alpha.0")]
    [InlineData("4.1.0-rc-001")]
    [InlineData("4.1.0-01a")]
    [InlineData("4.1.0--")]
    [InlineData("4.1.0--.rc")]
    [InlineData("4.1.0-rc.-")]
    [InlineData("4.1.0-RC.1")]
    [InlineData("4.1-rc.1")]
    [InlineData("4.1.0.0-rc.1")]
    [InlineData("4.1.0.1-rc.1")]
    [InlineData("2147483647.2147483647.2147483647.2147483647-rc.1")]
    [InlineData("4.1.0-9999999999999999999999999999999999999999")]
    [InlineData("4.1.0-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("4.1.0-9999999999999999999999999999999999999999999999999999999999")]
    public async Task ReleaseRegistryExcludesOnlyValidPrereleaseVersions(string version)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";
        const string floorTag = "v3.15.1";

        foreach (string[] versions in new[] { new[] { version }, new[] { "3.15.1", version } })
        {
            (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
                workflow,
                GitHubResponses(
                    (TagRefsEndpoint, 0, TagRefsResponse(floorTag)),
                    (CompareEndpoint(floorTag), 0, CompareResponse("identical")),
                    (ManifestEndpoint, 0, ManifestResponse(packageId))),
                RegistryResponses((packageId, 0, "200", PublishedVersions(versions))));

            result.ExitCode.ShouldBe(0, result.Error);
            result.Output.ShouldContain($"Release tag floor {floorTag} is at or above every published version");
        }
    }

    /// <summary>
    /// Invalid JSON values and misplaced invalid strings cannot be hidden by stable drift.
    /// </summary>
    /// <param name="entries">The raw registry version array entries.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"3.15.2\",\"4.1.0-rc..1\"")]
    [InlineData("\"4.1.0-rc..1\",\"3.15.2\"")]
    public async Task ReleaseRegistryRejectsInvalidIndexEntries(string entries)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";
        const string floorTag = "v3.15.1";

        foreach (string body in new[] { $"{{\"versions\":[{entries}]}}", $"{{\"versions\":[\"3.15.1\",{entries},\"4.1.0-rc.1\"]}}" })
        {
            (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
                workflow,
                GitHubResponses(
                    (TagRefsEndpoint, 0, TagRefsResponse(floorTag)),
                    (CompareEndpoint(floorTag), 0, CompareResponse("ahead")),
                    (ManifestEndpoint, 0, ManifestResponse(packageId))),
                RegistryResponses((packageId, 0, "200", body)));

            result.ExitCode.ShouldBe(1, result.Output);
            result.Error.ShouldContain($"The published version index for {packageId} could not be validated safely.");
            result.Output.ShouldNotContain("is at or above every published version");
            result.Error.ShouldNotContain("Published package versions exceed");
        }
    }

    /// <summary>
    /// Excluding valid prereleases retains the highest genuine stable version in a mixed index.
    /// </summary>
    [Fact]
    public async Task ReleaseRegistryReportsStableDriftAlongsideValidPrerelease()
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";
        const string floorTag = "v3.15.1";

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(floorTag)),
                (CompareEndpoint(floorTag), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions("3.15.1", "3.15.2", "4.1.0-rc.1"))));

        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain($"exceed the release tag floor {floorTag}");
        result.Error.ShouldContain($"{packageId} 3.15.2");
        result.Error.ShouldNotContain("4.1.0-rc.1");
        result.Error.ShouldNotContain("could not be validated safely");
    }

    /// <summary>
    /// Stable compatibility aliases and NuGet integer boundaries retain numeric ordering.
    /// </summary>
    /// <param name="version">The stable registry version.</param>
    /// <param name="floorTag">The reachable release tag.</param>
    /// <param name="exceedsFloor">Whether the stable version exceeds the floor.</param>
    [Theory]
    [InlineData("0.0.0", "v0.0.0", false)]
    [InlineData("3.15", "v3.15.1", false)]
    [InlineData("3.15.1.0", "v3.15.1", false)]
    [InlineData("2147483647.0.0", "v2147483647.0.0", false)]
    [InlineData("0.2147483647.0", "v0.2147483647.0", false)]
    [InlineData("0.0.2147483647", "v0.0.2147483647", false)]
    [InlineData("3.15.0.2147483647", "v3.15.1", false)]
    [InlineData("3.15.1.2147483647", "v3.15.1", true)]
    [InlineData("10.0.0", "v9.9.9", true)]
    public async Task ReleaseRegistryPreservesStableVersionBoundaries(string version, string floorTag, bool exceedsFloor)
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github/workflows/release.yml"));
        const string packageId = "Hexalith.Tenants.Contracts";

        (int ExitCode, string Output, string Error) result = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse(floorTag)),
                (CompareEndpoint(floorTag), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(packageId))),
            RegistryResponses((packageId, 0, "200", PublishedVersions(version))));

        result.ExitCode.ShouldBe(exceedsFloor ? 1 : 0, result.Error);
        if (exceedsFloor)
        {
            result.Error.ShouldContain($"exceed the release tag floor {floorTag}");
            result.Error.ShouldContain($"{packageId} {version}");
        }
        else
        {
            result.Output.ShouldContain($"Release tag floor {floorTag} is at or above every published version");
        }
    }

    [Fact]
    public async Task Release_workflow_registry_drift_guard_fails_closed_on_every_unprovable_state() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string[] allPackages = ExpectedPackageIds;
        string[] sortedPackages = [.. allPackages.OrderBy(id => id, StringComparer.Ordinal)];

        // A floor at or above every published version releases normally. Only the highest reachable
        // tag is compared, so one comparison call is made even though two release tags exist.
        //
        // compare/{base}...{head} reports head relative to base, so a tag that IS an ancestor of
        // the dispatched source reports "ahead". Verified live against this repository:
        // compare/v3.2.17...v3.2.18 -> {"status":"ahead","ahead_by":2}. Using "behind" as the
        // reachable fixture would make this suite green while the guard blocked every dispatch.
        (int ExitCode, string Output, string Error) covered = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.2.18", "v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => (id, 0, "200", PublishedVersions("3.2.18", "3.15.1")))]),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint("v3.15.1")}",
                $"api {ManifestEndpoint}",
                .. sortedPackages.Select(id => $"curl {RegistryUrl(id)}"),
            ]);
        covered.ExitCode.ShouldBe(0, covered.Error);
        covered.Output.ShouldContain("Release tag floor v3.15.1 is at or above every published version");

        // Run 30333764600: the v3.3.0..v3.15.1 tags were deleted while nuget.org kept those
        // versions, so the floor fell back to v3.2.18 and 3.3.0 was already taken.
        (int ExitCode, string Output, string Error) drifted = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.2.18")),
                (CompareEndpoint("v3.2.18"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => id == "Hexalith.Tenants.Contracts"
                ? (id, 0, "200", PublishedVersions("3.2.18", "3.3.0", "3.15.1"))
                : (id, 0, "200", PublishedVersions("3.2.18")))]));
        drifted.ExitCode.ShouldBe(1, drifted.Output);
        drifted.Error.ShouldContain("exceed the release tag floor v3.2.18");
        drifted.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.1");
        drifted.Error.ShouldContain("authentic tag for that published version");
        drifted.Error.ShouldContain("commit that actually produced it");
        drifted.Error.ShouldContain("reachable from the dispatched source");
        drifted.Error.ShouldContain("separately reviewed release-lineage recovery procedure");
        drifted.Error.ShouldContain("never create or move a tag merely to clear this guard");
        drifted.Error.ShouldNotContain("tag at or above the highest published version");
        drifted.Error.ShouldContain("Release lineage is unproven and a proposed version may collide");
        drifted.Error.ShouldContain("This guard does not compute the proposed release version");
        drifted.Error.ShouldNotContain("would propose a version that already exists");

        // A sparse registry still fails lineage validation even though it leaves version gaps.
        // No release proposal is computed, so neither a specific collision nor its absence is known.
        (int ExitCode, string Output, string Error) sparseRegistry = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.2.18")),
                (CompareEndpoint("v3.2.18"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.2.18", "3.15.1"))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint("v3.2.18")}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl("Hexalith.Tenants.Contracts")}",
            ]);
        sparseRegistry.ExitCode.ShouldBe(1, sparseRegistry.Output);
        sparseRegistry.Error.ShouldContain("exceed the release tag floor v3.2.18");
        sparseRegistry.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.1");
        sparseRegistry.Error.ShouldContain("Release lineage is unproven and a proposed version may collide");
        sparseRegistry.Error.ShouldContain("This guard does not compute the proposed release version");
        sparseRegistry.Error.ShouldNotContain("would propose a version that already exists");
        sparseRegistry.Error.ShouldContain("authentic tag for that published version");
        sparseRegistry.Error.ShouldContain("separately reviewed release-lineage recovery procedure");
        sparseRegistry.Output.ShouldNotContain("Release tag floor v3.2.18 is at or above");

        // 3.15.1 is above 3.2.18. A lexical comparison reverses that and reports no drift at all.
        (int ExitCode, string Output, string Error) lexical = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.2.18")),
                (CompareEndpoint("v3.2.18"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1"))));
        lexical.ExitCode.ShouldBe(1, lexical.Output);
        lexical.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.1");

        // The version just released is published at the floor. Equality is not a collision.
        (int ExitCode, string Output, string Error) equal = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v4.0.0")),
                (CompareEndpoint("v4.0.0"), 0, CompareResponse("identical")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => (id, 0, "200", PublishedVersions("3.2.18", "4.0.0")))]));
        equal.ExitCode.ShouldBe(0, equal.Error);

        // NuGet treats a missing revision and an explicit zero revision as the same stable version.
        (int ExitCode, string Output, string Error) zeroRevision = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1.0"))));
        zeroRevision.ExitCode.ShouldBe(0, zeroRevision.Error);

        // A nonzero revision participates in ordering and therefore exceeds its three-part floor.
        (int ExitCode, string Output, string Error) nonzeroRevision = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1.1"))));
        nonzeroRevision.ExitCode.ShouldBe(1, nonzeroRevision.Output);
        nonzeroRevision.Error.ShouldContain("exceed the release tag floor v3.15.1");
        nonzeroRevision.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.1.1");
        nonzeroRevision.Error.ShouldContain("cannot be represented by this repository's three-part release tags");
        nonzeroRevision.Error.ShouldContain("separately reviewed release-lineage recovery procedure");
        nonzeroRevision.Error.ShouldContain("never create or move a tag merely to clear this guard");
        nonzeroRevision.Error.ShouldNotContain("authentic tag for that published version");

        // Revisions are numeric components: 10 is newer than 9, not lexically older than it.
        (int ExitCode, string Output, string Error) revisionOrdering = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1.9", "3.15.1.10"))));
        revisionOrdering.ExitCode.ShouldBe(1, revisionOrdering.Output);
        revisionOrdering.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.1.10");

        // Revision ordering cannot cross a lower patch boundary: 3.15.0.999 remains below 3.15.1.
        (int ExitCode, string Output, string Error) lowerCoreHighRevision = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.0.999"))));
        lowerCoreHighRevision.ExitCode.ShouldBe(0, lowerCoreHighRevision.Error);

        // Semantic Release only considers tags merged into the released branch, so a higher tag on
        // an unmerged branch must not be mistaken for the floor.
        (int ExitCode, string Output, string Error) unreachable = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1", "v9.9.9")),
                (CompareEndpoint("v9.9.9"), 0, CompareResponse("diverged")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => (id, 0, "200", PublishedVersions("3.15.1")))]),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint("v9.9.9")}",
                $"api {CompareEndpoint("v3.15.1")}",
                $"api {ManifestEndpoint}",
                .. sortedPackages.Select(id => $"curl {RegistryUrl(id)}"),
            ]);
        unreachable.ExitCode.ShouldBe(0, unreachable.Error);
        unreachable.Output.ShouldContain("Release tag floor v3.15.1");

        // "behind" means the tag is a DESCENDANT of the dispatched source -- a tag on an unmerged
        // release branch, which Semantic Release will not see. Adopting it as the floor would set
        // the floor too high and pass vacuously over real drift.
        (int ExitCode, string Output, string Error) descendantTag = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1", "v9.9.9")),
                (CompareEndpoint("v9.9.9"), 0, CompareResponse("behind")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("4.5.0"))));
        descendantTag.ExitCode.ShouldBe(1, descendantTag.Output);
        descendantTag.Error.ShouldContain("exceed the release tag floor v3.15.1");
        descendantTag.Error.ShouldContain("Hexalith.Tenants.Contracts 4.5.0");

        // Every violating package is reported, so one redispatch resolves them all.
        (int ExitCode, string Output, string Error) manyViolations = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.2.18")),
                (CompareEndpoint("v3.2.18"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => (id, 0, "200", PublishedVersions("3.15.1")))]));
        manyViolations.ExitCode.ShouldBe(1, manyViolations.Output);
        foreach (string packageId in allPackages) {
            manyViolations.Error.ShouldContain($"{packageId} 3.15.1");
        }

        // NuGet normalises 3.16.0.0 to 3.16.0, so a four-segment version occupies a three-segment
        // one. Dropping it as unparseable would report an occupied version as free.
        (int ExitCode, string Output, string Error) fourSegment = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1", "3.15.2.0"))));
        fourSegment.ExitCode.ShouldBe(1, fourSegment.Output);
        fourSegment.Error.ShouldContain("Hexalith.Tenants.Contracts 3.15.2.0");

        // 4.1.0-rc.1 does not occupy 4.1.0, so treating a prerelease as a collision would block
        // releases that cannot collide.
        (int ExitCode, string Output, string Error) prerelease = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1", "4.1.0-rc.1"))));
        prerelease.ExitCode.ShouldBe(0, prerelease.Error);

        // An entry this cannot parse is unprovable, not absent.
        (int ExitCode, string Output, string Error) unrecognisedVersion = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1", "latest"))));
        unrecognisedVersion.ExitCode.ShouldBe(1, unrecognisedVersion.Output);
        unrecognisedVersion.Error.ShouldContain("could not be validated safely");

        // Only vMAJOR.MINOR.PATCH names are release tags. A two-segment tag would make the floor
        // 3.15, reading every published 3.15.x as drift; a non-version tag would abort jq raw.
        (int ExitCode, string Output, string Error) nonReleaseTags = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("nightly", "v3.15", "v9.9.9-rc.1", "v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
            RegistryResponses(("Hexalith.Tenants.Contracts", 0, "200", PublishedVersions("3.15.1"))),
            [
                $"api --paginate {TagRefsEndpoint}",
                $"api {CompareEndpoint("v3.15.1")}",
                $"api {ManifestEndpoint}",
                $"curl {RegistryUrl("Hexalith.Tenants.Contracts")}",
            ]);
        nonReleaseTags.ExitCode.ShouldBe(0, nonReleaseTags.Error);
        nonReleaseTags.Output.ShouldContain("Release tag floor v3.15.1");

        // A 200 whose body is the wrong shape must not become a silent "no tags".
        (int ExitCode, string Output, string Error) malformedTagRefs = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses((TagRefsEndpoint, 0, "[{\"ref\":\"v9.9.9\"}]")),
            RegistryResponses());
        malformedTagRefs.ExitCode.ShouldBe(1, malformedTagRefs.Output);
        malformedTagRefs.Error.ShouldContain("tag refs response could not be validated safely");

        // GitHub returns encoding "none" with empty content for files over 1 MB.
        (int ExitCode, string Output, string Error) unreadableManifest = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("none", string.Empty))),
            RegistryResponses());
        unreadableManifest.ExitCode.ShouldBe(1, unreadableManifest.Output);
        unreadableManifest.Error.ShouldContain("manifest response could not be validated safely");

        // The contents API is asked for one specific path; a response describing anything else --
        // a directory listing, or a different file carrying a packages array -- would validate ids
        // from the wrong inventory and probe the wrong packages.
        (int ExitCode, string Output, string Error) wrongManifestPath = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, JsonSerializer.Serialize(new {
                    type = "file",
                    path = "tools/other-packages.json",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        "{\"packages\":[{\"id\":\"Hexalith.Tenants.Contracts\"}]}")),
                }))),
            RegistryResponses());
        wrongManifestPath.ExitCode.ShouldBe(1, wrongManifestPath.Output);
        wrongManifestPath.Error.ShouldContain("manifest response could not be validated safely");

        // The dispatched SHA reaches three request URLs, so its shape is load bearing.
        (int ExitCode, string Output, string Error) badSha = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses((TagRefsEndpoint, 0, TagRefsResponse("v3.15.1"))),
            RegistryResponses(),
            dispatchSha: "main");
        badSha.ExitCode.ShouldBe(1, badSha.Output);
        badSha.Error.ShouldContain("exact lowercase commit SHA");

        // A package with no published versions at all cannot sit above the floor.
        (int ExitCode, string Output, string Error) neverPublished = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse(allPackages))),
            RegistryResponses([.. allPackages.Select(id => (id, 0, "404", "not found"))]));
        neverPublished.ExitCode.ShouldBe(0, neverPublished.Error);

        // Absence of drift must be proved, never assumed from an unusable response.
        (string Description, int ExitCode, string Status, string Body, string Expected)[] unprovableProbes = [
            ("unexpected status", 0, "503", "unavailable", "returned unexpected status 503"),
            ("transport failure", 7, "000", string.Empty, "could not be retrieved"),
            ("malformed index", 0, "200", "{\"versions\":\"all of them\"}", "could not be validated safely"),
        ];
        foreach ((string description, int exitCode, string status, string body, string expected) in unprovableProbes) {
            (int ExitCode, string Output, string Error) probe = await RunTagFloorGuardAsync(
                workflow,
                GitHubResponses(
                    (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                    (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                    (ManifestEndpoint, 0, ManifestResponse("Hexalith.Tenants.Contracts"))),
                RegistryResponses(("Hexalith.Tenants.Contracts", exitCode, status, body)));
            probe.ExitCode.ShouldBe(1, $"{description}: {probe.Output}");
            probe.Error.ShouldContain(expected, customMessage: description);
        }

        // Without a reachable release tag there is no floor to compare against.
        (int ExitCode, string Output, string Error) noFloor = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("behind"))),
            RegistryResponses());
        noFloor.ExitCode.ShouldBe(1, noFloor.Output);
        noFloor.Error.ShouldContain("No release tag is reachable from the dispatched source");

        // Every upstream read is load bearing: an unusable one stops the dispatch.
        (int ExitCode, string Output, string Error) tagRefsFailure = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses((TagRefsEndpoint, 1, string.Empty)),
            RegistryResponses());
        tagRefsFailure.ExitCode.ShouldBe(1, tagRefsFailure.Output);
        tagRefsFailure.Error.ShouldContain("tag refs could not be queried safely");

        (int ExitCode, string Output, string Error) comparisonFailure = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, "{\"status\":true}")),
            RegistryResponses());
        comparisonFailure.ExitCode.ShouldBe(1, comparisonFailure.Output);
        comparisonFailure.Error.ShouldContain("could not be validated safely");

        (int ExitCode, string Output, string Error) manifestFailure = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 1, string.Empty)),
            RegistryResponses());
        manifestFailure.ExitCode.ShouldBe(1, manifestFailure.Output);
        manifestFailure.Error.ShouldContain("manifest could not be read");

        // Manifest ids reach a request URL, so they are validated as untrusted input.
        (int ExitCode, string Output, string Error) hostileManifest = await RunTagFloorGuardAsync(
            workflow,
            GitHubResponses(
                (TagRefsEndpoint, 0, TagRefsResponse("v3.15.1")),
                (CompareEndpoint("v3.15.1"), 0, CompareResponse("ahead")),
                (ManifestEndpoint, 0, ManifestResponse("../../../etc/passwd"))),
            RegistryResponses());
        hostileManifest.ExitCode.ShouldBe(1, hostileManifest.Output);
        hostileManifest.Error.ShouldContain("manifest could not be validated safely");
    }

    [Fact]
    public void Release_package_manifest_matches_every_other_copy_of_the_inventory() {
        string repoRoot = FindRepoRoot();
        string manifestPath = Path.Combine(repoRoot, "tools/release-packages.json");
        File.Exists(manifestPath).ShouldBeTrue("tools/release-packages.json is the authoritative release inventory.");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] packages = [.. manifest.RootElement.GetProperty("packages").EnumerateArray()];

        string[] manifestIds = [.. packages.Select(package => package.GetProperty("id").GetString() ?? string.Empty)];
        string[] manifestProjects = [.. packages.Select(package => package.GetProperty("project").GetString() ?? string.Empty)];

        manifestIds.ShouldBe(ExpectedPackageIds, ignoreOrder: true, "The manifest must enumerate exactly the expected package ids.");
        manifestProjects.ShouldBe(PublishablePackageProjects, ignoreOrder: true, "The manifest must enumerate exactly the publishable package projects.");
        manifestIds.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(manifestIds.Length, "Manifest package ids must be unique.");
        manifestProjects.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(manifestProjects.Length, "Manifest package projects must be unique.");

        foreach (string project in manifestProjects) {
            File.Exists(Path.Combine(repoRoot, project)).ShouldBeTrue($"Manifest project {project} must exist.");
        }

        // The count is declared independently in the release workflow and in the preflight wrapper
        // so inventory drift fails closed. Those declarations must agree with the manifest.
        string releaseWorkflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/release.yml"));
        string preflightWrapper = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-publication-preflight.sh"));
        string expectedCount = manifestIds.Length.ToString(CultureInfo.InvariantCulture);
        releaseWorkflow.ShouldContain($"HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT: '{expectedCount}'");
        preflightWrapper.ShouldContain($"expected_package_count={expectedCount}");
    }

    [Fact]
    public void Ci_workflow_runs_package_consumer_validation_after_release_build_and_pack() {
        string repoRoot = FindRepoRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/ci.yml"));

        workflow.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("solution: Hexalith.Tenants.slnx");
    }

    [Fact]
    public void Consumer_package_reference_script_verifies_public_package_surfaces() {
        string repoRoot = FindRepoRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-consumer-package-references.py"));

        script.ShouldContain("Hexalith.Tenants.Contracts");
        script.ShouldContain("Hexalith.Tenants.Client");
        script.ShouldContain("Hexalith.Tenants.Testing");
        GetPythonStringCollectionEntries(script, "PACKAGE_IDS")
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals([
            "Hexalith.Tenants.Contracts",
            "Hexalith.Tenants.Client",
            "Hexalith.Tenants.Server",
            "Hexalith.Tenants.Testing",
        ])
            .ShouldBeTrue("The package-only consumer harness must enumerate exactly the domain consumer package ids.");
        GetPythonStringCollectionEntries(script, "PACKAGE_IDS").ShouldNotContain("Hexalith.Tenants.Aspire");
        script.ShouldContain("CreateTenant");
        script.ShouldContain("TenantCreated");
        script.ShouldContain("ListTenantsQuery");
        script.ShouldContain("InMemoryTenantService");
        script.ShouldContain("TenantTestHelpers");
        script.ShouldContain("InMemoryTenantProjection");
        script.ShouldContain("ProjectReference");
        script.ShouldContain("dotnet");
        script.ShouldContain("NuGet.Config");
        script.ShouldContain("local-tenants-packages");
        script.ShouldContain("inherited NuGet.Config sources are preserved");
        script.ShouldContain("NUGET_PACKAGES");
        script.ShouldContain("run_xunit_assembly");
        script.ShouldNotContain("[\"test\"");
    }

    [Fact]
    public void NuGet_package_validator_derives_boundaries_from_manifest_and_restore_evidence() {
        string repoRoot = FindRepoRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-nuget-packages.py"));

        script.ShouldContain("DEFAULT_MANIFEST");
        script.ShouldContain("\"release-packages.json\"");
        script.ShouldContain("project.assets.json");
        script.ShouldContain("projectFileDependencyGroups");
        script.ShouldContain("centralTransitiveDependencyGroups");
        script.ShouldContain("FORBIDDEN_DEPENDENCY_IDS");

        // Expected dependency sets are derived, but the forbidden surface stays a hand-declared policy:
        // no published package may ever depend on host, composition, sample, or test projects.
        script.ShouldContain("Hexalith.Tenants.AppHost");
        script.ShouldContain("Hexalith.Tenants.ServiceDefaults");
        script.ShouldContain("Hexalith.Tenants.Sample");
        script.ShouldContain("\".Tests\"");
        script.ShouldContain("\".Samples\"");
        script.Contains("EXPECTED_DEPENDENCIES", StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Coverage_gate_script_enforces_overall_and_named_isolation_thresholds() {
        string repoRoot = FindRepoRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-coverage.py"));

        script.ShouldContain("coverage.cobertura.xml");
        script.ShouldContain("minimum_line_coverage");
        script.ShouldContain("required_branch_coverage");
        script.ShouldContain("condition-coverage");
        script.ShouldContain("src/Hexalith.Tenants.Contracts/");
        script.ShouldContain("src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs");
        script.ShouldContain("src/Hexalith.Tenants.Server/Aggregates/GlobalAdministratorsAggregate.cs");
        script.ShouldContain("src/Hexalith.Tenants.Server/Validators/ChangeUserRoleValidator.cs");
        script.ShouldContain("src/Hexalith.Tenants.Server/Projections/TenantAuditOwnership.cs");
        File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"))
            .ShouldContain("src/Hexalith.Tenants.Server/Projections/TenantAuditOwnership.cs");
        script.ShouldContain("GITHUB_STEP_SUMMARY");
        script.ShouldContain("No publishable-package line coverage data found");
        script.ShouldContain("No isolation/auth branch coverage data found");
    }

    private static string FormatViolation(string projectPath, XElement packageReference)
        => $"{projectPath}: {FormatNode(packageReference)}";

    private static string FormatNode(XElement node)
        => node.ToString(SaveOptions.DisableFormatting);

    private static string FormatPropertyNodes(XDocument document, string propertyName) {
        string[] nodes = document.Descendants(propertyName)
            .Select(FormatNode)
            .ToArray();

        return nodes.Length == 0 ? "<missing>" : string.Join(", ", nodes);
    }

    private static string[] GetAdHocContainerFiles(string repoRoot)
        => Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("obj/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
            // node_modules is installed tooling (semantic-release et al.), not Tenants-owned container
            // config. It is present during the CI/release npm steps and contains source files whose names
            // start with "Dockerfile" (e.g. highlight.js' lib/languages/dockerfile.js), which would
            // otherwise be misread as ad-hoc container files. Exclude it like bin/ and obj/.
            .Where(path => !path.StartsWith("node_modules/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/node_modules/", StringComparison.Ordinal))
            // Submodules own their own container strategy; this governance is scoped to Tenants files.
            .Where(path => !path.StartsWith("references/Hexalith.EventStore/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.Commons/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.Builds/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.FrontComposer/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.Memories/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.AI.Tools/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("references/Hexalith.PolymorphicSerializations/", StringComparison.Ordinal))
            .Where(IsAdHocContainerFile)
            .ToArray();

    private static string[] GetOwnedProjectFiles(string repoRoot)
        => OwnedProjectRoots
            .Select(root => Path.Combine(repoRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] GetPackageReferenceGovernanceFiles(string repoRoot) {
        string[] ownedProjectFiles = GetOwnedProjectFiles(repoRoot);
        string[] sharedBuildFiles = OwnedProjectRoots
            .Select(root => Path.Combine(repoRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "Directory.Build.*", SearchOption.AllDirectories))
            .Append(Path.Combine(repoRoot, "Directory.Build.props"))
            .Append(Path.Combine(repoRoot, "Directory.Build.targets"))
            .Where(File.Exists)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .ToArray();

        return [.. ownedProjectFiles.Concat(sharedBuildFiles).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static string[] GetForbiddenPackageReferenceViolations(string repoRoot, string[] forbiddenPackageNameFragments) {
        List<string> violations = [];
        foreach (string projectPath in GetPackageReferenceGovernanceFiles(repoRoot)) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            foreach (XElement packageReference in project.Descendants().Where(element => element.Name.LocalName is "PackageReference" or "GlobalPackageReference")) {
                string? packageId = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("Update")?.Value;
                if (packageId is null) {
                    continue;
                }

                if (forbiddenPackageNameFragments.Any(fragment => packageId.Contains(fragment, StringComparison.OrdinalIgnoreCase))) {
                    violations.Add($"{projectPath}: analyzer package imports are not allowed: {FormatNode(packageReference)}");
                }
            }
        }

        return [.. violations];
    }

    private static HashSet<string> GetCentralPackageIds(string repoRoot) {
        string[] packagePropsFiles =
        [
            Path.Combine(repoRoot, "Directory.Packages.props"),
            Path.Combine(repoRoot, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"),
            Path.Combine(repoRoot, "..", "Hexalith.Builds", "Props", "Directory.Packages.props"),
            Path.Combine(repoRoot, "..", "..", "Hexalith.Builds", "Props", "Directory.Packages.props"),
        ];

        return packagePropsFiles
            .Where(File.Exists)
            .Select(XDocument.Load)
            .SelectMany(document => document.Descendants("PackageVersion"))
            .Select(package => package.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Select(packageId => packageId!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string[] GetPythonStringCollectionEntries(string script, string variableName) {
        Match assignment = Regex.Match(script, $@"(?m)^\s*{Regex.Escape(variableName)}\s*=");
        if (!assignment.Success) {
            throw new InvalidOperationException($"Could not find Python assignment {variableName}.");
        }

        int start = IndexOfFirstCollectionOpening(script, assignment.Index + assignment.Length);
        if (start < 0) {
            throw new InvalidOperationException($"Could not find Python collection {variableName}.");
        }

        int end = FindMatchingCollectionEnd(script, start);
        string collectionBody = script[start..(end + 1)];
        return [.. Regex.Matches(collectionBody, "['\"](?<value>(?:\\\\.|[^'\"\\\\])*)['\"]")
            .Select(match => match.Groups["value"].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))];
    }

    private static string[] GetRootSemanticReleaseConfigFiles(string repoRoot) {
        string[] configFileNames =
        [
            ".releaserc",
            ".releaserc.json",
            ".releaserc.yaml",
            ".releaserc.yml",
            ".releaserc.js",
            ".releaserc.cjs",
            "release.config.js",
            "release.config.cjs",
        ];

        return [.. configFileNames.Where(fileName => File.Exists(Path.Combine(repoRoot, fileName)))];
    }

    private static string GetYamlJobBlock(string workflow, string jobName) {
        string[] lines = workflow.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(lines, line => Regex.IsMatch(line, $@"^  ['""]?{Regex.Escape(jobName)}['""]?\s*:\s*$"));
        if (start < 0) {
            throw new InvalidOperationException($"Could not find workflow job {jobName}.");
        }

        int end = lines.Length;
        for (int index = start + 1; index < lines.Length; index++) {
            if (Regex.IsMatch(lines[index], @"^  ['""]?[A-Za-z0-9_-]+['""]?\s*:\s*$")) {
                end = index;
                break;
            }
        }

        return string.Join('\n', lines[start..end]);
    }

    private static string GetYamlStepBlock(string workflow, string stepName) {
        string[] lines = workflow.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(lines, line => line.Trim() == $"- name: {stepName}");
        if (start < 0) {
            throw new InvalidOperationException($"Could not find workflow step {stepName}.");
        }

        int indent = CountLeadingSpaces(lines[start]);
        int end = lines.Length;
        for (int index = start + 1; index < lines.Length; index++) {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            if (CountLeadingSpaces(line) < indent
                || (CountLeadingSpaces(line) == indent && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))) {
                end = index;
                break;
            }
        }

        return string.Join('\n', lines[start..end]);
    }

    private static string GetYamlStepRunBlock(string workflow, string stepName) {
        string[] lines = workflow.Replace("\r\n", "\n").Split('\n');
        int stepStart = Array.FindIndex(lines, line => line.Trim() == $"- name: {stepName}");
        if (stepStart < 0) {
            throw new InvalidOperationException($"Could not find workflow step {stepName}.");
        }

        int runStart = Array.FindIndex(lines, stepStart + 1, line => line.Trim() == "run: |");
        if (runStart < 0) {
            throw new InvalidOperationException($"Could not find run block for workflow step {stepName}.");
        }

        int runIndent = CountLeadingSpaces(lines[runStart]);
        List<string> block = [];
        for (int index = runStart + 1; index < lines.Length; index++) {
            string line = lines[index];
            if (!string.IsNullOrWhiteSpace(line) && CountLeadingSpaces(line) <= runIndent) {
                break;
            }

            block.Add(string.IsNullOrWhiteSpace(line) ? string.Empty : line[(runIndent + 2)..]);
        }

        return string.Join('\n', block) + '\n';
    }

    private static async Task<(int ExitCode, string Error)> RunPublicationPreflightAsync(string repoRoot, string version, string phase) {
        using Process process = new() {
            StartInfo = new ProcessStartInfo {
                FileName = "bash",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("scripts/validate-publication-preflight.sh");
        process.StartInfo.ArgumentList.Add(version);
        process.StartInfo.ArgumentList.Add(phase);

        // Unset every input the bridge reads so an inherited value cannot change which check reports
        // first, and so the bridge can never reach a real destination probe from a test. Unset rather
        // than blank: the script distinguishes unset from set-but-empty for the package count.
        foreach (string releaseInput in new string[] {
            "HEXALITH_BUILDS_EXECUTION_SHA",
            "GITHUB_SHA",
            "HEXALITH_RELEASE_SOURCE_BRANCH",
            "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW",
            "HEXALITH_RELEASE_PACKAGE_MANIFEST",
            "HEXALITH_RELEASE_ENVIRONMENT",
            "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT",
            "HEXALITH_RELEASE_CONTRACT_DIRECTORY",
            "HEXALITH_RELEASE_EVIDENCE_DIRECTORY",
            "HEXALITH_PUBLICATION_PREFLIGHT",
            "HEXALITH_ZOT_REGISTRY",
        }) {
            process.StartInfo.Environment.Remove(releaseInput);
        }

        process.Start();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        return (process.ExitCode, await error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunPublicationPostconditionAsync(
        string workflow,
        string releases,
        string githubResponses,
        string dispatchSha,
        string[] expectedInvocations,
        int releasesExitCode = 0) {
        string script = """
            gh() {
              printf '%s\n' "$*" >> "$FAKE_GH_LOG"
              if [ "$#" -eq 3 ] && [ "$1" = "api" ] && [ "$2" = "--paginate" ] && [ "$3" = "repos/${REPOSITORY}/releases?per_page=100" ]; then
                if [ "$FAKE_RELEASES_EXIT_CODE" -ne 0 ]; then
                  echo "simulated Releases API failure" >&2
                  return "$FAKE_RELEASES_EXIT_CODE"
                fi
                printf '%s\n' "$FAKE_RELEASES"
                return 0
              fi
              if [ "$#" -eq 2 ] && [ "$1" = "api" ]; then
                if ! response="$(printf '%s\n' "$FAKE_GH_RESPONSES" | jq -cer --arg endpoint "$2" '.[$endpoint]')"; then
                  echo "unexpected gh endpoint: $2" >&2
                  return 97
                fi
                response_exit_code="$(printf '%s\n' "$response" | jq -r '.exit_code')"
                if [ "$response_exit_code" -ne 0 ]; then
                  echo "simulated API failure for $2" >&2
                  return "$response_exit_code"
                fi
                printf '%s\n' "$response" | jq -r '.body'
                return 0
              fi
              echo "unexpected gh invocation: $*" >&2
              return 97
            }

            """ + GetYamlStepRunBlock(workflow, "Require published release for exact dispatched source");
        string invocationLog = Path.GetTempFileName();
        try {
            using Process process = new() {
                StartInfo = new ProcessStartInfo {
                    FileName = "bash",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add(script.ReplaceLineEndings("\n"));
            process.StartInfo.Environment["GH_TOKEN"] = "fixture-token";
            process.StartInfo.Environment["REPOSITORY"] = "Hexalith/Hexalith.Tenants";
            process.StartInfo.Environment["DISPATCH_SHA"] = dispatchSha;
            process.StartInfo.Environment["FAKE_RELEASES"] = releases;
            process.StartInfo.Environment["FAKE_RELEASES_EXIT_CODE"] = releasesExitCode.ToString(CultureInfo.InvariantCulture);
            process.StartInfo.Environment["FAKE_GH_RESPONSES"] = githubResponses;
            process.StartInfo.Environment["FAKE_GH_LOG"] = invocationLog;

            process.Start();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            string[] actualInvocations = await File.ReadAllLinesAsync(invocationLog);
            actualInvocations.ShouldBe(expectedInvocations, "The postcondition must call only the exact reviewed GitHub API endpoints.");
            return (process.ExitCode, await output, await error);
        }
        finally {
            File.Delete(invocationLog);
        }
    }

    private static string GitHubResponses(params (string Endpoint, int ExitCode, string Body)[] responses)
        => JsonSerializer.Serialize(responses.ToDictionary(
            response => response.Endpoint,
            response => new { exit_code = response.ExitCode, body = response.Body },
            StringComparer.Ordinal));

    private static async Task<(int ExitCode, string Output, string Error)> RunTagFloorGuardAsync(
        string workflow,
        string githubResponses,
        string registryResponses,
        string[]? expectedInvocations = null,
        string? dispatchSha = null) {
        string script = """
            gh() {
              printf '%s\n' "$*" >> "$FAKE_GH_LOG"
              if [ "$#" -eq 3 ] && [ "$1" = "api" ] && [ "$2" = "--paginate" ]; then
                endpoint="$3"
              elif [ "$#" -eq 2 ] && [ "$1" = "api" ]; then
                endpoint="$2"
              else
                echo "unexpected gh invocation: $*" >&2
                return 97
              fi
              if ! response="$(printf '%s\n' "$FAKE_GH_RESPONSES" | jq -cer --arg endpoint "$endpoint" '.[$endpoint]')"; then
                echo "unexpected gh endpoint: $endpoint" >&2
                return 97
              fi
              response_exit_code="$(printf '%s\n' "$response" | jq -r '.exit_code')"
              if [ "$response_exit_code" -ne 0 ]; then
                echo "simulated API failure for $endpoint" >&2
                return "$response_exit_code"
              fi
              printf '%s\n' "$response" | jq -r '.body'
              return 0
            }

            curl() {
              output_file=''
              url=''
              while [ "$#" -gt 0 ]; do
                case "$1" in
                  --output) output_file="$2"; shift 2 ;;
                  https://*) url="$1"; shift ;;
                  *) shift ;;
                esac
              done
              printf 'curl %s\n' "$url" >> "$FAKE_GH_LOG"
              if ! response="$(printf '%s\n' "$FAKE_REGISTRY_RESPONSES" | jq -cer --arg url "$url" '.[$url]')"; then
                echo "unexpected registry url: $url" >&2
                return 97
              fi
              transport_exit_code="$(printf '%s\n' "$response" | jq -r '.exit_code')"
              if [ "$transport_exit_code" -ne 0 ]; then
                echo "simulated transport failure for $url" >&2
                return "$transport_exit_code"
              fi
              printf '%s\n' "$response" | jq -r '.body' > "$output_file"
              printf '%s' "$response" | jq -r '.status'
              return 0
            }

            """ + GetYamlStepRunBlock(workflow, RegistryDriftGuardStepName);
        string invocationLog = Path.GetTempFileName();
        try {
            using Process process = new() {
                StartInfo = new ProcessStartInfo {
                    FileName = "bash",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add(script.ReplaceLineEndings("\n"));
            process.StartInfo.Environment["GH_TOKEN"] = "fixture-token";
            process.StartInfo.Environment["REPOSITORY"] = "Hexalith/Hexalith.Tenants";
            process.StartInfo.Environment["DISPATCH_SHA"] = dispatchSha ?? GuardDispatchSha;
            process.StartInfo.Environment["PACKAGE_MANIFEST"] = "tools/release-packages.json";
            process.StartInfo.Environment["FAKE_GH_RESPONSES"] = githubResponses;
            process.StartInfo.Environment["FAKE_REGISTRY_RESPONSES"] = registryResponses;
            process.StartInfo.Environment["FAKE_GH_LOG"] = invocationLog;

            process.Start();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (expectedInvocations is not null) {
                (await File.ReadAllLinesAsync(invocationLog))
                    .ShouldBe(expectedInvocations, "The guard must make only the exact reviewed reads.");
            }

            return (process.ExitCode, await output, await error);
        }
        finally {
            File.Delete(invocationLog);
        }
    }

    private static string CompareEndpoint(string tagName)
        => $"repos/Hexalith/Hexalith.Tenants/compare/{tagName}...{GuardDispatchSha}?per_page=1";

    private static string CompareResponse(string status)
        => JsonSerializer.Serialize(new { status });

    private static string TagRefsResponse(params string[] tagNames)
        => JsonSerializer.Serialize(tagNames.Select(tagName => new {
            @ref = $"refs/tags/{tagName}",
            @object = new { type = "commit", sha = new string('b', 40) },
        }));

    private static string ManifestResponse(params string[] packageIds)
        => ManifestResponse("base64", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            packages = packageIds.Select(packageId => new {
                id = packageId,
                project = $"src/{packageId}/{packageId}.csproj",
            }),
        }))));

    private static string ManifestResponse(string encoding, string content)
        => JsonSerializer.Serialize(new {
            type = "file",
            path = "tools/release-packages.json",
            encoding,
            content,
        });

    private static string RegistryUrl(string packageId)
        => $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";

    private static string RegistryResponses(params (string PackageId, int ExitCode, string Status, string Body)[] responses)
        => JsonSerializer.Serialize(responses.ToDictionary(
            response => RegistryUrl(response.PackageId),
            response => new { exit_code = response.ExitCode, status = response.Status, body = response.Body },
            StringComparer.Ordinal));

    private static string PublishedVersions(params string[] versions)
        => JsonSerializer.Serialize(new { versions });

    private static string TagRefEndpoint(string tagName)
        => $"repos/Hexalith/Hexalith.Tenants/git/ref/tags/{tagName}";

    private static string TagObjectEndpoint(string objectSha)
        => $"repos/Hexalith/Hexalith.Tenants/git/tags/{objectSha}";

    private static string TagRefResponse(string tagName, string objectType, string objectSha)
        => JsonSerializer.Serialize(new {
            @ref = $"refs/tags/{tagName}",
            @object = new { type = objectType, sha = objectSha },
        });

    private static string TagObjectResponse(string objectSha, string peeledType, string peeledSha)
        => JsonSerializer.Serialize(new {
            sha = objectSha,
            @object = new { type = peeledType, sha = peeledSha },
        });

    private static bool YamlBlockContainsKey(string yamlBlock, string key)
        => Regex.IsMatch(yamlBlock, $@"(?m)^\s*['""]?{Regex.Escape(key)}['""]?\s*:");

    private static string[] GetYamlLiteralBlockLines(string yamlBlock, string key) {
        string[] lines = yamlBlock.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(lines, line => Regex.IsMatch(line, $@"^\s*['""]?{Regex.Escape(key)}['""]?\s*:\s*\|\s*$"));
        if (start < 0) {
            throw new InvalidOperationException($"Could not find literal YAML block {key}.");
        }

        int baseIndent = CountLeadingSpaces(lines[start]);
        List<string> values = [];
        for (int index = start + 1; index < lines.Length; index++) {
            string line = lines[index];
            if (!string.IsNullOrWhiteSpace(line) && CountLeadingSpaces(line) <= baseIndent) {
                break;
            }

            string value = line.Trim();
            if (!string.IsNullOrWhiteSpace(value)) {
                values.Add(value);
            }
        }

        return [.. values];
    }

    private static int IndexOfFirstCollectionOpening(string text, int startIndex) {
        for (int index = startIndex; index < text.Length; index++) {
            if (text[index] is '[' or '{') {
                return index;
            }
        }

        return -1;
    }

    private static int FindMatchingCollectionEnd(string text, int openingIndex) {
        char opening = text[openingIndex];
        char closing = opening == '[' ? ']' : '}';
        int depth = 0;
        char? quotedBy = null;
        bool escaped = false;
        for (int index = openingIndex; index < text.Length; index++) {
            char current = text[index];
            if (quotedBy is not null) {
                if (escaped) {
                    escaped = false;
                }
                else if (current == '\\') {
                    escaped = true;
                }
                else if (current == quotedBy) {
                    quotedBy = null;
                }

                continue;
            }

            if (current is '\'' or '"') {
                quotedBy = current;
                continue;
            }

            if (current == opening) {
                depth++;
            }
            else if (current == closing && --depth == 0) {
                return index;
            }
        }

        throw new InvalidOperationException("Could not find matching Python collection delimiter.");
    }

    private static int CountLeadingSpaces(string line)
        => line.TakeWhile(character => character == ' ').Count();

    private static bool IsAdHocContainerFile(string path) {
        string fileName = Path.GetFileName(path);
        return fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".dcproj", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetProjectContainerDefaultOverrides(string repoRoot) {
        List<string> violations = [];
        foreach (string projectPath in GetOwnedProjectFiles(repoRoot)) {
            XDocument project = XDocument.Load(Path.Combine(repoRoot, projectPath));
            foreach (string propertyName in CentrallyGovernedContainerProperties) {
                violations.AddRange(project.Descendants(propertyName).Select(property => $"{projectPath}: {FormatNode(property)}"));
            }

            violations.AddRange(project.Descendants("ContainerPort").Select(port => $"{projectPath}: {FormatNode(port)}"));
            violations.AddRange(project.Descendants("ContainerLabel").Select(label => $"{projectPath}: {FormatNode(label)}"));
        }

        return [.. violations];
    }

    private static (string Action, string Reference)[] GetWorkflowActionReferences(string workflow)
        => GetWorkflowUsesValues(workflow)
            .Select(value => value.Split('@'))
            .Where(parts => parts.Length == 2)
            .Select(parts => (Action: parts[0], Reference: parts[1]))
            .ToArray();

    private static string[] GetWorkflowUsesValues(string workflow)
        => workflow
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].TrimStart() : line)
            .Where(line => line.StartsWith("uses: ", StringComparison.Ordinal))
            .Select(line => line["uses: ".Length..].Split(' ')[0])
            .ToArray();

    private static void AssertTrustedPublishingJob(string workflow, string job, string publishStepName) {
        workflow.ShouldNotContain("secrets.NUGET_API_KEY");
        workflow[..workflow.IndexOf("\njobs:", StringComparison.Ordinal)]
            .ShouldNotContain("id-token", customMessage: "OIDC issuance must be granted on the publishing job, not the whole workflow.");
        job.ShouldContain("environment: production");
        job.ShouldContain("id-token: write");
        GetWorkflowActionReferences(job).Count(action => action.Action == "NuGet/login").ShouldBe(1);

        string guardStep = GetYamlStepBlock(job, "Require the Trusted Publishing account");
        guardStep.ShouldContain("NUGET_USER: ${{ vars.NUGET_USER }}");
        guardStep.ShouldContain("${NUGET_USER//[[:space:]]/}");

        string loginStep = GetYamlStepBlock(job, "Exchange GitHub OIDC token for a temporary NuGet key");
        loginStep.ShouldContain("id: nuget-login");
        loginStep.ShouldContain("user: ${{ vars.NUGET_USER }}");
        GetYamlStepBlock(job, publishStepName).ShouldContain("NUGET_API_KEY: ${{ steps.nuget-login.outputs.NUGET_API_KEY }}");

        // The account check fails before any build work; the one-hour key is minted only after the
        // build, immediately before the step that pushes packages.
        int guardIndex = job.IndexOf("- name: Require the Trusted Publishing account", StringComparison.Ordinal);
        int loginIndex = job.IndexOf("- name: Exchange GitHub OIDC token for a temporary NuGet key", StringComparison.Ordinal);
        int publishIndex = job.IndexOf($"- name: {publishStepName}", StringComparison.Ordinal);
        guardIndex.ShouldBeLessThan(job.IndexOf("uses: actions/checkout@", StringComparison.Ordinal));
        loginIndex.ShouldBeGreaterThan(job.IndexOf("actions/setup-dotnet@", StringComparison.Ordinal));
        loginIndex.ShouldBeLessThan(publishIndex);
        job[loginIndex..publishIndex].Split("- name: ").Length.ShouldBe(2, "No step may run between NuGet login and publication.");
    }

    private static bool IsFullCommitSha(string value)
        => value.Length == 40 && value.All(IsLowerHexDigit);

    private static bool IsLowerHexDigit(char value)
        => value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static string? ValueFor(XDocument document, string elementName)
        => document.Descendants(elementName).FirstOrDefault()?.Value;

    private static string RequiredValueFor(XDocument document, string elementName)
        => ValueFor(document, elementName) ?? throw new InvalidOperationException($"Missing required MSBuild property {elementName}.");

    private static string FindRepoRoot() {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Tenants.slnx"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate Hexalith.Tenants repository root.");
    }
}
