using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class ScaffoldContractTests
{
    private static readonly string[] ExpectedSolutionProjects =
    [
        "samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj",
        "samples/Hexalith.Folders.Sample/Hexalith.Folders.Sample.csproj",
        "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj",
        "src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj",
        "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
        "src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj",
        "src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj",
        "src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj",
        "src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj",
        "src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj",
        "src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj",
        "src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj",
        "src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj",
        "src/Hexalith.Folders/Hexalith.Folders.csproj",
        "tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj",
        "tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj",
        "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj",
        "tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj",
        "tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj",
        "tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj",
        "tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj",
        "tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj",
        "tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj",
        "tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj"
    ];

    private static readonly string[] RequiredCanonicalSubmodules =
    [
        "Hexalith.AI.Tools",
        "Hexalith.Commons",
        "Hexalith.EventStore",
        "Hexalith.FrontComposer",
        "Hexalith.Memories",
        "Hexalith.Tenants",
    ];

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
        buildableProjects.ShouldBe(ExpectedSolutionProjects.Order(StringComparer.Ordinal).ToArray());
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
        AssertReferences(references, "Hexalith.Folders.Server", ["Hexalith.Folders", "Hexalith.Folders.Contracts", "Hexalith.Folders.ServiceDefaults"]);
        AssertReferences(references, "Hexalith.Folders.Client", ["Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Cli", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.Mcp", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.UI", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.Workers", ["Hexalith.Folders", "Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.AppHost", ["Hexalith.Folders.Aspire", "Hexalith.Folders.Server", "Hexalith.Folders.UI"]);
        AssertReferences(references, "Hexalith.Folders.Aspire", []);
        AssertReferences(references, "Hexalith.Folders.ServiceDefaults", []);
        AssertReferences(references, "Hexalith.Folders.Testing", ["Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Sample", ["Hexalith.Folders.Client"]);
        AssertReferences(references, "Hexalith.Folders.Sample.Tests", ["Hexalith.Folders.Sample"]);
        AssertReferences(references, "Hexalith.Folders.Contracts.Tests", ["Hexalith.Folders.Contracts"]);
        AssertReferences(references, "Hexalith.Folders.Tests", ["Hexalith.Folders", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Server.Tests", ["Hexalith.Folders.Server", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Client.Tests", ["Hexalith.Folders.Client", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Cli.Tests", ["Hexalith.Folders.Cli", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Mcp.Tests", ["Hexalith.Folders.Mcp", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.UI.Tests", ["Hexalith.Folders.UI", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Workers.Tests", ["Hexalith.Folders.Workers", "Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.Testing.Tests", ["Hexalith.Folders.Testing"]);
        AssertReferences(references, "Hexalith.Folders.IntegrationTests", ["Hexalith.Folders.Server", "Hexalith.Folders.Testing"]);
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
    }

    [Fact]
    public void RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions()
    {
        string root = RepositoryRoot();
        XDocument buildProps = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XDocument packagesProps = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        string[] projectsWithInlineVersions = ExpectedSolutionProjects
            .Select(project => Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar)))
            .Where(ProjectHasPackageReferenceVersion)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToArray();

        DescendantsByLocalName(buildProps, "TargetFramework").Single().Value.ShouldBe("net10.0");
        DescendantsByLocalName(buildProps, "Nullable").Single().Value.ShouldBe("enable");
        DescendantsByLocalName(buildProps, "ImplicitUsings").Single().Value.ShouldBe("enable");
        DescendantsByLocalName(buildProps, "TreatWarningsAsErrors").Single().Value.ShouldBe("true");
        DescendantsByLocalName(buildProps, "LangVersion").Single().Value.ShouldBe("latest");
        DescendantsByLocalName(packagesProps, "ManagePackageVersionsCentrally").Single().Value.ShouldBe("true");
        projectsWithInlineVersions.ShouldBeEmpty();
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

        string[] violations = ExpectedSolutionProjects
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
        string[] policyDocuments =
        [
            "AGENTS.md",
            "CLAUDE.md",
            "README.md",
            "tests/README.md",
        ];

        foreach (string document in policyDocuments)
        {
            string path = Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).ShouldBeTrue($"{document} should exist at the repository root or under tests/.");

            string content = File.ReadAllText(path);
            AssertCanonicalInitCommandPresent(content, document);

            // Discoverability of the prohibition itself (any of the canonical wordings).
            bool documentsProhibition =
                content.Contains("git submodule update --init --recursive", StringComparison.OrdinalIgnoreCase)
                || content.Contains("do not run recursive submodule initialization", StringComparison.OrdinalIgnoreCase)
                || content.Contains("not use recursive", StringComparison.OrdinalIgnoreCase);
            documentsProhibition.ShouldBeTrue($"{document} must document that recursive submodule init is forbidden by default.");
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

    private static Dictionary<string, string[]> BuildProjectReferenceMap(string root) =>
        ExpectedSolutionProjects.ToDictionary(
            project => Path.GetFileNameWithoutExtension(project),
            project => ReadProjectReferenceNames(Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar))),
            StringComparer.Ordinal);

    private static void AssertReferences(Dictionary<string, string[]> references, string project, string[] expected)
    {
        if (!references.TryGetValue(project, out string[]? actual))
        {
            actual.ShouldNotBeNull($"Project '{project}' is missing from the expected scaffold project list; update ExpectedSolutionProjects.");
            return;
        }
        actual.ShouldBe(expected, ignoreOrder: true, customMessage: $"{project} references drifted from policy.");
    }

    private static HashSet<string> RequireReferences(Dictionary<string, HashSet<string>> references, string project)
    {
        if (!references.TryGetValue(project, out HashSet<string>? refs))
        {
            throw new ShouldAssertException($"Project '{project}' is missing from the expected scaffold project list; update ExpectedSolutionProjects.");
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
            .Select(reference => Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include") ?? string.Empty))
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
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<XElement> DescendantsByLocalName(XContainer container, string localName) =>
        container.Descendants().Where(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static void AssertCanonicalInitCommandPresent(string content, string sourceDescription)
    {
        bool found = content.Split('\n').Any(line =>
        {
            if (!line.Contains("git submodule update --init", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return RequiredCanonicalSubmodules.All(module => line.Contains(module, StringComparison.OrdinalIgnoreCase));
        });

        found.ShouldBeTrue($"{sourceDescription} must document the canonical root submodule init command listing all of: {string.Join(", ", RequiredCanonicalSubmodules)}.");
    }

    private static IEnumerable<string> PolicyDocumentPaths(string root)
    {
        string[] rootMarkdown = SafeEnumerate(root, "*.md", SearchOption.TopDirectoryOnly);
        string[] rootSetupScripts = new[] { "*.ps1", "*.sh", "*.cmd", "*.bat" }
            .SelectMany(pattern => SafeEnumerate(root, pattern, SearchOption.TopDirectoryOnly))
            .ToArray();

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
            "Hexalith.AI.Tools/",
            "Hexalith.Commons/",
            "Hexalith.EventStore/",
            "Hexalith.FrontComposer/",
            "Hexalith.Memories/",
            "Hexalith.Tenants/",
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

            if (trimmedEnd.EndsWith('\\'))
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
