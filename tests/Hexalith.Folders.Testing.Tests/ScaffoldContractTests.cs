using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class ScaffoldContractTests
{
    private static readonly string[] ExpectedRootPolicyProjects =
    [
        "samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj",
        "samples/Hexalith.Folders.Sample/Hexalith.Folders.Sample.csproj",
        "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj",
        "src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj",
        "src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj",
        "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
        "src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj",
        "src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj",
        "src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj",
        "src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj",
        "src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj",
        "src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj",
        "src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj",
        "src/Hexalith.Folders/Hexalith.Folders.csproj",
        "tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj",
        "tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj",
        "tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj",
        "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj",
        "tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj",
        "tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj",
        "tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj",
        "tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj",
        "tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj",
        "tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj",
        "tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj",
        "tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj",
        "tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj",
        "tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj",
        "tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj"
    ];

    private static readonly string[] ExpectedSolutionProjects =
    [
        "references/Hexalith.Commons/src/libraries/Hexalith.Commons.ServiceDefaults/Hexalith.Commons.ServiceDefaults.csproj",
        "references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj",
        "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj",
        "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj",
        "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.csproj",
        "references/Hexalith.Memories/src/Hexalith.Memories.Aspire/Hexalith.Memories.Aspire.csproj",
        "references/Hexalith.Memories/src/Hexalith.Memories.Contracts/Hexalith.Memories.Contracts.csproj",
        "references/Hexalith.Tenants/src/Hexalith.Tenants.Aspire/Hexalith.Tenants.Aspire.csproj",
        "references/Hexalith.Tenants/src/Hexalith.Tenants.Client/Hexalith.Tenants.Client.csproj",
        "references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj",
        "references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj",
        "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj",
        "samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj",
        "samples/Hexalith.Folders.Sample/Hexalith.Folders.Sample.csproj",
        "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj",
        "src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj",
        "src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj",
        "src/Hexalith.Folders.Client/Generation/Shared/Hexalith.Folders.Client.Generation.Shared.csproj",
        "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
        "src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj",
        "src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj",
        "src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj",
        "src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj",
        "src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj",
        "src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj",
        "src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj",
        "src/Hexalith.Folders/Hexalith.Folders.csproj",
        "tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj",
        "tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj",
        "tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj",
        "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj",
        "tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj",
        "tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj",
        "tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj",
        "tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj",
        "tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj",
        "tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj",
        "tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj",
        "tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj",
        "tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj",
        "tests/load/Hexalith.Folders.LoadTests.csproj",
        "tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj",
        "tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj",
    ];

    private static readonly string[] RequiredCanonicalSubmodules =
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

    private static readonly Regex DirectedRecursiveSubmoduleProhibitionPattern = new(
        @"\b(?:do\s+not|don't|never|avoid|must\s+not|should\s+not|forbid(?:s|den)?|prohibit(?:s|ed)?)\b[^\r\n.;]*\brecursive\b(?:\s+or\s+[A-Za-z0-9_-]+)?\s+submodules?\s+(?:initializ(?:e|ed|es|ing|ation)|updat(?:e|ed|es|ing))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DisplayedRecursiveCommandProhibitionPattern = new(
        @"^(?:do\s+not|don't|never|must\s+not|should\s+not)\s+(?:run|use|execute)(?:\s+(?:this|the)\s+(?:command|setup))?(?:\s*:|\s*$)|^avoid\s+(?:running|using|executing)(?:\s+(?:this|the)\s+(?:command|setup))?(?:\s*:|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InverseRecursiveSubmoduleProhibitionPattern = new(
        @"^(?:do\s+not|don't|never|must\s+not|should\s+not)\s+(?:avoid|forbid(?:s|den)?|prohibit(?:s|ed)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void SolutionContainsOnlyCanonicalBuildableProjects()
    {
        string root = RepositoryRoot();
        string[] solutionProjects = ReadSolutionProjectPaths(root);
        string[] buildableProjects = EnumerateScaffoldBuildableProjectPaths(root)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        solutionProjects.ShouldBe(ExpectedSolutionProjects.Order(StringComparer.Ordinal).ToArray());
        buildableProjects.ShouldBe(ExpectedRootPolicyProjects.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ScaffoldProjectDiscoveryStaysInsideScaffoldRoots()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"folders-scaffold-{Guid.NewGuid():N}");
        try
        {
            string scaffoldProject = Path.Combine(tempRoot, "src", "Hexalith.Folders", "Hexalith.Folders.csproj");
            string siblingProject = Path.Combine(tempRoot, "Hexalith.Tenants", "src", "Hexalith.Folders.NotOurs", "Hexalith.Folders.NotOurs.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(scaffoldProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(siblingProject)!);
            File.WriteAllText(scaffoldProject, "<Project />");
            File.WriteAllText(siblingProject, "<Project />");

            string[] discovered = EnumerateScaffoldBuildableProjectPaths(tempRoot)
                .Select(path => Normalize(Path.GetRelativePath(tempRoot, path)))
                .ToArray();

            discovered.ShouldBe(["src/Hexalith.Folders/Hexalith.Folders.csproj"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ProjectReferencesFollowAllowedDependencyDirection()
    {
        string root = RepositoryRoot();
        Dictionary<string, string[]> references = BuildProjectReferenceMap(root);

        AssertReferences(references, "Hexalith.Folders.Contracts", []);
        AssertReferences(references, "Hexalith.Folders", ["Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Server", ["Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.DomainService", "Hexalith.Folders", "Hexalith.Folders.Contracts", "Hexalith.Folders.ServiceDefaults", "Hexalith.Memories.Client.Rest", "Hexalith.Memories.Contracts", "Hexalith.Tenants.Client", "Hexalith.Tenants.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Client", ["Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Cli", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.Mcp", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.UI", ["Hexalith.Folders.Client", "Hexalith.FrontComposer.Shell"]);
        AssertReferences(references, "Hexalith.Folders.Workers", ["Hexalith.EventStore.DomainService", "Hexalith.Folders", "Hexalith.Folders.Contracts", "Hexalith.Folders.ServiceDefaults", "Hexalith.Memories.Contracts", "Hexalith.Tenants.Client", "Hexalith.Tenants.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.AppHost", ["Hexalith.EventStore.Aspire", "Hexalith.Folders.Aspire", "Hexalith.Folders.Server", "Hexalith.Folders.UI", "Hexalith.Folders.Workers", "Hexalith.Memories.Aspire", "Hexalith.Tenants.Aspire"]);
        AssertReferences(references, "Hexalith.Folders.Aspire", []);
        AssertReferences(references, "Hexalith.Folders.ServiceDefaults", []);
        AssertReferences(references, "Hexalith.Folders.Testing", ["Hexalith.Folders", "Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Sample", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.Sample.Tests", ["Hexalith.Folders.Sample"]);
        AssertReferences(references, "Hexalith.Folders.Contracts.Tests", ["Hexalith.Folders.Aspire", "Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Tests", ["Hexalith.Folders", "Hexalith.Folders.Client", "Hexalith.Folders.Testing", "Hexalith.Folders.UI"]);
        AssertReferences(references, "Hexalith.Folders.Server.Tests", ["Hexalith.Folders.Server", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Client.Tests", ["Hexalith.Folders.Client", "Hexalith.Folders.Client.Generation.Shared", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Cli.Tests", ["Hexalith.Folders.Cli", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Mcp.Tests", ["Hexalith.Folders.Mcp", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.UI.E2E.Tests", ["Hexalith.Folders.Testing", "Hexalith.Folders.UI"]);
        AssertReferences(references, "Hexalith.Folders.UI.Tests", ["Hexalith.Folders.UI", "Hexalith.Folders.Testing", "Hexalith.FrontComposer.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Workers.Tests", ["Hexalith.Folders.Server", "Hexalith.Folders.Workers", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Testing.Tests", ["Hexalith.Folders", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.IntegrationTests", ["Hexalith.EventStore.Aspire", "Hexalith.Folders.Aspire", "Hexalith.Folders.Cli", "Hexalith.Folders.Client", "Hexalith.Folders.Mcp", "Hexalith.Folders.Server", "Hexalith.Folders.Testing", "Hexalith.Memories.Aspire"]);
        // Story 10.4 AC9: the Tier-3 harness references the worker project to seed/verify the live folders-index
        // round-trip by publishing real SearchIndexEntryChanged/Removed CloudEvents through the worker's pub/sub
        // component (and to reuse FoldersSemanticIndexingDefaults). Memories.Contracts flows in transitively; this is
        // a test project, so it is not subject to the production Memories-isolation rule (see ForbiddenReferencesAreNotIntroduced).
        AssertReferences(references, "Hexalith.Folders.AppHost.Tests", ["Hexalith.Folders.AppHost", "Hexalith.Folders.Aspire", "Hexalith.Folders.Workers"]);
        AssertReferences(references, "Hexalith.Folders.LoadTests.Tests", ["Hexalith.Folders.LoadTests"]);
        AssertReferences(references, "Hexalith.Folders.PatternExamples", ["Hexalith.Folders.Client", "Hexalith.Folders.Contracts"]);
    }

    [Fact]
    public void ForbiddenReferencesAreNotIntroduced()
    {
        string root = RepositoryRoot();
        Dictionary<string, HashSet<string>> references = BuildProjectReferenceMap(root)
            .ToDictionary(
                kv => kv.Key,
                kv => new HashSet<string>(kv.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);

        string[] forbiddenFromContracts =
        [
            "Hexalith.Folders",
            "Hexalith.Folders.Server",
            "Hexalith.Folders.Client",
            "Hexalith.Folders.Cli",
            "Hexalith.Folders.Mcp",
            "Hexalith.Folders.UI",
            "Hexalith.Folders.Workers",
            "Hexalith.Folders.Aspire",
            "Hexalith.Folders.AppHost",
            "Hexalith.Folders.ServiceDefaults",
            "Hexalith.Folders.Testing",
        ];
        HashSet<string> contractsRefs = RequireReferences(references, "Hexalith.Folders.Contracts");
        foreach (string forbidden in forbiddenFromContracts)
        {
            contractsRefs.ShouldNotContain(forbidden);
        }

        string[] forbiddenFromClient =
        [
            "Hexalith.Folders.Server",
            "Hexalith.Folders.UI",
            "Hexalith.Folders.Cli",
            "Hexalith.Folders.Mcp",
            "Hexalith.Folders.Workers",
            "Hexalith.Folders.AppHost",
        ];
        HashSet<string> clientRefs = RequireReferences(references, "Hexalith.Folders.Client");
        foreach (string forbidden in forbiddenFromClient)
        {
            clientRefs.ShouldNotContain(forbidden);
        }

        foreach (string adapter in new[] { "Hexalith.Folders.Cli", "Hexalith.Folders.Mcp", "Hexalith.Folders.UI" })
        {
            HashSet<string> adapterRefs = RequireReferences(references, adapter);
            adapterRefs.ShouldNotContain("Hexalith.Folders");
            adapterRefs.ShouldNotContain("Hexalith.Folders.Server");
            adapterRefs.ShouldNotContain("Hexalith.Folders.Workers");
            adapterRefs.ShouldNotContain("Hexalith.Folders.AppHost");
        }

        // Story 10.5 (Option B): Hexalith.Folders.Server is the ONE non-Worker project allowed to reference the
        // Memories client/contracts, for the authorized read-only search facade (mirrors Hexalith.Tenants.UI).
        // Every other Folders project stays Memories-free and reaches the facade only through the generated SDK.
        string[] foldersProjectsForbiddenFromMemoriesClient =
        [
            "Hexalith.Folders.Contracts",
            "Hexalith.Folders",
            "Hexalith.Folders.Client",
            "Hexalith.Folders.Cli",
            "Hexalith.Folders.Mcp",
            "Hexalith.Folders.UI",
            "Hexalith.Folders.Testing",
            "Hexalith.Folders.Aspire",
            "Hexalith.Folders.AppHost",
        ];
        string[] forbiddenMemoriesClientReferences =
        [
            "Hexalith.Memories.Client.Rest",
            "Hexalith.Memories.Contracts",
        ];
        foreach (string project in foldersProjectsForbiddenFromMemoriesClient)
        {
            HashSet<string> projectRefs = RequireReferences(references, project);
            foreach (string forbidden in forbiddenMemoriesClientReferences)
            {
                projectRefs.ShouldNotContain(forbidden, $"{project} must not reference {forbidden}; Memories client dependencies are isolated to Hexalith.Folders.Workers (producer) and Hexalith.Folders.Server (Story 10.5 read facade).");
            }
        }
    }

    [Fact]
    public void RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions()
    {
        string root = RepositoryRoot();
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        XDocument buildProps = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XDocument packagesProps = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XDocument catalogProps = XDocument.Load(Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"));
        XDocument appHostProject = XDocument.Load(Path.Combine(root, "src", "Hexalith.Folders.AppHost", "Hexalith.Folders.AppHost.csproj"));
        using JsonDocument exceptionInventory = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "references", "Hexalith.Builds", "Tools", "package-version-exceptions.json")));
        string[] projectsWithInlineVersions = ExpectedRootPolicyProjects
            .Select(project => Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar)))
            .Where(ProjectHasPackageReferenceVersion)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToArray();

        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");
        sdk.GetProperty("version").GetString().ShouldBe("10.0.400");
        sdk.GetProperty("rollForward").GetString().ShouldBe("latestPatch");
        sdk.GetProperty("allowPrerelease").GetBoolean().ShouldBeFalse();

        DescendantsByLocalName(buildProps, "TargetFramework").Single().Value.ShouldBe("net10.0");
        DescendantsByLocalName(buildProps, "Nullable").Single().Value.ShouldBe("enable");
        DescendantsByLocalName(buildProps, "ImplicitUsings").Single().Value.ShouldBe("enable");
        DescendantsByLocalName(buildProps, "TreatWarningsAsErrors").Single().Value.ShouldBe("true");
        DescendantsByLocalName(buildProps, "LangVersion").Single().Value.ShouldBe("latest");
        DescendantsByLocalName(packagesProps, "ManagePackageVersionsCentrally").Single().Value.ShouldBe("true");
        projectsWithInlineVersions.ShouldBeEmpty();

        string aspireHostingVersion = ((string?)DescendantsByLocalName(catalogProps, "PackageVersion")
            .Single(package => string.Equals((string?)package.Attribute("Include"), "Aspire.Hosting", StringComparison.Ordinal))
            .Attribute("Version")).ShouldNotBeNull();
        string appHostSdk = ((string?)appHostProject.Root?.Attribute("Sdk")).ShouldNotBeNull();
        appHostSdk.ShouldBe($"Aspire.AppHost.Sdk/{aspireHostingVersion}");
        XElement aspireUseCliBundle = DescendantsByLocalName(appHostProject, "AspireUseCliBundle").Single();
        aspireUseCliBundle.Value.ShouldBe("false");
        aspireUseCliBundle.Attribute("Condition").ShouldBeNull();
        aspireUseCliBundle.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();

        XElement noWarn = DescendantsByLocalName(appHostProject, "NoWarn").Single();
        noWarn.Value.ShouldBe("$(NoWarn);ASPIRE010");
        noWarn.Attribute("Condition").ShouldBeNull();
        noWarn.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();

        JsonElement[] foldersExceptions = exceptionInventory.RootElement
            .GetProperty("exceptions")
            .EnumerateArray()
            .Where(entry => string.Equals(entry.GetProperty("owner").GetString(), "Hexalith.Folders", StringComparison.Ordinal))
            .ToArray();
        foldersExceptions.Length.ShouldBe(1);
        JsonElement foldersException = foldersExceptions[0];
        foldersException.GetProperty("kind").GetString().ShouldBe("apphost-sdk");
        foldersException.GetProperty("path").GetString().ShouldBe("src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj");
        foldersException.GetProperty("id").GetString().ShouldBe("Aspire.AppHost.Sdk");
        foldersException.GetProperty("version").GetString().ShouldBe(aspireHostingVersion);
        foldersException.GetProperty("alignmentRule").GetString().ShouldBe("exact-catalog-package");
        foldersException.GetProperty("catalogPackage").GetString().ShouldBe("Aspire.Hosting");
    }

    [Fact]
    public void FrontComposerTestingDependencyFailsClosedOnPartialSourceCheckout()
    {
        string root = RepositoryRoot();
        XDocument buildProps = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XElement testingSourceFlag = DescendantsByLocalName(buildProps, "HexalithFrontComposerTestingFromSource").Single();
        string sourceFlagCondition = ((string?)testingSourceFlag.Attribute("Condition")).ShouldNotBeNull();

        sourceFlagCondition.ShouldContain("'$(UseHexalithProjectReferences)' == 'true'", Case.Sensitive);
        sourceFlagCondition.ShouldContain("Hexalith.FrontComposer.Testing\\Hexalith.FrontComposer.Testing.csproj", Case.Sensitive);

        XDocument uiTestsProject = XDocument.Load(Path.Combine(root, "tests", "Hexalith.Folders.UI.Tests", "Hexalith.Folders.UI.Tests.csproj"));
        XElement testingProjectReference = DescendantsByLocalName(uiTestsProject, "ProjectReference")
            .Single(reference => ((string?)reference.Attribute("Include"))?.Contains("Hexalith.FrontComposer.Testing", StringComparison.Ordinal) == true);
        ((string?)testingProjectReference.Attribute("Condition")).ShouldBe("'$(HexalithFrontComposerTestingFromSource)' == 'true'");

        XElement testingPackageReference = DescendantsByLocalName(uiTestsProject, "PackageReference")
            .Single(reference => string.Equals((string?)reference.Attribute("Include"), "Hexalith.FrontComposer.Testing", StringComparison.Ordinal));
        ((string?)testingPackageReference.Attribute("Condition")).ShouldBe("'$(HexalithFrontComposerFromSource)' != 'true'");

        XElement validationTarget = DescendantsByLocalName(uiTestsProject, "Target")
            .Single(target => string.Equals((string?)target.Attribute("Name"), "ValidateFrontComposerTestingSourceAvailability", StringComparison.Ordinal));
        string validationCondition = ((string?)validationTarget.Attribute("Condition")).ShouldNotBeNull();
        validationCondition.ShouldContain("'$(UseHexalithProjectReferences)' == 'true'", Case.Sensitive);
        validationCondition.ShouldContain("'$(HexalithFrontComposerFromSource)' == 'true'", Case.Sensitive);
        validationCondition.ShouldContain("'$(HexalithFrontComposerTestingFromSource)' != 'true'", Case.Sensitive);
        DescendantsByLocalName(validationTarget, "Error").Single().Attribute("Text")?.Value
            .ShouldContain("complete root FrontComposer submodule checkout", Case.Sensitive);
    }

    [Fact]
    public void ProjectsDoNotOverrideRootBuildConfigurationLocally()
    {
        string root = RepositoryRoot();
        // Settings the root file owns and projects must not override. IsPackable/IsPublishable are
        // deliberately excluded: they are opt-in per project (libraries flip IsPackable=true; hosts
        // flip IsPublishable=true).
        string[] driftingElements =
        [
            "TargetFramework",
            "TargetFrameworks",
            "Nullable",
            "ImplicitUsings",
            "LangVersion",
            "TreatWarningsAsErrors",
            "Deterministic",
            "ContinuousIntegrationBuild",
        ];

        string[] violations = ExpectedRootPolicyProjects
            .Select(project => Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar)))
            .SelectMany(path => FindLocalRootSettingOverrides(root, path, driftingElements))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void RequiredRootConfigurationFilesExist()
    {
        string root = RepositoryRoot();
        string[] requiredFiles =
        [
            ".editorconfig",
            ".gitmodules",
            "Directory.Build.props",
            "Directory.Packages.props",
            "Hexalith.Folders.slnx",
            "global.json",
            "nuget.config"
        ];

        string[] missingOrEmpty = requiredFiles
            .Select(file => (FileName: file, Path: Path.Combine(root, file)))
            .Where(entry => !File.Exists(entry.Path) || new FileInfo(entry.Path).Length == 0)
            .Select(entry => entry.FileName)
            .ToArray();

        missingOrEmpty.ShouldBeEmpty();
    }

    [Fact]
    public void NuGetConfigurationUsesPublicSourceWithoutCredentials()
    {
        string root = RepositoryRoot();
        string nugetConfigPath = Path.Combine(root, "nuget.config");
        string content = File.ReadAllText(nugetConfigPath);
        XDocument nugetConfig = XDocument.Parse(content);

        string[] sourceUrls = DescendantsByLocalName(nugetConfig, "packageSources")
            .SelectMany(packageSources => DescendantsByLocalName(packageSources, "add"))
            .Select(source => ((string?)source.Attribute("value")) ?? string.Empty)
            .ToArray();

        sourceUrls.ShouldBe(["https://api.nuget.org/v3/index.json"]);

        // Forbid any element that carries credentials or per-feed authentication.
        DescendantsByLocalName(nugetConfig, "packageSourceCredentials").ShouldBeEmpty();
        DescendantsByLocalName(nugetConfig, "apikeys").ShouldBeEmpty();
        DescendantsByLocalName(nugetConfig, "clientCertificates").ShouldBeEmpty();

        // Forbid inline user:password@ credentials in any source URL.
        Regex inlineUrlCredentials = new(@"://[^/@\s]+:[^@\s]+@", RegexOptions.Compiled);
        foreach (string url in sourceUrls)
        {
            inlineUrlCredentials.IsMatch(url).ShouldBeFalse($"NuGet source URL embeds credentials: {url}");
        }

        // Forbid machine-specific path interpolation that would tie the config to one developer's machine.
        string[] machinePathMarkers = ["%APPDATA%", "%LOCALAPPDATA%", "%USERPROFILE%", "$HOME"];
        foreach (string marker in machinePathMarkers)
        {
            content.ShouldNotContain(marker, Case.Insensitive);
        }
    }

    [Fact]
    public void SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup()
    {
        string root = RepositoryRoot();
        string[] declaredRootSubmodules = ReadGitmodulePaths(Path.Combine(root, ".gitmodules"));
        declaredRootSubmodules.Length.ShouldBe(
            RequiredCanonicalSubmodules.Length,
            ".gitmodules must declare each canonical root submodule path exactly once.");
        new HashSet<string>(declaredRootSubmodules, StringComparer.OrdinalIgnoreCase)
            .SetEquals(RequiredCanonicalSubmodules)
            .ShouldBeTrue(".gitmodules path inventory must exactly match the canonical root submodules.");

        string[] universalPolicyDocuments =
        [
            "AGENTS.md",
            "CLAUDE.md",
            ".github/copilot-instructions.md",
        ];
        string[] repositorySetupDocuments =
        [
            "README.md",
            "tests/README.md",
        ];

        foreach (string document in universalPolicyDocuments)
        {
            string path = Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).ShouldBeTrue($"{document} should exist at the repository root or under tests/.");

            string content = File.ReadAllText(path);
            ContainsFoldersSpecificInitCommand(content).ShouldBeFalse(
                $"{document} is a universal entry point and must not document the Folders-specific root submodule init command.");
            ContainsRecursiveInitProhibition(content).ShouldBeTrue(
                $"{document} must document that recursive submodule init is forbidden by default.");
        }

        foreach (string document in repositorySetupDocuments)
        {
            string path = Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).ShouldBeTrue($"{document} should exist at the repository root or under tests/.");

            string content = File.ReadAllText(path);
            ContainsCanonicalInitCommand(content).ShouldBeTrue(
                $"{document} must document the canonical root submodule init command listing all of: {string.Join(", ", RequiredCanonicalSubmodules)}.");
            ContainsRecursiveInitProhibition(content).ShouldBeTrue(
                $"{document} must document that recursive submodule init is forbidden by default.");
        }

        string[] violations = PolicyDocumentPaths(root)
            .SelectMany(path => RecursiveDefaultSetupViolations(path)
                .Select(line => $"{Normalize(Path.GetRelativePath(root, path))}:{line.LineNumber}: {line.Text}"))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"submodule-policy-{Guid.NewGuid():N}.md");
        try
        {
            File.WriteAllLines(tempPath,
            [
                "# Setup",
                "Nested submodules exist in some sibling modules.",
                "Run this default setup command:",
                "git submodule update --init --recursive",
            ]);

            RecursiveDefaultSetupViolations(tempPath)
                .Select(violation => violation.LineNumber)
                .ShouldContain(4);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void CanonicalInitCommandSupportsBashPowerShellAndCmdContinuations()
    {
        foreach (char marker in new[] { '\\', '`', '^' })
        {
            ContainsCanonicalInitCommand(CanonicalInitCommandWithContinuation(marker))
                .ShouldBeTrue($"Continuation marker '{marker}' should preserve the canonical command.");
        }

        string fencedCommand = $"```text\n{CanonicalInitCommand()}\n```";
        ContainsCanonicalInitCommand(fencedCommand).ShouldBeTrue("Markdown fences must not be joined as PowerShell continuations.");

        string reversedCommand = CanonicalInitCommand(RequiredCanonicalSubmodules.Reverse());
        ContainsCanonicalInitCommand(reversedCommand).ShouldBeTrue("Canonical root operands are order-insensitive.");

        string quotedCommand = CanonicalInitCommand(RequiredCanonicalSubmodules.Select(module => $"\"{module}\""));
        ContainsCanonicalInitCommand(quotedCommand).ShouldBeTrue("Unambiguous quoted root operands should be normalized.");
    }

    [Fact]
    public void CanonicalInitCommandRejectsUnsafeAndNonCanonicalVariants()
    {
        string operands = string.Join(" ", RequiredCanonicalSubmodules);
        string canonical = CanonicalInitCommand();
        string nestedOperands = string.Join(
            " ",
            RequiredCanonicalSubmodules.Select(module => module.Equals("references/Hexalith.Tenants", StringComparison.OrdinalIgnoreCase)
                ? $"{module}/nested"
                : module));
        string trailingSlashOperands = string.Join(" ", RequiredCanonicalSubmodules.Select(module => $"{module}/"));
        string[] unsafeCommands =
        [
            $"git submodule update --init --remote {operands}",
            $"git submodule update --init --recursive {operands}",
            $"git submodule update --init --recurse-submodules {operands}",
            $"git submodule update --init submodule.recurse=true {operands}",
            $"git -c submodule.recurse=true submodule update --init {operands}",
            $"git submodule update --init arbitrary-operand {operands}",
            $"{canonical} references/Hexalith.Extra",
            $"git submodule update --init {nestedOperands}",
            $"git submodule update --init {trailingSlashOperands}",
            $"{canonical} # setup comment",
            $"Run {canonical}",
            $"{canonical}; echo done",
            $"git submodule update --init\n{operands}",
        ];

        foreach (string command in unsafeCommands)
        {
            ContainsCanonicalInitCommand(command).ShouldBeFalse($"Strict canonical detection accepted: {command}");
        }
    }

    [Fact]
    public void FoldersSpecificInitCommandDetectionFindsCommandsWithInvalidExtras()
    {
        string withNinthPath = $"{CanonicalInitCommand()} references/Hexalith.Extra";
        string trailingSlashOperands = string.Join(" ", RequiredCanonicalSubmodules.Select(module => $"{module}/"));
        string withOptionsAndOperands =
            $"git submodule update --remote --init arbitrary-operand {trailingSlashOperands} references/Invalid.Extra";
        string withGitConfigOption =
            $"git -c submodule.recurse=true submodule update --init {string.Join(" ", RequiredCanonicalSubmodules)}";

        ContainsFoldersSpecificInitCommand(withNinthPath).ShouldBeTrue();
        ContainsFoldersSpecificInitCommand(withOptionsAndOperands).ShouldBeTrue();
        ContainsFoldersSpecificInitCommand(withGitConfigOption).ShouldBeTrue();
        ContainsFoldersSpecificInitCommand($"Run {withNinthPath}").ShouldBeTrue();
        ContainsFoldersSpecificInitCommand($"- {withNinthPath}").ShouldBeTrue();
        ContainsFoldersSpecificInitCommand($"sudo {withNinthPath}").ShouldBeTrue();
        ContainsFoldersSpecificInitCommand($"# {withNinthPath}").ShouldBeTrue();
        ContainsCanonicalInitCommand(withNinthPath).ShouldBeFalse();
        ContainsCanonicalInitCommand(withOptionsAndOperands).ShouldBeFalse();
        ContainsCanonicalInitCommand(withGitConfigOption).ShouldBeFalse();
    }

    [Fact]
    public void RecursiveInitProhibitionRequiresDirectedSubmoduleWarning()
    {
        string[] accepted =
        [
            "Never use recursive or remote submodule updates by default.",
            "Do not run recursive submodule initialization unless nested submodule work is explicitly requested.",
            "Do not use:\n\n```text\ngit submodule update --init --recursive\n```",
        ];
        string[] rejected =
        [
            "Never initialize submodules without --recursive.",
            "Never initialize a submodule without --recursive.",
            "Do not avoid recursive submodule initialization.",
            "Never prohibit recursive submodule updates.",
            "Never use recursive algorithms when selecting a submodule.",
            "Never expose credentials\nRecursive submodule updates are supported.",
            "Never use recursive filesystem scans.\ngit submodule update --init --recursive",
            "Recursive submodule updates can be slow.",
            "git submodule update --init --recursive",
        ];

        foreach (string content in accepted)
        {
            ContainsRecursiveInitProhibition(content).ShouldBeTrue($"Expected a directed prohibition: {content}");
        }
        foreach (string content in rejected)
        {
            ContainsRecursiveInitProhibition(content).ShouldBeFalse($"Expected no directed prohibition: {content}");
        }
    }

    [Fact]
    public void CopilotUnsafeRecursiveCommandRemainsViolationDespiteLaterProhibition()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"copilot-submodule-policy-{Guid.NewGuid():N}");
        try
        {
            string copilotDirectory = Path.Combine(tempRoot, ".github");
            Directory.CreateDirectory(copilotDirectory);
            string copilotPath = Path.Combine(copilotDirectory, "copilot-instructions.md");
            File.WriteAllLines(copilotPath,
            [
                "# Setup",
                "git submodule update --init --recursive",
                string.Empty,
                "Never use recursive or remote submodule updates by default.",
            ]);

            ContainsRecursiveInitProhibition(File.ReadAllText(copilotPath)).ShouldBeTrue();
            string[] violations = PolicyDocumentPaths(tempRoot)
                .SelectMany(path => RecursiveDefaultSetupViolations(path)
                    .Select(line => $"{Normalize(Path.GetRelativePath(tempRoot, path))}:{line.LineNumber}: {line.Text}"))
                .ToArray();

            violations.Any(violation => violation.StartsWith(".github/copilot-instructions.md:2:", StringComparison.OrdinalIgnoreCase))
                .ShouldBeTrue("A later prohibition must not exempt an earlier unsafe Copilot setup command.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string CanonicalInitCommand() => CanonicalInitCommand(RequiredCanonicalSubmodules);

    private static string CanonicalInitCommand(IEnumerable<string> submodules) =>
        $"git submodule update --init {string.Join(" ", submodules)}";

    private static string CanonicalInitCommandWithContinuation(char marker)
    {
        List<string> lines = [$"git submodule update --init {marker}"];
        for (int index = 0; index < RequiredCanonicalSubmodules.Length; index++)
        {
            string suffix = index == RequiredCanonicalSubmodules.Length - 1 ? string.Empty : $" {marker}";
            lines.Add($"  {RequiredCanonicalSubmodules[index]}{suffix}");
        }
        return string.Join("\n", lines);
    }

    private static Dictionary<string, string[]> BuildProjectReferenceMap(string root) =>
        ExpectedRootPolicyProjects.ToDictionary(
            project => Path.GetFileNameWithoutExtension(project),
            project => ReadProjectReferenceNames(Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar))),
            StringComparer.Ordinal);

    private static void AssertReferences(Dictionary<string, string[]> references, string project, string[] expected)
    {
        if (!references.TryGetValue(project, out string[]? actual))
        {
            actual.ShouldNotBeNull($"Project '{project}' is missing from the expected scaffold project list; update ExpectedRootPolicyProjects.");
            return;
        }
        actual.ShouldBe(expected, ignoreOrder: true, customMessage: $"{project} references drifted from policy.");
    }

    private static HashSet<string> RequireReferences(Dictionary<string, HashSet<string>> references, string project)
    {
        if (!references.TryGetValue(project, out HashSet<string>? refs))
        {
            throw new ShouldAssertException($"Project '{project}' is missing from the expected scaffold project list; update ExpectedRootPolicyProjects.");
        }
        return refs;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static string[] ReadSolutionProjectPaths(string root)
    {
        XDocument solution = XDocument.Load(Path.Combine(root, "Hexalith.Folders.slnx"));
        return DescendantsByLocalName(solution, "Project")
            .Select(project => Normalize((string?)project.Attribute("Path") ?? string.Empty))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadProjectReferenceNames(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return DescendantsByLocalName(project, "ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(Normalize((string?)reference.Attribute("Include") ?? string.Empty)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ProjectHasPackageReferenceVersion(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return DescendantsByLocalName(project, "PackageReference").Any(reference => reference.Attribute("Version") is not null);
    }

    private static IEnumerable<string> FindLocalRootSettingOverrides(string root, string projectPath, IEnumerable<string> driftingElements)
    {
        XDocument project = XDocument.Load(projectPath);
        string relative = Normalize(Path.GetRelativePath(root, projectPath));
        foreach (string element in driftingElements)
        {
            if (DescendantsByLocalName(project, element).Any())
            {
                yield return $"{relative} defines <{element}> locally; root Directory.Build.props owns this setting.";
            }
        }
    }

    private static IEnumerable<string> EnumerateScaffoldBuildableProjectPaths(string root)
    {
        string[] scaffoldRoots = ["samples", "src", "tests"];
        return scaffoldRoots
            .Select(area => Path.Combine(root, area))
            .Where(Directory.Exists)
            .SelectMany(area => SafeEnumerate(area, "Hexalith.Folders*.csproj", SearchOption.AllDirectories))
            .Where(path => !Normalize(Path.GetRelativePath(root, path)).StartsWith("src/Hexalith.Folders.Client/Generation/", StringComparison.Ordinal))
            .Where(path => !string.Equals(Normalize(Path.GetRelativePath(root, path)), "tests/load/Hexalith.Folders.LoadTests.csproj", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<XElement> DescendantsByLocalName(XContainer container, string localName) =>
        container.Descendants().Where(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsCanonicalInitCommand(string content) =>
        JoinContinuationLines(content.Split('\n')).Any(line => IsCanonicalRootInitCommand(line.Text));

    private static bool ContainsFoldersSpecificInitCommand(string content) =>
        JoinContinuationLines(content.Split('\n')).Any(line => IsFoldersSpecificInitCommand(line.Text));

    private static bool IsCanonicalRootInitCommand(string command)
    {
        if (!TryTokenizeCommand(command, out string[] tokens)
            || tokens.Length != RequiredCanonicalSubmodules.Length + 4
            || !tokens[0].Equals("git", StringComparison.OrdinalIgnoreCase)
            || !tokens[1].Equals("submodule", StringComparison.OrdinalIgnoreCase)
            || !tokens[2].Equals("update", StringComparison.OrdinalIgnoreCase)
            || !tokens[3].Equals("--init", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        HashSet<string> operands = new(tokens.Skip(4), StringComparer.OrdinalIgnoreCase);
        return operands.Count == RequiredCanonicalSubmodules.Length
            && operands.SetEquals(RequiredCanonicalSubmodules);
    }

    private static bool IsFoldersSpecificInitCommand(string command)
    {
        if (!TryTokenizeCommand(command, out string[] tokens)
            || tokens.Length < RequiredCanonicalSubmodules.Length + 4)
        {
            return false;
        }

        int gitIndex = Array.FindIndex(tokens, token => token.Equals("git", StringComparison.OrdinalIgnoreCase));
        if (gitIndex < 0)
        {
            return false;
        }

        int submoduleUpdateIndex = -1;
        for (int index = gitIndex + 1; index < tokens.Length - 1; index++)
        {
            if (tokens[index].Equals("submodule", StringComparison.OrdinalIgnoreCase)
                && tokens[index + 1].Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                submoduleUpdateIndex = index;
                break;
            }
        }
        if (submoduleUpdateIndex < 0
            || !tokens.Skip(submoduleUpdateIndex + 2)
                .Any(token => token.Equals("--init", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        HashSet<string> operands = tokens.Skip(submoduleUpdateIndex + 2)
            .Select(NormalizeSubmoduleOperand)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return RequiredCanonicalSubmodules.All(operands.Contains);
    }

    private static bool TryTokenizeCommand(string command, out string[] tokens)
    {
        List<string> result = [];
        int index = 0;
        while (index < command.Length)
        {
            while (index < command.Length && char.IsWhiteSpace(command[index]))
            {
                index++;
            }
            if (index == command.Length)
            {
                break;
            }

            if (IsCommandSeparator(command[index]))
            {
                int separatorStart = index++;
                if (index < command.Length && command[index] == command[separatorStart])
                {
                    index++;
                }
                result.Add(command[separatorStart..index]);
                continue;
            }

            char quote = command[index] is '\'' or '"' ? command[index++] : '\0';
            int tokenStart = index;
            if (quote != '\0')
            {
                while (index < command.Length && command[index] != quote)
                {
                    index++;
                }
                if (index == command.Length || index == tokenStart)
                {
                    tokens = [];
                    return false;
                }

                result.Add(command[tokenStart..index]);
                index++;
                if (index < command.Length
                    && !char.IsWhiteSpace(command[index])
                    && !IsCommandSeparator(command[index]))
                {
                    tokens = [];
                    return false;
                }
                continue;
            }

            while (index < command.Length
                && !char.IsWhiteSpace(command[index])
                && !IsCommandSeparator(command[index]))
            {
                if (command[index] is '\'' or '"')
                {
                    tokens = [];
                    return false;
                }
                index++;
            }
            result.Add(command[tokenStart..index]);
        }

        tokens = result.ToArray();
        return tokens.Length > 0;
    }

    private static bool IsCommandSeparator(char value) => value is ';' or '|' or '&';

    private static string NormalizeSubmoduleOperand(string operand)
    {
        string normalized = Normalize(operand);
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        return normalized.TrimEnd('/');
    }

    private static bool ContainsRecursiveInitProhibition(string content)
    {
        bool hasDirectedProhibition = DirectedRecursiveSubmoduleProhibitionPattern.Matches(content)
            .Any(match => !InverseRecursiveSubmoduleProhibitionPattern.IsMatch(match.Value));
        if (hasDirectedProhibition)
        {
            return true;
        }

        LogicalLine[] logicalLines = JoinContinuationLines(content.Split('\n'));
        for (int index = 0; index < logicalLines.Length; index++)
        {
            LogicalLine line = logicalLines[index];
            if (ContainsRecursiveSubmoduleSetup(line.Text))
            {
                string precedingProse = CollectPrecedingProseContext(logicalLines, index, maxLines: 8);
                if (IsWarningOrNestedOptInContext(line.Text, precedingProse)
                    && DisplayedRecursiveCommandProhibitionPattern.IsMatch(precedingProse.Trim()))
                {
                    return true;
                }
                continue;
            }
        }

        return false;
    }

    private static string[] ReadGitmodulePaths(string gitmodulesPath) =>
        File.ReadAllLines(gitmodulesPath)
            .Select(ParseGitmodulePath)
            .Where(path => path is not null)
            .Select(path => path!)
            .ToArray();

    private static string? ParseGitmodulePath(string line)
    {
        int separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0
            || !line[..separatorIndex].Trim().Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path = line[(separatorIndex + 1)..].Trim();
        if (path.Length >= 2
            && path[0] is '\'' or '"'
            && path[^1] == path[0])
        {
            path = path[1..^1];
        }
        return Normalize(path).TrimEnd('/');
    }

    private static IEnumerable<string> PolicyDocumentPaths(string root)
    {
        string[] rootMarkdown = SafeEnumerate(root, "*.md", SearchOption.TopDirectoryOnly);
        string[] rootSetupScripts = new[] { "*.ps1", "*.sh", "*.cmd", "*.bat" }
            .SelectMany(pattern => SafeEnumerate(root, pattern, SearchOption.TopDirectoryOnly))
            .ToArray();
        string copilotInstructions = Path.Combine(root, ".github", "copilot-instructions.md");
        string[] copilotPolicyDocuments = File.Exists(copilotInstructions) ? [copilotInstructions] : [];

        string testsRoot = Path.Combine(root, "tests");
        string[] testsMarkdown = Directory.Exists(testsRoot)
            ? SafeEnumerate(testsRoot, "*.md", SearchOption.TopDirectoryOnly)
            : [];
        string[] testsScripts = Directory.Exists(testsRoot)
            ? new[] { "*.ps1", "*.sh", "*.cmd", "*.bat" }
                .SelectMany(pattern => SafeEnumerate(testsRoot, pattern, SearchOption.TopDirectoryOnly))
                .ToArray()
            : [];

        string docsRoot = Path.Combine(root, "docs");
        string[] docsDocuments = Directory.Exists(docsRoot)
            ? SafeEnumerate(docsRoot, "*.md", SearchOption.AllDirectories)
            : [];

        return rootMarkdown
            .Concat(rootSetupScripts)
            .Concat(copilotPolicyDocuments)
            .Concat(testsMarkdown)
            .Concat(testsScripts)
            .Concat(docsDocuments)
            .Where(path => IsPolicyDocument(root, path));
    }

    private static string[] SafeEnumerate(string directory, string pattern, SearchOption option)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, option).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static bool IsPolicyDocument(string root, string path)
    {
        string relative = Normalize(Path.GetRelativePath(root, path));
        if (relative.StartsWith("_bmad", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        foreach (string submodule in new[]
        {
            "references/Hexalith.AI.Tools/",
            "references/Hexalith.Builds/",
            "references/Hexalith.Commons/",
            "references/Hexalith.EventStore/",
            "references/Hexalith.FrontComposer/",
            "references/Hexalith.Memories/",
            "references/Hexalith.Tenants/",
        })
        {
            if (relative.StartsWith(submodule, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<(int LineNumber, string Text)> RecursiveDefaultSetupViolations(string path)
    {
        LogicalLine[] logicalLines = JoinContinuationLines(File.ReadAllLines(path));

        for (int index = 0; index < logicalLines.Length; index++)
        {
            LogicalLine line = logicalLines[index];
            if (!ContainsRecursiveSubmoduleSetup(line.Text))
            {
                continue;
            }

            string precedingProse = CollectPrecedingProseContext(logicalLines, index, maxLines: 8);
            if (!IsWarningOrNestedOptInContext(line.Text, precedingProse))
            {
                yield return (line.OriginalLineNumber, line.Text.Trim());
            }
        }
    }

    private readonly record struct LogicalLine(int OriginalLineNumber, string Text);

    private static LogicalLine[] JoinContinuationLines(string[] rawLines)
    {
        List<LogicalLine> result = [];
        StringBuilder buffer = new();
        int firstOriginalLine = -1;

        for (int i = 0; i < rawLines.Length; i++)
        {
            string raw = rawLines[i];
            string trimmedEnd = raw.TrimEnd();
            if (firstOriginalLine < 0)
            {
                firstOriginalLine = i + 1;
            }

            bool isMarkdownFence = trimmedEnd.TrimStart().StartsWith("```", StringComparison.Ordinal);
            bool hasContinuationMarker = trimmedEnd.Length > 0
                && trimmedEnd[^1] is '\\' or '`' or '^';
            if (!isMarkdownFence && hasContinuationMarker)
            {
                buffer.Append(trimmedEnd[..^1]).Append(' ');
                continue;
            }

            buffer.Append(raw);
            result.Add(new LogicalLine(firstOriginalLine, buffer.ToString()));
            buffer.Clear();
            firstOriginalLine = -1;
        }

        if (buffer.Length > 0)
        {
            result.Add(new LogicalLine(firstOriginalLine < 0 ? rawLines.Length : firstOriginalLine, buffer.ToString()));
        }

        return result.ToArray();
    }

    private static string CollectPrecedingProseContext(LogicalLine[] lines, int violationIndex, int maxLines)
    {
        List<string> collected = [];
        int proseLinesSeen = 0;
        for (int i = violationIndex - 1; i >= 0 && collected.Count < maxLines; i--)
        {
            string trimmed = lines[i].Text.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal)
                || trimmed.StartsWith("## ", StringComparison.Ordinal)
                || trimmed.StartsWith("### ", StringComparison.Ordinal)
                || trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }
            collected.Add(lines[i].Text);
            proseLinesSeen++;
            if (proseLinesSeen == 1 && !ContainsRecursivePolicyWarning(trimmed))
            {
                break;
            }
        }
        return string.Join(" ", collected);
    }

    private static readonly Regex[] RecursiveSetupPatterns =
    [
        // `git ... submodule ... --recursive` with arbitrary tokens (including global flags) in between.
        new(@"\bgit\b[\s\S]*?\bsubmodule\b[\s\S]*?--recursive\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Bare `--recurse-submodules` flag in any position (covers `git clone --recurse-submodules`).
        new(@"--recurse-submodules\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // `submodule.recurse` git config key (equivalent recursive default mechanism).
        new(@"\bsubmodule\.recurse\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // `submodule ... --recursive` without explicit `git` prefix (wrapper scripts, shell vars).
        new(@"\bsubmodule\b[\s\S]*?--recursive\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static bool ContainsRecursiveSubmoduleSetup(string line) =>
        RecursiveSetupPatterns.Any(pattern => pattern.IsMatch(line));

    private static readonly string[] WarningContextKeywords =
    [
        "do not",
        "don't",
        "never",
        "avoid",
        "forbid",       // covers "forbidden", "forbids"
        "prohibit",     // covers "prohibited"
        "should not",
        "shouldn't",
        "must not",
        "mustn't",
        "deprecated",
        "discouraged",
        "not use",
    ];

    private static bool IsWarningOrNestedOptInContext(string commandLine, string precedingContext) =>
        ContainsRecursivePolicyWarning(commandLine) || ContainsRecursivePolicyWarning(precedingContext);

    private static bool ContainsRecursivePolicyWarning(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        bool directWarning = WarningContextKeywords.Any(keyword => context.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        bool explicitNestedOptIn = context.Contains("nested submodule", StringComparison.OrdinalIgnoreCase)
            && context.Contains("explicit", StringComparison.OrdinalIgnoreCase);

        return directWarning || explicitNestedOptIn || context.Contains("user-requested", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
