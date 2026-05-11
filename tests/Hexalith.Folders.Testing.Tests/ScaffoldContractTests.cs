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

    [Fact]
    public void SolutionContainsOnlyCanonicalBuildableProjects()
    {
        string root = RepositoryRoot();
        string[] solutionProjects = ReadSolutionProjectPaths(root);
        string[] buildableProjects = Directory
            .EnumerateFiles(root, "Hexalith.Folders*.csproj", SearchOption.AllDirectories)
            .Where(path => IsScaffoldBuildableArea(root, path))
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        solutionProjects.ShouldBe(ExpectedSolutionProjects.Order(StringComparer.Ordinal).ToArray());
        buildableProjects.ShouldBe(ExpectedSolutionProjects.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ProjectReferencesFollowAllowedDependencyDirection()
    {
        string root = RepositoryRoot();
        Dictionary<string, string[]> references = ExpectedSolutionProjects
            .ToDictionary(
                project => Path.GetFileNameWithoutExtension(project),
                project => ReadProjectReferenceNames(Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar))),
                StringComparer.Ordinal);

        references["Hexalith.Folders.Contracts"].ShouldBeEmpty();
        references["Hexalith.Folders"].ShouldBe(["Hexalith.Folders.Contracts"], ignoreOrder: true);
        references["Hexalith.Folders.Server"].ShouldBe(["Hexalith.Folders", "Hexalith.Folders.Contracts", "Hexalith.Folders.ServiceDefaults"], ignoreOrder: true);
        references["Hexalith.Folders.Client"].ShouldBe(["Hexalith.Folders.Contracts"], ignoreOrder: true);
        references["Hexalith.Folders.Cli"].ShouldBe(["Hexalith.Folders.Client"], ignoreOrder: true);
        references["Hexalith.Folders.Mcp"].ShouldBe(["Hexalith.Folders.Client"], ignoreOrder: true);
        references["Hexalith.Folders.UI"].ShouldBe(["Hexalith.Folders.Client"], ignoreOrder: true);
        references["Hexalith.Folders.Workers"].ShouldBe(["Hexalith.Folders", "Hexalith.Folders.Contracts"], ignoreOrder: true);
        references["Hexalith.Folders.AppHost"].ShouldBe(["Hexalith.Folders.Aspire", "Hexalith.Folders.Server", "Hexalith.Folders.UI"], ignoreOrder: true);
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

        buildProps.Descendants("TargetFramework").Single().Value.ShouldBe("net10.0");
        buildProps.Descendants("Nullable").Single().Value.ShouldBe("enable");
        buildProps.Descendants("ImplicitUsings").Single().Value.ShouldBe("enable");
        buildProps.Descendants("TreatWarningsAsErrors").Single().Value.ShouldBe("true");
        buildProps.Descendants("LangVersion").Single().Value.ShouldBe("latest");
        packagesProps.Descendants("ManagePackageVersionsCentrally").Single().Value.ShouldBe("true");
        projectsWithInlineVersions.ShouldBeEmpty();
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

        string[] missingFiles = requiredFiles
            .Where(file => !File.Exists(Path.Combine(root, file)))
            .ToArray();

        missingFiles.ShouldBeEmpty();
    }

    [Fact]
    public void NuGetConfigurationUsesPublicSourceWithoutCredentials()
    {
        string root = RepositoryRoot();
        XDocument nugetConfig = XDocument.Load(Path.Combine(root, "nuget.config"));
        string content = File.ReadAllText(Path.Combine(root, "nuget.config"));

        nugetConfig.Descendants("packageSources")
            .Descendants("add")
            .Select(source => ((string?)source.Attribute("value")) ?? string.Empty)
            .ShouldBe(["https://api.nuget.org/v3/index.json"]);

        content.ShouldNotContain("packageSourceCredentials", Case.Insensitive);
        content.ShouldNotContain("cleartextpassword", Case.Insensitive);
        content.ShouldNotContain("password", Case.Insensitive);
        content.ShouldNotContain("token", Case.Insensitive);
        content.ShouldNotContain("%userprofile%", Case.Insensitive);
        content.ShouldNotContain("$HOME", Case.Insensitive);
    }

    [Fact]
    public void SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup()
    {
        string root = RepositoryRoot();
        string[] policyDocuments =
        [
            "AGENTS.md",
            "CLAUDE.md",
            "README.md"
        ];

        foreach (string document in policyDocuments)
        {
            string path = Path.Combine(root, document);
            File.Exists(path).ShouldBeTrue($"{document} should exist at the repository root.");

            string content = File.ReadAllText(path);
            content.ShouldContain("git submodule update --init Hexalith.AI.Tools Hexalith.EventStore Hexalith.FrontComposer Hexalith.Tenants", Case.Insensitive);
            content.ShouldContain("git submodule update --init --recursive", Case.Insensitive);
            content.ShouldContain("Nested submodules must only be initialized when a user explicitly requests nested submodule work.", Case.Insensitive);
        }

        string[] violations = PolicyDocumentPaths(root)
            .SelectMany(path => RecursiveDefaultSetupViolations(path)
                .Select(line => $"{Normalize(Path.GetRelativePath(root, path))}:{line.LineNumber}: {line.Text}"))
            .ToArray();

        violations.ShouldBeEmpty();
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
        return solution
            .Descendants("Project")
            .Select(project => Normalize((string?)project.Attribute("Path") ?? string.Empty))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadProjectReferenceNames(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return project
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include") ?? string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ProjectHasPackageReferenceVersion(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return project.Descendants("PackageReference").Any(reference => reference.Attribute("Version") is not null);
    }

    private static bool IsScaffoldBuildableArea(string root, string path)
    {
        string relative = Normalize(Path.GetRelativePath(root, path));
        return relative.StartsWith("src/", StringComparison.Ordinal)
            || relative.StartsWith("tests/", StringComparison.Ordinal)
            || relative.StartsWith("samples/", StringComparison.Ordinal);
    }

    private static IEnumerable<string> PolicyDocumentPaths(string root)
    {
        string[] rootDocuments = Directory
            .EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Where(path => IsPolicyDocument(path))
            .ToArray();

        string docsRoot = Path.Combine(root, "docs");
        string[] docsDocuments = Directory.Exists(docsRoot)
            ? Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
                .Where(path => IsPolicyDocument(path))
                .ToArray()
            : [];

        return rootDocuments.Concat(docsDocuments);
    }

    private static IEnumerable<(int LineNumber, string Text)> RecursiveDefaultSetupViolations(string path)
    {
        string[] lines = File.ReadAllLines(path);
        for (int index = 0; index < lines.Length; index++)
        {
            if (!ContainsRecursiveSubmoduleSetup(lines[index]))
            {
                continue;
            }

            string context = string.Join(
                ' ',
                lines.Skip(Math.Max(0, index - 4)).Take(Math.Min(7, lines.Length - Math.Max(0, index - 4))));

            if (!IsWarningOrNestedOptInContext(context))
            {
                yield return (index + 1, lines[index].Trim());
            }
        }
    }

    private static bool IsPolicyDocument(string path)
    {
        string relative = Normalize(path);
        return !relative.Contains("/_bmad", StringComparison.Ordinal)
            && !relative.Contains("/Hexalith.AI.Tools/", StringComparison.Ordinal)
            && !relative.Contains("/Hexalith.EventStore/", StringComparison.Ordinal)
            && !relative.Contains("/Hexalith.FrontComposer/", StringComparison.Ordinal)
            && !relative.Contains("/Hexalith.Tenants/");
    }

    private static bool ContainsRecursiveSubmoduleSetup(string line)
    {
        string normalized = line.ToLowerInvariant();
        return normalized.Contains("git submodule", StringComparison.Ordinal)
                && normalized.Contains("--recursive", StringComparison.Ordinal)
            || normalized.Contains("git clone", StringComparison.Ordinal)
                && normalized.Contains("--recurse-submodules", StringComparison.Ordinal)
            || normalized.Contains("--recurse-submodules", StringComparison.Ordinal)
            || normalized.Contains("git submodule foreach", StringComparison.Ordinal)
                && normalized.Contains("--recursive", StringComparison.Ordinal);
    }

    private static bool IsWarningOrNestedOptInContext(string context)
    {
        string normalized = context.ToLowerInvariant();
        return normalized.Contains("do not", StringComparison.Ordinal)
            || normalized.Contains("never", StringComparison.Ordinal)
            || normalized.Contains("avoid", StringComparison.Ordinal)
            || normalized.Contains("forbid", StringComparison.Ordinal)
            || normalized.Contains("forbidden", StringComparison.Ordinal)
            || normalized.Contains("unless", StringComparison.Ordinal)
            || normalized.Contains("nested submodule", StringComparison.Ordinal)
            || normalized.Contains("explicitly requests", StringComparison.Ordinal)
            || normalized.Contains("opt-in", StringComparison.Ordinal);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
