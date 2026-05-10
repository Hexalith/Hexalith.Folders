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

    private static string Normalize(string path) => path.Replace('\\', '/');
}
